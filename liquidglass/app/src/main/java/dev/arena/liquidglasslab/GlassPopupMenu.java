package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.util.AttributeSet;
import android.view.MotionEvent;

/**
 * A menu that pops out of its button: a thick glass slab with rows that highlight from
 * the touch point, growing in with a short spring.
 */
public class GlassPopupMenu extends GlassSurfaceView {

    public interface Listener {
        void onItem(int index);
        void onDismissed();
    }

    private String[] items = {
            "Regular ↔ Clear", "Пресеты материала", "Параметры вживую",
            "Сменить фон", "Показать / скрыть панель"};
    private int highlighted = -1;
    private float appear;
    private boolean open;
    private Listener listener;
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private float rowHeight;

    public GlassPopupMenu(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                          GlassParams params) {
        super(context, attrs, backdrop, params);
        setCornerRadius(26f);
        setShadowLift(12f);
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        setClickable(true);
    }

    public void setListener(Listener l) {
        listener = l;
    }

    public void setItems(String... set) {
        items = set;
        invalidate();
    }

    public boolean isOpen() {
        return open;
    }

    public void setOpen(boolean value) {
        open = value;
        if (!value) highlighted = -1;
        invalidate();
    }

    @Override
    protected void drawContents(Canvas canvas, float w, float h) {
        float d = getResources().getDisplayMetrics().density;
        float target = open ? 1f : 0f;
        appear += (target - appear) * 0.28f;
        if (Math.abs(appear - target) > 0.004f) postInvalidateOnAnimation();

        float pad = 7f * d;
        rowHeight = (h - pad * 2f) / Math.max(items.length, 1);

        if (highlighted >= 0 && highlighted < items.length) {
            paint.setColor(params.darkBackdrop ? 0x2BFFFFFF : 0x1A000000);
            float top = pad + highlighted * rowHeight;
            canvas.drawRoundRect(pad, top + 1f, w - pad, top + rowHeight - 1f,
                    13f * d, 13f * d, paint);
        }

        text.setTextSize(Math.min(14f * d, rowHeight * 0.38f));
        text.setTextAlign(Paint.Align.LEFT);
        Paint.FontMetrics fm = text.getFontMetrics();
        float gs = Math.min(17f * d, rowHeight * 0.44f);
        for (int i = 0; i < items.length; i++) {
            float cy = pad + rowHeight * (i + 0.5f);
            boolean hl = i == highlighted;
            text.setColor(hl ? params.ink() : params.inkSecondary());
            GlassButton.drawGlyph(canvas, glyphFor(i), 18f * d, cy - gs / 2f, gs,
                    hl ? params.ink() : params.inkSecondary());
            canvas.drawText(items[i], 18f * d + gs + 12f * d,
                    cy - (fm.ascent + fm.descent) / 2f, text);
        }
    }

    private GlassButton.Glyph glyphFor(int i) {
        switch (i) {
            case 0: return GlassButton.Glyph.sparkle;
            case 1: return GlassButton.Glyph.grid;
            case 2: return GlassButton.Glyph.tune;
            case 3: return GlassButton.Glyph.photo;
            default: return GlassButton.Glyph.dots;
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        float x = e.getX();
        float y = e.getY();
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
            case MotionEvent.ACTION_MOVE:
                highlighted = rowAt(y);
                animatePress(true, x, y);
                invalidate();
                return true;
            case MotionEvent.ACTION_UP: {
                int row = rowAt(y);
                animatePress(false, x, y);
                highlighted = -1;
                if (row >= 0 && listener != null) listener.onItem(row);
                else if (listener != null) listener.onDismissed();
                invalidate();
                return true;
            }
            case MotionEvent.ACTION_CANCEL:
                highlighted = -1;
                animatePress(false, x, y);
                return true;
            default:
                return true;
        }
    }

    private int rowAt(float y) {
        if (rowHeight <= 0f) return -1;
        float pad = 7f * getResources().getDisplayMetrics().density;
        int row = (int) Math.floor((y - pad) / rowHeight);
        if (row < 0 || row >= items.length) return -1;
        return row;
    }
}
