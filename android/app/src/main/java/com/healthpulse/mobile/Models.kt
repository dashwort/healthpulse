package com.healthpulse.mobile

import java.net.URI
import java.time.Instant

data class HealthTemplate(
    val id: String,
    val name: String,
    val category: String,
    val normalizedUnit: String,
    val allowedUnits: List<String>,
    val isCustom: Boolean,
    val isTracked: Boolean
)

data class HealthReading(
    val id: String,
    val templateId: String,
    val templateName: String,
    val value: Double,
    val unit: String,
    val recordedAtUtc: String,
    val note: String?
)

data class Reminder(
    val templateId: String,
    val cadence: ReminderCadence,
    val intervalDays: Int = 1
)

enum class ReminderCadence { DAILY, WEEKLY, CUSTOM }

enum class SyncStatus {
    SETUP_REQUIRED,
    OFFLINE,
    SYNCING,
    UP_TO_DATE,
    CHANGES_QUEUED,
    ERROR
}

data class AppSnapshot(
    val serverUrl: String? = null,
    val isSignedIn: Boolean = false,
    val templates: List<HealthTemplate> = emptyList(),
    val readings: List<HealthReading> = emptyList(),
    val reminders: List<Reminder> = emptyList(),
    val queuedChanges: Int = 0,
    val syncStatus: SyncStatus = SyncStatus.SETUP_REQUIRED,
    val errorMessage: String? = null
) {
    val trackedTemplates: List<HealthTemplate>
        get() = templates.filter { it.isTracked }
    val recentReadings: List<HealthReading>
        get() = readings.sortedByDescending { it.recordedAtUtc }.take(12)
}

object AppValidation {
    private val allowedCodeVerifier = Regex("^[A-Za-z0-9\\-._~]{43,128}$")

    fun serverUrl(value: String, allowHttp: Boolean): String? {
        val uri = runCatching { URI(value.trim()) }.getOrNull() ?: return "Enter a valid server address."
        if (uri.host.isNullOrBlank() || !uri.query.isNullOrBlank() || !uri.fragment.isNullOrBlank()) {
            return "Enter a valid server address."
        }
        if (uri.scheme != "https" && !(allowHttp && uri.scheme == "http")) {
            return "Use HTTPS. HTTP is only available in local debug builds."
        }
        return null
    }

    fun reading(value: String, note: String, recordedAt: Instant): String? {
        val numericValue = value.toDoubleOrNull()
        return when {
            numericValue == null -> "Enter a number."
            !numericValue.isFinite() || numericValue !in 0.0..1_000_000.0 ->
                "Enter a value between 0 and 1,000,000."
            note.length > 140 -> "Notes can contain up to 140 characters."
            recordedAt > Instant.now().plusSeconds(5 * 60) ->
                "A reading cannot be more than five minutes in the future."
            else -> null
        }
    }

    fun codeVerifier(value: String): Boolean = allowedCodeVerifier.matches(value)
}
