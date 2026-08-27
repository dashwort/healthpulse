package com.healthpulse.mobile

import android.content.Context
import androidx.test.core.app.ApplicationProvider
import com.google.common.truth.Truth.assertThat
import java.time.Instant
import java.util.concurrent.ConcurrentHashMap
import kotlinx.coroutines.runBlocking
import org.json.JSONArray
import org.json.JSONObject
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [36])
class HealthPulseRepositoryTest {
    private lateinit var context: Context

    @Before
    fun setUp() {
        context = ApplicationProvider.getApplicationContext()
        context.filesDir.resolve("healthpulse-cache.json").delete()
    }

    @Test
    fun load_without_a_configured_server_requires_setup() {
        // Arrange
        val credentials = MemoryCredentials()
        val repository = HealthPulseRepository(context, credentials, RecordingHttpClient())

        // Act
        val snapshot = repository.load()

        // Assert
        assertThat(snapshot.serverUrl).isNull()
        assertThat(snapshot.isSignedIn).isFalse()
        assertThat(snapshot.syncStatus).isEqualTo(SyncStatus.SETUP_REQUIRED)
    }

    @Test
    fun add_reading_writes_local_data_and_queues_a_create_operation() {
        // Arrange
        val repository = HealthPulseRepository(
            context,
            MemoryCredentials(),
            RecordingHttpClient()
        )
        val template = template()

        // Act
        val snapshot = repository.addReading(
            template,
            5.2,
            "Fasting",
            Instant.parse("2024-01-15T08:30:00Z")
        )

        // Assert
        assertThat(snapshot.syncStatus).isEqualTo(SyncStatus.CHANGES_QUEUED)
        assertThat(snapshot.queuedChanges).isEqualTo(1)
        assertThat(snapshot.readings).hasSize(1)
        assertThat(snapshot.readings.single().templateId).isEqualTo(template.id)
        assertThat(snapshot.readings.single().note).isEqualTo("Fasting")
    }

    @Test
    fun update_remote_reading_updates_cache_and_queues_a_put_operation() {
        // Arrange
        val repository = HealthPulseRepository(context, MemoryCredentials(), RecordingHttpClient())
        writeCache(
            JSONObject().put(
                "readings",
                JSONArray().put(readingJson("server-1", 5.2))
            )
        )
        val updated = HealthReading(
            id = "server-1",
            templateId = "glucose",
            templateName = "Blood glucose",
            value = 6.1,
            unit = "mmol/L",
            recordedAtUtc = "2024-01-15T08:30:00Z",
            note = "Updated"
        )

        // Act
        val snapshot = repository.updateReading(updated)

        // Assert
        assertThat(snapshot.readings.single().value).isEqualTo(6.1)
        assertThat(snapshot.queuedChanges).isEqualTo(1)
        val queue = JSONObject(context.filesDir.resolve("healthpulse-cache.json").readText())
            .getJSONArray("queue")
            .getJSONObject(0)
        assertThat(queue.getString("method")).isEqualTo("PUT")
        assertThat(queue.getString("path")).isEqualTo("/api/readings/server-1")
    }

    @Test
    fun delete_local_reading_removes_it_without_a_server_operation() {
        // Arrange
        val repository = HealthPulseRepository(context, MemoryCredentials(), RecordingHttpClient())
        writeCache(
            JSONObject().put(
                "readings",
                JSONArray().put(readingJson("local-1", 5.2))
            )
        )

        // Act
        val snapshot = repository.deleteReading("local-1")

        // Assert
        assertThat(snapshot.readings).isEmpty()
        assertThat(snapshot.queuedChanges).isEqualTo(0)
    }

    @Test
    fun delete_remote_reading_queues_a_delete_operation() {
        // Arrange
        val repository = HealthPulseRepository(context, MemoryCredentials(), RecordingHttpClient())
        writeCache(
            JSONObject().put(
                "readings",
                JSONArray().put(readingJson("server-1", 5.2))
            )
        )

        // Act
        val snapshot = repository.deleteReading("server-1")

        // Assert
        assertThat(snapshot.readings).isEmpty()
        assertThat(snapshot.queuedChanges).isEqualTo(1)
        val queue = JSONObject(context.filesDir.resolve("healthpulse-cache.json").readText())
            .getJSONArray("queue")
            .getJSONObject(0)
        assertThat(queue.getString("method")).isEqualTo("DELETE")
    }

    @Test
    fun tracking_change_updates_the_template_and_queues_the_matching_http_method() {
        // Arrange
        val repository = HealthPulseRepository(context, MemoryCredentials(), RecordingHttpClient())
        val template = template().copy(isTracked = true)
        writeCache(JSONObject().put("templates", JSONArray().put(template.toTestJson())))

        // Act
        val snapshot = repository.setTracking(template, false)

        // Assert
        assertThat(snapshot.templates.single().isTracked).isFalse()
        assertThat(snapshot.queuedChanges).isEqualTo(1)
        val queue = JSONObject(context.filesDir.resolve("healthpulse-cache.json").readText())
            .getJSONArray("queue")
            .getJSONObject(0)
        assertThat(queue.getString("method")).isEqualTo("DELETE")
        assertThat(queue.getString("path")).isEqualTo("/api/templates/glucose/track")
    }

    @Test
    fun saving_a_reminder_replaces_the_previous_reminder_for_the_same_template() {
        // Arrange
        val repository = HealthPulseRepository(context, MemoryCredentials(), RecordingHttpClient())
        val first = Reminder("glucose", ReminderCadence.DAILY)
        val replacement = Reminder("glucose", ReminderCadence.WEEKLY, intervalDays = 7)
        writeCache(JSONObject().put("reminders", JSONArray().put(first.toTestJson())))

        // Act
        val snapshot = repository.saveReminder(replacement)

        // Assert
        assertThat(snapshot.reminders).containsExactly(replacement)
        assertThat(snapshot.queuedChanges).isEqualTo(0)
    }

    @Test
    fun sign_out_clears_credentials_cache_and_local_data() {
        // Arrange
        val credentials = MemoryCredentials()
        val repository = HealthPulseRepository(context, credentials, RecordingHttpClient())
        repository.configureServer("https://health.example.test")
        credentials.putString("refresh_token", "refresh")
        writeCache(JSONObject().put("readings", JSONArray().put(readingJson("local-1", 5.2))))

        // Act
        repository.signOutAndClearData()

        // Assert
        val snapshot = repository.load()
        assertThat(snapshot.serverUrl).isNull()
        assertThat(snapshot.isSignedIn).isFalse()
        assertThat(snapshot.readings).isEmpty()
        assertThat(snapshot.syncStatus).isEqualTo(SyncStatus.SETUP_REQUIRED)
    }

    @Test
    fun sync_flushes_queued_operations_and_reads_all_server_pages() {
        // Arrange
        val credentials = MemoryCredentials()
        val http = SequenceHttpClient(
            listOf(
                HealthPulseHttpResponse(
                    201,
                    readingJson("server-1", 5.2).toString()
                ),
                HealthPulseHttpResponse(
                    200,
                    JSONArray().put(template().toTestJson()).toString()
                ),
                HealthPulseHttpResponse(
                    200,
                    JSONObject()
                        .put("items", JSONArray().put(readingJson("server-1", 5.2)))
                        .put("totalCount", 1)
                        .toString()
                )
            )
        )
        val repository = HealthPulseRepository(context, credentials, http)
        repository.configureServer("https://health.example.test")
        credentials.putString("refresh_token", "refresh")
        credentials.putString("access_token", "access")
        credentials.putLong("access_expires", System.currentTimeMillis() + 60_000)
        repository.addReading(template(), 5.2, null, Instant.parse("2024-01-15T08:30:00Z"))

        // Act
        val snapshot = runBlocking { repository.sync() }

        // Assert
        assertThat(snapshot.syncStatus).isEqualTo(SyncStatus.UP_TO_DATE)
        assertThat(snapshot.queuedChanges).isEqualTo(0)
        assertThat(snapshot.readings.single().id).isEqualTo("server-1")
        assertThat(http.requests.map { it.method }).containsExactly("POST", "GET", "GET").inOrder()
    }

    @Test
    fun complete_sign_in_exchanges_the_callback_and_starts_a_sync() {
        // Arrange
        val credentials = MemoryCredentials()
        val verifier = "a".repeat(64)
        val http = SequenceHttpClient(
            listOf(
                HealthPulseHttpResponse(
                    200,
                    JSONObject()
                        .put("accessToken", "hpma_access")
                        .put("refreshToken", "hpmr_refresh")
                        .put("accessTokenExpiresUtc", "2030-01-01T00:00:00Z")
                        .toString()
                ),
                HealthPulseHttpResponse(200, JSONArray().toString()),
                HealthPulseHttpResponse(
                    200,
                    JSONObject().put("items", JSONArray()).put("totalCount", 0).toString()
                )
            )
        )
        val repository = HealthPulseRepository(context, credentials, http)
        repository.configureServer("https://health.example.test")
        credentials.putString("pkce_state", "state-1")
        credentials.putString("pkce_verifier", verifier)

        // Act
        val snapshot = runBlocking {
            repository.completeSignIn(
                "healthpulse://auth/callback?code=hpac_code&state=state-1"
            )
        }

        // Assert
        assertThat(snapshot.isSignedIn).isTrue()
        assertThat(snapshot.syncStatus).isEqualTo(SyncStatus.UP_TO_DATE)
        assertThat(credentials.contains("pkce_state")).isFalse()
        assertThat(http.requests.first().url).isEqualTo("https://health.example.test/api/mobile/auth/token")
    }

    @Test
    fun configure_server_normalizes_the_url_and_clears_cached_data()
    {
        // Arrange
        val credentials = MemoryCredentials()
        val repository = HealthPulseRepository(context, credentials, RecordingHttpClient())
        repository.addReading(template(), 5.2, null, Instant.now())

        // Act
        repository.configureServer("  https://health.example.test/  ")

        // Assert
        val snapshot = repository.load()
        assertThat(snapshot.serverUrl).isEqualTo("https://health.example.test")
        assertThat(snapshot.readings).isEmpty()
        assertThat(snapshot.queuedChanges).isEqualTo(0)
        assertThat(snapshot.isSignedIn).isFalse()
    }

    @Test
    fun authorization_url_contains_the_mobile_callback_and_pkce_challenge()
    {
        // Arrange
        val repository = HealthPulseRepository(
            context,
            MemoryCredentials(),
            RecordingHttpClient()
        )
        repository.configureServer("https://health.example.test")

        // Act
        val authorizationUrl = repository.authorizationUrl()

        // Assert
        assertThat(authorizationUrl).startsWith("https://health.example.test/api/mobile/auth/authorize?")
        assertThat(authorizationUrl).contains("code_challenge=")
        assertThat(authorizationUrl).contains("redirect_uri=healthpulse%3A%2F%2Fauth%2Fcallback")
        assertThat(authorizationUrl).contains("state=")
    }

    @Test
    fun update_check_returns_an_available_release_when_the_version_is_newer()
    {
        // Arrange
        val credentials = MemoryCredentials()
        val http = RecordingHttpClient(
            HealthPulseHttpResponse(
                200,
                "{\"latestVersion\":\"2.0.0\",\"apkUrl\":\"https://example.test/app.apk\",\"releaseNotes\":\"Fixes\"}"
            )
        )
        val repository = HealthPulseRepository(context, credentials, http)
        repository.configureServer("https://health.example.test")

        // Act
        val result = runBlocking { repository.checkForUpdate("1.5.0") }

        // Assert
        assertThat(result).isEqualTo(
            UpdateCheck.Available("2.0.0", "https://example.test/app.apk", "Fixes")
        )
        assertThat(http.requests.single().url)
            .isEqualTo("https://health.example.test/.well-known/healthpulse-android-update")
    }

    private fun template() = HealthTemplate(
        id = "glucose",
        name = "Blood glucose",
        category = "Blood chemistry",
        normalizedUnit = "mmol/L",
        allowedUnits = listOf("mmol/L", "mg/dL"),
        isCustom = false,
        isTracked = true
    )

    private fun readingJson(id: String, value: Double): JSONObject = JSONObject()
        .put("id", id)
        .put("templateId", "glucose")
        .put("templateName", "Blood glucose")
        .put("value", value)
        .put("unit", "mmol/L")
        .put("recordedAtUtc", "2024-01-15T08:30:00Z")
        .put("note", JSONObject.NULL)

    private fun HealthTemplate.toTestJson(): JSONObject = JSONObject()
        .put("id", id)
        .put("name", name)
        .put("category", category)
        .put("normalizedUnit", normalizedUnit)
        .put("allowedUnits", JSONArray(allowedUnits))
        .put("isCustom", isCustom)
        .put("isTracked", isTracked)

    private fun Reminder.toTestJson(): JSONObject = JSONObject()
        .put("templateId", templateId)
        .put("cadence", cadence.name)
        .put("intervalDays", intervalDays)

    private fun writeCache(root: JSONObject) {
        context.filesDir.resolve("healthpulse-cache.json").writeText(root.toString())
    }

    private class MemoryCredentials : CredentialStore {
        private val values = ConcurrentHashMap<String, String>()

        override fun contains(key: String): Boolean = values.containsKey(key)

        override fun getString(key: String, defaultValue: String?): String? =
            values[key] ?: defaultValue

        override fun getLong(key: String, defaultValue: Long): Long =
            values[key]?.toLongOrNull() ?: defaultValue

        override fun putString(key: String, value: String) {
            values[key] = value
        }

        override fun putLong(key: String, value: Long) {
            values[key] = value.toString()
        }

        override fun remove(key: String) {
            values.remove(key)
        }

        override fun clear() {
            values.clear()
        }
    }

    private class RecordingHttpClient(
        private val response: HealthPulseHttpResponse = HealthPulseHttpResponse(200, "{}")
    ) : HealthPulseHttpClient {
        val requests = mutableListOf<RecordedRequest>()

        override fun request(
            url: String,
            method: String,
            payload: org.json.JSONObject?,
            accessToken: String?
        ): HealthPulseHttpResponse {
            requests += RecordedRequest(url, method, payload, accessToken)
            return response
        }
    }

    private class SequenceHttpClient(
        responses: List<HealthPulseHttpResponse>
    ) : HealthPulseHttpClient {
        private val responses = ArrayDeque(responses)
        val requests = mutableListOf<RecordedRequest>()

        override fun request(
            url: String,
            method: String,
            payload: JSONObject?,
            accessToken: String?
        ): HealthPulseHttpResponse {
            requests += RecordedRequest(url, method, payload, accessToken)
            return responses.removeFirst()
        }
    }

    private data class RecordedRequest(
        val url: String,
        val method: String,
        val payload: org.json.JSONObject?,
        val accessToken: String?
    )
}
