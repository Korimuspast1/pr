package lav.glasscam.alav;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.util.AttributeSet;
import android.view.View;

/** Сетка «правило третей» поверх превью. */
public class GridView extends View {

    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

    public GridView(Context context, AttributeSet attrs) {
        super(context, attrs);
        paint.setColor(0x66FFFFFF);
        paint.setStrokeWidth(2f);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        float w = getWidth();
        float h = getHeight();
        canvas.drawLine(w / 3f, 0, w / 3f, h, paint);
        canvas.drawLine(2 * w / 3f, 0, 2 * w / 3f, h, paint);
        canvas.drawLine(0, h / 3f, w, h / 3f, paint);
        canvas.drawLine(0, 2 * h / 3f, w, 2 * h / 3f, paint);
    }
}
