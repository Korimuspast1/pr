package dev.arena.liquidglasslab;

import android.graphics.Color;

/**
 * A preset is a named snapshot of {@link GlassParams} plus the backdrop it is
 * meant for. The presets mirror what Apple ships in iOS 26 / macOS 26 so the
 * lab can be used to compare "our" material against the real thing.
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
        p.bevel = 14f;
        p.lens = 16f;
        p.lensProfile = 0.55f;
        p.chroma = 0.10f;
        p.blur = 10f;
        p.saturation = 1.55f;
        p.highlight = 1.0f;
        p.highlightWidth = 1.1f;
        p.innerShadow = 0.30f;
        p.shadowRadius = 12f;
        p.shadowAlpha = 0.18f;
        p.tintColor = Color.argb(16, 255, 255, 255);
        p.opacity = 1f;
        return p;
    }

    public static java.util.List<GlassPreset> all() {
        java.util.ArrayList<GlassPreset> list = new java.util.ArrayList<>();

        GlassParams ios = base();
        list.add(new GlassPreset("ios26", "iOS 26 · Regular",
                "Базовый материал: мягкое размытие, линза 16 dp, блик 1 dp", 0xFF0A84FF, ios));

        GlassParams thick = base();
        thick.bevel = 22f;
        thick.lens = 24f;
        thick.depth = 0.8f;
        thick.blur = 14f;
        thick.chroma = 0.16f;
        thick.innerShadow = 0.42f;
        thick.shadowRadius = 20f;
        thick.shadowAlpha = 0.26f;
        list.add(new GlassPreset("thick", "Thick slab",
                "Крупные поверхности: меню, шторки — «толще» стекло", 0xFF9B59FF, thick));

        GlassParams clear = base();
        clear.variant = GlassParams.Variant.CLEAR;
        clear.opacity = 0.62f;
        clear.tintColor = Color.argb(8, 255, 255, 255);
        clear.highlight = 1.35f;
        clear.blur = 4f;
        list.add(new GlassPreset("clear", "Clear",
                "Без адаптации, почти прозрачное — только поверх медиа", 0xFF32D74B, clear));

        GlassParams frosted = base();
        frosted.blur = 26f;
        frosted.saturation = 1.2f;
        frosted.bevel = 10f;
        frosted.lens = 10f;
        frosted.tintColor = Color.argb(46, 255, 255, 255);
        frosted.innerShadow = 0.18f;
        list.add(new GlassPreset("frosted", "Frosted (Reduced Transparency)",
                "Режим доступности: сильнее морозит фон", 0xFF8E8E93, frosted));

        GlassParams lensy = base();
        lensy.bevel = 26f;
        lensy.lens = 34f;
        lensy.lensProfile = 1.0f;
        lensy.chroma = 0.55f;
        lensy.highlight = 1.5f;
        lensy.highlightWidth = 1.6f;
        lensy.innerShadow = 0.22f;
        list.add(new GlassPreset("lens", "Lens / physical",
                "Максимальная рефракция и дисперсия — «как лупа»", 0xFFFF9F0A, lensy));

        GlassParams flat = base();
        flat.bevel = 6f;
        flat.lens = 4f;
        flat.chroma = 0f;
        flat.highlight = 0.5f;
        flat.innerShadow = 0.1f;
        flat.grain = 0f;
        list.add(new GlassPreset("flat", "Minimal glass",
                "Только подсветка кромки, почти без линзы", 0xFF64D2FF, flat));

        return list;
    }

    public static GlassPreset byId(String id) {
        for (GlassPreset p : all()) if (p.id.equals(id)) return p;
        return all().get(0);
    }
}
