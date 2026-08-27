package com.healthpulse.mobile

import android.app.NotificationManager
import androidx.test.core.app.ApplicationProvider
import com.google.common.truth.Truth.assertThat
import org.junit.Test

class ReminderReceiverInstrumentedTest {
    @Test
    fun reminder_channel_is_created_through_the_public_scheduler_api()
    {
        // Arrange
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()

        // Act
        ReminderScheduler.ensureChannel(context)

        // Assert
        val manager = context.getSystemService(NotificationManager::class.java)
        assertThat(manager.getNotificationChannel(ReminderScheduler.CHANNEL_ID)).isNotNull()
    }
}
