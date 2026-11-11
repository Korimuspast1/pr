package dev.arena.liquidglasslab;

import android.graphics.Color;

import java.util.ArrayList;
import java.util.List;

/**
 * Every tunable of the Liquid Glass optical model, in one place.
 *
 * <p>The values are the ones the docs describe as "iOS 26 look": a soft frosted
 * backdrop, a refracting bevel on the rim, a specular highlight from the top
 * left, a thin inner shadow for thickness and a hair of grain.
 *
 * <p>All lengths are expressed in dp and converted with the current display
 * density when drawn, so the material keeps its physical size on any screen.
 */
public final class GlassParams {

    /** Regular adapts to the backdrop, Clear stays transparent and needs a dim layer. */
    public enum Variant { REGULAR, CLEAR }

    public interface Listener {
        void onGlassParamsChanged(GlassParams params);
    }

    private final List<Listener> listeners = new ArrayList<>();

    // ---------------------------------------------------------------- optics
    /** Width of the refracting rim. */
    public float bevel = 14f;
    /** Base displacement at the silhouette (sign is applied in the shader). */
    public float lens = 16f;
    /** Profile exponent: 0.55 squircle-ish, 1 circular, >1 sharper rim. */
    public float lensProfile = 0.55f;
    /** Extra centroid pull, used by big surfaces (menus, sheets). */
    public float depth = 0f;
    /** RGB dispersion amount 0..1. */
    public float chroma = 0.10f;

    // ------------------------------------------------------------ backdrop
    /** Backdrop blur radius. */
    public float blur = 10f;
    /** Backdrop saturation multiplier. */
    public float saturation = 1.55f;
    /** Backdrop brightness delta (RenderEffect color matrix). */
    public float brightness = 0f;

    // --------------------------------------------------------------- surface
    /** Tint mixed into the glass; alpha is the mix amount. */
    public int tintColor = Color.argb(16, 255, 255, 255);
    /** Overall surface opacity (Clear uses ~0.6). */
    public float opacity = 1f;
    /** Dim layer strength behind Clear glass (HIG: 35 % over bright content). */
    public float clearDim = 0.35f;

    // ----------------------------------------------------------------- rim
    /** Specular intensity. */
    public float highlight = 1.0f;
    /** Specular falloff distance, dp. */
    public float highlightWidth = 1.1f;
    /** Specular exponent, higher = tighter to the light side. */
    public float highlightFalloff = 1.0f;
    /** Light direction, degrees (0 = right, 90 = down). Default: top-left. */
    public float lightAngle = 225f;
    /** Inner shadow strength, 0..1. */
    public float innerShadow = 0.32f;
    /** Outer drop shadow radius, dp. */
    public float shadowRadius = 12f;
    /** Outer drop shadow alpha, 0..1. */
    public float shadowAlpha = 0.18f;
    /** Film grain, 0..1. */
    public float grain = 1f;

    // ----------------------------------------------------------- interaction
    /** Extra lens bulge under the finger while pressed, dp. */
    public float pressBulge = 10f;
    /** Press scale (interactive glass flexes). */
    public float pressScale = 1.035f;

    // ---------------------------------------------------------- behaviour
    public Variant variant = Variant.REGULAR;
    /** Accessibility: Reduce Transparency -> frostier, nearly opaque. */
    public boolean reduceTransparency = false;
    /** Accessibility: Increase Contrast -> contrasting 1 dp outline. */
    public boolean increaseContrast = false;

    public GlassParams() {
    }

    public GlassParams copy() {
        GlassParams p = new GlassParams();
        p.bevel = bevel;
        p.lens = lens;
        p.lensProfile = lensProfile;
        p.depth = depth;
        p.chroma = chroma;
        p.blur = blur;
        p.saturation = saturation;
        p.brightness = brightness;
        p.tintColor = tintColor;
        p.opacity = opacity;
        p.clearDim = clearDim;
        p.highlight = highlight;
        p.highlightWidth = highlightWidth;
        p.highlightFalloff = highlightFalloff;
        p.lightAngle = lightAngle;
        p.innerShadow = innerShadow;
        p.shadowRadius = shadowRadius;
        p.shadowAlpha = shadowAlpha;
        p.grain = grain;
        p.pressBulge = pressBulge;
        p.pressScale = pressScale;
        p.variant = variant;
        p.reduceTransparency = reduceTransparency;
        p.increaseContrast = increaseContrast;
        return p;
    }

    public void set(GlassParams o) {
        bevel = o.bevel;
        lens = o.lens;
        lensProfile = o.lensProfile;
        depth = o.depth;
        chroma = o.chroma;
        blur = o.blur;
        saturation = o.saturation;
        brightness = o.brightness;
        tintColor = o.tintColor;
        opacity = o.opacity;
        clearDim = o.clearDim;
        highlight = o.highlight;
        highlightWidth = o.highlightWidth;
        highlightFalloff = o.highlightFalloff;
        lightAngle = o.lightAngle;
        innerShadow = o.innerShadow;
        shadowRadius = o.shadowRadius;
        shadowAlpha = o.shadowAlpha;
        grain = o.grain;
        pressBulge = o.pressBulge;
        pressScale = o.pressScale;
        variant = o.variant;
        reduceTransparency = o.reduceTransparency;
        increaseContrast = o.increaseContrast;
        notifyChanged();
    }

    /** Effective opacity after variant + accessibility are applied. */
    public float effectiveOpacity() {
        if (reduceTransparency) return 1f;
        return variant == Variant.CLEAR ? Math.min(opacity, 0.62f) : opacity;
    }

    /** Effective tint alpha after variant + accessibility are applied. */
    public float effectiveTintAlpha() {
        float a = Color.alpha(tintColor) / 255f;
        if (reduceTransparency) return Math.max(a, 0.55f);
        if (variant == Variant.CLEAR) return Math.min(a, 0.03f);
        return a;
    }

    /** Effective blur: Reduce Transparency frosts the backdrop much harder. */
    public float effectiveBlur() {
        return reduceTransparency ? blur * 2.2f + 6f : blur;
    }

    public void notifyChanged() {
        for (int i = listeners.size() - 1; i >= 0; i--) {
            listeners.get(i).onGlassParamsChanged(this);
        }
    }

    public void addListener(Listener l) {
        if (!listeners.contains(l)) listeners.add(l);
    }

    public void removeListener(Listener l) {
        listeners.remove(l);
    }
}
