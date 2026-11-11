package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.animation.PathInterpolator;

/**
 * Bottom tab bar: a floating glass capsule with a separate circular search bubble,
 * an animated selection pill that glides between tabs (with velocity stretch and a
 * droplet bulge under the finger), and icon + label per tab.
 */
public class GlassTabBar extends GlassSurfaceView {

    public interface Listener {
        void onTabSelected(int index);
    }

    private String[] labels = {"Главная", "Поиск", "Медиа", "Профиль"};
    private GlassButton.Glyph[] glyphs = {
            GlassButton.Glyph.play, GlassButton.Glyph.search,
            GlassButton.Glyph.sparkle, GlassButton.Glyph.sliders};
    private int selected = 0;
    private int pending = 0;
    private float offset = 0f;          // animated index
    private float velocity = 0f;
    private float pressProgress = 0f;
    private boolean tracking;
    private float downX;
    private float bubbleScale = 1f;
    private Listener listener;

    private final TextPaint labelPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Path pill = new Path();
    private float tabWidth;

    public GlassTabBar(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(30f);
        setShadowLift(6f);
        labelPaint.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        labelPaint.setTextAlign(Paint.Align.CENTER);
        setClickable(true);
    }

    public void setListener(Listener l) {
        listener = l;
    }

    public void setSelected(int index) {
        selected = index;
        pending = index;
        invalidate();
    }

    public int getSelected() {
        return selected;
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        int n = labels.length;
        float side = 6f * d;
        tabWidth = (w - side * 2f) / n;

        // animate towards the selected index with a spring-ish ease
        float delta = pending - offset;
        if (Math.abs(delta) > 0.002f || Math.abs(velocity) > 0.002f) {
            velocity += delta * 0.30f;
            velocity *= 0.72f;
            offset += velocity;
            postInvalidateOnAnimation();
        } else {
            offset = pending;
            velocity = 0f;
        }
        pressProgress += ((tracking ? 1f : 0f) - pressProgress) * 0.25f;
        if (Math.abs(pressProgress - (tracking ? 1f : 0f)) > 0.002f) postInvalidateOnAnimation();

        // selection pill, stretched by velocity like a droplet
        float cx = side + tabWidth * (offset + 0.5f);
        float stretch = 1f + Math.min(Math.abs(velocity) * 0.9f, 0.35f);
        float pillH = h - 12f * d;
        float pillW = tabWidth * 0.86f * stretch;
        float py = 6f * d;
        pill.setFillType(Path.FillType.EVEN_ODD);
        pill.reset();
        pill.addRoundRect(cx - pillW / 2f, py, cx + pillW / 2f, py + pillH,
                pillH / 2f, pillH / 2f, Path.Direction.CW);
        Paint pillPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        pillPaint.setColor(0xFF0A84FF);
        pillPaint.setAlpha((int) (255 * 0.92f));
        canvas.drawPath(pill, pillPaint);

        // tab glyphs
        labelPaint.setTextSize(10f * d);
        for (int i = 0; i < n; i++) {
            float tcx = side + tabWidth * (i + 0.5f);
            boolean sel = Math.abs(offset - i) < 0.5f;
            int color = sel ? 0xFFFFFFFF : 0xFF3C4048;
            float gs = 20f * d;
            GlassButton.drawGlyph(canvas, glyphs[i], tcx - gs / 2f, 12f * d, gs, color);
            labelPaint.setColor(color);
            canvas.drawText(labels[i], tcx, h - 9f * d, labelPaint);
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                tracking = true;
                downX = x;
                animatePress(true, x, e.getY());
                invalidate();
                return true;
            case MotionEvent.ACTION_MOVE: {
                if (tabWidth <= 0f) return true;
                float side = 6f * getResources().getDisplayMetrics().density;
                int idx = (int) Math.floor((x - side) / tabWidth);
                idx = Math.max(0, Math.min(labels.length - 1, idx));
                if (idx != pending) {
                    pending = idx;
                    if (listener != null) listener.onTabSelected(idx);
                }
                return true;
            }
            case MotionEvent.ACTION_UP:
            case MotionEvent.ACTION_CANCEL: {
                tracking = false;
                animatePress(false, x, e.getY());
                selected = pending;
                if (listener != null) listener.onTabSelected(pending);
                invalidate();
                return true;
            }
            default:
                return true;
        }
    }
}
