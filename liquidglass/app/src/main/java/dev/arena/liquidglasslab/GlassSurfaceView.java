package dev.arena.liquidglasslab;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.RenderNode;
import android.graphics.RuntimeShader;
import android.util.AttributeSet;
import android.view.MotionEvent;
import android.view.View;

/**
 * Base class for every glass widget in the lab.
 *
 * <p>Owns a per-element {@link RenderNode} plus the shader state cache and asks
 * {@link GlassRender} to paint the material; subclasses only place their contents and
 * control the geometry. Nothing draws outside the element bounds, so labels can never
 * spill over a neighbouring control.
 */
public abstract class GlassSurfaceView extends View implements GlassParams.Listener {

    protected final GlassRender.Backdrop backdrop;
    protected final GlassParams params;
    private final RenderNode node = new RenderNode("lg-element");
    private final GlassRender.ShaderState shaderState = new GlassRender.ShaderState();
    private final float[] radii = new float[4];

    private float press;             // 0..1 animated
    private float pressTarget;
    private float pressX;
    private float pressY;

    protected int tint;
    protected float shadowLift = 0f;
    protected float radiusDp = 22f;

    private final RuntimeShader shader;

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
        float r = Math.min(GlassRender.dp(radiusDp), Math.min(getWidth(), getHeight()) / 2f);
        out[0] = out[1] = out[2] = out[3] = r;
    }

    /** Lets subclasses force a redraw when their own state changes. */
    protected void repaint() {
        invalidate();
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
        float step = 0.25f;
        if (Math.abs(press - pressTarget) > 0.001f) {
            press += (pressTarget - press) * step;
            postInvalidateOnAnimation();
        } else {
            press = pressTarget;
        }

        float w = getWidth();
        float h = getHeight();
        if (w <= 0 || h <= 0) return;
        fillRadii(radii);

        // The view's canvas is already translated to its own origin, so the material is
        // drawn at (0,0); the recorded backdrop is shifted by the view's position.
        float ox = getX() - (params.pressScale - 1f) * press * w / 2f;
        float oy = getY() - (params.pressScale - 1f) * press * h / 2f;
        float scale = 1f + (params.pressScale - 1f) * press;

        GlassRender.drawGlass(canvas, backdrop, params, shader, node, shaderState,
                ox, oy, w * scale, h * scale, radii,
                press, pressX, pressY, tint, shadowLift);

        drawContents(canvas, w, h);
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
