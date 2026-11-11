package dev.arena.liquidglasslab;

import android.graphics.Color;

/**
 * A preset is a named snapshot of {@link GlassParams}, tuned to match a look that
 * Apple ships or that the reference Android port documents. Presets are applied as a
 * whole, so "reset" is just re-applying the first one.
 */
public final class GlassPreset {

    public final String id;
    public final String title;
    public final String subtitle;
    public final int accent;
    public final GlassParams params;

    private GlassPreset(String id, String title, String subtitle, int accent, GlassParams params) {
        this.id = id;
        this.title = title;
        this.subtitle = subtitle;
        this.accent = accent;
        this.params = params;
    }

    private static GlassParams base() {
        GlassParams p = new GlassParams();
        p.bevel = 18f;
        p.lens = 22f;
        p.falloff = 5f;
        p.chroma = 0.10f;
        p.blur = 8f;
        p.saturation = 1.5f;
        p.highlight = 1f;
        p.highlightWidth = 6f;
        p.shadowRadius = 10f;
        p.shadowAlpha = 0.18f;
        p.adaptiveTint = true;
        return p;
    }

    public static java.util.List<GlassPreset> all() {
        java.util.ArrayList<GlassPreset> list = new java.util.ArrayList<>();

        GlassParams ios = base();
        list.add(new GlassPreset("ios26", "iOS 26 · Regular",
                "Базовый материал: линза 22 dp, дисперсия 0.10, адаптивный тинт",
                0xFF0A84FF, ios));

        GlassParams thick = base();
        thick.bevel = 28f;
        thick.lens = 34f;
        thick.falloff = 3.2f;
        thick.blur = 12f;
        thick.chroma = 0.16f;
        thick.shadowRadius = 18f;
        thick.shadowAlpha = 0.26f;
        thick.highlightWidth = 9f;
        list.add(new GlassPreset("thick", "Thick slab",
                "Крупные поверхности: меню, шторки — толще стекло, глубже тень",
                0xFF9B59FF, thick));

        GlassParams clear = base();
        clear.variant = GlassParams.Variant.CLEAR;
        clear.adaptiveTint = false;
        clear.blur = 3f;
        clear.highlight = 1.3f;
        clear.clearDim = 0.30f;
        list.add(new GlassPreset("clear", "Clear",
                "Без адаптации, почти прозрачное + слой затемнения 35 %",
                0xFF32D74B, clear));

        GlassParams frosted = base();
        frosted.blur = 24f;
        frosted.saturation = 1.2f;
        frosted.bevel = 10f;
        frosted.lens = 10f;
        frosted.falloff = 2f;
        frosted.reduceTransparency = true;
        list.add(new GlassPreset("frosted", "Frosted (Reduce Transparency)",
                "Режим доступности: сильнее морозит фон, почти непрозрачно",
                0xFF8E8E93, frosted));

        GlassParams lensy = base();
        lensy.bevel = 30f;
        lensy.lens = 46f;
        lensy.falloff = 6.5f;
        lensy.chroma = 0.24f;
        lensy.highlight = 1.2f;
        lensy.highlightWidth = 7f;
        list.add(new GlassPreset("lens", "Lens / physical",
                "Максимальная рефракция и дисперсия — «как лупа»",
                0xFFFF9F0A, lensy));

        GlassParams minimal = base();
        minimal.bevel = 8f;
        minimal.lens = 4f;
        minimal.falloff = 1f;
        minimal.chroma = 0f;
        minimal.highlight = 0.6f;
        minimal.highlightWidth = 3f;
        minimal.adaptiveTint = false;
        minimal.tintColor = Color.argb(20, 255, 255, 255);
        list.add(new GlassPreset("minimal", "Minimal glass",
                "Только подсветка кромки, почти без линзы",
                0xFF64D2FF, minimal));

        return list;
    }

    public static GlassPreset byId(String id) {
        for (GlassPreset p : all()) if (p.id.equals(id)) return p;
        return all().get(0);
    }
}
