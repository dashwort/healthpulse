package com.healthpulse.mobile

import android.app.Application
import androidx.test.core.app.ApplicationProvider
import com.google.common.truth.Truth.assertThat
import java.time.Instant
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.annotation.Config
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [36])
class HealthPulseViewModelTest {
    @Test
    fun add_reading_returns_validation_error_without_writing_to_the_repository() {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)
        val template = template()

        // Act
        val error = viewModel.addReading(
            template,
            "not-a-number",
            "",
            Instant.parse("2024-01-15T08:30:00Z")
        )

        // Assert
        assertThat(error).isEqualTo("Enter a number.")
        assertThat(repository.addReadingCalls).isEqualTo(0)
    }

    @Test
    fun configure_server_updates_the_public_snapshot()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)

        // Act
        viewModel.configureServer("https://health.example.test")

        // Assert
        assertThat(viewModel.snapshot.value.serverUrl).isEqualTo("https://health.example.test")
        assertThat(viewModel.snapshot.value.syncStatus).isEqualTo(SyncStatus.OFFLINE)
    }

    @Test
    fun save_reminder_persists_it_and_calls_the_scheduler_port()
    {
        // Arrange
        val repository = FakeRepository()
        val scheduler = RecordingScheduler()
        val viewModel = createViewModel(repository, scheduler)
        val reminder = Reminder("glucose", ReminderCadence.WEEKLY)

        // Act
        viewModel.saveReminder(reminder)

        // Assert
        assertThat(repository.savedReminder).isEqualTo(reminder)
        assertThat(scheduler.reminder).isEqualTo(reminder)
        assertThat(scheduler.templateName).isEqualTo("Blood glucose")
    }

    @Test
    fun begin_sign_in_exposes_the_repository_authorization_url()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)

        // Act
        viewModel.beginSignIn()

        // Assert
        assertThat(viewModel.browserUrl.value).isEqualTo("https://example.test/authorize")
    }

    @Test
    fun browser_opened_clears_the_pending_authorization_url()
    {
        // Arrange
        val viewModel = createViewModel(FakeRepository())
        viewModel.beginSignIn()

        // Act
        viewModel.browserOpened()

        // Assert
        assertThat(viewModel.browserUrl.value).isNull()
    }

    @Test
    fun sign_out_reloads_the_cleared_repository_snapshot()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)

        // Act
        viewModel.signOut()

        // Assert
        assertThat(viewModel.snapshot.value.isSignedIn).isFalse()
        assertThat(viewModel.snapshot.value.templates).isEmpty()
    }

    @Test
    fun clear_error_removes_the_visible_error_from_the_snapshot()
    {
        // Arrange
        val repository = FakeRepository(initialErrorMessage = "Network unavailable")
        val viewModel = createViewModel(repository)

        // Act
        viewModel.clearError()

        // Assert
        assertThat(viewModel.snapshot.value.errorMessage).isNull()
    }

    @Test
    fun update_action_uses_the_public_repository_port()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)
        val reading = HealthReading(
            id = "server-1",
            templateId = "glucose",
            templateName = "Blood glucose",
            value = 5.2,
            unit = "mmol/L",
            recordedAtUtc = "2024-01-15T08:30:00Z",
            note = null
        )
        // Act
        viewModel.updateReading(reading)

        // Assert
        assertThat(repository.updatedReading).isEqualTo(reading)
    }

    @Test
    fun delete_action_uses_the_public_repository_port()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)
        val reading = HealthReading(
            id = "server-1",
            templateId = "glucose",
            templateName = "Blood glucose",
            value = 5.2,
            unit = "mmol/L",
            recordedAtUtc = "2024-01-15T08:30:00Z",
            note = null
        )

        // Act
        viewModel.deleteReading(reading)

        // Assert
        assertThat(repository.deletedReadingId).isEqualTo("server-1")
    }

    @Test
    fun tracking_action_uses_the_public_repository_port()
    {
        // Arrange
        val repository = FakeRepository()
        val viewModel = createViewModel(repository)
        val template = template()

        // Act
        viewModel.setTracking(template, false)

        // Assert
        assertThat(repository.trackingChange).isEqualTo(template to false)
    }

    private fun createViewModel(
        repository: FakeRepository,
        scheduler: RecordingScheduler = RecordingScheduler()
    ): HealthPulseViewModel = HealthPulseViewModel(
        ApplicationProvider.getApplicationContext<Application>(),
        repository,
        scheduler
    )

    private fun template() = HealthTemplate(
        id = "glucose",
        name = "Blood glucose",
        category = "Blood chemistry",
        normalizedUnit = "mmol/L",
        allowedUnits = listOf("mmol/L"),
        isCustom = false,
        isTracked = true
    )

    private class RecordingScheduler : ReminderSchedulerPort {
        var reminder: Reminder? = null
        var templateName: String? = null

        override fun schedule(
            context: android.content.Context,
            reminder: Reminder,
            templateName: String
        ) {
            this.reminder = reminder
            this.templateName = templateName
        }
    }

    private class FakeRepository(
        initialErrorMessage: String? = null
    ) : HealthPulseRepositoryPort {
        private val trackedTemplate = HealthTemplate(
            id = "glucose",
            name = "Blood glucose",
            category = "Blood chemistry",
            normalizedUnit = "mmol/L",
            allowedUnits = listOf("mmol/L"),
            isCustom = false,
            isTracked = true
        )
        private var currentSnapshot = AppSnapshot(
            isSignedIn = true,
            templates = listOf(trackedTemplate),
            syncStatus = SyncStatus.UP_TO_DATE,
            errorMessage = initialErrorMessage
        )
        var addReadingCalls = 0
        var savedReminder: Reminder? = null
        var updatedReading: HealthReading? = null
        var deletedReadingId: String? = null
        var trackingChange: Pair<HealthTemplate, Boolean>? = null

        override fun load(): AppSnapshot = currentSnapshot

        override fun configureServer(rawServerUrl: String) {
            currentSnapshot = currentSnapshot.copy(
                serverUrl = rawServerUrl,
                syncStatus = SyncStatus.OFFLINE
            )
        }

        override fun signOutAndClearData() {
            currentSnapshot = AppSnapshot()
        }

        override fun authorizationUrl(): String = "https://example.test/authorize"

        override suspend fun completeSignIn(callbackUri: String): AppSnapshot = currentSnapshot

        override suspend fun sync(): AppSnapshot = currentSnapshot

        override fun addReading(
            template: HealthTemplate,
            value: Double,
            note: String?,
            recordedAt: Instant
        ): AppSnapshot {
            addReadingCalls++
            return currentSnapshot
        }

        override fun updateReading(reading: HealthReading): AppSnapshot {
            updatedReading = reading
            return currentSnapshot
        }

        override fun deleteReading(readingId: String): AppSnapshot {
            deletedReadingId = readingId
            return currentSnapshot
        }

        override fun setTracking(template: HealthTemplate, shouldTrack: Boolean): AppSnapshot {
            trackingChange = template to shouldTrack
            return currentSnapshot
        }

        override fun saveReminder(reminder: Reminder): AppSnapshot {
            savedReminder = reminder
            currentSnapshot = currentSnapshot.copy(reminders = listOf(reminder))
            return currentSnapshot
        }

        override suspend fun checkForUpdate(currentVersion: String): UpdateCheck = UpdateCheck.UpToDate
    }
}
