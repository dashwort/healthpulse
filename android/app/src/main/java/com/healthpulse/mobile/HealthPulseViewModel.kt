package com.healthpulse.mobile

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.time.Instant

class HealthPulseViewModel(application: Application) : AndroidViewModel(application) {
    private val repository = HealthPulseRepository(application)
    private val _snapshot = MutableStateFlow(repository.load())
    val snapshot: StateFlow<AppSnapshot> = _snapshot.asStateFlow()

    private val _browserUrl = MutableStateFlow<String?>(null)
    val browserUrl: StateFlow<String?> = _browserUrl.asStateFlow()

    fun configureServer(serverUrl: String) {
        repository.configureServer(serverUrl)
        _snapshot.value = repository.load()
    }

    fun beginSignIn() {
        runCatching { repository.authorizationUrl() }
            .onSuccess { _browserUrl.value = it }
            .onFailure { showError(it.message ?: "Unable to begin sign-in.") }
    }

    fun browserOpened() {
        _browserUrl.value = null
    }

    fun receiveAuthCallback(uri: String) {
        viewModelScope.launch {
            _snapshot.value = _snapshot.value.copy(syncStatus = SyncStatus.SYNCING, errorMessage = null)
            runCatching { repository.completeSignIn(uri) }
                .onSuccess { _snapshot.value = it }
                .onFailure { showError(it.message ?: "Sign-in failed.") }
        }
    }

    fun sync() {
        if (!_snapshot.value.isSignedIn) return
        viewModelScope.launch {
            _snapshot.value = _snapshot.value.copy(syncStatus = SyncStatus.SYNCING, errorMessage = null)
            runCatching { repository.sync() }
                .onSuccess { _snapshot.value = it }
                .onFailure {
                    _snapshot.value = repository.load().copy(
                        syncStatus = if (_snapshot.value.queuedChanges > 0) SyncStatus.CHANGES_QUEUED else SyncStatus.OFFLINE,
                        errorMessage = it.message ?: "Changes remain safely queued on this device."
                    )
                }
        }
    }

    fun addReading(template: HealthTemplate, value: String, note: String, recordedAt: Instant): String? {
        val error = AppValidation.reading(value, note, recordedAt)
        if (error != null) return error
        _snapshot.value = repository.addReading(template, value.toDouble(), note, recordedAt)
        sync()
        return null
    }

    fun updateReading(reading: HealthReading) {
        _snapshot.value = repository.updateReading(reading)
        sync()
    }

    fun deleteReading(reading: HealthReading) {
        _snapshot.value = repository.deleteReading(reading.id)
        sync()
    }

    fun setTracking(template: HealthTemplate, track: Boolean) {
        _snapshot.value = repository.setTracking(template, track)
        sync()
    }

    fun saveReminder(reminder: Reminder) {
        _snapshot.value = repository.saveReminder(reminder)
        val templateName = _snapshot.value.templates.firstOrNull { it.id == reminder.templateId }?.name
            ?: "your measurement"
        ReminderScheduler.schedule(getApplication(), reminder, templateName)
    }

    fun checkForUpdate(currentVersion: String, callback: (UpdateCheck) -> Unit) {
        viewModelScope.launch {
            runCatching { repository.checkForUpdate(currentVersion) }
                .onSuccess(callback)
                .onFailure { callback(UpdateCheck.Unavailable) }
        }
    }

    fun signOut() {
        repository.signOutAndClearData()
        _snapshot.value = repository.load()
    }

    fun clearError() {
        _snapshot.value = _snapshot.value.copy(errorMessage = null)
    }

    private fun showError(message: String) {
        _snapshot.value = repository.load().copy(syncStatus = SyncStatus.ERROR, errorMessage = message)
    }
}
