package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A slider on a glass capsule: title and value on the left, a thin tinted track on the
 * right, and a white knob that grows into a droplet while dragging — the iOS 26 knob
 * behaviour. All measurements are inside the capsule.
 */
public class GlassSlider extends GlassSurfaceView {

    public interface Listener {
        void onValueChanged(float value);
    }

    private float value = 0.4f;
    private String title = "Значение";
    private String suffix = "%";
    private Listener listener;
    private boolean dragging;
    private float dragProgress;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private float trackLeft;
    private float trackRight;

    public GlassSlider(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(22f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String title, float value, Listener l) {
        this.title = title;
        this.value = Math.max(0f, Math.min(1f, value));
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

    /** Text inside the slider, e.g. "12 dp" instead of "12 %". */
    public void setSuffix(String s) {
        suffix = s;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        dragProgress += ((dragging ? 1f : 0f) - dragProgress) * 0.25f;
        if (Math.abs(dragProgress - (dragging ? 1f : 0f)) > 0.004f) postInvalidateOnAnimation();

        text.setColor(params.ink());
        text.setTextSize(Math.min(13f * d, h * 0.24f));
        text.setTextAlign(Paint.Align.LEFT);
        Paint.FontMetrics fm = text.getFontMetrics();
        float baseline = h / 2f - (fm.ascent + fm.descent) / 2f;
        canvas.drawText(title, 18f * d, baseline, text);

        float titleW = text.measureText(title);
        trackLeft = 18f * d + titleW + 14f * d;
        trackRight = w - 18f * d - Math.max(30f * d, h * 0.5f);
        if (trackRight < trackLeft) trackRight = trackLeft + 1f;

        float cy = h / 2f;
        float trackH = Math.max(4f * d, 3f);
        paint.setColor(params.darkBackdrop ? 0x40FFFFFF : 0x33000000);
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, trackRight, cy + trackH / 2f,
                trackH, trackH, paint);
        paint.setColor(params.accent());
        float vx = trackLeft + (trackRight - trackLeft) * value;
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, vx, cy + trackH / 2f,
                trackH, trackH, paint);

        float r = Math.min(h * 0.19f, 10f * d) * (1f + 0.35f * dragProgress);
        paint.setColor(0x40000000);
        canvas.drawCircle(vx, cy + 1.5f * d, r, paint);
        paint.setColor(0xFFFFFFFF);
        canvas.drawCircle(vx, cy, r, paint);

        text.setTextAlign(Paint.Align.RIGHT);
        text.setColor(params.inkSecondary());
        text.setTextSize(Math.min(12f * d, h * 0.22f));
        canvas.drawText(Math.round(value * 100) + suffix, w - 16f * d, baseline, text);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                animatePress(true, x, e.getY());
                update(x);
                return true;
            case MotionEvent.ACTION_MOVE:
                update(x);
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                animatePress(false, x, e.getY());
                return true;
            default:
                return true;
        }
    }

    private void update(float x) {
        if (trackRight <= trackLeft) return;
        float v = Math.max(0f, Math.min(1f, (x - trackLeft) / (trackRight - trackLeft)));
        if (Math.abs(v - value) > 0.0005f) {
            value = v;
            if (listener != null) listener.onValueChanged(value);
            invalidate();
        }
    }
}
