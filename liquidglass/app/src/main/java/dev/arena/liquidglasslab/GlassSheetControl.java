package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A compact tuning row on a glass card: parameter name, current value, and a wide drag
 * area acting as a 0..1 slider. Used by the "Тюнинг" tab to change every optical
 * parameter live.
 */
public class GlassSheetControl extends GlassSurfaceView {

    public interface Listener {
        void onValueChanged(GlassSheetControl control, float value);
    }

    private String label = "Параметр";
    private String formatted = "";
    private float value;
    private Listener listener;
    private boolean dragging;
    private float dragProgress;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private float trackLeft, trackRight;

    public GlassSheetControl(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                             GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(20f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String label, float value, String formatted, Listener l) {
        this.label = label;
        this.value = Math.max(0f, Math.min(1f, value));
        this.formatted = formatted;
        this.listener = l;
        invalidate();
    }

    public float value() {
        return value;
    }

    public void setValue(float v, String formatted) {
        value = Math.max(0f, Math.min(1f, v));
        this.formatted = formatted;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        dragProgress += ((dragging ? 1f : 0f) - dragProgress) * 0.25f;
        if (Math.abs(dragProgress - (dragging ? 1f : 0f)) > 0.004f) postInvalidateOnAnimation();

        text.setTextAlign(Paint.Align.LEFT);
        text.setTextSize(Math.min(12f * d, h * 0.22f));
        text.setColor(params.ink());
        canvas.drawText(label, 14f * d, h * 0.34f, text);
        text.setTextAlign(Paint.Align.RIGHT);
        text.setColor(params.inkSecondary());
        canvas.drawText(formatted, w - 14f * d, h * 0.34f, text);

        trackLeft = 14f * d;
        trackRight = w - 14f * d;
        float cy = h * 0.68f;
        float trackH = Math.max(4f * d, 3f);
        paint.setColor(params.darkBackdrop ? 0x38FFFFFF : 0x2E000000);
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, trackRight, cy + trackH / 2f,
                trackH, trackH, paint);
        paint.setColor(params.accent());
        float vx = trackLeft + (trackRight - trackLeft) * value;
        canvas.drawRoundRect(trackLeft, cy - trackH / 2f, vx, cy + trackH / 2f,
                trackH, trackH, paint);
        float r = Math.min(h * 0.13f, 7f * d) * (1f + 0.4f * dragProgress);
        paint.setColor(0x40000000);
        canvas.drawCircle(vx, cy + 1.2f * d, r, paint);
        paint.setColor(0xFFFFFFFF);
        canvas.drawCircle(vx, cy, r, paint);
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
        float v = Math.max(0f, Math.min(1f, (x - trackLeft) / (trackRight - trackLeft)));
        if (Math.abs(v - value) > 0.0005f) {
            value = v;
            if (listener != null) listener.onValueChanged(this, value);
            invalidate();
        }
    }
}
