package lav.visaroom.alav;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;

/**
 * Bounded bitmap decoding for bytes that ultimately came from another
 * account (avatars and chat wallpapers).  A compressed image can be only
 * a few dozen kilobytes while still declaring enormous dimensions, so a
 * plain decodeByteArray()/decodeFile() call could allocate hundreds of
 * megabytes and crash the viewer.  These helpers inspect the bounds
 * first and choose a power-of-two sample size that satisfies both a
 * maximum dimension and a maximum total pixel count.
 */
final class ImageDecode {

    private ImageDecode() {
    }

    static Bitmap decodeBytes(byte[] data, int maxDimension, int maxPixels) {
        if (data == null || data.length == 0) {
            return null;
        }
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeByteArray(data, 0, data.length, bounds);
        int sample = sampleSize(bounds.outWidth, bounds.outHeight,
                maxDimension, maxPixels);
        if (sample <= 0) {
            return null;
        }
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        return BitmapFactory.decodeByteArray(data, 0, data.length, options);
    }

    static Bitmap decodeFile(String path, int maxDimension, int maxPixels) {
        if (path == null || path.length() == 0) {
            return null;
        }
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeFile(path, bounds);
        int sample = sampleSize(bounds.outWidth, bounds.outHeight,
                maxDimension, maxPixels);
        if (sample <= 0) {
            return null;
        }
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sample;
        return BitmapFactory.decodeFile(path, options);
    }

    static int sampleSize(int width, int height,
                                  int maxDimension, int maxPixels) {
        if (width <= 0 || height <= 0 || maxDimension <= 0 || maxPixels <= 0) {
            return -1;
        }
        int sample = 1;
        while (sample <= (1 << 20)) {
            long sampledWidth = (width + (long) sample - 1L) / sample;
            long sampledHeight = (height + (long) sample - 1L) / sample;
            if (sampledWidth <= maxDimension
                    && sampledHeight <= maxDimension
                    && sampledWidth * sampledHeight <= maxPixels) {
                return sample;
            }
            sample *= 2;
        }
        return -1;
    }
}
