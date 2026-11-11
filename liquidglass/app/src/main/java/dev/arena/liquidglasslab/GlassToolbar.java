package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * The floating glass navigation bar: a wide capsule with a title on the left and one
 * or two circular glass bubbles on the right — the iOS 26 top-bar pattern.
 *
 * <p>The title never overlaps the bubbles: it is ellipsised to the space that is left.
 */
public class GlassToolbar extends GlassSurfaceView {

    public interface Actions {
        /** Tap on a trailing bubble, 0 = rightmost. */
        void onTrailing(int index);
    }

    private String title = "";
    private String subtitle = "";
    private int bubbleCount = 2;
    private GlassButton.Glyph[] glyphs = {GlassButton.Glyph.tune, GlassButton.Glyph.close};
    private Actions actions;
    private final TextPaint titlePaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint subPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint disc = new Paint(Paint.ANTI_ALIAS_FLAG);

    private final float[] bubbleX = new float[3];
    private float bubbleY, bubbleR;

    public GlassToolbar(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                        GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(30f);
        titlePaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        subPaint.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String title, String subtitle, Actions actions) {
        this.title = title == null ? "" : title;
        this.subtitle = subtitle == null ? "" : subtitle;
        this.actions = actions;
        invalidate();
    }

    /** Sets the trailing bubbles; index 0 is the rightmost. */
    public void bubbles(GlassButton.Glyph... set) {
        bubbleCount = Math.min(set.length, 3);
        for (int i = 0; i < bubbleCount; i++) glyphs[i] = set[i];
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        float pad = 7f * d;
        bubbleR = (h - pad * 2f) / 2f;
        bubbleY = h / 2f;
        float step = bubbleR * 2f + 7f * d;
        for (int i = 0; i < bubbleCount; i++) {
            bubbleX[i] = w - pad - bubbleR - i * step;
        }

        int ink = params.ink();
        disc.setColor(params.discInk());
        for (int i = 0; i < bubbleCount; i++) {
            canvas.drawCircle(bubbleX[i], bubbleY, bubbleR, disc);
            GlassButton.drawGlyph(canvas, glyphs[i], bubbleX[i] - bubbleR * 0.66f,
                    bubbleY - bubbleR * 0.66f, bubbleR * 1.32f, ink);
        }

        float left = 22f * d;
        float right = bubbleCount > 0 ? bubbleX[bubbleCount - 1] - bubbleR - 14f * d : w - 14f * d;
        float maxWidth = Math.max(right - left, 0f);
        float titleSize = Math.min(19f * d, h * 0.34f);
        titlePaint.setTextSize(titleSize);
        titlePaint.setColor(ink);
        String shown = ellipsise(title, titlePaint, maxWidth);

        Paint.FontMetrics fm = titlePaint.getFontMetrics();
        if (subtitle.isEmpty()) {
            float ty = h / 2f - (fm.ascent + fm.descent) / 2f;
            canvas.drawText(shown, left, ty, titlePaint);
        } else {
            subPaint.setTextSize(Math.min(12f * d, h * 0.22f));
            subPaint.setColor(params.inkSecondary());
            float ty = h / 2f - 1.5f * d;
            canvas.drawText(shown, left, ty, titlePaint);
            canvas.drawText(ellipsise(subtitle, subPaint, maxWidth), left, ty + subPaint.getTextSize() + 2f * d, subPaint);
        }
    }

    static String ellipsise(String text, TextPaint paint, float maxWidth) {
        if (maxWidth <= 0f || text.isEmpty()) return "";
        if (paint.measureText(text) <= maxWidth) return text;
        String ell = "…";
        int lo = 0, hi = text.length();
        while (lo < hi) {
            int mid = (lo + hi + 1) / 2;
            if (paint.measureText(text.substring(0, mid) + ell) <= maxWidth) lo = mid;
            else hi = mid - 1;
        }
        return text.substring(0, lo) + ell;
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        float y = e.getY();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                animatePress(true, x, y);
                return true;
            case MotionEvent.ACTION_UP:
                animatePress(false, x, y);
                for (int i = 0; i < bubbleCount; i++) {
                    if (dist(x, y, bubbleX[i], bubbleY) < bubbleR * 1.25f) {
                        if (actions != null) actions.onTrailing(i);
                        performClick();
                        return true;
                    }
                }
                return true;
            case MotionEvent.ACTION_CANCEL:
                animatePress(false, x, y);
                return true;
            default:
                return true;
        }
    }

    private static float dist(float x0, float y0, float x1, float y1) {
        return (float) Math.hypot(x0 - x1, y0 - y1);
    }
}
