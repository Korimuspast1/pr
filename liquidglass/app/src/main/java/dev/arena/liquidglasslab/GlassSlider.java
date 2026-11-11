package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * Slider on a glass track: the track is a glass capsule, the value is a tinted
 * capsule, and the thumb is a circular glass lens that swells under the finger —
 * the iOS 26 control behaviour where the knob becomes a droplet while dragging.
 */
public class GlassSlider extends GlassSurfaceView {

    public interface Listener {
        void onValueChanged(float value);
    }

    private float value = 0.4f;
    private String title = "Значение";
    private String valueSuffix = "%";
    private Listener listener;
    private boolean dragging;
    private float dragProgress;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint track = new Paint(Paint.ANTI_ALIAS_FLAG);
    private int accent = 0xFF0A84FF;

    public GlassSlider(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(26f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String title, float value, int accent, Listener l) {
        this.title = title;
        this.value = value;
        this.accent = accent;
        this.listener = l;
        invalidate();
    }

    public float value() {
        return value;
    }

    public void setValue(float v) {
        value = Math.max(0f, Math.min(1f, v));
        invalidate();
    }

    private float trackLeft;
    private float trackRight;

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        dragProgress += ((dragging ? 1f : 0f) - dragProgress) * 0.22f;
        if (Math.abs(dragProgress - (dragging ? 1f : 0f)) > 0.003f) postInvalidateOnAnimation();

        text.setColor(0xFF1A1C21);
        text.setTextSize(11f * d);
        text.setTextAlign(Paint.Align.LEFT);
        canvas.drawText(title, 18f * d, h * 0.40f, text);
        canvas.drawText(((int) (value * 100)) + valueSuffix, 18f * d, h * 0.78f, text);

        trackLeft = w * 0.40f;
        trackRight = w - 18f * d;
        float cy = h / 2f;
        float trackH = 5f * d;
        track.setColor(0x2E000000);
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, trackRight, cy + trackH / 2f,
                trackH, trackH, track);
        track.setColor(accent);
        float vx = trackLeft + (trackRight - trackLeft) * value;
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, vx, cy + trackH / 2f,
                trackH, trackH, track);

        // thumb: a lens that grows into a droplet while dragging
        float r = 9f * d * (1f + 0.35f * dragProgress);
        Paint thumbPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        thumbPaint.setColor(0x33000000);
        canvas.drawCircle(vx, cy + 1.5f * d, r, thumbPaint);
        thumbPaint.setColor(0xFFFFFFFF);
        canvas.drawCircle(vx, cy, r, thumbPaint);
        thumbPaint.setStyle(Paint.Style.STROKE);
        thumbPaint.setStrokeWidth(0.8f * d);
        thumbPaint.setColor(0x22000000);
        canvas.drawCircle(vx, cy, r, thumbPaint);
        // highlight arc
        thumbPaint.setColor(0x66FFFFFF);
        canvas.drawArc(vx - r, cy - r, vx + r, cy + r, 200f, 140f, false, thumbPaint);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        float y = e.getY();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                animatePress(true, x, y);
                update(x);
                return true;
            case MotionEvent.ACTION_MOVE:
                update(x);
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                animatePress(false, x, y);
                return true;
            default:
                return true;
        }
    }

    private void update(float x) {
        if (trackRight <= trackLeft) return;
        float v = (x - trackLeft) / (trackRight - trackLeft);
        v = Math.max(0f, Math.min(1f, v));
        if (Math.abs(v - value) > 0.0005f) {
            value = v;
            if (listener != null) listener.onValueChanged(value);
            invalidate();
        }
    }
}
