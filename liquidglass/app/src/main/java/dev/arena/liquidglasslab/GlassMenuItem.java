package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * One row of a glass list: icon, label, optional trailing value, and a highlight that
 * lights the glass from the touch point.
 */
public class GlassMenuItem extends GlassSurfaceView {

    public interface Listener {
        void onPicked(GlassMenuItem item);
    }

    private String label = "Пункт";
    private String value = "";
    private String subtitle = "";
    private GlassButton.Glyph glyph = GlassButton.Glyph.sparkle;
    private int accentDot;
    private Listener listener;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint valueText = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

    public GlassMenuItem(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                         GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(18f);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        valueText.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        valueText.setTextAlign(Paint.Align.RIGHT);
        setClickable(true);
    }

    /** Optional second line (used by the preset list). */
    public void subtitle(String text) {
        this.subtitle = text == null ? "" : text;
        invalidate();
    }

    public void bind(String label, String value, GlassButton.Glyph glyph, Listener l) {
        this.label = label;
        this.value = value;
        this.glyph = glyph;
        this.listener = l;
        invalidate();
    }

    /** Optional coloured dot instead of an icon (used by the preset list). */
    public void dot(int color) {
        accentDot = color;
        invalidate();
    }

    public String label() {
        return label;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        float cy = h / 2f;
        float left = 18f * d;

        if (accentDot != 0) {
            paint.setColor(accentDot);
            canvas.drawCircle(left + 7f * d, cy, 7f * d, paint);
            left += 28f * d;
        } else {
            float gs = Math.min(18f * d, h * 0.40f);
            GlassButton.drawGlyph(canvas, glyph, left, cy - gs / 2f, gs, params.ink());
            left += gs + 12f * d;
        }

        text.setColor(params.ink());
        text.setTextSize(Math.min(14f * d, h * 0.30f));
        text.setTextAlign(Paint.Align.LEFT);
        Paint.FontMetrics fm = text.getFontMetrics();
        float available = w - left - 18f * d;
        if (!value.isEmpty()) available -= valueText.measureText(value) + 30f * d;
        if (!subtitle.isEmpty() && h > 48f * d) {
            // two-line row: title above, caption below — never overlapping
            canvas.drawText(GlassToolbar.ellipsise(label, text, available),
                    left, cy - 4f * d, text);
            text.setTextSize(Math.min(11.5f * d, h * 0.24f));
            text.setColor(params.inkSecondary());
            canvas.drawText(GlassToolbar.ellipsise(subtitle, text, available),
                    left, cy + text.getTextSize() + 4f * d, text);
        } else {
            canvas.drawText(GlassToolbar.ellipsise(label, text, available),
                    left, cy - (fm.ascent + fm.descent) / 2f, text);
        }

        if (!value.isEmpty()) {
            valueText.setColor(params.inkSecondary());
            valueText.setTextSize(Math.min(13f * d, h * 0.31f));
            canvas.drawText(value, w - 18f * d, cy - (fm.ascent + fm.descent) / 2f, valueText);
        }
        if (isClickable()) {
            GlassButton.drawGlyph(canvas, GlassButton.Glyph.chevron, w - 22f * d, cy - 6f * d,
                    12f * d, params.inkSecondary());
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
                performClick();
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
