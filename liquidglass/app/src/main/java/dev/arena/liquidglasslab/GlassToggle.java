package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A switch on a glass capsule: label on the left, capsule track on the right that
 * tints with the state, and a knob that stretches slightly while animating.
 */
public class GlassToggle extends GlassSurfaceView {

    public interface Listener {
        void onToggled(boolean on);
    }

    private boolean on;
    private float position;
    private float target;
    private Listener listener;
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private String title = "";
    private float downX;
    private float drag;
    private boolean dragging;

    public GlassToggle(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(22f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String title, boolean initial, Listener l) {
        this.title = title;
        on = initial;
        target = initial ? 1f : 0f;
        position = target;
        listener = l;
        invalidate();
    }

    public boolean isOn() {
        return on;
    }

    public void setOn(boolean v) {
        on = v;
        target = v ? 1f : 0f;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        if (Math.abs(position - target) > 0.002f) {
            position += (target - position) * 0.24f;
            postInvalidateOnAnimation();
        } else {
            position = target;
        }

        if (!title.isEmpty()) {
            text.setColor(params.ink());
            text.setTextSize(Math.min(13f * d, h * 0.24f));
            text.setTextAlign(Paint.Align.LEFT);
            Paint.FontMetrics fm = text.getFontMetrics();
            canvas.drawText(title, 18f * d, h / 2f - (fm.ascent + fm.descent) / 2f, text);
        }

        float trackH = Math.min(h * 0.56f, 32f * d);
        float trackW = trackH * 1.72f;
        float left = w - 16f * d - trackW;
        float top = (h - trackH) / 2f;
        paint.setColor(params.darkBackdrop ? 0x3DFFFFFF : 0x26000000);
        canvas.drawRoundRect(left, top, left + trackW, top + trackH,
                trackH / 2f, trackH / 2f, paint);
        if (position > 0.01f) {
            paint.setColor(0xFF34C759);
            paint.setAlpha((int) (255 * position));
            canvas.drawRoundRect(left, top, left + trackW, top + trackH,
                    trackH / 2f, trackH / 2f, paint);
        }

        float knobR = trackH * 0.44f;
        float pad = (trackH - knobR * 2f) / 2f;
        float cx = left + pad + knobR + (trackW - knobR * 2f - pad * 2f) * position;
        float cy = h / 2f;
        float stretch = 1f + Math.min(Math.abs(target - position) * 0.5f, 0.10f);
        paint.setColor(0x33000000);
        canvas.drawRoundRect(cx - knobR * stretch, cy - knobR + 1.2f * d,
                cx + knobR * stretch, cy + knobR + 1.2f * d, knobR, knobR, paint);
        paint.setColor(0xFFFFFFFF);
        canvas.drawRoundRect(cx - knobR * stretch, cy - knobR,
                cx + knobR * stretch, cy + knobR, knobR, knobR, paint);
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                dragging = true;
                drag = 0f;
                downX = e.getX();
                animatePress(true, e.getX(), e.getY());
                return true;
            case MotionEvent.ACTION_MOVE:
                drag += Math.abs(e.getX() - downX);
                downX = e.getX();
                return true;
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL:
                dragging = false;
                animatePress(false, e.getX(), e.getY());
                if (drag < getWidth() * 0.25f) toggle(!on);
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
