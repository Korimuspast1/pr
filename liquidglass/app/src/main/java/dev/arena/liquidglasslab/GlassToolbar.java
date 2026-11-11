package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A floating navigation bar: a wide glass capsule with a back chevron, a title and
 * trailing circular glass buttons — the iOS 26 top-bar pattern.
 */
public class GlassToolbar extends GlassSurfaceView {

    public interface Actions {
        void onBack();

        void onTrailing(int index);
    }

    private String title = "Liquid Glass";
    private String subtitle = "";
    private Actions actions;
    private final TextPaint titlePaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint subPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);

    private float backCx, backCy, backR;
    private float[] trailX = new float[2];
    private float trailY, trailR;
    private int downTarget = -1;

    public GlassToolbar(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                        GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(28f);
        titlePaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        subPaint.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        setClickable(true);
    }

    public void bind(String title, String subtitle, Actions actions) {
        this.title = title;
        this.subtitle = subtitle;
        this.actions = actions;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        float pad = 10f * d;
        backR = (h - pad * 2f) / 2f;
        backCx = pad + backR;
        backCy = h / 2f;

        // back button: its own small glass bubble, drawn as an inlaid disc
        Paint disc = new Paint(Paint.ANTI_ALIAS_FLAG);
        disc.setColor(0x22FFFFFF);
        canvas.drawCircle(backCx, backCy, backR, disc);
        GlassButton.drawGlyph(canvas, GlassButton.Glyph.chevron,
                backCx - backR * 0.75f, backCy - backR * 0.75f, backR * 1.5f, 0xFF15171C);

        // trailing buttons
        trailR = backR;
        trailY = h / 2f;
        trailX[0] = w - pad - trailR;
        trailX[1] = trailX[0] - trailR * 2f - 8f * d;
        for (int i = 0; i < 2; i++) {
            canvas.drawCircle(trailX[i], trailY, trailR, disc);
        }
        GlassButton.drawGlyph(canvas, i0(), trailX[1] - trailR * 0.7f, trailY - trailR * 0.7f,
                trailR * 1.4f, 0xFF15171C);
        GlassButton.drawGlyph(canvas, GlassButton.Glyph.plus,
                trailX[0] - trailR * 0.7f, trailY - trailR * 0.7f, trailR * 1.4f, 0xFF15171C);

        // title block
        float x = backCx + backR + 14f * d;
        titlePaint.setColor(0xFF101114);
        titlePaint.setTextSize(19f * d);
        subPaint.setColor(0xFF54585F);
        subPaint.setTextSize(12f * d);
        Paint.FontMetrics fm = titlePaint.getFontMetrics();
        float ty = subtitle.isEmpty() ? h / 2f - (fm.ascent + fm.descent) / 2f
                : h / 2f - (fm.ascent + fm.descent) / 2f - 3f * d;
        canvas.drawText(title, x, ty, titlePaint);
        if (!subtitle.isEmpty()) {
            canvas.drawText(subtitle, x, ty + 15f * d, subPaint);
        }
    }

    private GlassButton.Glyph i0() {
        return GlassButton.Glyph.sliders;
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        float y = e.getY();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                animatePress(true, x, y);
                downTarget = hit(x, y);
                return true;
            case MotionEvent.ACTION_UP:
                animatePress(false, x, y);
                int hit = hit(x, y);
                if (hit == 0 && actions != null) actions.onBack();
                if (hit > 0 && actions != null) actions.onTrailing(hit - 1);
                downTarget = -1;
                return true;
            case MotionEvent.ACTION_CANCEL:
                animatePress(false, x, y);
                downTarget = -1;
                return true;
            default:
                return true;
        }
    }

    /** 0 = back, 1 = first trailing, 2 = second trailing, -1 = none. */
    private int hit(float x, float y) {
        if (dist(x, y, backCx, backCy) < backR * 1.35f) return 0;
        if (dist(x, y, trailX[0], trailY) < trailR * 1.2f) return 1;
        if (dist(x, y, trailX[1], trailY) < trailR * 1.2f) return 2;
        return -1;
    }

    private static float dist(float x0, float y0, float x1, float y1) {
        return (float) Math.hypot(x0 - x1, y0 - y1);
    }
}
