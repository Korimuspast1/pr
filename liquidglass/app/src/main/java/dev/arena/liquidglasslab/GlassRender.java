package dev.arena.liquidglasslab;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorMatrix;
import android.graphics.ColorMatrixColorFilter;
import android.graphics.Paint;
import android.graphics.RenderEffect;
import android.graphics.RenderNode;
import android.graphics.RuntimeShader;
import android.graphics.Shader;
import android.os.Build;
import android.util.Log;


/**
 * The glass painter: RenderNode plumbing + the optical shader.
 *
 * <p>Architecture (same as QWEA0/Liquid-Glass-Android and Apple's effect):
 * <ol>
 *   <li>the lab view records the whole scene once per frame;</li>
 *   <li>each element records its own slice of that scene into a RenderNode whose
 *       bounds are the element plus {@code margin} on every side, so the shader can
 *       sample "outside" the silhouette without hitting transparent black;</li>
 *   <li>the render effect is {@code lens shader -> blur}: the shader bends light
 *       along the SDF normal, splits channels for dispersion and paints the rim;</li>
 *   <li>the shader clips itself with the SDF, so no canvas clip is needed and the
 *       element can never paint outside its own bounds.</li>
 * </ol>
 *
 * <p>If AGSL is unavailable (API &lt; 33 or a driver that rejects the shader) the
 * element falls back to a hand-painted glass look built from the same backdrop, so
 * every screen still shows glass instead of a flat plate.
 */
public final class GlassRender {

    private static final String TAG = "LiquidGlass";

    public static final boolean SHADER_SUPPORTED = Build.VERSION.SDK_INT >= 33;
    public static final boolean EFFECT_SUPPORTED = Build.VERSION.SDK_INT >= 31;

    /** Why the app is on the fallback path; shown in the lab's status line. */
    public static String diagnostics = SHADER_SUPPORTED ? "AGSL lens" : "API < 33: fallback";

    /** AGSL is compiled once per element: uniform values live in the shader instance. */
    private static boolean shaderBroken;
    private static String shaderSource = "";

    private static float sDensity = 1f;

    private GlassRender() {
    }

    public static void setDensity(float density) {
        sDensity = density <= 0f ? 1f : density;
    }

    public static float dp(float value) {
        return value * sDensity;
    }

    /**
     * Returns a fresh {@link RuntimeShader} for one element. Each element needs its own
     * instance because a RenderEffect snapshots the shader's uniform values when it is
     * built; sharing one instance would make every element show the last element's ones.
     */
    public static RuntimeShader shader(String key, String source) {
        if (!SHADER_SUPPORTED || shaderBroken) return null;
        shaderSource = source;
        try {
            RuntimeShader shader = new RuntimeShader(source);
            diagnostics = "AGSL lens";
            return shader;
        } catch (Throwable t) {
            Log.w(TAG, "RuntimeShader rejected: " + t);
            shaderBroken = true;
            diagnostics = "shader compile failed: " + t.getClass().getSimpleName();
            return null;
        }
    }

    // ---------------------------------------------------------------- backdrop

    /** Scene capture. Reusable, no bitmap allocation. */
    public static final class Backdrop {
        private final RenderNode sceneNode = new RenderNode("lg-scene");
        int width;
        int height;
        private boolean recorded;

        public RenderNode scene() {
            return sceneNode;
        }

        public int width() {
            return width;
        }

        public int height() {
            return height;
        }

        public boolean isRecorded() {
            return recorded;
        }

        public void setSize(int w, int h) {
            width = w;
            height = h;
        }

        public void record(Canvas target, SceneSource source) {
            if (width <= 0 || height <= 0) return;
            sceneNode.setPosition(0, 0, width, height);
            Canvas c = sceneNode.beginRecording();
            c.drawColor(Color.BLACK);
            source.drawScene(c, width, height);
            sceneNode.endRecording();
            recorded = true;
        }
    }

    /** What the app must provide: draw everything that lives behind the glass. */
    public interface SceneSource {
        void drawScene(Canvas canvas, int width, int height);
    }

    // ------------------------------------------------------------------ glass

    /**
     * Draws one glass element.
     *
     * @param canvas   canvas in the container's coordinate space
     * @param backdrop recorded scene, same coordinate space
     * @param params   material parameters
     * @param shader   compiled lens shader (null -> fallback material)
     * @param node     per-element RenderNode (reused across frames)
     * @param shaderState per-element cached effect state (reused)
     * @param ox,oy   element origin in backdrop coordinates (the canvas itself is in
     *                element-local space, so the material is drawn at (0,0) and the
     *                recorded backdrop is shifted by -origin)
     * @param w,h      element size
     * @param radii    corner radii tl, tr, br, bl in px
     * @param press    0..1 press progress
     * @param px,py    press point, element-local px
     * @param tint     per-element body colour (0 = none)
     * @param shadowDp extra shadow lift for raised elements
     */
    public static void drawGlass(Canvas canvas, Backdrop backdrop, GlassParams params,
                                 RuntimeShader shader, RenderNode node, ShaderState shaderState,
                                 float ox, float oy, float w, float h, float[] radii,
                                 float press, float px, float py, int tint, float shadowDp) {

        if (w < 1f || h < 1f) return;

        int margin = Math.max(1, (int) Math.ceil(dp(params.lens) * 1.6f + dp(params.blur) * 1.5f));

        // Note: a dropped shadow is intentionally not painted. A View clips its children
        // to their bounds, so anything outside the silhouette turns into a hard edge —
        // depth is communicated by the rim shading inside the material instead.

        boolean shaderPath = SHADER_SUPPORTED && shader != null && EFFECT_SUPPORTED
                && backdrop.isRecorded();

        if (shaderPath) {
            int recW = Math.round(w) + margin * 2;
            int recH = Math.round(h) + margin * 2;
            node.setPosition(-margin, -margin, -margin + recW, -margin + recH);
            Canvas rc = node.beginRecording();
            rc.translate(margin - ox, margin - oy);
            rc.drawRenderNode(backdrop.scene());
            node.endRecording();

            if (shaderState.prepare(shader, params, w, h, radii, margin, press, px, py,
                    tint, backdrop.width(), backdrop.height())) {
                node.setRenderEffect(RenderEffect.createRuntimeShaderEffect(shader, "content"));
            }
            canvas.drawRenderNode(node);
        } else {
            drawFallback(canvas, backdrop, params, ox, oy, w, h, radii, tint, press, px, py);
        }

        // ---- Increase Contrast: contrasting hairline outline
        if (params.increaseContrast) {
            Paint outline = new Paint(Paint.ANTI_ALIAS_FLAG);
            outline.setStyle(Paint.Style.STROKE);
            outline.setStrokeWidth(dp(1.2f));
            outline.setColor(Color.argb(190, 255, 255, 255));
            canvas.drawRoundRect(0f, 0f, w, h, radii[0], radii[0], outline);
        }
    }

    /** Cached effect inputs, so the RenderEffect is only rebuilt when they change. */
    public static final class ShaderState {
        private float[] last = new float[0];
        private boolean valid;

        boolean prepare(RuntimeShader sh, GlassParams p, float w, float h, float[] radii,
                        int margin, float press, float px, float py, int tint,
                        int sceneW, int sceneH) {
            float lightAngle = (float) Math.toRadians(p.lightAngle);
            float lx = (float) Math.cos(lightAngle);
            float ly = (float) Math.sin(lightAngle);

            float[] now = {
                    margin, w, h, radii[0], radii[1], radii[2], radii[3],
                    dp(p.bevel), -dp(p.lens), p.falloff,
                    p.variant == GlassParams.Variant.CLEAR ? p.chroma * 0.6f : p.chroma,
                    lx, ly, p.effectiveHighlight(), dp(p.highlightWidth) * 0.6f,
                    tint, p.adaptiveTintFlag(), p.effectiveDim(), press, px, p.glassTint
            };
            if (last.length != now.length) last = new float[now.length];
            boolean changed = !valid;
            if (!changed) {
                for (int i = 0; i < now.length; i++) {
                    if (Math.abs(now[i] - last[i]) > 1e-4f) {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return false;
            System.arraycopy(now, 0, last, 0, now.length);
            valid = true;

            sh.setFloatUniform("margin", (float) margin);
            sh.setFloatUniform("viewSize", w, h);
            sh.setFloatUniform("radii", radii[0], radii[1], radii[2], radii[3]);
            sh.setFloatUniform("bevel", Math.max(dp(p.bevel), 1f));
            sh.setFloatUniform("refractPx", Math.max(dp(p.lens), 0f));
            sh.setFloatUniform("falloff", Math.max(p.falloff, 0f));
            sh.setFloatUniform("dispersion", Math.max(0f, Math.min(p.chroma, 1f)));
            sh.setFloatUniform("lightDir", lx, ly);
            sh.setFloatUniform("specStrength", p.effectiveHighlight());
            sh.setFloatUniform("rimBandMax", Math.max(dp(p.highlightWidth), 2f));
            int t = tint != 0 ? tint : p.tintColor;
            int body = p.glassTint;
            sh.setFloatUniform("glassTint", Color.red(body) / 255f, Color.green(body) / 255f,
                    Color.blue(body) / 255f, Color.alpha(body) / 255f);
            sh.setFloatUniform("tintColor", Color.red(t) / 255f, Color.green(t) / 255f,
                    Color.blue(t) / 255f, tint != 0 ? Color.alpha(t) / 255f
                            : (p.variant == GlassParams.Variant.CLEAR ? 0.10f : 0f));
            sh.setFloatUniform("adaptiveTint", body != 0 ? 0f : p.adaptiveTintFlag());
            sh.setFloatUniform("dimAmount", p.effectiveDim());
            sh.setFloatUniform("press", press);
            sh.setFloatUniform("touchPos", px, py);
            sh.setFloatUniform("touchAmp", dp(p.pressBulge));
            return true;
        }
    }

    /**
     * Fallback material that still looks like glass: the blurred backdrop itself, a
     * soft tint, brightness that rises towards the top, a lit rim on the light side
     * and a shaded counter-rim. No flat plate.
     */
    private static void drawFallback(Canvas canvas, Backdrop backdrop, GlassParams params,
                                     float ox, float oy, float w, float h, float[] radii,
                                     int tint, float press, float px, float py) {
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        android.graphics.Path clip = new android.graphics.Path();
        buildSilhouette(clip, w, h, radii);

        canvas.save();
        canvas.clipPath(clip);

        // 1. what is behind the glass
        if (backdrop.isRecorded() && EFFECT_SUPPORTED) {
            RenderNode blur = blurCache;
            if (blur == null) {
                blur = new RenderNode("lg-fallback-blur");
                blurCache = blur;
            }
            float r = Math.min(Math.max(dp(params.effectiveBlur()), 0.1f), 28f);
            int key = (backdrop.hashCode() * 31 + Float.floatToIntBits(r)) * 31
                    + Float.floatToIntBits(params.effectiveSaturation());
            if (key != blurKey) {
                blurKey = key;
                blur.setPosition(0, 0, backdrop.width(), backdrop.height());
                Canvas bc = blur.beginRecording();
                bc.drawRenderNode(backdrop.scene());
                blur.endRecording();
                ColorMatrix cm = new ColorMatrix();
                cm.setSaturation(params.effectiveSaturation());
                RenderEffect effect = RenderEffect.createChainEffect(
                        RenderEffect.createColorFilterEffect(new ColorMatrixColorFilter(cm)),
                        RenderEffect.createBlurEffect(r, r, Shader.TileMode.CLAMP));
                blur.setRenderEffect(effect);
            }
            canvas.translate(-ox, -oy);
            canvas.drawRenderNode(blur);
            canvas.translate(ox, oy);
        } else {
            paint.setColor(0xFF1A1D24);
            canvas.drawRect(0f, 0f, w, h, paint);
        }

        // 2. tint / dim
        float dim = params.effectiveDim();
        if (dim > 0f) {
            paint.setColor(Color.argb((int) (255 * dim), 0, 0, 0));
            canvas.drawRect(0f, 0f, w, h, paint);
        }
        if (tint != 0) {
            paint.setColor(tint);
            canvas.drawRect(0f, 0f, w, h, paint);
        }

        // 3. thickness: brighter towards the light, darker at the opposite edge
        paint.setShader(new android.graphics.LinearGradient(0f, 0f, w * 0.4f, h,
                new int[]{0x22FFFFFF, 0x00FFFFFF, 0x33000000},
                new float[]{0f, 0.45f, 1f}, Shader.TileMode.CLAMP));
        canvas.drawRect(0f, 0f, w, h, paint);
        paint.setShader(null);

        // 4. lit rim: bright on the light side, shaded on the opposite side
        Paint rim = new Paint(Paint.ANTI_ALIAS_FLAG);
        rim.setStyle(Paint.Style.STROKE);
        rim.setStrokeWidth(Math.max(dp(1.4f), 1.2f));
        rim.setShader(new android.graphics.LinearGradient(0f, 0f, w, h,
                new int[]{0xCCFFFFFF, 0x33FFFFFF, 0x66000000},
                new float[]{0f, 0.5f, 1f}, Shader.TileMode.CLAMP));
        canvas.drawPath(clip, rim);

        // 5. inner shadow hugging the silhouette (sells the thickness)
        Paint inner = new Paint(Paint.ANTI_ALIAS_FLAG);
        inner.setStyle(Paint.Style.STROKE);
        inner.setStrokeWidth(dp(8f));
        inner.setColor(0x33000000);
        canvas.save();
        canvas.clipPath(clip);
        canvas.drawPath(clip, inner);
        canvas.restore();

        // 6. press: a soft light under the finger
        if (press > 0.01f) {
            paint.setShader(new android.graphics.RadialGradient(px, py, Math.max(w, h) * 0.6f,
                    0x30FFFFFF, 0x00FFFFFF, Shader.TileMode.CLAMP));
            canvas.drawRect(0f, 0f, w, h, paint);
            paint.setShader(null);
        }

        canvas.restore();
    }

    private static RenderNode blurCache;
    private static int blurKey = 0;

    private static RenderNode shadowNode;

    /** Draws a blurred silhouette behind the element. */
    private static void drawShadowNode(Canvas canvas, float x, float y, float w, float h,
                                       float[] radii, float radius, int alpha) {
        int pad = (int) Math.ceil(radius * 2.5f);
        if (shadowNode == null) shadowNode = new RenderNode("lg-shadow");
        int nw = (int) Math.ceil(w) + pad * 2;
        int nh = (int) Math.ceil(h) + pad * 2;
        if (nw <= 0 || nh <= 0) return;
        shadowNode.setPosition(0, 0, nw, nh);
        Canvas c = shadowNode.beginRecording();
        c.translate(pad, pad);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(Color.argb(Math.max(0, Math.min(255, alpha)), 0, 0, 0));
        android.graphics.Path path = new android.graphics.Path();
        buildSilhouette(path, w, h, radii);
        c.drawPath(path, p);
        shadowNode.endRecording();
        shadowNode.setRenderEffect(RenderEffect.createBlurEffect(radius, radius,
                Shader.TileMode.CLAMP));
        canvas.save();
        canvas.translate(x - pad, y - pad + radius * 0.35f);
        canvas.drawRenderNode(shadowNode);
        canvas.restore();
    }

    // ----------------------------------------------------------------- shapes

    /** Rounded rect with per-corner radii (squircle-ish corner handles). */
    public static void buildSilhouette(android.graphics.Path path, float w, float h, float[] r) {
        path.reset();
        float max = Math.min(w, h) / 2f;
        float tl = Math.min(r[0], max);
        float tr = Math.min(r[1], max);
        float br = Math.min(r[2], max);
        float bl = Math.min(r[3], max);
        float k = 0.55f;
        path.moveTo(tl, 0f);
        path.lineTo(w - tr, 0f);
        path.cubicTo(w - tr * (1f - k), 0f, w, tr * (1f - k), w, tr);
        path.lineTo(w, h - br);
        path.cubicTo(w, h - br * (1f - k), w - br * (1f - k), h, w - br, h);
        path.lineTo(bl, h);
        path.cubicTo(bl * (1f - k), h, 0f, h - bl * (1f - k), 0f, h - bl);
        path.lineTo(0f, tl);
        path.cubicTo(0f, tl * (1f - k), tl * (1f - k), 0f, tl, 0f);
        path.close();
    }

    public static float[] radii(float r) {
        return new float[]{r, r, r, r};
    }
}
