package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;

/**
 * A glass pill button — the iOS 26 "capsule" control.
 *
 * <p>Contains: capsule silhouette, optional leading glyph (drawn with paths, no
 * assets), label, an interactive press that both flexes the silhouette and adds a
 * droplet bulge under the finger.
 */
public class GlassButton extends GlassSurfaceView {

    private String label = "Button";
    private final TextPaint textPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private float glyphSize;
    private GlassButton.Glyph glyph = Glyph.none;
    private int contentColor = 0xFF101114;

    public enum Glyph { none, plus, play, pause, chevron, search, sliders, sparkle }

    public GlassButton(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        textPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setCornerRadius(999f);
        setClickable(true);
    }

    public GlassButton label(String text) {
        label = text;
        return this;
    }

    public GlassButton glyph(Glyph g) {
        glyph = g;
        return this;
    }

    public GlassButton textColor(int color) {
        contentColor = color;
        return this;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float density = getResources().getDisplayMetrics().density;
        float textSize = h * 0.34f;
        textPaint.setTextSize(textSize);
        textPaint.setColor(contentColor);
        textPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        textPaint.setTextAlign(Paint.Align.LEFT);

        float pad = h * 0.34f;
        glyphSize = h * 0.40f;
        float gx = pad;
        boolean hasGlyph = glyph != Glyph.none;
        float textX = hasGlyph ? gx + glyphSize + h * 0.18f : pad;
        float textW = textPaint.measureText(label);
        float total = (hasGlyph ? glyphSize + h * 0.18f : 0f) + textW;
        textX = (w - total) / 2f + (hasGlyph ? glyphSize + h * 0.18f : 0f);
        gx = textX - glyphSize - h * 0.18f;

        if (hasGlyph) {
            drawGlyph(canvas, glyph, gx, (h - glyphSize) / 2f, glyphSize, contentColor);
        }
        Paint.FontMetrics fm = textPaint.getFontMetrics();
        float baseline = h / 2f - (fm.ascent + fm.descent) / 2f;
        canvas.drawText(label, textX, baseline, textPaint);
    }

    /** Tiny path-based icon set, so the APK needs no vector assets. */
    static void drawGlyph(Canvas c, Glyph glyph, float x, float y, float s, int color) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(color);
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeWidth(s * 0.14f);
        p.setStrokeCap(Paint.Cap.ROUND);
        p.setStrokeJoin(Paint.Join.ROUND);
        float cx = x + s / 2f;
        float cy = y + s / 2f;
        switch (glyph) {
            case plus:
                c.drawLine(cx - s * 0.3f, cy, cx + s * 0.3f, cy, p);
                c.drawLine(cx, cy - s * 0.3f, cx, cy + s * 0.3f, p);
                break;
            case play: {
                Path path = new Path();
                path.moveTo(x + s * 0.28f, y + s * 0.18f);
                path.lineTo(x + s * 0.84f, cy);
                path.lineTo(x + s * 0.28f, y + s * 0.82f);
                path.close();
                p.setStyle(Paint.Style.FILL);
                c.drawPath(path, p);
                break;
            }
            case pause:
                p.setStyle(Paint.Style.FILL);
                c.drawRoundRect(x + s * 0.26f, y + s * 0.2f, x + s * 0.42f, y + s * 0.8f,
                        s * 0.05f, s * 0.05f, p);
                c.drawRoundRect(x + s * 0.58f, y + s * 0.2f, x + s * 0.74f, y + s * 0.8f,
                        s * 0.05f, s * 0.05f, p);
                break;
            case chevron:
                c.drawLine(cx - s * 0.18f, cy - s * 0.26f, cx + s * 0.12f, cy, p);
                c.drawLine(cx + s * 0.12f, cy, cx - s * 0.18f, cy + s * 0.26f, p);
                break;
            case search:
                c.drawCircle(cx - s * 0.06f, cy - s * 0.06f, s * 0.26f, p);
                c.drawLine(cx + s * 0.14f, cy + s * 0.14f, cx + s * 0.32f, cy + s * 0.32f, p);
                break;
            case sliders:
                for (int i = 0; i < 2; i++) {
                    float ly = cy + (i == 0 ? -s * 0.18f : s * 0.18f);
                    c.drawLine(x + s * 0.2f, ly, x + s * 0.8f, ly, p);
                    float kx = x + s * (i == 0 ? 0.38f : 0.66f);
                    p.setStyle(Paint.Style.FILL);
                    c.drawCircle(kx, ly, s * 0.1f, p);
                    p.setStyle(Paint.Style.STROKE);
                }
                break;
            case sparkle: {
                Path path = new Path();
                for (int i = 0; i < 4; i++) {
                    double a = Math.PI / 2 * i;
                    path.moveTo(cx, cy);
                    path.quadTo((float) (cx + Math.cos(a) * s * 0.16f),
                            (float) (cy + Math.sin(a) * s * 0.16f),
                            (float) (cx + Math.cos(a) * s * 0.36f),
                            (float) (cy + Math.sin(a) * s * 0.36f));
                }
                c.drawPath(path, p);
                break;
            }
            default:
                break;
        }
    }
}
