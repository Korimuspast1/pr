package com.glass.launcher

import android.content.Context
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.net.Uri
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.File

object Media {
    /** Decode an image from [uri], downscale to [maxSize] and store it inside app files. Returns the absolute path. */
    fun saveScaled(ctx: Context, uri: Uri, dir: String, name: String, maxSize: Int, png: Boolean): String? = try {
        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        ctx.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, bounds) }
        var sample = 1
        while (bounds.outWidth / (sample * 2) >= maxSize && bounds.outHeight / (sample * 2) >= maxSize) sample *= 2
        val opts = BitmapFactory.Options().apply { inSampleSize = sample }
        val bmp = ctx.contentResolver.openInputStream(uri)?.use { BitmapFactory.decodeStream(it, null, opts) }
        if (bmp == null) null else {
            val scale = minOf(1f, maxSize.toFloat() / maxOf(bmp.width, bmp.height))
            val out = if (scale < 1f) Bitmap.createScaledBitmap(bmp, (bmp.width * scale).toInt().coerceAtLeast(1),
                (bmp.height * scale).toInt().coerceAtLeast(1), true) else bmp
            val folder = File(ctx.filesDir, dir).apply { mkdirs() }
            val f = File(folder, name)
            f.outputStream().use { out.compress(if (png) Bitmap.CompressFormat.PNG else Bitmap.CompressFormat.JPEG, 92, it) }
            f.absolutePath
        }
    } catch (e: Throwable) {
        null
    }
}

object Wallpaper {
    private var cache: Pair<String, ImageBitmap>? = null

    @Composable
    fun bitmap(ctx: Context): ImageBitmap? {
        val path = Store.bgImage
        val state by produceState(cache?.takeIf { it.first == path }?.second, path) {
            if (value == null && path.isNotBlank()) {
                value = withContext(Dispatchers.IO) {
                    try {
                        BitmapFactory.decodeFile(path)?.asImageBitmap()
                    } catch (e: Throwable) {
                        null
                    }
                }?.also { cache = path to it }
            }
        }
        return state
    }
}
