package com.healthpulse.mobile

import android.Manifest
import androidx.compose.ui.test.assertIsNotEnabled
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.test.rule.GrantPermissionRule
import org.junit.Rule
import org.junit.Test

class MainActivityInstrumentedTest {
    @get:Rule(order = 0)
    val permissionRule = GrantPermissionRule.grant(Manifest.permission.POST_NOTIFICATIONS)

    @get:Rule(order = 1)
    val composeRule = createAndroidComposeRule<MainActivity>()

    @Test
    fun setup_screen_requires_a_server_address_before_sign_in() {
        // Arrange
        val signInButton = composeRule.onNodeWithText("Continue with Google")

        // Act
        composeRule.waitForIdle()

        // Assert
        signInButton.assertIsNotEnabled()
        composeRule.onNodeWithText("Server address").assertExists()
    }
}
