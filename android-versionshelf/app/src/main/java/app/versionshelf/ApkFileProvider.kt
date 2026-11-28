package app.versionshelf

import android.content.ContentProvider
import android.content.ContentValues
import android.database.Cursor
import android.database.MatrixCursor
import android.net.Uri
import android.os.Environment
import android.os.ParcelFileDescriptor
import android.provider.OpenableColumns
import java.io.File
import java.io.FileNotFoundException

/** A deliberately narrow content provider: it exposes only completed APKs owned by this app. */
class ApkFileProvider : ContentProvider() {
    override fun onCreate(): Boolean = true

    override fun getType(uri: Uri): String {
        checkedFile(uri)
        return "application/vnd.android.package-archive"
    }

    override fun openFile(uri: Uri, mode: String): ParcelFileDescriptor {
        if (mode != "r") throw FileNotFoundException("Read-only provider")
        return ParcelFileDescriptor.open(checkedFile(uri), ParcelFileDescriptor.MODE_READ_ONLY)
    }

    override fun query(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?,
    ): Cursor {
        val file = checkedFile(uri)
        val columns = projection ?: arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE)
        return MatrixCursor(columns).apply {
            val row = newRow()
            columns.forEach { column ->
                when (column) {
                    OpenableColumns.DISPLAY_NAME -> row.add(file.name)
                    OpenableColumns.SIZE -> row.add(file.length())
                    else -> row.add(null)
                }
            }
        }
    }

    override fun insert(uri: Uri, values: ContentValues?): Uri = throw UnsupportedOperationException()
    override fun delete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int =
        throw UnsupportedOperationException()
    override fun update(uri: Uri, values: ContentValues?, selection: String?, selectionArgs: Array<out String>?): Int =
        throw UnsupportedOperationException()

    private fun checkedFile(uri: Uri): File {
        val context = context ?: throw FileNotFoundException("Provider has no context")
        if (uri.authority != authority(context) || uri.pathSegments.size != 2 || uri.pathSegments[0] != "apk") {
            throw FileNotFoundException("Unknown APK URI")
        }
        val name = uri.pathSegments[1]
        if (!SAFE_NAME.matches(name)) throw FileNotFoundException("Unsafe APK name")
        val directory = downloadDirectory(context).canonicalFile
        val file = File(directory, name).canonicalFile
        if (file.parentFile != directory || !file.isFile) throw FileNotFoundException("APK does not exist")
        return file
    }

    companion object {
        private val SAFE_NAME = Regex("^[A-Za-z0-9._-]+\\.apk$")

        fun downloadDirectory(context: android.content.Context): File {
            return (context.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS) ?: context.filesDir)
                .resolve("verified-apks")
                .apply { mkdirs() }
        }

        fun uriFor(context: android.content.Context, apk: File): Uri {
            return Uri.Builder()
                .scheme("content")
                .authority(authority(context))
                .appendPath("apk")
                .appendPath(apk.name)
                .build()
        }

        private fun authority(context: android.content.Context): String =
            "${context.packageName}.apkfileprovider"
    }
}
