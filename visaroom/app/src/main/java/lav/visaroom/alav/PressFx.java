package lav.visaroom.alav;

import android.app.Activity;
import android.app.Dialog;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.animation.DecelerateInterpolator;
import android.view.animation.OvershootInterpolator;
import android.widget.Button;
import android.widget.CompoundButton;
import android.widget.EditText;
import android.widget.HorizontalScrollView;
import android.widget.ImageButton;
import android.widget.ScrollView;

import java.util.WeakHashMap;

/**
 * Small spring-like press feedback for buttons, rows and chat bubbles.
 * A touch compresses the view smoothly; releasing it lets the view spring
 * back with a tiny overshoot. The listener never consumes the event, so
 * normal clicks, long-presses and scrolling keep working.
 */
final class PressFx {

    private static final WeakHashMap<View, Boolean> ATTACHED = new WeakHashMap<>();
    private static final WeakHashMap<View, Runnable> RELEASES = new WeakHashMap<>();
    private static final WeakHashMap<View, View.OnTouchListener> EXTRA_TOUCH =
            new WeakHashMap<>();

    private PressFx() {
    }

    static void install(Activity activity) {
        if (activity == null || activity.getWindow() == null) {
            return;
        }
        View root = activity.getWindow().getDecorView();
        root.post(() -> applyTree(root));
        root.postDelayed(() -> applyTree(root), 450L);
    }

    static void install(Dialog dialog) {
        if (dialog == null || dialog.getWindow() == null) {
            return;
        }
        View root = dialog.getWindow().getDecorView();
        root.post(() -> applyTree(root));
        root.postDelayed(() -> applyTree(root), 80L);
    }

    static void applyTree(View root) {
        if (root == null) {
            return;
        }
        if (shouldAttach(root)) {
            attach(root);
        }
        if (root instanceof ViewGroup) {
            ViewGroup group = (ViewGroup) root;
            for (int i = 0; i < group.getChildCount(); i++) {
                applyTree(group.getChildAt(i));
            }
        }
    }

    /**
     * Attaches the spring press effect while preserving a feature's
     * own gesture listener. Chat bubbles use this for swipe-to-reply:
     * Android permits only one OnTouchListener per View, so the two
     * behaviours have to be chained deliberately here.
     */
    static void attachWithTouch(View view, View.OnTouchListener extraTouch) {
        if (view == null) {
            return;
        }
        if (extraTouch != null) {
            EXTRA_TOUCH.put(view, extraTouch);
        }
        attach(view);
    }

    static void attach(View view) {
        if (view == null || ATTACHED.containsKey(view)) {
            return;
        }
        ATTACHED.put(view, Boolean.TRUE);
        view.addOnAttachStateChangeListener(new View.OnAttachStateChangeListener() {
            @Override
            public void onViewAttachedToWindow(View v) {
            }

            @Override
            public void onViewDetachedFromWindow(View v) {
                Runnable pending = RELEASES.remove(v);
                if (pending != null) {
                    v.removeCallbacks(pending);
                }
                v.animate().cancel();
                v.setScaleX(1f);
                v.setScaleY(1f);
                v.setAlpha(1f);
            }
        });
        view.setOnTouchListener((v, event) -> {
            if (!v.isEnabled()) {
                return false;
            }
            int action = event.getActionMasked();
            if (action == MotionEvent.ACTION_DOWN) {
                press(v);
            } else if (action == MotionEvent.ACTION_UP
                    || action == MotionEvent.ACTION_CANCEL) {
                release(v);
            }
            View.OnTouchListener extra = EXTRA_TOUCH.get(v);
            if (extra != null && extra.onTouch(v, event)) {
                return true;
            }
            // Let Android continue with click, long-press and scrolling.
            return false;
        });
    }

    static void release(View view) {
        if (view == null) {
            return;
        }
        Runnable pending = RELEASES.remove(view);
        if (pending != null) {
            view.removeCallbacks(pending);
        }
        view.animate().cancel();
        view.animate()
                .scaleX(1f)
                .scaleY(1f)
                .alpha(1f)
                .setDuration(430L)
                .setInterpolator(new OvershootInterpolator(2.35f))
                .start();
    }

    private static void press(View view) {
        Runnable old = RELEASES.remove(view);
        if (old != null) {
            view.removeCallbacks(old);
        }
        if (view.getWidth() > 0) {
            view.setPivotX(view.getWidth() / 2f);
        }
        if (view.getHeight() > 0) {
            view.setPivotY(view.getHeight() / 2f);
        }
        Runnable release = () -> release(view);
        RELEASES.put(view, release);
        // A long-press may open a dialog before ACTION_UP reaches the
        // original window. Never leave that view stuck in a pressed scale.
        view.postDelayed(release, 650L);
        float scale = pressedScale(view);
        float alpha = view instanceof Button || view instanceof ImageButton
                ? 0.94f : 0.985f;
        view.animate().cancel();
        view.animate()
                .scaleX(scale)
                .scaleY(scale)
                .alpha(alpha)
                .setDuration(150L)
                .setInterpolator(new DecelerateInterpolator(1.7f))
                .start();
    }

    private static float pressedScale(View view) {
        float density = view.getResources().getDisplayMetrics().density;
        float widthDp = density <= 0 ? view.getWidth() : view.getWidth() / density;
        if (widthDp >= 320f) {
            return 0.985f;
        }
        if (widthDp >= 180f) {
            return 0.97f;
        }
        if (widthDp >= 90f) {
            return 0.955f;
        }
        return 0.92f;
    }

    private static boolean shouldAttach(View view) {
        if (view instanceof EditText
                || view instanceof CompoundButton
                || view instanceof ScrollView
                || view instanceof HorizontalScrollView) {
            return false;
        }
        return view instanceof Button
                || view instanceof ImageButton
                || view.isClickable()
                || view.isLongClickable();
    }
}
