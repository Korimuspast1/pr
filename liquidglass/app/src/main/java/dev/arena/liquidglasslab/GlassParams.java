package dev.arena.liquidglasslab;

import android.graphics.Color;

import java.util.ArrayList;
import java.util.List;

/**
 * Every tunable of the Liquid Glass optical model, in one place.
 *
 * <p>Defaults follow the reference implementation in QWEA0/Liquid-Glass-Android:
 * a refracted bevel squeezed into the outer pixels of the silhouette, per-channel
 * dispersion, two symmetric rim lobes and per-pixel adaptive tinting.
 *
 * <p>All lengths are in dp and converted with the display density when drawn.
 */
public final class GlassParams {

    /** Regular adapts to the backdrop, Clear stays transparent and needs a dim layer. */
    public enum Variant { REGULAR, CLEAR }

    public interface Listener {
        void onGlassParamsChanged(GlassParams params);
    }

    private final List<Listener> listeners = new ArrayList<>();

    // ---------------------------------------------------------------- optics
    /** Width of the refracting bevel, dp. */
    public float bevel = 18f;
    /** Displacement at the silhouette, dp. */
    public float lens = 22f;
    /** Profile: inverse-power exponent (4-8 squeezes the bend into the rim). */
    public float falloff = 5f;
    /** RGB dispersion, 0..1 (the reference caps it at 0.25 before it goes rainbow). */
    public float chroma = 0.10f;

    // ------------------------------------------------------------ backdrop
    /** Backdrop blur radius, dp. */
    public float blur = 8f;
    /** Backdrop saturation, 1 = untouched. */
    public float saturation = 1.5f;

    // --------------------------------------------------------------- surface
    /** Tint for Clear mode; alpha is the mix amount. */
    public int tintColor = Color.argb(0, 255, 255, 255);
    /** Body colour of the glass (straight alpha, a = 0 disables). */
    public int glassTint = 0;
    /** Clear-mode dim layer (HIG: 35 % over bright content). */
    public float clearDim = 0.30f;
    /** Regular mode: per-pixel adaptive tinting instead of a flat tint. */
    public boolean adaptiveTint = true;

    // ----------------------------------------------------------------- rim
    /** Specular rim intensity. */
    public float highlight = 1.0f;
    /** Cap for the glow band width, dp. */
    public float highlightWidth = 6f;
    /** Light direction, degrees (0 = right, 90 = down). Default: top-left. */
    public float lightAngle = 225f;
    /** Outer drop shadow radius, dp. */
    public float shadowRadius = 10f;
    /** Outer drop shadow alpha, 0..1. */
    public float shadowAlpha = 0.18f;

    // ----------------------------------------------------------- interaction
    /** Droplet bulge under the finger, dp. */
    public float pressBulge = 10f;
    // ---------------------------------------------------------- behaviour
    public Variant variant = Variant.REGULAR;
    /** Accessibility: Reduce Transparency -> frostier, opaque. */
    public boolean reduceTransparency = false;
    /** Accessibility: Increase Contrast -> contrasting outline. */
    public boolean increaseContrast = false;

    /**
     * True when the backdrop behind the chrome is dark, so labels inside the glass
     * must be light. Set by the lab from the current scene (Apple's material adapts
     * its content colour the same way).
     */
    public boolean darkBackdrop = true;

    public GlassParams() {
    }

    // --------------------------------------------------------------- content

    /** Primary label colour for anything drawn on top of the material. */
    public int ink() {
        return darkBackdrop ? 0xFFFFFFFF : 0xFF0B0B0F;
    }

    /** Secondary label colour (values, captions, unselected tabs). */
    public int inkSecondary() {
        return darkBackdrop ? 0xB3FFFFFF : 0x99101014;
    }

    /** Faint ink used for the glyph discs inside a toolbar. */
    public int discInk() {
        return darkBackdrop ? 0x24FFFFFF : 0x12000000;
    }

    /** System accent, used for selected states and tinted glass. */
    public int accent() {
        return 0xFF0A84FF;
    }

    // ------------------------------------------------------------------ copy

    public GlassParams copy() {
        GlassParams p = new GlassParams();
        p.setFrom(this);
        return p;
    }

    public void set(GlassParams o) {
        setFrom(o);
        notifyChanged();
    }

    private void setFrom(GlassParams o) {
        bevel = o.bevel;
        lens = o.lens;
        falloff = o.falloff;
        chroma = o.chroma;
        blur = o.blur;
        saturation = o.saturation;
        tintColor = o.tintColor;
        glassTint = o.glassTint;
        clearDim = o.clearDim;
        adaptiveTint = o.adaptiveTint;
        highlight = o.highlight;
        highlightWidth = o.highlightWidth;
        lightAngle = o.lightAngle;
        shadowRadius = o.shadowRadius;
        shadowAlpha = o.shadowAlpha;
        pressBulge = o.pressBulge;
        variant = o.variant;
        reduceTransparency = o.reduceTransparency;
        increaseContrast = o.increaseContrast;
        darkBackdrop = o.darkBackdrop;
    }

    /** Effective blur: Reduce Transparency frosts the backdrop much harder. */
    public float effectiveBlur() {
        return reduceTransparency ? blur * 2.2f + 6f : blur;
    }

    /** Clear mode dims the content behind it; Reduce Transparency pushes towards opaque. */
    public float effectiveDim() {
        if (reduceTransparency) return 0f;
        return variant == Variant.CLEAR ? clearDim : 0f;
    }

    /** Regular uses per-pixel adaptive tinting unless the user turned it off. */
    public float adaptiveTintFlag() {
        if (variant == Variant.CLEAR) return 0f;
        return adaptiveTint && !reduceTransparency ? 1f : 0f;
    }

    /** Effective saturation after accessibility. */
    public float effectiveSaturation() {
        return reduceTransparency ? Math.max(1f, saturation - 0.3f) : saturation;
    }

    /** Effective specular strength after accessibility. */
    public float effectiveHighlight() {
        return highlight * (reduceTransparency ? 0.7f : 1f)
                * (increaseContrast ? 1.35f : 1f);
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
