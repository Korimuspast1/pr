package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A labelled control row for the "lab" sheet: title, current value, and a wide
 * horizontal drag area that behaves like a slider (0..1) — used to tune every
 * parameter of {@link GlassParams} live.
 */
public class GlassSheetControl extends GlassSurfaceView {

    public interface Listener {
        void onValueChanged(GlassSheetControl control, float value);
    }

    private String label = "Параметр";
    private float value;
    private float min;
    private float max;
    private boolean integer;
    private Listener listener;
    private boolean dragging;
    private float dragProgress;
    private int accent = 0xFF0A84FF;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private float trackLeft, trackRight;

    public GlassSheetControl(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                             GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(18f);
        setShadowLift(-4f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String label, float min, float max, boolean integer, float value, int accent,
                     Listener l) {
        this.label = label;
        this.min = min;
        this.max = max;
        this.integer = integer;
        this.value = value;
        this.accent = accent;
        this.listener = l;
        invalidate();
    }

    public float value() {
        return value;
    }

    public void setValue(float v) {
        value = clampNorm(v);
        invalidate();
    }

    private float clampNorm(float v) {
        return Math.max(0f, Math.min(1f, v));
    }

    private float actual() {
        return min + (max - min) * value;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        dragProgress += ((dragging ? 1f : 0f) - dragProgress) * 0.22f;
        if (Math.abs(dragProgress - (dragging ? 1f : 0f)) > 0.003f) postInvalidateOnAnimation();

        text.setColor(0xFF14161B);
        text.setTextSize(12f * d);
        text.setTextAlign(Paint.Align.LEFT);
        canvas.drawText(label, 14f * d, h * 0.38f, text);
        text.setTextAlign(Paint.Align.RIGHT);
        text.setColor(0xFF5B6069);
        canvas.drawText(format(actual()), w - 14f * d, h * 0.38f, text);

        trackLeft = 14f * d;
        trackRight = w - 14f * d;
        float cy = h * 0.70f;
        float trackH = 4f * d;
        paint.setColor(0x33000000);
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, trackRight, cy + trackH / 2f,
                trackH, trackH, paint);
        paint.setColor(accent);
        float vx = trackLeft + (trackRight - trackLeft) * value;
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, vx, cy + trackH / 2f,
                trackH, trackH, paint);
        float r = 6f * d * (1f + 0.4f * dragProgress);
        paint.setColor(0x40000000);
        canvas.drawCircle(vx, cy + 1.2f * d, r, paint);
        paint.setColor(0xFFFFFFFF);
        canvas.drawCircle(vx, cy, r, paint);
    }

    private String format(float v) {
        if (integer) return String.valueOf(Math.round(v));
        return String.format(java.util.Locale.US, "%.2f", v);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                animatePress(true, e.getX(), e.getY());
                update(e.getX());
                return true;
            case MotionEvent.ACTION_MOVE:
                update(e.getX());
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                animatePress(false, e.getX(), e.getY());
                return true;
            default:
                return true;
        }
    }

    private void update(float x) {
        if (trackRight <= trackLeft) return;
        float v = clampNorm((x - trackLeft) / (trackRight - trackLeft));
        if (Math.abs(v - value) > 0.0005f) {
            value = v;
            if (listener != null) listener.onValueChanged(this, value);
            invalidate();
        }
    }
}
