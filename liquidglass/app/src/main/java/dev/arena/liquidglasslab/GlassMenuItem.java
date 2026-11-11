package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * One row of a glass menu: icon, label, optional value, hover/press highlight that
 * lights the glass from the touch point (iOS 26 menu rows behave like this).
 */
public class GlassMenuItem extends GlassSurfaceView {

    public interface Listener {
        void onPicked(GlassMenuItem item);
    }

    private String label = "Пункт";
    private String value = "";
    private GlassButton.Glyph glyph = GlassButton.Glyph.sparkle;
    private boolean highlighted;
    private Listener listener;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint valueText = new TextPaint(Paint.ANTI_ALIAS_FLAG);

    public GlassMenuItem(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                         GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(14f);
        setShadowLift(-6f);
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        valueText.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        valueText.setTextAlign(Paint.Align.RIGHT);
        setClickable(true);
    }

    public void bind(String label, String value, GlassButton.Glyph glyph, Listener l) {
        this.label = label;
        this.value = value;
        this.glyph = glyph;
        this.listener = l;
        invalidate();
    }

    public void setHighlighted(boolean h) {
        if (highlighted != h) {
            highlighted = h;
            invalidate();
        }
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        if (highlighted) {
            Paint hi = new Paint(Paint.ANTI_ALIAS_FLAG);
            hi.setColor(0x24FFFFFF);
            canvas.drawRoundRect(0, 0, w, h, 14f * d, 14f * d, hi);
        }
        float gs = 17f * d;
        GlassButton.drawGlyph(canvas, glyph, 12f * d, (h - gs) / 2f, gs, 0xFF1A1C21);
        text.setColor(0xFF14161B);
        text.setTextSize(14f * d);
        Paint.FontMetrics fm = text.getFontMetrics();
        canvas.drawText(label, 12f * d + gs + 10f * d,
                h / 2f - (fm.ascent + fm.descent) / 2f, text);
        if (!value.isEmpty()) {
            valueText.setColor(0xFF777C86);
            valueText.setTextSize(12f * d);
            canvas.drawText(value, w - 12f * d, h / 2f - (fm.ascent + fm.descent) / 2f, valueText);
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                animatePress(true, e.getX(), e.getY());
                return true;
            case MotionEvent.ACTION_UP:
                animatePress(false, e.getX(), e.getY());
                if (listener != null) listener.onPicked(this);
                return true;
            case MotionEvent.ACTION_CANCEL:
                animatePress(false, e.getX(), e.getY());
                return true;
            default:
                return true;
        }
    }
}
