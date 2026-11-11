package dev.arena.liquidglasslab;

import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorMatrix;
import android.graphics.ColorMatrixColorFilter;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RenderEffect;
import android.graphics.RenderNode;
import android.graphics.RuntimeShader;
import android.graphics.Shader;
import android.os.Build;

import java.util.HashMap;
import java.util.Map;

/**
 * The glass painter: RenderNode plumbing + the optical shader.
 *
 * <p>Per frame the app
 * <ol>
 *   <li>records the whole scene (wallpaper + content behind the chrome) into
 *       {@code sceneNode};</li>
 *   <li>{@code backdropNode} draws that scene through a RenderEffect chain
 *       {@code blur -> colour matrix}, i.e. "what you can see through the glass";</li>
 *   <li>every glass element is its own RenderNode whose content is a translated copy
 *       of {@code backdropNode} and whose render effect is the runtime shader keyed on
 *       the {@code content} child. The shader bends light, paints the rim and the
 *       inner shadow, and is clipped to the element silhouette.</li>
 * </ol>
 *
 * <p>That is the same architecture Apple's effect uses (render the backdrop once,
 * re-project it per element), and the same one used by the Compose ports.
 * Requires API 33 for {@link RuntimeShader}; API 31–32 falls back to a frosted
 * surface with a rim.
 */
public final class GlassRender {

    public static final boolean SHADER_SUPPORTED = Build.VERSION.SDK_INT >= 33;
    public static final boolean EFFECT_SUPPORTED = Build.VERSION.SDK_INT >= 31;

    private static final Map<String, RuntimeShader> SHADERS = new HashMap<>();

    private static float sDensity = 1f;

    private GlassRender() {
    }

    public static void setDensity(float density) {
        sDensity = density <= 0f ? 1f : density;
    }

    public static float dp(float value) {
        return value * sDensity;
    }

    /** Compiles (and caches) a RuntimeShader by key. Returns null on API &lt; 33. */
    public static RuntimeShader shader(String key, String source) {
        if (!SHADER_SUPPORTED) return null;
        if (SHADERS.containsKey(key)) return SHADERS.get(key);
        RuntimeShader shader;
        try {
            shader = new RuntimeShader(source);
        } catch (Throwable t) {
            shader = null;
        }
        SHADERS.put(key, shader);
        return shader;
    }

    // ---------------------------------------------------------------- backdrop

    /** Scene capture + blurred content node. Reusable, no bitmap allocation. */
    public static final class Backdrop {
        private final RenderNode sceneNode = new RenderNode("lg-scene");
        private final RenderNode backdropNode = new RenderNode("lg-backdrop");
        private final ColorMatrix matrix = new ColorMatrix();
        int width;
        int height;
        private boolean recorded;

        public RenderNode scene() {
            return sceneNode;
        }

        public RenderNode blurred() {
            return backdropNode;
        }

        public int width() {
            return width;
        }

        public int height() {
            return height;
        }

        public void setSize(int w, int h) {
            width = w;
            height = h;
        }

        public void record(Canvas target, SceneSource source, GlassParams params) {
            if (width <= 0 || height <= 0) return;
            // 1. scene
            sceneNode.setPosition(0, 0, width, height);
            Canvas c = sceneNode.beginRecording();
            c.drawColor(Color.BLACK);
            source.drawScene(c, width, height);
            sceneNode.endRecording();
            recorded = true;

            // 2. backdrop = scene + blur + colour matrix
            if (!EFFECT_SUPPORTED) return;
            backdropNode.setPosition(0, 0, width, height);
            Canvas b = backdropNode.beginRecording();
            b.drawRenderNode(sceneNode);
            backdropNode.endRecording();

            matrix.setSaturation(params.saturation);
            RenderEffect effect = null;
            float blurPx = dp(params.effectiveBlur());
            if (blurPx > 0.05f) {
                effect = RenderEffect.createBlurEffect(blurPx, blurPx, Shader.TileMode.CLAMP);
            }
            if (params.brightness != 0f) {
                float off = params.brightness * 255f;
                matrix.postConcat(new ColorMatrix(new float[]{
                        1, 0, 0, 0, off,
                        0, 1, 0, 0, off,
                        0, 0, 1, 0, off,
                        0, 0, 0, 1, 0}));
            }
            // a ColorMatrixColorFilter snapshots its matrix, so it is rebuilt whenever
            // the saturation/brightness parameters change
            effect = RenderEffect.createColorFilterEffect(new ColorMatrixColorFilter(matrix), effect);
            backdropNode.setRenderEffect(effect);
        }

        public boolean isRecorded() {
            return recorded;
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
     * @param canvas    hardware canvas in window coordinates
     * @param backdrop  recorded backdrop
     * @param params    material parameters
     * @param shader    compiled optical shader (null on API &lt; 33)
     * @param node      a per-element RenderNode (reused across frames)
     * @param path      a per-element Path (reused)
     * @param x,y,w,h   element bounds, window coordinates
     * @param radii     corner radii tl, tr, br, bl in px
     * @param press     0..1 press progress
     * @param px,py     press point, element-local px
     * @param tint      per-element tint (0 = use the global tint)
     * @param shadowDp  extra shadow lift for raised elements (0 = none)
     * @param drawOrigin {x, y} of the element position in canvas space; the
     *                   element's own bounds are used for the shader
     */
    public static void drawGlass(Canvas canvas, Backdrop backdrop, GlassParams params,
                                 RuntimeShader shader, RenderNode node, Path path,
                                 float x, float y, float w, float h, float[] radii,
                                 float press, float px, float py, int tint, float shadowDp,
                                 float[] drawOrigin) {

        if (w < 1f || h < 1f) return;

        buildSilhouette(path, w, h, radii);

        // ---- outer shadow: a blurred copy of the silhouette in its own RenderNode.
        // (Paint.setShadowLayer only renders text on a hardware canvas, so shapes need this.)
        float shadowRadius = params.shadowRadius + shadowDp;
        if (shadowRadius > 0f && params.shadowAlpha > 0f && EFFECT_SUPPORTED) {
            drawShadowNode(canvas, path, x - drawOrigin[0], y - drawOrigin[1], w, h,
                    Math.max(dp(shadowRadius), 1f),
                    (int) (255 * Math.min(1f, params.shadowAlpha) * 0.55f));
        }

        // ---- Clear variant: dim layer (HIG: 35 % black over bright content)
        boolean clear = params.variant == GlassParams.Variant.CLEAR;
        if (clear && params.clearDim > 0f) {
            Paint dim = new Paint(Paint.ANTI_ALIAS_FLAG);
            dim.setColor(Color.argb((int) (255 * Math.min(1f, params.clearDim)), 0, 0, 0));
            canvas.save();
            canvas.translate(x - drawOrigin[0], y - drawOrigin[1]);
            canvas.drawPath(path, dim);
            canvas.restore();
        }

        int tintColor = tint != 0 ? tint : params.tintColor;

        canvas.save();
        canvas.translate(x - drawOrigin[0], y - drawOrigin[1]);
        canvas.clipPath(path);
        canvas.translate(x, y);

        if (SHADER_SUPPORTED && shader != null && EFFECT_SUPPORTED && backdrop.isRecorded()) {
            // content of this element = the blurred backdrop slice
            node.setPosition(0, 0, Math.round(w), Math.round(h));
            Canvas rc = node.beginRecording();
            rc.translate(-x, -y);
            rc.drawRenderNode(backdrop.blurred());
            node.endRecording();

            applyUniforms(shader, params, w, h, radii, press, px, py, tintColor);
            node.setRenderEffect(RenderEffect.createRuntimeShaderEffect(shader, "content"));
            canvas.drawRenderNode(node);
        } else {
            // fallback: frosted scrim + rim
            Paint scrim = new Paint(Paint.ANTI_ALIAS_FLAG);
            scrim.setColor(Color.argb((int) (255 * 0.55f * params.effectiveOpacity()),
                    236, 240, 248));
            canvas.drawRect(0f, 0f, w, h, scrim);
            drawFallbackRim(canvas, w, h, params);
        }

        // ---- Increase Contrast: contrasting hairline outline
        if (params.increaseContrast) {
            Paint outline = new Paint(Paint.ANTI_ALIAS_FLAG);
            outline.setStyle(Paint.Style.STROKE);
            outline.setStrokeWidth(dp(1f));
            outline.setColor(Color.argb(150, 255, 255, 255));
            canvas.drawPath(path, outline);
            outline.setStrokeWidth(dp(0.5f));
            outline.setColor(Color.argb(90, 0, 0, 0));
            canvas.drawPath(path, outline);
        }

        canvas.restore();
    }

    private static RenderNode shadowNode;

    /** Draws a blurred silhouette behind the element (see comment above). */
    private static void drawShadowNode(Canvas canvas, Path silhouette, float x, float y,
                                       float w, float h, float radius, int alpha) {
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
        c.drawPath(silhouette, p);
        shadowNode.endRecording();
        shadowNode.setRenderEffect(RenderEffect.createBlurEffect(radius, radius,
                Shader.TileMode.CLAMP));
        canvas.save();
        canvas.translate(x - pad, y - pad + radius * 0.3f);
        canvas.drawRenderNode(shadowNode);
        canvas.restore();
    }

    private static void applyUniforms(RuntimeShader s, GlassParams p, float w, float h,
                                      float[] radii, float press, float px, float py,
                                      int tint) {
        s.setFloatUniform("uOrigin", 0f, 0f);
        s.setFloatUniform("uSize", w, h);
        s.setFloatUniform("uRadii", radii[0], radii[1], radii[2], radii[3]);
        s.setFloatUniform("uBevel", Math.max(dp(p.bevel), 0.5f));
        s.setFloatUniform("uLens", -dp(p.lens));
        s.setFloatUniform("uLensProfile", p.lensProfile);
        s.setFloatUniform("uDepth", p.depth);
        s.setFloatUniform("uChroma", p.chroma);
        float a = p.effectiveTintAlpha();
        s.setFloatUniform("uTint", Color.red(tint) / 255f, Color.green(tint) / 255f,
                Color.blue(tint) / 255f, a);
        s.setFloatUniform("uHighlight", p.highlight * (p.reduceTransparency ? 0.6f : 1f));
        s.setFloatUniform("uHighlightWidth", Math.max(dp(p.highlightWidth), 0.4f));
        s.setFloatUniform("uHighlightFalloff", p.highlightFalloff);
        double angle = Math.toRadians(p.lightAngle);
        s.setFloatUniform("uLightDir", (float) Math.cos(angle), (float) Math.sin(angle));
        s.setFloatUniform("uInnerShadow", p.innerShadow);
        s.setFloatUniform("uPress", press);
        s.setFloatUniform("uPressPoint", px, py);
        s.setFloatUniform("uPressBulge", dp(p.pressBulge));
        s.setFloatUniform("uGrain", p.reduceTransparency ? 0f : p.grain);
        s.setFloatUniform("uOpacity", p.effectiveOpacity());
    }

    private static void drawFallbackRim(Canvas canvas, float w, float h, GlassParams p) {
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setStyle(Paint.Style.STROKE);
        paint.setStrokeWidth(Math.max(dp(p.highlightWidth), dp(1f)));
        paint.setColor(Color.argb(120, 255, 255, 255));
        canvas.drawRect(0.5f, 0.5f, w - 0.5f, h - 0.5f, paint);
    }

    // ------------------------------------------------------------- viewbackdrop

    /**
     * Convenience wrapper: records windows-sized scene into a {@link Backdrop},
     * so widgets only need to know how to paint the scene.
     */
    public static final class ViewBackdrop {
        public final Backdrop backdrop = new Backdrop();
        private final SceneSource source;

        public ViewBackdrop(SceneSource source) {
            this.source = source;
        }

        public void layout(int width, int height) {
            if (backdrop.width() == width && backdrop.height() == height) return;
            backdrop.width = width;
            backdrop.height = height;
        }

        public void refresh(Canvas canvas, GlassParams params) {
            backdrop.record(canvas, source, params);
        }

        public GlassRender.Backdrop get() {
            return backdrop;
        }
    }

    // ----------------------------------------------------------------- shapes

    /** Rounded rect with per-corner radii, using squircle-ish cubic handles. */
    public static void buildSilhouette(Path path, float w, float h, float[] r) {
        path.reset();
        float max = Math.min(w, h) / 2f;
        float tl = Math.min(r[0], max);
        float tr = Math.min(r[1], max);
        float br = Math.min(r[2], max);
        float bl = Math.min(r[3], max);
        // 0.42 ~ circular arc, 0.55 ~ squircle (smoother transition into the bezel)
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
