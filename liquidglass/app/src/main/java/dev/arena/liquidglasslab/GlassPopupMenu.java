package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RenderNode;
import android.graphics.RenderEffect;
import android.graphics.RuntimeShader;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A menu that "pops open" out of its button: the sheet is a thick glass slab, rows
 * highlight from the touch point, and the whole thing grows with a short spring.
 */
public class GlassPopupMenu extends GlassSurfaceView {

    public interface Listener {
        void onItem(int index);
        void onDismissed();
    }

    private static final String[] ITEMS = {
            "Регулярный / Clear", "Пресеты материала", "Параметры вживую",
            "Сменить фон", "Скрыть панель"};

    private int highlighted = -1;
    private float appear;
    private boolean open;
    private Listener listener;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();
    private float rowHeight;

    public GlassPopupMenu(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                          GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(24f);
        setShadowLift(10f);
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        setClickable(true);
    }

    public void setListener(Listener l) {
        listener = l;
    }

    public boolean isOpen() {
        return open;
    }

    public void setOpen(boolean value) {
        open = value;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        float target = open ? 1f : 0f;
        appear += (target - appear) * 0.25f;
        if (Math.abs(appear - target) > 0.004f) postInvalidateOnAnimation();
        if (appear < 0.01f) return;

        int n = ITEMS.length;
        rowHeight = h / n;
        for (int i = 0; i < n; i++) {
            float top = i * rowHeight;
            if (i == highlighted) {
                paint.setColor(0x1FFFFFFF);
                canvas.drawRoundRect(8f * d, top + 2f * d, w - 8f * d,
                        top + rowHeight - 2f * d, 12f * d, 12f * d, paint);
            }
            text.setColor(0xFF14161B);
            text.setTextSize(14f * d);
            Paint.FontMetrics fm = text.getFontMetrics();
            canvas.drawText(ITEMS[i], 18f * d,
                    top + rowHeight / 2f - (fm.ascent + fm.descent) / 2f, text);
            GlassButton.drawGlyph(canvas, GlassButton.Glyph.chevron, w - 26f * d, top + rowHeight / 2f,
                    10f * d, 0x77000000);
            if (i < n - 1) {
                paint.setColor(0x14000000);
                canvas.drawRect(16f * d, top + rowHeight - 0.5f, w - 16f * d,
                        top + rowHeight + 0.5f, paint);
            }
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float rowH = getHeight() / (float) ITEMS.length;
        int index = Math.max(0, Math.min(ITEMS.length - 1, (int) (e.getY() / rowH)));
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                highlighted = index;
                animatePress(true, e.getX(), e.getY());
                invalidate();
                return true;
            case MotionEvent.ACTION_MOVE:
                if (index != highlighted) {
                    highlighted = index;
                    invalidate();
                }
                return true;
            case MotionEvent.ACTION_UP:
                animatePress(false, e.getX(), e.getY());
                highlighted = -1;
                if (listener != null) listener.onItem(index);
                invalidate();
                return true;
            case MotionEvent.ACTION_CANCEL:
                animatePress(false, e.getX(), e.getY());
                highlighted = -1;
                invalidate();
                return true;
            default:
                return true;
        }
    }
}
