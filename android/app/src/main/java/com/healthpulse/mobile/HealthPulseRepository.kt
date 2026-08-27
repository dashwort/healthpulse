package com.healthpulse.mobile

import android.content.Context
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject
import java.io.File
import java.net.URI
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import java.security.SecureRandom
import java.time.Instant
import java.util.Base64

class HealthPulseRepository(
    context: Context,
    private val credentials: CredentialStore = SecureStore(context.applicationContext),
    private val httpClient: HealthPulseHttpClient = UrlConnectionHealthPulseHttpClient()
) : HealthPulseRepositoryPort {
    private val applicationContext = context.applicationContext
    private val cacheFile = File(applicationContext.filesDir, "healthpulse-cache.json")

    override fun load(): AppSnapshot {
        val root = readCache()
        return AppSnapshot(
            serverUrl = credentials.getString(SERVER_URL, null),
            isSignedIn = credentials.contains(REFRESH_TOKEN),
            templates = root.optJSONArray("templates").toTemplates(),
            readings = root.optJSONArray("readings").toReadings(),
            reminders = root.optJSONArray("reminders").toReminders(),
            queuedChanges = root.optJSONArray("queue")?.length() ?: 0,
            syncStatus = when {
                credentials.getString(SERVER_URL, null).isNullOrBlank() -> SyncStatus.SETUP_REQUIRED
                else -> SyncStatus.OFFLINE
            }
        )
    }

    override fun configureServer(rawServerUrl: String) {
        val normalized = rawServerUrl.trim().trimEnd('/')
        credentials.putString(SERVER_URL, normalized)
        credentials.remove(ACCESS_TOKEN)
        credentials.remove(REFRESH_TOKEN)
        credentials.remove(ACCESS_EXPIRES)
        credentials.remove(PKCE_VERIFIER)
        credentials.remove(PKCE_STATE)
        cacheFile.delete()
    }

    override fun signOutAndClearData() {
        credentials.clear()
        cacheFile.delete()
    }

    override fun authorizationUrl(): String {
        val server = requireServer()
        val state = randomToken(24)
        val verifier = randomToken(48)
        check(AppValidation.codeVerifier(verifier))
        credentials.putString(PKCE_STATE, state)
        credentials.putString(PKCE_VERIFIER, verifier)
        val challenge = base64Url(sha256(verifier))
        return server + "/api/mobile/auth/authorize?code_challenge=" + encode(challenge) +
            "&state=" + encode(state) + "&redirect_uri=" + encode(REDIRECT_URI)
    }

    override suspend fun completeSignIn(callbackUri: String): AppSnapshot = withContext(Dispatchers.IO) {
        val uri = URI(callbackUri)
        val values = uri.rawQuery
            ?.split("&")
            ?.mapNotNull { segment ->
                val index = segment.indexOf('=')
                if (index < 0) null else segment.substring(0, index) to java.net.URLDecoder.decode(
                    segment.substring(index + 1),
                    StandardCharsets.UTF_8
                )
            }
            ?.toMap()
            ?: emptyMap()
        val state = values["state"]
        val code = values["code"]
        val expectedState = credentials.getString(PKCE_STATE, null)
        val verifier = credentials.getString(PKCE_VERIFIER, null)
        require(!state.isNullOrBlank() && state == expectedState && !code.isNullOrBlank() && !verifier.isNullOrBlank()) {
            "The sign-in response could not be verified."
        }
        val token = postJson(
            "/api/mobile/auth/token",
            JSONObject()
                .put("grantType", "authorization_code")
                .put("code", code)
                .put("codeVerifier", verifier)
        )
        saveTokens(token)
        credentials.remove(PKCE_STATE)
        credentials.remove(PKCE_VERIFIER)
        sync()
    }

    override suspend fun sync(): AppSnapshot = withContext(Dispatchers.IO) {
        require(credentials.contains(REFRESH_TOKEN)) { "Sign in before synchronising." }
        flushQueue()
        val templates = getAuthorizedArray("/api/templates/catalogue").toTemplates()
        val readings = mutableListOf<HealthReading>()
        var page = 1
        while (true) {
            val response = getAuthorized("/api/readings?page=" + page + "&pageSize=100")
            val items = response.optJSONArray("items").toReadings()
            readings += items
            if (items.isEmpty() || readings.size >= response.optInt("totalCount")) break
            page += 1
        }
        val root = readCache()
        root.put("templates", templates.toTemplateJsonArray())
        root.put("readings", readings.toReadingJsonArray())
        writeCache(root)
        snapshot(SyncStatus.UP_TO_DATE)
    }

    override fun addReading(
        template: HealthTemplate,
        value: Double,
        note: String?,
        recordedAt: Instant
    ): AppSnapshot {
        val root = readCache()
        val localId = "local-" + java.util.UUID.randomUUID()
        val reading = HealthReading(
            id = localId,
            templateId = template.id,
            templateName = template.name,
            value = value,
            unit = template.normalizedUnit,
            recordedAtUtc = recordedAt.toString(),
            note = note?.trim()?.ifBlank { null }
        )
        root.optJSONArray("readings").orEmpty().put(reading.toJson()).also { root.put("readings", it) }
        appendQueue(
            root,
            QueueOperation(
                kind = "createReading",
                localId = localId,
                path = "/api/readings",
                method = "POST",
                body = JSONObject()
                    .put("templateId", template.id)
                    .put("value", value)
                    .put("unit", template.normalizedUnit)
                    .put("recordedAtUtc", reading.recordedAtUtc)
                    .put("note", reading.note)
            )
        )
        writeCache(root)
        return snapshot(SyncStatus.CHANGES_QUEUED)
    }

    override fun updateReading(reading: HealthReading): AppSnapshot {
        val root = readCache()
        val readings = root.optJSONArray("readings").orEmpty()
        for (index in 0 until readings.length()) {
            if (readings.getJSONObject(index).optString("id") == reading.id) {
                readings.put(index, reading.toJson())
                break
            }
        }
        if (!reading.id.startsWith("local-")) {
            appendQueue(
                root,
                QueueOperation(
                    kind = "updateReading",
                    localId = reading.id,
                    path = "/api/readings/" + reading.id,
                    method = "PUT",
                    body = reading.toUpdateJson()
                )
            )
        }
        writeCache(root)
        return snapshot(SyncStatus.CHANGES_QUEUED)
    }

    override fun deleteReading(readingId: String): AppSnapshot {
        val root = readCache()
        val remaining = JSONArray()
        root.optJSONArray("readings").orEmpty().forEachObject {
            if (it.optString("id") != readingId) remaining.put(it)
        }
        root.put("readings", remaining)
        if (!readingId.startsWith("local-")) {
            appendQueue(
                root,
                QueueOperation("deleteReading", readingId, "/api/readings/" + readingId, "DELETE", null)
            )
        }
        writeCache(root)
        return snapshot(SyncStatus.CHANGES_QUEUED)
    }

    override fun setTracking(template: HealthTemplate, shouldTrack: Boolean): AppSnapshot {
        val root = readCache()
        val templates = root.optJSONArray("templates").orEmpty()
        for (index in 0 until templates.length()) {
            val item = templates.getJSONObject(index)
            if (item.optString("id") == template.id) {
                item.put("isTracked", shouldTrack)
                break
            }
        }
        appendQueue(
            root,
            QueueOperation(
                kind = "tracking",
                localId = template.id,
                path = "/api/templates/" + template.id + "/track",
                method = if (shouldTrack) "POST" else "DELETE",
                body = null
            )
        )
        writeCache(root)
        return snapshot(SyncStatus.CHANGES_QUEUED)
    }

    override fun saveReminder(reminder: Reminder): AppSnapshot {
        val root = readCache()
        val reminders = JSONArray()
        root.optJSONArray("reminders").orEmpty().forEachObject {
            if (it.optString("templateId") != reminder.templateId) reminders.put(it)
        }
        reminders.put(reminder.toJson())
        root.put("reminders", reminders)
        writeCache(root)
        return snapshot(SyncStatus.CHANGES_QUEUED)
    }

    override suspend fun checkForUpdate(currentVersion: String): UpdateCheck = withContext(Dispatchers.IO) {
        val result = httpClient.request(
            requireServer() + "/.well-known/healthpulse-android-update",
            "GET",
            null,
            null
        )
        if (result.status !in 200..299) return@withContext UpdateCheck.Unavailable
        val payload = JSONObject(result.body)
        val version = payload.optString("latestVersion")
        val apkUrl = payload.optString("apkUrl")
        if (version.isBlank() || apkUrl.isBlank() || !isVersionNewer(version, currentVersion)) {
            UpdateCheck.UpToDate
        } else {
            UpdateCheck.Available(version, apkUrl, payload.optString("releaseNotes"))
        }
    }

    private suspend fun flushQueue() {
        val root = readCache()
        val queued = root.optJSONArray("queue").orEmpty()
        val remaining = JSONArray()
        for (index in 0 until queued.length()) {
            val operation = QueueOperation.fromJson(queued.getJSONObject(index))
            val result = authorizedRequest(operation.path, operation.method, operation.body)
            if (result.status !in 200..299) {
                remaining.put(operation.toJson())
                for (rest in index + 1 until queued.length()) remaining.put(queued.getJSONObject(rest))
                break
            }
            if (operation.kind == "createReading") {
                replaceLocalReading(root, operation.localId, JSONObject(result.body).toReading())
            }
        }
        root.put("queue", remaining)
        writeCache(root)
    }

    private suspend fun getAuthorized(path: String): JSONObject {
        val response = authorizedRequest(path, "GET", null)
        check(response.status in 200..299) { "Server request failed (" + response.status + ")." }
        return JSONObject(response.body)
    }

    private suspend fun getAuthorizedArray(path: String): JSONArray {
        val response = authorizedRequest(path, "GET", null)
        check(response.status in 200..299) { "Server request failed (" + response.status + ")." }
        return JSONArray(response.body)
    }

    private suspend fun authorizedRequest(
        path: String,
        method: String,
        body: JSONObject?
    ): HealthPulseHttpResponse {
        var token = credentials.getString(ACCESS_TOKEN, null)
        if (token.isNullOrBlank() || credentials.getLong(ACCESS_EXPIRES, 0) <= System.currentTimeMillis()) {
            token = refreshAccessToken()
        }
        var result = httpClient.request(requireServer() + path, method, body, token)
        if (result.status == java.net.HttpURLConnection.HTTP_UNAUTHORIZED) {
            token = refreshAccessToken()
            result = httpClient.request(requireServer() + path, method, body, token)
        }
        return result
    }

    private fun refreshAccessToken(): String {
        val refreshToken = credentials.getString(REFRESH_TOKEN, null)
            ?: throw IllegalStateException("Sign in again to continue.")
        val response = postJson(
            "/api/mobile/auth/token",
            JSONObject().put("grantType", "refresh_token").put("refreshToken", refreshToken)
        )
        saveTokens(response)
        return credentials.getString(ACCESS_TOKEN, null)!!
    }

    private fun postJson(path: String, payload: JSONObject): JSONObject {
        val response = httpClient.request(requireServer() + path, "POST", payload, null)
        check(response.status in 200..299) {
            JSONObject(response.body).optString("detail", "The server rejected the request.")
        }
        return JSONObject(response.body)
    }

    private fun saveTokens(payload: JSONObject) {
        val expiresAt = Instant.parse(payload.getString("accessTokenExpiresUtc")).toEpochMilli()
        credentials.putString(ACCESS_TOKEN, payload.getString("accessToken"))
        credentials.putString(REFRESH_TOKEN, payload.getString("refreshToken"))
        credentials.putLong(ACCESS_EXPIRES, expiresAt)
    }

    private fun snapshot(status: SyncStatus): AppSnapshot = load().copy(syncStatus = status)

    private fun requireServer(): String =
        credentials.getString(SERVER_URL, null)
            ?: throw IllegalStateException("Configure your HealthPulse server first.")

    private fun readCache(): JSONObject = runCatching {
        if (cacheFile.exists()) JSONObject(cacheFile.readText()) else JSONObject()
    }.getOrElse { JSONObject() }

    private fun writeCache(root: JSONObject) {
        cacheFile.writeText(root.toString())
    }

    private fun appendQueue(root: JSONObject, operation: QueueOperation) {
        root.optJSONArray("queue").orEmpty().put(operation.toJson()).also { root.put("queue", it) }
    }

    private fun replaceLocalReading(root: JSONObject, localId: String, saved: HealthReading) {
        val readings = root.optJSONArray("readings").orEmpty()
        for (index in 0 until readings.length()) {
            if (readings.getJSONObject(index).optString("id") == localId) {
                readings.put(index, saved.toJson())
                return
            }
        }
    }

    private fun randomToken(byteCount: Int): String = base64Url(SecureRandom().generateSeed(byteCount))

    private fun sha256(value: String): ByteArray =
        MessageDigest.getInstance("SHA-256").digest(value.toByteArray(StandardCharsets.US_ASCII))

    private fun base64Url(value: ByteArray): String =
        Base64.getUrlEncoder().withoutPadding().encodeToString(value)

    private fun encode(value: String): String = URLEncoder.encode(value, StandardCharsets.UTF_8)

    private fun isVersionNewer(candidate: String, current: String): Boolean {
        val candidateParts = candidate.substringBefore('-').split('.').map { it.toIntOrNull() ?: 0 }
        val currentParts = current.substringBefore('-').split('.').map { it.toIntOrNull() ?: 0 }
        return (0..2).firstOrNull { index ->
            candidateParts.getOrElse(index) { 0 } != currentParts.getOrElse(index) { 0 }
        }?.let { candidateParts.getOrElse(it) { 0 } > currentParts.getOrElse(it) { 0 } } ?: false
    }

    private data class QueueOperation(
        val kind: String,
        val localId: String,
        val path: String,
        val method: String,
        val body: JSONObject?
    ) {
        fun toJson() = JSONObject()
            .put("kind", kind)
            .put("localId", localId)
            .put("path", path)
            .put("method", method)
            .put("body", body)

        companion object {
            fun fromJson(value: JSONObject) = QueueOperation(
                value.getString("kind"),
                value.getString("localId"),
                value.getString("path"),
                value.getString("method"),
                value.optJSONObject("body")
            )
        }
    }

    companion object {
        private const val SERVER_URL = "server_url"
        private const val ACCESS_TOKEN = "access_token"
        private const val REFRESH_TOKEN = "refresh_token"
        private const val ACCESS_EXPIRES = "access_expires"
        private const val PKCE_VERIFIER = "pkce_verifier"
        private const val PKCE_STATE = "pkce_state"
        private const val REDIRECT_URI = "healthpulse://auth/callback"
    }
}

sealed interface UpdateCheck {
    data object UpToDate : UpdateCheck
    data object Unavailable : UpdateCheck
    data class Available(val version: String, val apkUrl: String, val releaseNotes: String) : UpdateCheck
}

private fun JSONArray?.orEmpty(): JSONArray = this ?: JSONArray()

private fun JSONArray?.toTemplates(): List<HealthTemplate> =
    buildList { this@toTemplates?.forEachObject { add(it.toTemplate()) } }

private fun JSONArray?.toReadings(): List<HealthReading> =
    buildList { this@toReadings?.forEachObject { add(it.toReading()) } }

private fun JSONArray?.toReminders(): List<Reminder> =
    buildList {
        this@toReminders?.forEachObject {
            add(
                Reminder(
                    it.getString("templateId"),
                    ReminderCadence.valueOf(it.getString("cadence")),
                    it.optInt("intervalDays", 1)
                )
            )
        }
    }

private inline fun JSONArray.forEachObject(action: (JSONObject) -> Unit) {
    for (index in 0 until length()) action(getJSONObject(index))
}

private fun JSONObject.toTemplate(): HealthTemplate = HealthTemplate(
    id = getString("id"),
    name = getString("name"),
    category = getString("category"),
    normalizedUnit = getString("normalizedUnit"),
    allowedUnits = optJSONArray("allowedUnits")?.let { units ->
        buildList { for (index in 0 until units.length()) add(units.getString(index)) }
    } ?: emptyList(),
    isCustom = optBoolean("isCustom"),
    isTracked = optBoolean("isTracked")
)

private fun JSONObject.toReading(): HealthReading = HealthReading(
    id = getString("id"),
    templateId = getString("templateId"),
    templateName = getString("templateName"),
    value = getDouble("value"),
    unit = getString("unit"),
    recordedAtUtc = getString("recordedAtUtc"),
    note = if (isNull("note")) null else optString("note").ifBlank { null }
)

private fun HealthTemplate.toJson(): JSONObject = JSONObject()
    .put("id", id)
    .put("name", name)
    .put("category", category)
    .put("normalizedUnit", normalizedUnit)
    .put("allowedUnits", JSONArray(allowedUnits))
    .put("isCustom", isCustom)
    .put("isTracked", isTracked)

private fun List<HealthTemplate>.toTemplateJsonArray(): JSONArray = JSONArray().also { array ->
    forEach { array.put(it.toJson()) }
}

private fun HealthReading.toJson(): JSONObject = JSONObject()
    .put("id", id)
    .put("templateId", templateId)
    .put("templateName", templateName)
    .put("value", value)
    .put("unit", unit)
    .put("recordedAtUtc", recordedAtUtc)
    .put("note", note)

private fun List<HealthReading>.toReadingJsonArray(): JSONArray = JSONArray().also { array ->
    forEach { array.put(it.toJson()) }
}

private fun HealthReading.toUpdateJson(): JSONObject = JSONObject()
    .put("value", value)
    .put("unit", unit)
    .put("recordedAtUtc", recordedAtUtc)
    .put("note", note)

private fun Reminder.toJson(): JSONObject = JSONObject()
    .put("templateId", templateId)
    .put("cadence", cadence.name)
    .put("intervalDays", intervalDays)
