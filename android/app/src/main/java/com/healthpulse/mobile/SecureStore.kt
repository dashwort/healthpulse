package com.healthpulse.mobile

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.nio.charset.StandardCharsets
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Small encrypted key-value store for mobile session material. The AES key never leaves Android Keystore.
 */
class SecureStore(context: Context) : CredentialStore {
    private val preferences = context.getSharedPreferences("healthpulse.credentials", Context.MODE_PRIVATE)

    override fun contains(key: String): Boolean = preferences.contains(key)

    override fun getString(key: String, defaultValue: String?): String? {
        val encrypted = preferences.getString(key, null) ?: return defaultValue
        return runCatching { decrypt(encrypted) }.getOrDefault(defaultValue)
    }

    override fun getLong(key: String, defaultValue: Long): Long =
        getString(key)?.toLongOrNull() ?: defaultValue

    fun getString(key: String): String? = getString(key, null)

    fun getLong(key: String): Long = getLong(key, 0)

    override fun putString(key: String, value: String) {
        preferences.edit().putString(key, encrypt(value)).apply()
    }

    override fun putLong(key: String, value: Long) {
        putString(key, value.toString())
    }

    override fun remove(key: String) {
        preferences.edit().remove(key).apply()
    }

    override fun clear() {
        preferences.edit().clear().apply()
    }

    private fun encrypt(value: String): String {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, key)
        val encrypted = cipher.doFinal(value.toByteArray(StandardCharsets.UTF_8))
        return Base64.encodeToString(cipher.iv, Base64.NO_WRAP) + ":" +
            Base64.encodeToString(encrypted, Base64.NO_WRAP)
    }

    private fun decrypt(value: String): String {
        val parts = value.split(":", limit = 2)
        require(parts.size == 2)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(
            Cipher.DECRYPT_MODE,
            key,
            GCMParameterSpec(128, Base64.decode(parts[0], Base64.NO_WRAP))
        )
        return String(cipher.doFinal(Base64.decode(parts[1], Base64.NO_WRAP)), StandardCharsets.UTF_8)
    }

    private val key: SecretKey
        get() {
            val store = KeyStore.getInstance(ANDROID_KEY_STORE).apply { load(null) }
            (store.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
            val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, ANDROID_KEY_STORE)
            generator.init(
                KeyGenParameterSpec.Builder(
                    KEY_ALIAS,
                    KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
                )
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .build()
            )
            return generator.generateKey()
        }

    private companion object {
        const val ANDROID_KEY_STORE = "AndroidKeyStore"
        const val KEY_ALIAS = "healthpulse.mobile.session.v1"
        const val TRANSFORMATION = "AES/GCM/NoPadding"
    }
}
