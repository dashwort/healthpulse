package com.healthpulse.mobile

import androidx.test.core.app.ApplicationProvider
import com.google.common.truth.Truth.assertThat
import org.junit.Before
import org.junit.Test

class SecureStoreInstrumentedTest {
    private lateinit var store: SecureStore

    @Before
    fun setUp() {
        store = SecureStore(ApplicationProvider.getApplicationContext())
        store.clear()
    }

    @Test
    fun encrypted_values_round_trip_and_clear()
    {
        // Arrange

        // Act
        store.putString("access", "secret-value")

        // Assert
        assertThat(store.contains("access")).isTrue()
        assertThat(store.getString("access")).isEqualTo("secret-value")
    }

    @Test
    fun long_values_round_trip_as_numbers()
    {
        // Arrange
        val value = 123456789L

        // Act
        store.putLong("expires", value)

        // Assert
        assertThat(store.getLong("expires")).isEqualTo(value)
    }
}
