package dev.arena.liquidglasslab;

import android.content.Context;
import android.content.Intent;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.view.MotionEvent;
import android.view.View;
import android.widget.FrameLayout;

import java.util.ArrayList;
import java.util.List;

/**
 * The playground. A {@link FrameLayout} that records the scene into a RenderNode every
 * frame and hosts the glass chrome: toolbar, tab bar, buttons, sliders, switches, the
 * popup menu and the tuning cards.
 *
 * <p>Layout is a strict vertical flow per tab — every element is placed in its own
 * band, so nothing can overlap and every label sits inside its own glass capsule. The
 * only free-floating pieces are the toolbar (top) and the tab bar (bottom).
 */
public final class LabView extends FrameLayout {

    private static final int TAB_HOME = 0;
    private static final int TAB_PRESETS = 1;
    private static final int TAB_SCENE = 2;
    private static final int TAB_TUNE = 3;

    private final Scene scene = new Scene();
    private final GlassParams params = new GlassParams();
    private final GlassRender.Backdrop backdrop = new GlassRender.Backdrop();
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

    private final GlassToolbar toolbar;
    private final GlassTabBar tabBar;
    private final GlassPopupMenu menu;
    private final GlassButton primary;
    private final GlassButton secondary;
    private final GlassButton searchBubble;
    private final GlassSlider slider;
    private final GlassToggle toggle;

    private final List<GlassMenuItem> presetRows = new ArrayList<>();
    private final List<GlassMenuItem> sceneRows = new ArrayList<>();
    private final List<GlassSheetControl> tuningRows = new ArrayList<>();
    private final List<GlassMenuItem> allRows = new ArrayList<>();

    private final List<GlassPreset> presets = GlassPreset.all();

    private int tab = TAB_HOME;
    private boolean menuOpen;
    private boolean chromeVisible = true;
    private String status = "Тапни по стеклу";

    public LabView(Context context) {
        super(context);
        setWillNotDraw(false);
        GlassRender.setDensity(getResources().getDisplayMetrics().density);

        toolbar = new GlassToolbar(context, null, backdrop, params);
        tabBar = new GlassTabBar(context, null, backdrop, params);
        menu = new GlassPopupMenu(context, null, backdrop, params);
        primary = new GlassButton(context, null, backdrop, params);
        secondary = new GlassButton(context, null, backdrop, params);
        searchBubble = new GlassButton(context, null, backdrop, params);
        slider = new GlassSlider(context, null, backdrop, params);
        toggle = new GlassToggle(context, null, backdrop, params);

        toolbar.bind("Liquid Glass", "лаборатория материала", index -> {
            if (index == 1) {
                chromeVisible = !chromeVisible;
                status = chromeVisible ? "Хром показан" : "Хром скрыт — тапни по фону";
            } else {
                setMenuOpen(!menuOpen);
            }
            requestLayout();
            invalidate();
        });
        toolbar.bubbles(GlassButton.Glyph.tune, GlassButton.Glyph.close);

        primary.label("Продолжить").glyph(GlassButton.Glyph.play).tinted();
        primary.setOnClickListener(v -> say("Кнопка: Продолжить"));
        secondary.label("Меню").glyph(GlassButton.Glyph.sliders);
        secondary.setOnClickListener(v -> setMenuOpen(!menuOpen));
        searchBubble.glyph(GlassButton.Glyph.search);
        searchBubble.setOnClickListener(v -> say("Круглая кнопка поиска"));

        slider.bind("Прозрачность", 0.4f, value -> say("Слайдер " + Math.round(value * 100) + " %"));
        toggle.bind("Переключатель", true, on -> say("Тумблер: " + (on ? "вкл" : "выкл")));

        menu.setListener(new GlassPopupMenu.Listener() {
            @Override
            public void onItem(int index) {
                switch (index) {
                    case 0:
                        params.variant = params.variant == GlassParams.Variant.REGULAR
                                ? GlassParams.Variant.CLEAR : GlassParams.Variant.REGULAR;
                        params.notifyChanged();
                        say("Вариант: " + params.variant);
                        break;
                    case 1:
                        selectTab(TAB_PRESETS);
                        break;
                    case 2:
                        selectTab(TAB_TUNE);
                        break;
                    case 3:
                        nextScene();
                        break;
                    default:
                        chromeVisible = !chromeVisible;
                        requestLayout();
                        break;
                }
                setMenuOpen(false);
            }

            @Override
            public void onDismissed() {
                setMenuOpen(false);
            }
        });

        tabBar.setListener(this::selectTab);

        buildPresetRows(context);
        buildSceneRows(context);
        buildTuningRows(context);

        addView(toolbar);
        addView(primary);
        addView(secondary);
        addView(searchBubble);
        addView(slider);
        addView(toggle);
        addView(tabBar);
        addView(menu);
        for (GlassMenuItem r : allRows) addView(r);
        for (GlassSheetControl c : tuningRows) addView(c);

        setBackgroundColor(0xFF000000);
        applyVisibility();
    }

    // ------------------------------------------------------------------ setup

    private void buildPresetRows(Context context) {
        for (GlassPreset p : presets) {
            GlassMenuItem row = new GlassMenuItem(context, null, backdrop, params);
            row.bind(p.title, p.subtitle, GlassButton.Glyph.none, item -> {
                params.set(p.params);
                params.darkBackdrop = scene.style().dark;
                params.notifyChanged();
                syncTuningRows();
                say("Пресет: " + p.title);
            });
            row.dot(p.accent);
            presetRows.add(row);
            allRows.add(row);
        }
    }

    private void buildSceneRows(Context context) {
        for (Scene.Style style : Scene.Style.values()) {
            GlassMenuItem row = new GlassMenuItem(context, null, backdrop, params);
            row.bind(style.title, style.dark ? "тёмный фон" : "светлый фон",
                    GlassButton.Glyph.photo, item -> {
                        scene.setStyle(style);
                        params.darkBackdrop = style.dark;
                        params.notifyChanged();
                        say("Фон: " + style.title);
                        if (style == Scene.Style.PHOTO) pickPhoto();
                    });
            sceneRows.add(row);
            allRows.add(row);
        }
    }

    private void buildTuningRows(Context context) {
        String[] labels = {"bevel, dp", "lens, dp", "falloff", "chroma", "blur, dp",
                "saturation", "highlight", "rim width, dp"};
        for (int i = 0; i < labels.length; i++) {
            final int index = i;
            GlassSheetControl c = new GlassSheetControl(context, null, backdrop, params);
            c.bind(labels[i], norm(i), format(i), (control, value) -> {
                applyParameter(index, value);
                control.setValue(value, format(index));
                status = labels[index] + " = " + format(index);
                params.notifyChanged();
                invalidate();
            });
            tuningRows.add(c);
        }
    }

    private float norm(int i) {
        switch (i) {
            case 0: return (params.bevel - 4f) / 44f;
            case 1: return params.lens / 60f;
            case 2: return params.falloff / 9f;
            case 3: return params.chroma / 0.3f;
            case 4: return params.blur / 30f;
            case 5: return (params.saturation - 1f) / 1.2f;
            case 6: return (params.highlight - 0.2f) / 1.8f;
            default: return (params.highlightWidth - 2f) / 14f;
        }
    }

    private void applyParameter(int i, float v) {
        switch (i) {
            case 0: params.bevel = 4f + v * 44f; break;
            case 1: params.lens = v * 60f; break;
            case 2: params.falloff = v * 9f; break;
            case 3: params.chroma = v * 0.3f; break;
            case 4: params.blur = v * 30f; break;
            case 5: params.saturation = 1f + v * 1.2f; break;
            case 6: params.highlight = 0.2f + v * 1.8f; break;
            default: params.highlightWidth = 2f + v * 14f; break;
        }
    }

    private String format(int i) {
        switch (i) {
            case 0: return String.format(java.util.Locale.US, "%.1f dp", params.bevel);
            case 1: return String.format(java.util.Locale.US, "%.1f dp", params.lens);
            case 2: return String.format(java.util.Locale.US, "%.1f", params.falloff);
            case 3: return String.format(java.util.Locale.US, "%.2f", params.chroma);
            case 4: return String.format(java.util.Locale.US, "%.1f dp", params.blur);
            case 5: return String.format(java.util.Locale.US, "%.2f", params.saturation);
            case 6: return String.format(java.util.Locale.US, "%.2f", params.highlight);
            default: return String.format(java.util.Locale.US, "%.1f dp", params.highlightWidth);
        }
    }

    private void syncTuningRows() {
        for (int i = 0; i < tuningRows.size(); i++) {
            tuningRows.get(i).setValue(norm(i), format(i));
        }
    }

    // ---------------------------------------------------------------- layout

    @Override
    protected void onSizeChanged(int w, int h, int ow, int oh) {
        super.onSizeChanged(w, h, ow, oh);
        backdrop.setSize(w, h);
        params.darkBackdrop = scene.style().dark;
        invalidate();
    }

    private int insetTop() {
        android.view.WindowInsets insets = getRootWindowInsets();
        return insets == null ? 0
                : insets.getInsets(android.view.WindowInsets.Type.statusBars()).top;
    }

    private int insetBottom() {
        android.view.WindowInsets insets = getRootWindowInsets();
        return insets == null ? 0
                : insets.getInsets(android.view.WindowInsets.Type.navigationBars()).bottom;
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        int w = r - l;
        int h = b - t;
        float d = getResources().getDisplayMetrics().density;
        int pad = Math.round(16 * d);
        int colGap = Math.round(12 * d);
        int rowGap = Math.round(10 * d);
        int rowH = Math.round(56 * d);

        int top = insetTop() + Math.round(8 * d);
        int toolbarH = Math.round(58 * d);
        toolbar.layout(pad, top, w - pad, top + toolbarH);

        int contentTop = top + toolbarH + Math.round(14 * d);

        // ---- tab bar (bottom), menu above it, buttons in between
        int barH = Math.round(62 * d);
        int barBottom = h - insetBottom() - Math.round(10 * d);
        int barTop = barBottom - barH;
        int bubbleSize = barH;
        int gap = Math.round(10 * d);
        tabBar.layout(pad, barTop, w - pad - bubbleSize - gap, barBottom);
        searchBubble.layout(w - pad - bubbleSize, barTop, w - pad, barTop + bubbleSize);

        int contentBottom = barTop - Math.round(16 * d);

        // ---- menu appears under the toolbar, above the content
        int menuH = Math.round(46f * d) * 5;
        int menuW = Math.min(Math.round(300 * d), w - pad * 2);
        menu.layout(pad, contentTop, pad + menuW, contentTop + menuH);

        int buttonsTop = contentBottom - rowH - rowGap;
        int bw = (w - pad * 2 - colGap) / 2;
        primary.layout(pad, buttonsTop, pad + bw, buttonsTop + rowH);
        secondary.layout(pad + bw + colGap, buttonsTop, w - pad, buttonsTop + rowH);
        slider.layout(pad, buttonsTop - rowGap - rowH, w - pad, buttonsTop - rowGap);
        toggle.layout(pad, buttonsTop - rowGap * 2 - rowH * 2,
                w - pad, buttonsTop - rowGap * 2 - rowH);

        // ---- preset rows: one per band
        for (int i = 0; i < presetRows.size(); i++) {
            int y = contentTop + i * (rowH + rowGap);
            presetRows.get(i).layout(pad, y, w - pad, y + rowH);
        }

        // ---- scene rows
        for (int i = 0; i < sceneRows.size(); i++) {
            int y = contentTop + i * (rowH + rowGap);
            sceneRows.get(i).layout(pad, y, w - pad, y + rowH);
        }

        // ---- tuning grid: two columns of cards
        int cardW = (w - pad * 2 - colGap) / 2;
        int cardH = Math.round(64 * d);
        for (int i = 0; i < tuningRows.size(); i++) {
            int col = i % 2;
            int row = i / 2;
            int x = pad + col * (cardW + colGap);
            int y = contentTop + row * (cardH + rowGap);
            tuningRows.get(i).layout(x, y, x + cardW, y + cardH);
        }

        menu.bringToFront();
        tabBar.bringToFront();
        searchBubble.bringToFront();
        toolbar.bringToFront();
    }

    // ----------------------------------------------------------------- state

    private void say(String message) {
        status = message;
        invalidate();
    }

    /** Repaint the status line after any state change (cheap, one text draw). */
    private void refreshChrome() {
        invalidate();
    }

    private void selectTab(int index) {
        tab = Math.max(0, Math.min(3, index));
        tabBar.setSelected(tab);
        applyVisibility();
        if (tab == TAB_TUNE) syncTuningRows();
        requestLayout();
        invalidate();
    }

    private void applyVisibility() {
        setVisible(primary, tab == TAB_HOME);
        setVisible(secondary, tab == TAB_HOME);
        setVisible(slider, tab == TAB_HOME);
        setVisible(toggle, tab == TAB_HOME);
        for (GlassMenuItem r : presetRows) setVisible(r, tab == TAB_PRESETS);
        for (GlassMenuItem r : sceneRows) setVisible(r, tab == TAB_SCENE);
        for (GlassSheetControl c : tuningRows) setVisible(c, tab == TAB_TUNE);
        setVisible(tabBar, chromeVisible);
        setVisible(searchBubble, chromeVisible);
        setVisible(menu, menuOpen && chromeVisible);
    }

    private static void setVisible(View v, boolean visible) {
        v.setVisibility(visible ? VISIBLE : GONE);
    }

    private void setMenuOpen(boolean open) {
        menuOpen = open;
        menu.setOpen(open);
        menu.setVisibility(open && chromeVisible ? VISIBLE : GONE);
        if (open) menu.bringToFront();
        invalidate();
    }

    private void nextScene() {
        Scene.Style[] styles = Scene.Style.values();
        Scene.Style next = styles[(scene.style().ordinal() + 1) % styles.length];
        scene.setStyle(next);
        params.darkBackdrop = next.dark;
        params.notifyChanged();
        say("Фон: " + next.title);
        invalidate();
    }

    private void pickPhoto() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        intent.addCategory(Intent.CATEGORY_OPENABLE);
        intent.setType("image/*");
        if (getContext() instanceof MainActivity) {
            ((MainActivity) getContext()).startActivityForResult(intent, 42);
        }
    }

    public void setPhoto(android.graphics.Bitmap bitmap) {
        scene.setPhoto(bitmap);
        params.darkBackdrop = true;
        params.notifyChanged();
        say("Фото загружено");
        invalidate();
    }

    public GlassParams params() {
        return params;
    }

    public void onShown() {
        invalidate();
    }

    // ------------------------------------------------------------------ draw

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        int w = getWidth();
        int h = getHeight();
        if (w == 0 || h == 0) return;

        // the whole scene is recorded once per frame and sampled by every element
        backdrop.record(canvas, (c, cw, ch) -> scene.paint(c, cw, ch));
        canvas.drawRenderNode(backdrop.scene());

        // the status line lives *behind* the chrome so it can be refracted by it
        float d = getResources().getDisplayMetrics().density;
        text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
        text.setTextSize(12.5f * d);
        text.setColor(params.darkBackdrop ? 0xE6FFFFFF : 0xE60A0A0F);
        canvas.drawText(status, 20 * d, contentTopForText(), text);
        text.setTextSize(11f * d);
        text.setColor(params.darkBackdrop ? 0x99FFFFFF : 0x990A0A0F);
        canvas.drawText(GlassRender.diagnostics + " · " + params.variant + " · "
                + scene.style().title, 20 * d, contentTopForText() + 20 * d, text);
    }

    private float contentTopForText() {
        float d = getResources().getDisplayMetrics().density;
        return insetTop() + Math.round(8 * d) + Math.round(58 * d) + Math.round(34 * d);
    }

    // --------------------------------------------------------------- touches

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
            if (menuOpen) {
                setMenuOpen(false);
                return true;
            }
            if (!chromeVisible) {
                chromeVisible = true;
                applyVisibility();
                requestLayout();
                invalidate();
                return true;
            }
            if (tab == TAB_SCENE) {
                nextScene();
                return true;
            }
        }
        return super.onTouchEvent(event);
    }
}
