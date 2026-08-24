package com.healthpulse.mobile

import android.app.AlarmManager
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat

class ReminderReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val name = intent.getStringExtra(ReminderScheduler.EXTRA_NAME) ?: "your measurement"
        ReminderScheduler.ensureChannel(context)
        val notification = NotificationCompat.Builder(context, ReminderScheduler.CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_dialog_info)
            .setContentTitle("Time for a HealthPulse reading")
            .setContentText("Record " + name + " when you are ready.")
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setAutoCancel(true)
            .build()
        NotificationManagerCompat.from(context).notify(intent.getIntExtra(ReminderScheduler.EXTRA_ID, 0), notification)
        ReminderScheduler.schedule(
            context,
            Reminder(
                intent.getStringExtra(ReminderScheduler.EXTRA_TEMPLATE_ID).orEmpty(),
                ReminderCadence.valueOf(intent.getStringExtra(ReminderScheduler.EXTRA_CADENCE) ?: "DAILY"),
                intent.getIntExtra(ReminderScheduler.EXTRA_INTERVAL, 1)
            ),
            name
        )
    }
}

object ReminderScheduler {
    const val CHANNEL_ID = "reading_reminders"
    const val EXTRA_ID = "id"
    const val EXTRA_NAME = "name"
    const val EXTRA_TEMPLATE_ID = "template_id"
    const val EXTRA_CADENCE = "cadence"
    const val EXTRA_INTERVAL = "interval"

    fun schedule(context: Context, reminder: Reminder, templateName: String) {
        val requestCode = reminder.templateId.hashCode()
        val pendingIntent = PendingIntent.getBroadcast(
            context,
            requestCode,
            Intent(context, ReminderReceiver::class.java)
                .putExtra(EXTRA_ID, requestCode)
                .putExtra(EXTRA_NAME, templateName)
                .putExtra(EXTRA_TEMPLATE_ID, reminder.templateId)
                .putExtra(EXTRA_CADENCE, reminder.cadence.name)
                .putExtra(EXTRA_INTERVAL, reminder.intervalDays),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE
        )
        val next = System.currentTimeMillis() + reminder.intervalDays.coerceIn(1, 365) * DAY_MILLIS
        context.getSystemService(AlarmManager::class.java)
            .setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, next, pendingIntent)
    }

    fun ensureChannel(context: Context) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            context.getSystemService(NotificationManager::class.java).createNotificationChannel(
                NotificationChannel(
                    CHANNEL_ID,
                    context.getString(R.string.notification_channel_name),
                    NotificationManager.IMPORTANCE_DEFAULT
                )
            )
        }
    }

    private const val DAY_MILLIS = 24L * 60L * 60L * 1000L
}
