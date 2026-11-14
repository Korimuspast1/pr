package com.rootdb.pro.data

import android.util.Base64
import java.net.URLDecoder
import javax.crypto.Cipher
import javax.crypto.spec.IvParameterSpec
import javax.crypto.spec.SecretKeySpec

/** Декодеры значений и AES-расшифровка отдельных полей */
object CryptoTools {

    /** Все применимые декодирования для значения */
    fun decodeValue(raw: String): List<DecodedValue> {
        val out = mutableListOf<DecodedValue>()
        val s = raw.trim()
        if (s.isEmpty()) return out

        // Base64
        if (s.matches(Regex("^[A-Za-z0-9+/=_-]+$")) && s.length >= 8) {
            try {
                val bytes = Base64.decode(s, Base64.DEFAULT)
                val text = String(bytes, Charsets.UTF_8)
                if (text.isNotBlank() && text.all { it.isLetterOrDigit() || it.isWhitespace() || it in ".,;:!?()[]{}\"'@#\$%^&*-_=+<>|/\\~`" }) {
                    out += DecodedValue("Base64", text)
                }
            } catch (_: Exception) {
            }
        }
        // Hex
        if (s.matches(Regex("^[0-9a-fA-F]+$")) && s.length >= 4 && s.length % 2 == 0) {
            try {
                val bytes = s.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
                val text = String(bytes, Charsets.UTF_8)
                if (text.isNotBlank() && text.all { it.isLetterOrDigit() || it.isWhitespace() || it in ".,;:!?()[]{}\"'@#\$%^&*-_=+<>|/\\~`" }) {
                    out += DecodedValue("Hex → текст", text)
                }
                out += DecodedValue("Hex → Base64", Base64.encodeToString(bytes, Base64.NO_WRAP))
            } catch (_: Exception) {
            }
        }
        // URL
        if (s.contains("%")) {
            try {
                out += DecodedValue("URL-декод", URLDecoder.decode(s, "UTF-8"))
            } catch (_: Exception) {
            }
        }
        // Unicode-escape (\uXXXX)
        if (s.contains("\\u")) {
            try {
                val decoded = s.replace(Regex("\\\\u([0-9a-fA-F]{4})")) {
                    it.groupValues[1].toInt(16).toChar().toString()
                }
                if (decoded != s) out += DecodedValue("Unicode-escape", decoded)
            } catch (_: Exception) {
            }
        }
        // JSON pretty
        if ((s.startsWith("{") && s.endsWith("}")) || (s.startsWith("[") && s.endsWith("]"))) {
            try {
                val pretty = if (s.startsWith("{")) org.json.JSONObject(s).toString(2)
                else org.json.JSONArray(s).toString(2)
                out += DecodedValue("JSON", pretty)
            } catch (_: Exception) {
            }
        }
        // Обратное экранирование
        if (s.contains("\\n") || s.contains("\\t") || s.contains("\\\"")) {
            out += DecodedValue("Экранирование", s.replace("\\n", "\n").replace("\\t", "\t").replace("\\\"", "\""))
        }
        return out
    }

    /** AES-расшифровка: CBC (с IV) или ECB. Ключ — hex или пароль (SHA-256). */
    fun aesDecrypt(data: ByteArray, keyText: String, ivHex: String?, mode: String): String {
        val keyBytes = parseKey(keyText)
        val cipher = Cipher.getInstance("AES/$mode/PKCS5Padding")
        if (mode == "CBC") {
            val iv = ivHex?.trim()?.let { hexToBytes(it) } ?: ByteArray(16)
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(keyBytes, "AES"), IvParameterSpec(iv))
        } else {
            cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(keyBytes, "AES"))
        }
        val plain = cipher.doFinal(data)
        return String(plain, Charsets.UTF_8)
    }

    private fun parseKey(keyText: String): ByteArray {
        val k = keyText.trim()
        if (k.matches(Regex("^[0-9a-fA-F]+$")) && (k.length == 32 || k.length == 48 || k.length == 64)) {
            return hexToBytes(k)
        }
        // Пароль → SHA-256 (32 байта = AES-256)
        val digest = java.security.MessageDigest.getInstance("SHA-256")
        return digest.digest(k.toByteArray(Charsets.UTF_8))
    }

    fun hexToBytes(hex: String): ByteArray =
        hex.trim().chunked(2).map { it.toInt(16).toByte() }.toByteArray()

    fun bytesToHex(bytes: ByteArray): String = bytes.joinToString("") { "%02x".format(it) }

    /** Попытка расшифровать значение как Base64+AES (частый паттерн в приложениях) */
    fun tryBase64Aes(value: String, keyText: String, ivHex: String?, mode: String): String? {
        return try {
            val data = Base64.decode(value.trim(), Base64.DEFAULT)
            aesDecrypt(data, keyText, ivHex, mode)
        } catch (_: Exception) {
            null
        }
    }
}
