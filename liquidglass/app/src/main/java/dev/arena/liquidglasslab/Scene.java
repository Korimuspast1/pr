package dev.arena.liquidglasslab;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.RadialGradient;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.text.TextPaint;

/**
 * The content behind the glass.
 *
 * <p>Seven backdrops chosen to stress different parts of the optical model:
 * colourful gradients (tint/adaptivity), a nearly white one (legibility), a dark one
 * (rim visibility), a high-frequency grid and text (only visible with edges behind the
 * glass can refraction and dispersion be judged), a user photo, and flat grey.
 *
 * <p>Everything here is plain content — no controls are drawn on this layer, so the
 * glass chrome on top of it is always the only UI.
 */
public final class Scene {

    public enum Style {
        AURORA("Aurora", true), LIGHT("Светлая", false), DARK("Ночная", true),
        GRID("Сетка", false), TEXT("Текст", true), PHOTO("Фото", false),
        SOLID("Ровный", false);

        public final String title;
        /** True when the backdrop is dark overall (light labels on the glass). */
        public final boolean dark;

        Style(String title, boolean dark) {
            this.title = title;
            this.dark = dark;
        }
    }

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);

    private Style style = Style.AURORA;
    private Bitmap photo;

    public Scene() {
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
    }

    public Style style() {
        return style;
    }

    public void setStyle(Style s) {
        style = s;
    }

    public void setPhoto(Bitmap bmp) {
        photo = bmp;
        style = Style.PHOTO;
    }

    public void paint(Canvas canvas, int w, int h) {
        float fw = w;
        float fh = h;
        switch (style) {
            case LIGHT:
                canvas.drawColor(0xFFF4F2EF);
                mesh(canvas, fw, fh, new int[]{0xFFFDFCFA, 0xFFE7ECF8, 0xFFFBEDE4},
                        new float[]{0.20f, 0.85f, 0.42f});
                photoGrid(canvas, fw, fh, 0xFF1B1C1F);
                break;
            case DARK:
                canvas.drawColor(0xFF06060A);
                mesh(canvas, fw, fh, new int[]{0xFF2A1460, 0xFF0B3F63, 0xFF7A1240, 0xFFE0701A},
                        new float[]{0.25f, 0.90f, 0.45f, 0.22f});
                feed(canvas, fw, fh);
                break;
            case GRID:
                canvas.drawColor(0xFF0E1118);
                float step = Math.max(18f, fw / 28f);
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(1.5f);
                paint.setColor(0x59FFFFFF);
                for (float x = 0; x < fw; x += step) canvas.drawLine(x, 0, x, fh, paint);
                for (float y = 0; y < fh; y += step) canvas.drawLine(0, y, fw, y, paint);
                paint.setStyle(Paint.Style.FILL);
                int[] cols = {0xFFE23B4C, 0xFF2C6BFF, 0xFF25C06A, 0xFFF5B301};
                for (int i = 0; i < 4; i++) {
                    paint.setColor(cols[i]);
                    canvas.drawCircle(fw * (0.14f + 0.24f * i), fh * (i % 2 == 0 ? 0.42f : 0.68f),
                            fw * 0.09f, paint);
                }
                break;
            case TEXT:
                canvas.drawColor(0xFF0B0B10);
                text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
                text.setColor(0xFFF2F4F8);
                text.setTextSize(fw * 0.085f);
                canvas.drawText("Liquid Glass", fw * 0.07f, fh * 0.10f, text);
                text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
                text.setTextSize(fw * 0.036f);
                text.setColor(0xFFAEB6C2);
                String[] lines = {
                        "Материал изгибает свет, а не только размывает его",
                        "Regular адаптируется к фону, Clear — нет",
                        "Кромка преломляет содержимое под стеклом",
                        "Дисперсия даёт радужный край на цветных объектах",
                        "Свет ходит по силуэту при касании",
                        "Меню «выпрыгивает» из своей кнопки",
                        "Панели и капсулы лежат на одной плоскости",
                        "Крупные поверхности моделируют более толстое стекло",
                };
                float y = fh * 0.16f;
                for (String line : lines) {
                    canvas.drawText(line, fw * 0.07f, y, text);
                    y += fw * 0.070f;
                }
                break;
            case PHOTO:
                if (photo != null) {
                    RectF dst = new RectF(0, 0, fw, fh);
                    float scale = Math.max(fw / photo.getWidth(), fh / photo.getHeight());
                    float dw = photo.getWidth() * scale;
                    float dh = photo.getHeight() * scale;
                    dst.set((fw - dw) / 2f, (fh - dh) / 2f, (fw + dw) / 2f, (fh + dh) / 2f);
                    canvas.drawBitmap(photo, null, dst, paint);
                } else {
                    canvas.drawColor(0xFF11131A);
                    mesh(canvas, fw, fh, new int[]{0xFF2F7BF6, 0xFF12B5A5, 0xFFF2A03D},
                            new float[]{0.30f, 0.80f, 0.35f});
                    text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
                    text.setColor(0xCCFFFFFF);
                    text.setTextSize(fw * 0.045f);
                    canvas.drawText("Своё фото: меню → «Сменить фон»", fw * 0.07f, fh * 0.5f, text);
                }
                break;
            case SOLID:
                canvas.drawColor(0xFF6F7683);
                break;
            case AURORA:
            default:
                canvas.drawColor(0xFF07070C);
                mesh(canvas, fw, fh, new int[]{0xFF2D6BFF, 0xFF8B2BE2, 0xFFE24B7A, 0xFFFFA32B,
                        0xFF12C2C2}, new float[]{0.22f, 0.70f, 0.50f, 0.35f, 0.30f});
                feed(canvas, fw, fh);
                break;
        }
    }


    /**
     * A media feed: album art, a track card and a list of rows. This is the content
     * under the glass — without edges and text behind it, refraction has nothing to
     * bend and the material looks like a plain tinted plate.
     */
    private void feed(Canvas c, float w, float h) {
        float d = w / 1080f;
        // hero album art
        RectF art = new RectF(60f * d, 220f * d, w - 60f * d, 760f * d);
        paint.setShader(new LinearGradient(art.left, art.top, art.right, art.bottom,
                0xFFE2573F, 0xFF2B2E8C, Shader.TileMode.CLAMP));
        c.drawRoundRect(art, 40f * d, 40f * d, paint);
        paint.setShader(null);

        // track card
        RectF card = new RectF(60f * d, 800f * d, w - 60f * d, 1010f * d);
        paint.setColor(0x1FFFFFFF);
        c.drawRoundRect(card, 32f * d, 32f * d, paint);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        text.setColor(0xFFFFFFFF);
        text.setTextSize(34f * d);
        c.drawText("Liquid Glass Sessions", card.left + 32f * d, card.top + 78f * d, text);
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        text.setTextSize(26f * d);
        text.setColor(0xCCFFFFFF);
        c.drawText("Biffy Clyro · Nao · Selena", card.left + 32f * d, card.top + 122f * d, text);
        paint.setColor(0xFFFFFFFF);
        c.drawCircle(card.right - 70f * d, card.centerY(), 34f * d, paint);
        paint.setColor(0xFF16181D);
        android.graphics.Path tri = new android.graphics.Path();
        tri.moveTo(card.right - 80f * d, card.centerY() - 18f * d);
        tri.lineTo(card.right - 52f * d, card.centerY());
        tri.lineTo(card.right - 80f * d, card.centerY() + 18f * d);
        tri.close();
        c.drawPath(tri, paint);

        // list rows: these are what the tab bar and the buttons will refract
        String[] names = {"Трек 1 — Refraction", "Трек 2 — Dispersion", "Трек 3 — Bevel",
                "Трек 4 — Specular", "Трек 5 — Adaptivity", "Трек 6 — Clear",
                "Трек 7 — Regular"};
        text.setTextSize(27f * d);
        float y = 1060f * d;
        for (int i = 0; i < names.length && y + 100f * d < h - 40f * d; i++) {
            RectF row = new RectF(60f * d, y, w - 60f * d, y + 88f * d);
            paint.setColor(0x1AFFFFFF);
            c.drawRoundRect(row, 24f * d, 24f * d, paint);
            paint.setColor(0x40FFFFFF);
            c.drawCircle(row.left + 46f * d, row.centerY(), 22f * d, paint);
            text.setColor(0xE6FFFFFF);
            c.drawText(names[i], row.left + 86f * d, row.centerY() + 10f * d, text);
            y += 100f * d;
        }
    }

    /** Overlapping radial gradients: the classic iOS wallpaper look. */
    private void mesh(Canvas c, float w, float h, int[] colors, float[] radii) {
        float[] cx = {0.12f, 0.86f, 0.18f, 0.78f, 0.50f};
        float[] cy = {0.10f, 0.06f, 0.88f, 0.92f, 0.50f};
        paint.setAlpha(228);
        for (int i = 0; i < colors.length; i++) {
            float r = Math.max(w, h) * radii[i % radii.length];
            paint.setShader(new RadialGradient(w * cx[i % cx.length], h * cy[i % cy.length],
                    r, colors[i], Color.TRANSPARENT, Shader.TileMode.CLAMP));
            c.drawRect(0, 0, w, h, paint);
        }
        paint.setShader(null);
        paint.setAlpha(255);
    }

    /** A photo-feed look for the light backdrop: colour blocks and captions. */
    private void photoGrid(Canvas c, float w, float h, int ink) {
        float d = w / 1080f;
        int[] cols = {0xFFE2573F, 0xFFF2C230, 0xFF2B6E71, 0xFF7B5AA6};
        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 3; j++) {
                float left = 40f * d + i * (w / 2f - 10f * d);
                float top = 120f * d + j * 300f * d;
                RectF r = new RectF(left, top, left + w / 2f - 70f * d, top + 260f * d);
                if (r.bottom > h) break;
                paint.setShader(new LinearGradient(r.left, r.top, r.right, r.bottom,
                        cols[(i + j) % cols.length], 0xFFFFFFFF, Shader.TileMode.CLAMP));
                c.drawRoundRect(r, 26f * d, 26f * d, paint);
                paint.setShader(null);
                text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
                text.setColor(ink);
                text.setAlpha(170);
                text.setTextSize(13f * d);
                c.drawText("Кадр " + (i * 3 + j + 1), r.left + 14f * d, r.bottom + 26f * d, text);
                text.setAlpha(255);
            }
        }
    }
}
