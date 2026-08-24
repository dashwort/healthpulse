package com.healthpulse.mobile

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import java.time.Instant

class AppValidationTest {
    @Test
    fun releaseBuildsRequireHttps() {
        assertEquals(
            "Use HTTPS. HTTP is only available in local debug builds.",
            AppValidation.serverUrl("http://example.test", allowHttp = false)
        )
        assertNull(AppValidation.serverUrl("https://example.test/healthpulse", allowHttp = false))
    }

    @Test
    fun debugBuildsPermitLocalHttp() {
        assertNull(AppValidation.serverUrl("http://10.0.2.2:5000", allowHttp = true))
    }

    @Test
    fun readingValidationProtectsLocalQueue() {
        assertEquals(
            "Enter a number.",
            AppValidation.reading("not-a-number", "", Instant.now())
        )
        assertEquals(
            "Notes can contain up to 140 characters.",
            AppValidation.reading("5", "x".repeat(141), Instant.now())
        )
        assertNull(AppValidation.reading("5.2", "Fasting", Instant.now()))
    }

    @Test
    fun historicalReadingDatesAreAllowed() {
        assertNull(AppValidation.reading("5.2", "Backdated", Instant.parse("2024-01-15T08:30:00Z")))
    }
}
