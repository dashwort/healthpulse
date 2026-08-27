package com.healthpulse.mobile

import com.google.common.truth.Truth.assertThat
import java.time.Instant
import org.junit.Test

class AppValidationTest {
    @Test
    fun releaseBuildsRequireHttps() {
        // Arrange
        val serverUrl = "http://example.test"

        // Act
        val error = AppValidation.serverUrl(serverUrl, allowHttp = false)

        // Assert
        assertThat(error).isEqualTo("Use HTTPS. HTTP is only available in local debug builds.")
    }

    @Test
    fun debugBuildsPermitLocalHttp() {
        // Arrange
        val serverUrl = "http://10.0.2.2:5000"

        // Act
        val error = AppValidation.serverUrl(serverUrl, allowHttp = true)

        // Assert
        assertThat(error).isNull()
    }

    @Test
    fun serverUrlsWithQueriesOrFragmentsAreRejected() {
        // Arrange
        val serverUrl = "https://example.test/path?tenant=one"

        // Act
        val error = AppValidation.serverUrl(serverUrl, allowHttp = false)

        // Assert
        assertThat(error).isEqualTo("Enter a valid server address.")
    }

    @Test
    fun readingValidation_rejects_non_numeric_values() {
        // Arrange

        // Act
        val error = AppValidation.reading("not-a-number", "", Instant.now())

        // Assert
        assertThat(error).isEqualTo("Enter a number.")
    }

    @Test
    fun readingValidation_rejects_values_outside_the_supported_range() {
        // Arrange

        // Act
        val error = AppValidation.reading("1000001", "", Instant.now())

        // Assert
        assertThat(error).isEqualTo("Enter a value between 0 and 1,000,000.")
    }

    @Test
    fun readingValidation_rejects_notes_over_140_characters() {
        // Arrange
        val note = "x".repeat(141)

        // Act
        val error = AppValidation.reading("5", note, Instant.now())

        // Assert
        assertThat(error).isEqualTo("Notes can contain up to 140 characters.")
    }

    @Test
    fun readingValidation_rejects_timestamps_more_than_five_minutes_in_the_future() {
        // Arrange
        val recordedAt = Instant.now().plusSeconds(6 * 60)

        // Act
        val error = AppValidation.reading("5", "", recordedAt)

        // Assert
        assertThat(error).isEqualTo("A reading cannot be more than five minutes in the future.")
    }

    @Test
    fun historical_reading_dates_are_allowed() {
        // Arrange
        val recordedAt = Instant.parse("2024-01-15T08:30:00Z")

        // Act
        val error = AppValidation.reading("5.2", "Backdated", recordedAt)

        // Assert
        assertThat(error).isNull()
    }

    @Test
    fun code_verifier_accepts_the_minimum_pkce_length() {
        // Arrange
        val validVerifier = "a".repeat(43)

        // Act
        val valid = AppValidation.codeVerifier(validVerifier)

        // Assert
        assertThat(valid).isTrue()
    }

    @Test
    fun code_verifier_rejects_values_shorter_than_the_pkce_minimum() {
        // Arrange
        val invalidVerifier = "a".repeat(42)

        // Act
        val valid = AppValidation.codeVerifier(invalidVerifier)

        // Assert
        assertThat(valid).isFalse()
    }
}
