package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RenderNode;
import android.graphics.RuntimeShader;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.View;

/**
 * Base class for every glass widget in the lab.
 *
 * <p>It owns a per-element {@link RenderNode}, a reusable {@link Path} and the
 * press animation, and asks {@link GlassRender} to paint the material. Subclasses
 * only place their contents and control the geometry.
 */
public abstract class GlassSurfaceView extends View implements GlassParams.Listener {

    protected final GlassRender.Backdrop backdrop;
    protected final GlassParams params;
    private final RenderNode node = new RenderNode("lg-element");
    private final Path path = new Path();
    private final float[] radii = new float[4];

    private float press;             // 0..1 animated
    private float pressTarget;
    private float pressX;
    private float pressY;

    protected int tint;
    protected float shadowLift = 0f;
    protected float radiusDp = 22f;

    private final RuntimeShader shader;
    /** Where the element is drawn in the parent's coordinate space (press insight). */
    private final float[] drawOrigin = new float[2];

    public GlassSurfaceView(Context context, AttributeSet attrs, GlassRender.Backdrop backdrop,
                            GlassParams params) {
        super(context, attrs);
        this.backdrop = backdrop;
        this.params = params;
        setWillNotDraw(false);
        shader = GlassRender.shader(Shaders.KEY, Shaders.LIQUID_GLASS);
        params.addListener(this);
    }

    public void setCornerRadius(float dp) {
        radiusDp = dp;
        invalidate();
    }

    public void setTint(int color) {
        tint = color;
        invalidate();
    }

    public void setShadowLift(float dp) {
        shadowLift = dp;
        invalidate();
    }

    /** Uniform corner radius in px for this element. */
    protected void fillRadii(float[] out) {
        float r = GlassRender.dp(radiusDp);
        out[0] = out[1] = out[2] = out[3] = r;
    }

    @Override
    public void onGlassParamsChanged(GlassParams p) {
        postInvalidateOnAnimation();
    }

    @Override
    protected void onDetachedFromWindow() {
        params.removeListener(this);
        super.onDetachedFromWindow();
    }

    /** Press animation: glass flexes and lights up on touch. */
    public void animatePress(boolean down, float x, float y) {
        pressTarget = down ? 1f : 0f;
        if (down) {
            pressX = x;
            pressY = y;
        }
        invalidate();
    }

    protected float press() {
        return press;
    }

    @Override
    protected void onDraw(Canvas canvas) {
        float step = 0.18f;
        if (Math.abs(press - pressTarget) > 0.001f) {
            press += (pressTarget - press) * step;
            postInvalidateOnAnimation();
        } else {
            press = pressTarget;
        }

        float w = getWidth();
        float h = getHeight();
        fillRadii(radii);

        // interactive flex: the material is drawn slightly larger (as if it swells)
        float scale = 1f + (params.pressScale - 1f) * press;
        float[] origin = origin();
        float dx = origin[0] - (scale - 1f) * w / 2f;
        float dy = origin[1] - (scale - 1f) * h / 2f;
        float sw = w * scale;
        float sh = h * scale;
        try {
            GlassRender.drawGlass(canvas, backdrop, params, shader, node, path,
                    dx, dy, sw, sh, radii,
                    press, pressX, pressY, tint, shadowLift, origin);
        } catch (Throwable t) {
            // never let the material take the UI down (odd GPU / driver)
            canvas.drawRoundRect(0f, 0f, w, h, radii[0], radii[0],
                    fallbackPaint(params));
        }

        drawContents(canvas, w, h);
    }

    /**
     * Elements are drawn into the window coordinate space (that is what the backdrop
     * sampling needs), so they must offset themselves back by their own position.
     */
    private float[] origin() {
        // Coordinates of the parent container (= the coordinate space the backdrop was
        // recorded in), not window coordinates: the backdrop node starts at the
        // container's own origin, so insets must not leak into the sampling offset.
        android.view.ViewParent parent = getParent();
        if (parent instanceof View) {
            drawOrigin[0] = getX();
            drawOrigin[1] = getY();
        } else {
            int[] loc = new int[2];
            getLocationInWindow(loc);
            drawOrigin[0] = loc[0];
            drawOrigin[1] = loc[1];
        }
        return drawOrigin;
    }

    private static Paint fallbackPaint(GlassParams params) {
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setColor(0xCC2A2F3A);
        paint.setAlpha((int) (255 * Math.max(0.35f, params.effectiveOpacity() * 0.7f)));
        return paint;
    }

    /** Subclass hook: draw icons/labels on top of the material. */
    protected void drawContents(Canvas canvas, float w, float h) {
    }

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        switch (event.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                animatePress(true, event.getX(), event.getY());
                return true;
            case MotionEvent.ACTION_UP:
                animatePress(false, event.getX(), event.getY());
                if (isClickable()) performClick();
                return true;
            case MotionEvent.ACTION_CANCEL:
                animatePress(false, event.getX(), event.getY());
                return true;
            default:
                return true;
        }
    }

    @Override
    public boolean performClick() {
        return super.performClick();
    }
}
