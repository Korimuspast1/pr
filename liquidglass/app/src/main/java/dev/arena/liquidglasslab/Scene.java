package dev.arena.liquidglasslab;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RadialGradient;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.Typeface;
import android.text.TextPaint;

/**
 * The scene behind the glass: wallpapers + sample content.
 *
 * <p>Seven backdrops, chosen to stress different parts of the optical model:
 * colourful gradients (tint/adaptivity), a nearly white one (legibility), a dark one
 * (rim visibility), a high-frequency grid and text (refraction and dispersion are
 * only visible with edges behind the glass), and a user photo.
 */
public final class Scene {

    public enum Style {
        AURORA("Aurora"), LIGHT("Светлая"), DARK("Ночная"), GRID("Сетка"),
        TEXT("Текст"), PHOTO("Фото"), SOLID("Ровный фон");

        public final String title;

        Style(String title) {
            this.title = title;
        }
    }

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();

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

    public Bitmap photo() {
        return photo;
    }

    public void paint(Canvas canvas, int w, int h) {
        float fw = w;
        float fh = h;
        switch (style) {
            case LIGHT:
                canvas.drawColor(0xFFF2F1EE);
                mesh(canvas, fw, fh, new int[]{0xFFFDFCFA, 0xFFEFEFF6, 0xFFFBF3EE},
                        new float[]{0.2f, 0.85f, 0.4f});
                contentList(canvas, fw, fh, 0xFF1B1C1F, 0.10f);
                break;
            case DARK:
                canvas.drawColor(0xFF07070B);
                mesh(canvas, fw, fh, new int[]{0xFF2A1460, 0xFF0B3F63, 0xFF7A1240, 0xFFE0701A},
                        new float[]{0.25f, 0.9f, 0.45f, 0.22f});
                contentList(canvas, fw, fh, 0xFFF2F4F8, 0.10f);
                break;
            case GRID:
                canvas.drawColor(0xFF10131A);
                float step = 26f * (fw / 1080f);
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(2f);
                paint.setColor(0x66FFFFFF);
                for (float x = 0; x < fw; x += step) canvas.drawLine(x, 0, x, fh, paint);
                for (float y = 0; y < fh; y += step) canvas.drawLine(0, y, fw, y, paint);
                paint.setStyle(Paint.Style.FILL);
                for (int i = 0; i < 4; i++) {
                    paint.setColor(new int[]{0xFFE23B4C, 0xFF2C6BFF, 0xFF25C06A, 0xFFF5B301}[i]);
                    float bx = fw * (0.08f + 0.22f * i);
                    canvas.drawCircle(bx, fh * 0.45f, fw * 0.10f, paint);
                }
                break;
            case TEXT:
                canvas.drawColor(0xFF0C0C10);
                text.setColor(0xFFEDEFF4);
                text.setTextSize(fw * 0.075f);
                text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
                canvas.drawText("Liquid Glass", fw * 0.07f, fh * 0.12f, text);
                text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
                text.setTextSize(fw * 0.032f);
                text.setColor(0xFF9CA3AF);
                String[] lines = {"Материал изгибает свет, а не только размывает его",
                        "Regular адаптируется к фону, Clear — нет",
                        "Кромка преломляет содержимое под стеклом",
                        "Дисперсия добавляет радужный край на цветных объектах",
                        "Тени усиливаются, когда под стеклом текст",
                        "Капсулы, меню и панели лежат на одном слое",
                        "Морф между состояниями сохраняет единство плоскости",
                        "Свет ходит по силуэту при касании"};
                float y = fh * 0.17f;
                for (String line : lines) {
                    canvas.drawText(line, fw * 0.07f, y, text);
                    y += fw * 0.062f;
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
                    canvas.drawColor(0xFF14151A);
                    mesh(canvas, fw, fh, new int[]{0xFF2F7BF6, 0xFF12B5A5, 0xFFF2A03D},
                            new float[]{0.3f, 0.8f, 0.35f});
                }
                break;
            case SOLID:
                canvas.drawColor(0xFF6F7683);
                break;
            case AURORA:
            default:
                canvas.drawColor(0xFF07070C);
                mesh(canvas, fw, fh, new int[]{0xFF2D6BFF, 0xFF8B2BE2, 0xFFE24B7A, 0xFFFFA32B,
                        0xFF12C2C2}, new float[]{0.22f, 0.7f, 0.5f, 0.35f, 0.3f});
                contentCards(canvas, fw, fh);
                break;
        }
    }

    /** Overlapping radial gradients: the classic iOS wallpaper look. */
    private void mesh(Canvas c, float w, float h, int[] colors, float[] radii) {
        float[] cx = {0.12f, 0.86f, 0.18f, 0.78f, 0.5f};
        float[] cy = {0.10f, 0.06f, 0.88f, 0.92f, 0.5f};
        paint.setAlpha(225);
        for (int i = 0; i < colors.length; i++) {
            float r = Math.max(w, h) * radii[i % radii.length];
            paint.setShader(new RadialGradient(w * cx[i % cx.length], h * cy[i % cy.length],
                    r, colors[i], Color.TRANSPARENT, Shader.TileMode.CLAMP));
            c.drawRect(0, 0, w, h, paint);
        }
        paint.setShader(null);
        paint.setAlpha(255);
    }

    /** Album-art cards + list rows: content that refraction can bend. */
    private void contentCards(Canvas c, float w, float h) {
        float d = w / 1080f;
        text.setTextSize(30f * d);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        text.setColor(0xFFFFFFFF);
        c.drawText("Слушайте сейчас", 56f * d, 250f * d, text);

        String[] names = {"Biffy Clyro", "Nao", "Selena", "Nils Frahm"};
        int[] cols = {0xFFE2573F, 0xFFF2C230, 0xFF2B2B2B, 0xFF3C6E71};
        for (int i = 0; i < 2; i++) {
            float left = 56f * d + i * 500f * d;
            RectF card = new RectF(left, 300f * d, left + 460f * d, 760f * d);
            paint.setShader(new LinearGradient(card.left, card.top, card.right, card.bottom,
                    cols[i], cols[i + 1], Shader.TileMode.CLAMP));
            c.drawRoundRect(card, 34f * d, 34f * d, paint);
            paint.setShader(null);
            text.setColor(0xE6FFFFFF);
            text.setTextSize(34f * d);
            c.drawText(names[i], card.left + 28f * d, card.bottom - 40f * d, text);
        }

        text.setTextSize(28f * d);
        for (int i = 0; i < 7; i++) {
            float y = 860f * d + i * 108f * d;
            if (y > h - 40f * d) break;
            paint.setColor(0x1AFFFFFF);
            c.drawRoundRect(new RectF(56f * d, y, w - 56f * d, y + 92f * d),
                    24f * d, 24f * d, paint);
            paint.setColor(0x33FFFFFF);
            c.drawCircle(56f * d + 56f * d, y + 46f * d, 26f * d, paint);
            text.setColor(0xE6FFFFFF);
            text.setTextSize(26f * d);
            c.drawText("Трек " + (i + 1) + " — Liquid Glass Sessions", 56f * d + 104f * d,
                    y + 56f * d, text);
        }
    }

    private void contentList(Canvas c, float w, float h, int color, float alpha) {
        float d = w / 1080f;
        text.setColor(color);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        text.setTextSize(58f * d);
        text.setAlpha(235);
        c.drawText("Заметки", 56f * d, 190f * d, text);
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        text.setTextSize(30f * d);
        text.setAlpha((int) (255 * 0.8f));
        float y = 270f * d;
        for (int i = 0; i < 14 && y < h - 60f * d; i++) {
            paint.setColor(color);
            paint.setAlpha((int) (255 * alpha * 0.6f));
            c.drawRoundRect(new RectF(56f * d, y, w - 56f * d, y + 92f * d),
                    22f * d, 22f * d, paint);
            paint.setAlpha(255);
            text.setColor(color);
            text.setAlpha(230);
            c.drawText("Строка " + (i + 1) + " под стеклом", 88f * d, y + 58f * d, text);
            y += 104f * d;
        }
        text.setAlpha(255);
    }
}
