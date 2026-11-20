package m.chatrulet.m

import android.os.Build
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyFactory
import java.security.KeyPair
import java.security.KeyPairGenerator
import java.security.KeyStore
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.SecureRandom
import java.security.spec.ECGenParameterSpec
import java.security.spec.X509EncodedKeySpec
import javax.crypto.Cipher
import javax.crypto.KeyAgreement
import javax.crypto.KeyGenerator
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * ════════════════════════════════════════════════════════════════════
 *  Настоящее сквозное шифрование (E2E).
 *
 *  AES с длиной ключа 512 бит НЕ существует: стандарт AES допускает
 *  максимум 256 бит. Поэтому здесь используется максимально сильный
 *  существующий вариант:
 *
 *   • ECDH на кривой P-256 — обмен ключами прямо между двумя телефонами
 *     (публичные ключи передаются через Firestore, приватные не покидают устройство);
 *   • SHA-256 — вывод сессионного ключа из общего секрета;
 *   • AES-256-GCM — шифрование каждого сообщения чата (аутентифицированное).
 *
 *  Ключ живёт только в памяти и только на время одного звонка:
 *  ни сервер, ни Firestore прочитать сообщения не могут.
 *  Поверх этого сам WebRTC-канал (DataChannel) уже шифрован DTLS —
 *  получается двойной слой.
 * ════════════════════════════════════════════════════════════════════
 */
object E2E {
    private val rnd = SecureRandom()

    fun keyPair(): KeyPair = KeyPairGenerator.getInstance("EC").run {
        initialize(ECGenParameterSpec("secp256r1"))
        generateKeyPair()
    }

    fun pubB64(kp: KeyPair): String = Base64.encodeToString(kp.public.encoded, Base64.NO_WRAP)

    /** ECDH(priv мой, pub собеседника) → SHA-256 → AES-256 ключ. */
    fun derive(priv: PrivateKey, peerPubB64: String): SecretKeySpec {
        val pub = KeyFactory.getInstance("EC")
            .generatePublic(X509EncodedKeySpec(Base64.decode(peerPubB64, Base64.NO_WRAP)))
        val ka = KeyAgreement.getInstance("ECDH")
        ka.init(priv)
        ka.doPhase(pub, true)
        val digest = MessageDigest.getInstance("SHA-256")
        digest.update(ka.generateSecret())
        digest.update("GlassRoulette/E2E/v1".toByteArray(Charsets.UTF_8))
        return SecretKeySpec(digest.digest(), "AES")
    }

    fun encrypt(key: SecretKeySpec, plain: String): String {
        val iv = ByteArray(12).also { rnd.nextBytes(it) }
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        c.init(Cipher.ENCRYPT_MODE, key, GCMParameterSpec(128, iv))
        val ct = c.doFinal(plain.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(iv + ct, Base64.NO_WRAP)
    }

    fun decrypt(key: SecretKeySpec, data: String): String? = try {
        val raw = Base64.decode(data, Base64.NO_WRAP)
        val iv = raw.copyOfRange(0, 12)
        val ct = raw.copyOfRange(12, raw.size)
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        c.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(128, iv))
        String(c.doFinal(ct), Charsets.UTF_8)
    } catch (e: Throwable) {
        null
    }
}

/**
 * Локальное «хранилище» для чувствительных данных (список блокировок).
 * Ключ AES-256 лежит в аппаратном AndroidKeyStore и не извлекается из устройства.
 * На устройствах без KeyStore — программный ключ в приватном хранилище приложения (всё равно AES-256-GCM).
 */
object Vault {
    private const val KEY_ALIAS = "gr_vault_key"
    private const val SOFT_KEY_PREF = "gr_vault_soft"
    private val rnd = SecureRandom()

    fun init() {
        if (Build.VERSION.SDK_INT >= 23) {
            try {
                val ks = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
                if (!ks.containsAlias(KEY_ALIAS)) {
                    val gen = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
                    gen.init(
                        KeyGenParameterSpec.Builder(
                            KEY_ALIAS,
                            KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT,
                        )
                            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                            .setKeySize(256)
                            .build(),
                    )
                    gen.generateKey()
                }
            } catch (e: Throwable) {
                // останемся на программном ключе
            }
        }
    }

    private fun softKey(): SecretKeySpec {
        // программный ключ, лежит в приватных prefs (доступен только этому приложению)
        val existing = Store.prefs.getString(SOFT_KEY_PREF, null)
        if (existing != null) {
            return SecretKeySpec(Base64.decode(existing, Base64.NO_WRAP), "AES")
        }
        val raw = ByteArray(32).also { rnd.nextBytes(it) }
        Store.prefs.edit().putString(SOFT_KEY_PREF, Base64.encodeToString(raw, Base64.NO_WRAP)).apply()
        return SecretKeySpec(raw, "AES")
    }

    private fun cipher(encrypt: Boolean, iv: ByteArray?): Cipher {
        val c = Cipher.getInstance("AES/GCM/NoPadding")
        val key = if (Build.VERSION.SDK_INT >= 23) {
            try {
                val ks = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
                (ks.getEntry(KEY_ALIAS, null) as? KeyStore.SecretKeyEntry)?.secretKey ?: softKey()
            } catch (e: Throwable) {
                softKey()
            }
        } else {
            softKey()
        }
        if (encrypt) c.init(Cipher.ENCRYPT_MODE, key) else c.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(128, iv))
        return c
    }

    fun encrypt(plain: String): String {
        return try {
            val c = cipher(true, null)
            val iv = c.iv
            val ct = c.doFinal(plain.toByteArray(Charsets.UTF_8))
            Base64.encodeToString(iv + ct, Base64.NO_WRAP)
        } catch (e: Throwable) {
            // fallback: программный ключ с явным IV
            val iv = ByteArray(12).also { rnd.nextBytes(it) }
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.ENCRYPT_MODE, softKey(), GCMParameterSpec(128, iv))
            Base64.encodeToString(iv + c.doFinal(plain.toByteArray(Charsets.UTF_8)), Base64.NO_WRAP)
        }
    }

    fun decrypt(data: String): String? = try {
        val raw = Base64.decode(data, Base64.NO_WRAP)
        val iv = raw.copyOfRange(0, 12)
        val ct = raw.copyOfRange(12, raw.size)
        val c = cipher(false, iv)
        String(c.doFinal(ct), Charsets.UTF_8)
    } catch (e: Throwable) {
        try {
            val raw = Base64.decode(data, Base64.NO_WRAP)
            val iv = raw.copyOfRange(0, 12)
            val ct = raw.copyOfRange(12, raw.size)
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.DECRYPT_MODE, softKey(), GCMParameterSpec(128, iv))
            String(c.doFinal(ct), Charsets.UTF_8)
        } catch (e2: Throwable) {
            null
        }
    }
}
