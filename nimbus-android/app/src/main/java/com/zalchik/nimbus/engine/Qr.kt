package com.zalchik.nimbus.engine

import com.google.zxing.BarcodeFormat
import com.google.zxing.BinaryBitmap
import com.google.zxing.DecodeHintType
import com.google.zxing.EncodeHintType
import com.google.zxing.LuminanceSource
import com.google.zxing.MultiFormatReader
import com.google.zxing.NotFoundException
import com.google.zxing.RGBLuminanceSource
import com.google.zxing.Result
import com.google.zxing.common.HybridBinarizer
import com.google.zxing.qrcode.QRCodeWriter
import com.google.zxing.qrcode.decoder.ErrorCorrectionLevel
import java.util.EnumMap

/**
 * QR codes: the link or config a user shares, as a picture another phone can scan — the part of
 * nimbus/qr.py the UI needs.
 *
 * Desktop carries its own encoder (Nayuki's reference implementation) to avoid a dependency for
 * one dialog; a phone already ships ZXing for the camera, so it does both directions with it.
 */
object Qr {
    /** The smallest level that still fits a long share link in a scannable code. */
    private val LEVEL = ErrorCorrectionLevel.M

    class QrError(message: String) : Exception(message)

    /** The code as a square of modules (true = dark), with a quiet zone of [margin]. */
    fun encode(text: String, size: Int = 512, margin: Int = 2): Array<BooleanArray> {
        if (text.isEmpty()) throw QrError("nothing to encode")
        val hints = EnumMap<EncodeHintType, Any>(EncodeHintType::class.java).apply {
            put(EncodeHintType.ERROR_CORRECTION, LEVEL)
            put(EncodeHintType.MARGIN, margin)
            put(EncodeHintType.CHARACTER_SET, "UTF-8")
        }
        val matrix = try {
            QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints)
        } catch (e: com.google.zxing.WriterException) {
            throw QrError(e.message ?: "too long for a QR code")
        }
        return Array(matrix.height) { y -> BooleanArray(matrix.width) { x -> matrix[x, y] } }
    }

    /** The code as ARGB pixels, for a Bitmap. */
    fun pixels(text: String, size: Int = 512, margin: Int = 2, dark: Int = 0xFF000000.toInt(), light: Int = 0xFFFFFFFF.toInt()): IntArray {
        val modules = encode(text, size, margin)
        val height = modules.size
        val width = modules.firstOrNull()?.size ?: 0
        return IntArray(width * height) { index ->
            if (modules[index / width][index % width]) dark else light
        }
    }

    /** What a camera frame or a picked image holds, or null when there's no code in it. */
    fun decode(pixels: IntArray, width: Int, height: Int): String? {
        val source: LuminanceSource = RGBLuminanceSource(width, height, pixels)
        return try {
            val result: Result = MultiFormatReader().decode(
                BinaryBitmap(HybridBinarizer(source)),
                mapOf(DecodeHintType.TRY_HARDER to true),
            )
            result.text
        } catch (e: NotFoundException) {
            null
        } catch (e: Exception) {
            null
        }
    }
}
