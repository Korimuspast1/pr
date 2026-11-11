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
 * assets), label, an interactive press that flexes the silhouette and adds a droplet
 * bulge under the finger. Everything is centred inside the capsule and clipped to it.
 */
public class GlassButton extends GlassSurfaceView {

    private String label = "";
    private final TextPaint textPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private GlassButton.Glyph glyph = Glyph.none;
    private int contentColor = 0;
    private boolean tinted;
    private int bodyColor;

    public enum Glyph { none, plus, play, pause, chevron, chevronDown, search, sliders, sparkle,
        grid, check, close, photo, tune, dots }

    public GlassButton(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        textPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        setCornerRadius(999f);
        setClickable(true);
    }

    public GlassButton label(String text) {
        label = text;
        invalidate();
        return this;
    }

    public GlassButton glyph(Glyph g) {
        glyph = g;
        invalidate();
        return this;
    }

    /**
     * The iOS "prominent" glass button: the material itself takes the accent colour
     * (body tint), instead of a flat overlay on top of the refraction.
     */
    public GlassButton tinted() {
        tinted = true;
        bodyColor = 0xFF0A84FF;
        invalidate();
        return this;
    }

    /** Any other prominent colour. */
    public GlassButton bodyColor(int argb) {
        bodyColor = argb;
        invalidate();
        return this;
    }

    /** Fixed content colour; by default it follows the backdrop (params.ink()). */
    public GlassButton textColor(int color) {
        contentColor = color;
        invalidate();
        return this;
    }

    protected int ink() {
        if (contentColor != 0) return contentColor;
        // on a prominent (accent-coloured) button the label is always white
        return bodyColor != 0 ? 0xFFFFFFFF : params.ink();
    }

    @Override
    protected void onDraw(Canvas canvas) {
        int saved = params.glassTint;
        params.glassTint = bodyColor;
        super.onDraw(canvas);
        params.glassTint = saved;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        int color = ink();
        float textSize = Math.min(h * 0.34f, 15f * d);
        textPaint.setTextSize(textSize);
        textPaint.setColor(color);
        textPaint.setTextAlign(Paint.Align.LEFT);

        float glyphSize = Math.min(h * 0.42f, 20f * d);
        boolean hasGlyph = glyph != Glyph.none;
        float gap = hasGlyph ? 7f * d : 0f;
        float textW = label.isEmpty() ? 0f : textPaint.measureText(label);
        float total = (hasGlyph ? glyphSize : 0f) + gap + textW;
        float x = (w - total) / 2f;

        if (hasGlyph) {
            drawGlyph(canvas, glyph, x, (h - glyphSize) / 2f, glyphSize, color);
            x += glyphSize + gap;
        }
        if (!label.isEmpty()) {
            Paint.FontMetrics fm = textPaint.getFontMetrics();
            float baseline = h / 2f - (fm.ascent + fm.descent) / 2f;
            canvas.drawText(label, x, baseline, textPaint);
        }
    }

    /** Tiny path-based icon set, so the APK needs no vector assets. */
    static void drawGlyph(Canvas c, Glyph glyph, float x, float y, float s, int color) {
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(color);
        p.setStyle(Paint.Style.STROKE);
        p.setStrokeWidth(Math.max(s * 0.13f, 1.2f));
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
                path.moveTo(x + s * 0.30f, y + s * 0.16f);
                path.lineTo(x + s * 0.84f, cy);
                path.lineTo(x + s * 0.30f, y + s * 0.84f);
                path.close();
                p.setStyle(Paint.Style.FILL);
                c.drawPath(path, p);
                break;
            }
            case pause:
                p.setStyle(Paint.Style.FILL);
                c.drawRoundRect(x + s * 0.26f, y + s * 0.18f, x + s * 0.43f, y + s * 0.82f,
                        s * 0.05f, s * 0.05f, p);
                c.drawRoundRect(x + s * 0.57f, y + s * 0.18f, x + s * 0.74f, y + s * 0.82f,
                        s * 0.05f, s * 0.05f, p);
                break;
            case chevron:
                c.drawLine(cx - s * 0.10f, cy - s * 0.24f, cx - s * 0.30f, cy, p);
                c.drawLine(cx - s * 0.30f, cy, cx - s * 0.10f, cy + s * 0.24f, p);
                break;
            case chevronDown:
                c.drawLine(cx - s * 0.24f, cy - s * 0.08f, cx, cy + s * 0.16f, p);
                c.drawLine(cx, cy + s * 0.16f, cx + s * 0.24f, cy - s * 0.08f, p);
                break;
            case search:
                c.drawCircle(cx - s * 0.06f, cy - s * 0.06f, s * 0.24f, p);
                c.drawLine(cx + s * 0.12f, cy + s * 0.12f, cx + s * 0.30f, cy + s * 0.30f, p);
                break;
            case sliders:
                for (int i = 0; i < 2; i++) {
                    float ly = cy + (i == 0 ? -s * 0.17f : s * 0.17f);
                    c.drawLine(x + s * 0.20f, ly, x + s * 0.80f, ly, p);
                    float kx = x + s * (i == 0 ? 0.38f : 0.64f);
                    p.setStyle(Paint.Style.FILL);
                    c.drawCircle(kx, ly, s * 0.09f, p);
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
                p.setStyle(Paint.Style.FILL);
                c.drawPath(path, p);
                break;
            }
            case grid:
                p.setStrokeWidth(Math.max(s * 0.10f, 1f));
                for (int i = 0; i < 2; i++) {
                    for (int j = 0; j < 2; j++) {
                        c.drawRect(x + s * (0.16f + 0.36f * i), y + s * (0.16f + 0.36f * j),
                                x + s * (0.44f + 0.36f * i), y + s * (0.44f + 0.36f * j), p);
                    }
                }
                break;
            case check:
                c.drawLine(cx - s * 0.26f, cy, cx - s * 0.06f, cy + s * 0.20f, p);
                c.drawLine(cx - s * 0.06f, cy + s * 0.20f, cx + s * 0.28f, cy - s * 0.20f, p);
                break;
            case close:
                c.drawLine(cx - s * 0.22f, cy - s * 0.22f, cx + s * 0.22f, cy + s * 0.22f, p);
                c.drawLine(cx + s * 0.22f, cy - s * 0.22f, cx - s * 0.22f, cy + s * 0.22f, p);
                break;
            case photo:
                c.drawRoundRect(x + s * 0.14f, y + s * 0.20f, x + s * 0.86f, y + s * 0.80f,
                        s * 0.10f, s * 0.10f, p);
                c.drawCircle(x + s * 0.34f, y + s * 0.36f, s * 0.06f, p);
                break;
            case tune:
                for (int i = 0; i < 3; i++) {
                    float ly = y + s * (0.24f + 0.26f * i);
                    c.drawLine(x + s * 0.14f, ly, x + s * 0.86f, ly, p);
                    p.setStyle(Paint.Style.FILL);
                    c.drawCircle(x + s * (i == 1 ? 0.64f : 0.36f), ly, s * 0.07f, p);
                    p.setStyle(Paint.Style.STROKE);
                }
                break;
            case dots:
                p.setStyle(Paint.Style.FILL);
                for (int i = 0; i < 3; i++) {
                    c.drawCircle(cx + s * (i - 1) * 0.26f, cy, s * 0.07f, p);
                }
                break;
            default:
                break;
        }
    }
}
