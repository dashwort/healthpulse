package com.healthpulse.mobile

import android.content.Context
import org.json.JSONObject
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URI
import java.nio.charset.StandardCharsets

interface CredentialStore {
    fun contains(key: String): Boolean
    fun getString(key: String, defaultValue: String? = null): String?
    fun getLong(key: String, defaultValue: Long = 0): Long
    fun putString(key: String, value: String)
    fun putLong(key: String, value: Long)
    fun remove(key: String)
    fun clear()
}

data class HealthPulseHttpResponse(val status: Int, val body: String)

fun interface HealthPulseHttpClient {
    fun request(
        url: String,
        method: String,
        payload: JSONObject?,
        accessToken: String?
    ): HealthPulseHttpResponse
}

class UrlConnectionHealthPulseHttpClient : HealthPulseHttpClient {
    override fun request(
        url: String,
        method: String,
        payload: JSONObject?,
        accessToken: String?
    ): HealthPulseHttpResponse {
        val connection = (URI(url).toURL().openConnection() as HttpURLConnection).apply {
            requestMethod = method
            connectTimeout = 15_000
            readTimeout = 20_000
            setRequestProperty("Accept", "application/json")
            if (accessToken != null) setRequestProperty("Authorization", "Bearer $accessToken")
            if (payload != null) {
                doOutput = true
                setRequestProperty("Content-Type", "application/json")
                outputStream.use {
                    it.write(payload.toString().toByteArray(StandardCharsets.UTF_8))
                }
            }
        }
        return try {
            val status = connection.responseCode
            val stream = if (status in 200..299) connection.inputStream else connection.errorStream
            HealthPulseHttpResponse(
                status,
                stream?.bufferedReader()?.use { it.readText() }.orEmpty()
            )
        } catch (exception: IOException) {
            throw exception
        } finally {
            connection.disconnect()
        }
    }
}

interface HealthPulseRepositoryPort {
    fun load(): AppSnapshot
    fun configureServer(rawServerUrl: String)
    fun signOutAndClearData()
    fun authorizationUrl(): String
    suspend fun completeSignIn(callbackUri: String): AppSnapshot
    suspend fun sync(): AppSnapshot
    fun addReading(
        template: HealthTemplate,
        value: Double,
        note: String?,
        recordedAt: java.time.Instant
    ): AppSnapshot
    fun updateReading(reading: HealthReading): AppSnapshot
    fun deleteReading(readingId: String): AppSnapshot
    fun setTracking(template: HealthTemplate, shouldTrack: Boolean): AppSnapshot
    fun saveReminder(reminder: Reminder): AppSnapshot
    suspend fun checkForUpdate(currentVersion: String): UpdateCheck
}

fun interface ReminderSchedulerPort {
    fun schedule(context: Context, reminder: Reminder, templateName: String)
}

object AndroidReminderScheduler : ReminderSchedulerPort {
    override fun schedule(context: Context, reminder: Reminder, templateName: String) {
        ReminderScheduler.schedule(context, reminder, templateName)
    }
}
