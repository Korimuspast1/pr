package lav.visaroom.alav;

import android.app.job.JobParameters;
import android.app.job.JobService;

import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;

/**
 * Periodic, battery-friendly background check for new cloud messages.
 * Android decides the exact execution window; the work itself is the
 * same single bounded chat-index request used by foreground checks.
 */
public class MessageCheckJobService extends JobService {

    private ExecutorService executor;
    private Future<?> running;

    @Override
    public boolean onStartJob(JobParameters params) {
        if (executor == null || executor.isShutdown()) {
            executor = Executors.newSingleThreadExecutor();
        }
        running = executor.submit(() -> {
            try {
                NotificationFx.checkNow(getApplicationContext());
            } finally {
                jobFinished(params, false);
            }
        });
        return true;
    }

    @Override
    public boolean onStopJob(JobParameters params) {
        if (running != null) {
            running.cancel(true);
            running = null;
        }
        return true;
    }

    @Override
    public void onDestroy() {
        if (running != null) {
            running.cancel(true);
        }
        if (executor != null) {
            executor.shutdownNow();
        }
        super.onDestroy();
    }
}
