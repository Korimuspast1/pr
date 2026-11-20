package lav.bottleflip.alav;

import android.media.AudioAttributes;
import android.media.AudioFormat;
import android.media.AudioManager;
import android.media.AudioTrack;
import android.os.Build;

/** Короткие синтезированные звуки (PCM генерируется в коде, без ассетов). */
final class SoundFx {

    private static final int RATE = 22050;
    private AudioTrack jump;
    private AudioTrack land;
    private AudioTrack fail;
    private boolean ready;

    synchronized void init() {
        if (ready) {
            return;
        }
        try {
            jump = make(sweep(480, 900, 0.12, false));
            land = make(sweep(220, 90, 0.10, true));
            fail = make(sweep(330, 90, 0.38, true));
            ready = true;
        } catch (RuntimeException ignored) {
            // Звук — необязателен; без него игра работает.
        }
    }

    void playJump() {
        play(jump);
    }

    void playLand() {
        play(land);
    }

    void playFail() {
        play(fail);
    }

    private static void play(AudioTrack track) {
        if (track == null) {
            return;
        }
        try {
            track.setPlaybackHeadPosition(0);
            track.play();
        } catch (RuntimeException ignored) {
        }
    }

    private static AudioTrack make(short[] pcm) {
        AudioTrack track;
        if (Build.VERSION.SDK_INT >= 23) {
            track = new AudioTrack.Builder()
                    .setAudioAttributes(new AudioAttributes.Builder()
                            .setUsage(AudioAttributes.USAGE_GAME)
                            .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                            .build())
                    .setAudioFormat(new AudioFormat.Builder()
                            .setSampleRate(RATE)
                            .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                            .setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                            .build())
                    .setBufferSizeInBytes(pcm.length * 2)
                    .setTransferMode(AudioTrack.MODE_STATIC)
                    .build();
        } else {
            track = new AudioTrack(AudioManager.STREAM_MUSIC, RATE,
                    AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT,
                    pcm.length * 2, AudioTrack.MODE_STATIC);
        }
        track.write(pcm, 0, pcm.length);
        return track;
    }

    /** Синус-свип с затуханием; noisy добавляет призвук «глухого удара». */
    private static short[] sweep(double f0, double f1, double seconds,
                                 boolean noisy) {
        int n = (int) (RATE * seconds);
        short[] out = new short[n];
        double phase = 0;
        java.util.Random rng = new java.util.Random(42);
        for (int i = 0; i < n; i++) {
            double t = (double) i / n;
            double freq = f0 + (f1 - f0) * t;
            phase += 2.0 * Math.PI * freq / RATE;
            double env = Math.pow(1.0 - t, 1.6);
            double s = Math.sin(phase) * env;
            if (noisy) {
                s = (s * 0.7 + (rng.nextDouble() * 2 - 1) * 0.3) * env;
            }
            out[i] = (short) (s * 9000);
        }
        return out;
    }
}
