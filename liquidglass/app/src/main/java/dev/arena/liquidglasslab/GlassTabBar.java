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
 * Bottom tab bar: a floating glass capsule with an animated selection pill that glides
 * between tabs (with velocity stretch) and a separate circular search bubble next to
 * it — the iOS 26 pattern. Icons and labels are drawn inside the capsule only.
 */
public class GlassTabBar extends GlassSurfaceView {

    public interface Listener {
        void onTabSelected(int index);
    }

    private String[] labels = {"Главная", "Пресеты", "Сцена", "Тюнинг"};
    private GlassButton.Glyph[] glyphs = {
            GlassButton.Glyph.play, GlassButton.Glyph.grid,
            GlassButton.Glyph.photo, GlassButton.Glyph.tune};
    private int selected = 0;
    private int pending = 0;
    private float offset = 0f;
    private float velocity = 0f;
    private boolean tracking;
    private float downX;
    private Listener listener;

    private final TextPaint labelPaint = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path pill = new Path();
    private float tabWidth;

    public GlassTabBar(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                       GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(28f);
        setShadowLift(8f);
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
        float side = 5f * d;
        tabWidth = (w - side * 2f) / n;

        // spring towards the selected index
        float delta = pending - offset;
        if (Math.abs(delta) > 0.002f || Math.abs(velocity) > 0.002f) {
            velocity += delta * 0.30f;
            velocity *= 0.70f;
            offset += velocity;
            postInvalidateOnAnimation();
        } else {
            offset = pending;
            velocity = 0f;
        }

        // selection pill, stretched by velocity like a droplet
        float cx = side + tabWidth * (offset + 0.5f);
        float stretch = 1f + Math.min(Math.abs(velocity) * 0.8f, 0.30f);
        float pillH = h - 12f * d;
        float pillW = tabWidth * 0.88f * stretch;
        pill.reset();
        pill.addRoundRect(cx - pillW / 2f, 6f * d, cx + pillW / 2f, 6f * d + pillH,
                pillH / 2f, pillH / 2f, Path.Direction.CW);
        paint.setColor(params.accent());
        paint.setAlpha(235);
        canvas.drawPath(pill, paint);

        float glyphSize = Math.min(20f * d, h * 0.30f);
        labelPaint.setTextSize(Math.min(11f * d, h * 0.16f));
        for (int i = 0; i < n; i++) {
            float tcx = side + tabWidth * (i + 0.5f);
            boolean sel = Math.abs(offset - i) < 0.5f;
            int color = sel ? 0xFFFFFFFF : params.inkSecondary();
            GlassButton.drawGlyph(canvas, glyphs[i], tcx - glyphSize / 2f,
                    h * 0.20f, glyphSize, color);
            labelPaint.setColor(color);
            canvas.drawText(labels[i], tcx, h - 9f * d, labelPaint);
        }
    }

    private int tabAt(float x) {
        if (tabWidth <= 0f) return selected;
        float side = 5f * getResources().getDisplayMetrics().density;
        int idx = (int) Math.floor((x - side) / tabWidth);
        return Math.max(0, Math.min(labels.length - 1, idx));
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                tracking = true;
                downX = x;
                // a plain tap never produces a MOVE, so resolve the tab here as well
                pending = tabAt(x);
                animatePress(true, x, e.getY());
                invalidate();
                return true;
            case MotionEvent.ACTION_MOVE: {
                int idx = tabAt(x);
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
