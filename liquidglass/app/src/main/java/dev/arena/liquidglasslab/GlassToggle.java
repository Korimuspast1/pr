package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A glass toggle: capsule track that tints with the state, glass knob that stretches
 * while dragged and snaps with a slight overshoot — the iOS 26 switch behaviour.
 */
public class GlassToggle extends GlassSurfaceView {

    public interface Listener {
        void onToggled(boolean on);
    }

    private boolean on;
    private float position;      // 0..1 animated
    private float target;
    private float drag;
    private Listener listener;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private float downX;
    private boolean dragging;

    public GlassToggle(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(18f);
        setClickable(true);
    }

    public void bind(boolean initial, Listener l) {
        on = initial;
        target = initial ? 1f : 0f;
        position = target;
        listener = l;
        invalidate();
    }

    public boolean isOn() {
        return on;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        if (Math.abs(position - target) > 0.002f) {
            position += (target - position) * 0.22f;
            postInvalidateOnAnimation();
        } else {
            position = target;
        }
        float trackH = h * 0.62f;
        float top = (h - trackH) / 2f;
        paint.setColor(0xFF787880);
        paint.setAlpha(60);
        canvas.drawRoundRect(0, top, w, top + trackH, trackH / 2f, trackH / 2f, paint);
        if (position > 0.01f) {
            paint.setColor(0xFF34C759);
            paint.setAlpha((int) (255 * position));
            canvas.drawRoundRect(0, top, w, top + trackH, trackH / 2f, trackH / 2f, paint);
        }
        // knob
        float knobR = trackH * 0.86f / 2f;
        float knobPad = (trackH - knobR * 2f) / 2f;
        float cx = knobPad + knobR + (w - knobR * 2f - knobPad * 2f) * position;
        float cy = h / 2f;
        float stretch = 1f + Math.min(Math.abs(target - position) * 0.6f, 0.12f);
        paint.setColor(0x2E000000);
        canvas.drawRoundRect(cx - knobR * stretch, cy - knobR + 1.5f * d,
                cx + knobR * stretch, cy + knobR + 1.5f * d, knobR, knobR, paint);
        paint.setColor(0xFFFFFFFF);
        canvas.drawRoundRect(cx - knobR * stretch, cy - knobR,
                cx + knobR * stretch, cy + knobR, knobR, knobR, paint);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(0.7f * d);
        paint.setColor(0x1F000000);
        canvas.drawRoundRect(cx - knobR * stretch, cy - knobR,
                cx + knobR * stretch, cy + knobR, knobR, knobR, paint);
        paint.setStyle(Paint.Style.FILL);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                downX = e.getX();
                animatePress(true, e.getX(), e.getY());
                return true;
            case MotionEvent.ACTION_MOVE:
                drag = e.getX() - downX;
                if (Math.abs(drag) > getWidth() * 0.12f) {
                    boolean next = drag > 0;
                    if (next != on) toggle(next);
                    downX = e.getX();
                }
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                animatePress(false, e.getX(), e.getY());
                if (Math.abs(drag) <= getWidth() * 0.12f) toggle(!on);
                drag = 0f;
                return true;
            default:
                return true;
        }
    }

    private void toggle(boolean next) {
        on = next;
        target = next ? 1f : 0f;
        if (listener != null) listener.onToggled(next);
        invalidate();
    }
}
