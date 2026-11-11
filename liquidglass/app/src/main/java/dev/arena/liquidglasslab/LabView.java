package dev.arena.liquidglasslab;

import android.content.Context;
import android.content.Intent;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RectF;
import android.graphics.Typeface;
import android.text.TextPaint;
import android.view.MotionEvent;
import android.view.View;
import android.widget.FrameLayout;

import java.util.List;

/**
 * The playground. A single custom view draws the scene plus all floating glass chrome
 * on the same canvas (so every element can sample the same backdrop), and a
 * {@link FrameLayout} on top hosts the interactive widgets (they are real Views with
 * their own touch handling and their own RenderNode).
 *
 * <p>Tabs:
 * <ul>
 *   <li>Главная — buttons, toolbar, menu, sliders, switches;</li>
 *   <li>Пресеты — material presets;</li>
 *   <li>Сцена — backdrops and variants (Regular/Clear, accessibility);</li>
 *   <li>Тюнинг — live parameter sheet;</li>
 *   <li>?</li> — in-app docs.
 * </ul>
 */
public final class LabView extends FrameLayout {

    private final Scene scene = new Scene();
    private final GlassParams params = new GlassParams();
    private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint text = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Path path = new Path();

    private final GlassRender.Backdrop backdrop = new GlassRender.Backdrop();

    private GlassToolbar toolbar;
    private GlassTabBar tabBar;
    private GlassButton primaryButton;
    private GlassButton secondaryButton;
    private GlassPopupMenu menu;
    private GlassSlider slider;
    private GlassToggle toggle;
    private GlassSlider tuneSlider;
    private final GlassSheetControl[] controls = new GlassSheetControl[10];
    private final View[] tabPages = new View[4];

    private int tab = 0;
    private boolean menuOpen = false;
    private boolean dirty = true;
    private boolean controlsVisible = true;
    private final List<GlassPreset> presets = GlassPreset.all();
    private String lastAction = "Готово";

    public LabView(Context context) {
        super(context);
        setWillNotDraw(false);
        GlassRender.setDensity(getResources().getDisplayMetrics().density);

        toolbar = new GlassToolbar(context, null, backdrop, params);
        tabBar = new GlassTabBar(context, null, backdrop, params);
        primaryButton = new GlassButton(context, null, backdrop, params);
        secondaryButton = new GlassButton(context, null, backdrop, params);
        menu = new GlassPopupMenu(context, null, backdrop, params);
        slider = new GlassSlider(context, null, backdrop, params);
        toggle = new GlassToggle(context, null, backdrop, params);
        tuneSlider = new GlassSlider(context, null, backdrop, params);

        primaryButton.label("Продолжить").glyph(GlassButton.Glyph.play);
        secondaryButton.label("Меню").glyph(GlassButton.Glyph.sliders);
        slider.bind("Прозрачность", 0.4f, 0xFF0A84FF, value -> {
            lastAction = "Слайдер: " + Math.round(value * 100) + " %";
            dirty = true;
        });
        toggle.bind(true, on -> {
            lastAction = "Переключатель: " + (on ? "вкл" : "выкл");
            dirty = true;
        });
        tuneSlider.bind("Сила линзы", 0.5f, 0xFF9B59FF,
                value -> {
                    params.lens = 2f + value * 40f;
                    params.notifyChanged();
                    dirty = true;
                });

        toolbar.bind("Liquid Glass", "лаборатория материала", new GlassToolbar.Actions() {
            @Override
            public void onBack() {
                lastAction = "Назад";
                dirty = true;
            }

            @Override
            public void onTrailing(int index) {
                if (index == 0) {
                    setMenuOpen(!menuOpen);
                } else {
                    openDocs();
                }
            }
        });

        secondaryButton.setOnClickListener(v -> setMenuOpen(!menuOpen));
        primaryButton.setOnClickListener(v -> {
            lastAction = "Кнопка нажата";
            dirty = true;
        });

        menu.setVisibility(GONE);
        menu.setListener(new GlassPopupMenu.Listener() {
            @Override
            public void onItem(int index) {
                switch (index) {
                    case 0:
                        params.variant = params.variant == GlassParams.Variant.REGULAR
                                ? GlassParams.Variant.CLEAR : GlassParams.Variant.REGULAR;
                        lastAction = "Вариант: " + params.variant;
                        params.notifyChanged();
                        break;
                    case 1:
                        selectTab(1);
                        break;
                    case 2:
                        selectTab(3);
                        break;
                    case 3:
                        nextScene();
                        break;
                    default:
                        controlsVisible = !controlsVisible;
                        lastAction = controlsVisible ? "Панель показана" : "Панель скрыта";
                        break;
                }
                setMenuOpen(false);
                dirty = true;
            }

            @Override
            public void onDismissed() {
                setMenuOpen(false);
            }
        });

        tabBar.setListener(this::selectTab);

        buildControls(context);

        addView(toolbar);
        addView(primaryButton);
        addView(secondaryButton);
        addView(slider);
        addView(toggle);
        addView(tuneSlider);
        addView(menu);
        addView(tabBar);
        for (GlassSheetControl c : controls) if (c != null) addView(c);

        setBg();
    }

    // ---------------------------------------------------------------- layout

    @Override
    protected void onSizeChanged(int w, int h, int ow, int oh) {
        super.onSizeChanged(w, h, ow, oh);
        backdrop.setSize(w, h);
        dirty = true;
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        int w = r - l;
        int h = b - t;
        float d = getResources().getDisplayMetrics().density;
        int pad = Math.round(16 * d);
        int toolbarH = Math.round(60 * d);
        int barH = Math.round(64 * d);

        // respect the status bar / gesture bar without pulling in AndroidX
        int topInset = 0;
        int bottomInset = 0;
        android.view.WindowInsets insets = getRootWindowInsets();
        if (insets != null) {
            topInset = insets.getInsets(android.view.WindowInsets.Type.statusBars()).top;
            bottomInset = insets.getInsets(android.view.WindowInsets.Type.navigationBars()).bottom;
        }
        int toolbarTop = topInset + Math.round(10 * d);
        toolbar.layout(pad, toolbarTop, w - pad, toolbarTop + toolbarH);

        int menuW = Math.round(280 * d);
        int menuH = Math.round(250 * d);
        int menuTop = toolbarTop + toolbarH + Math.round(10 * d);
        menu.layout(pad, menuTop, pad + menuW, menuTop + menuH);

        int btnH = Math.round(48 * d);
        int btnW = Math.round(170 * d);
        int sy = h - bottomInset - barH - Math.round(40 * d) - btnH * 2 - Math.round(16 * d);
        primaryButton.layout(w / 2 - btnW / 2, sy, w / 2 + btnW / 2, sy + btnH);
        secondaryButton.layout(w / 2 - btnW / 2, sy + btnH + Math.round(14 * d),
                w / 2 + btnW / 2, sy + btnH * 2 + Math.round(14 * d));

        int sliderH = Math.round(56 * d);
        int sliderY = sy - sliderH - Math.round(20 * d);
        slider.layout(pad, sliderY, w - pad, sliderY + sliderH);

        int tuneH = Math.round(56 * d);
        tuneSlider.layout(pad, sliderY - tuneH - Math.round(12 * d),
                w - pad, sliderY - Math.round(12 * d));

        int toggleW = Math.round(56 * d);
        int toggleH = Math.round(34 * d);
        int toggleY = sliderY - tuneH - Math.round(30 * d) - toggleH;
        toggle.layout(w - pad - toggleW, toggleY, w - pad, toggleY + toggleH);

        int barBottom = h - bottomInset - Math.round(12 * d);
        tabBar.layout(pad, barBottom - barH, w - pad, barBottom);

        // tuning sheet controls (left column, only on the tuning tab)
        int cy = Math.round(160 * d);
        int cw = (w - pad * 3) / 2;
        int ch = Math.round(58 * d);
        for (int i = 0; i < controls.length; i++) {
            GlassSheetControl c = controls[i];
            if (c == null) continue;
            int col = i % 2;
            int row = i / 2;
            int x = pad + col * (cw + pad);
            int y = cy + row * (ch + Math.round(10 * d));
            c.layout(x, y, x + cw, y + ch);
        }

        menu.bringToFront();
        tabBar.bringToFront();
    }

    private void buildControls(Context context) {
        String[] labels = {"bevel, dp", "lens, dp", "chroma", "blur, dp", "saturation",
                "highlight", "inner shadow", "grain", "opacity", "press bulge"};
        float[] max = {40f, 40f, 1f, 30f, 3f, 3f, 1f, 1f, 1f, 24f};
        for (int i = 0; i < labels.length; i++) {
            final int index = i;
            GlassSheetControl c = new GlassSheetControl(context, null, backdrop, params);
            float current = norm(i);
            c.bind(labels[i], 0f, max[i], false, current, 0xFF0A84FF,
                    (control, value) -> {
                        apply(index, value);
                        dirty = true;
                    });
            controls[i] = c;
        }
    }

    private float norm(int index) {
        switch (index) {
            case 0: return params.bevel / 40f;
            case 1: return params.lens / 40f;
            case 2: return params.chroma;
            case 3: return params.blur / 30f;
            case 4: return params.saturation / 3f;
            case 5: return params.highlight / 3f;
            case 6: return params.innerShadow;
            case 7: return params.grain;
            case 8: return params.opacity;
            default: return params.pressBulge / 24f;
        }
    }

    private void apply(int index, float v) {
        switch (index) {
            case 0: params.bevel = v * 40f; break;
            case 1: params.lens = v * 40f; break;
            case 2: params.chroma = v; break;
            case 3: params.blur = v * 30f; break;
            case 4: params.saturation = v * 3f; break;
            case 5: params.highlight = v * 3f; break;
            case 6: params.innerShadow = v; break;
            case 7: params.grain = v; break;
            case 8: params.opacity = Math.max(0.2f, v); break;
            case 9: params.pressBulge = v * 24f; break;
            default: break;
        }
        params.notifyChanged();
    }

    // ----------------------------------------------------------------- state

    private void setMenuOpen(boolean open) {
        menuOpen = open;
        menu.setOpen(open);
        menu.setVisibility(open ? VISIBLE : GONE);
        dirty = true;
        invalidate();
    }

    private void selectTab(int index) {
        tab = Math.max(0, Math.min(3, index));
        tabBar.setSelected(tab);
        applyTabVisibility();
        lastAction = "Вкладка: " + index;
        dirty = true;
        invalidate();
    }

    private void applyTabVisibility() {
        boolean tuning = tab == 3;
        for (GlassSheetControl c : controls) if (c != null) c.setVisibility(tuning ? VISIBLE : GONE);
        tuneSlider.setVisibility(tuning ? GONE : VISIBLE);
        boolean home = tab == 0;
        primaryButton.setVisibility(home ? VISIBLE : GONE);
        secondaryButton.setVisibility(home ? VISIBLE : GONE);
        slider.setVisibility(home ? VISIBLE : GONE);
        toggle.setVisibility(home ? VISIBLE : GONE);
    }

    private void nextScene() {
        Scene.Style[] styles = Scene.Style.values();
        int i = (scene.style().ordinal() + 1) % styles.length;
        scene.setStyle(styles[i]);
        lastAction = "Фон: " + styles[i].title;
        if (styles[i] == Scene.Style.PHOTO) pickPhoto();
        dirty = true;
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
        lastAction = "Фото загружено";
        dirty = true;
        invalidate();
    }

    private void openDocs() {
        getContext().startActivity(new Intent(getContext(), DocActivity.class));
    }

    public void onShown() {
        dirty = true;
        invalidate();
    }

    private void setBg() {
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        applyTabVisibility();
    }

    // ------------------------------------------------------------------ draw

    @Override
    protected void onDraw(Canvas canvas) {
        super.onDraw(canvas);
        int w = getWidth();
        int h = getHeight();
        if (w == 0 || h == 0) return;

        // 1. record scene + blurred backdrop once
        backdrop.record(canvas, (c, cw, ch) -> scene.paint(c, cw, ch), params);

        // 2. scene itself
        canvas.save();
        canvas.clipRect(0, 0, w, h);
        canvas.drawRenderNode(backdrop.scene());
        canvas.restore();

        // 3. tab-specific overlays (text cards etc. drawn on the scene layer)
        if (tab == 1) drawPresetOverlay(canvas, w, h);
        if (tab == 2) drawSceneOverlay(canvas, w, h);

        // 4. status line, drawn behind the glass chrome so it can be refracted
        float d = getResources().getDisplayMetrics().density;
        if (controlsVisible) {
            text.setColor(0xE6FFFFFF);
            text.setTextSize(12 * d);
            text.setTypeface(Typeface.create("sans-serif", Typeface.NORMAL));
            int top = getRootWindowInsets() == null ? 0
                    : getRootWindowInsets().getInsets(
                    android.view.WindowInsets.Type.statusBars()).top;
            canvas.drawText(lastAction + "  ·  " + params.variant, 20 * d,
                    top + 84 * d, text);
        }
    }

    private void drawPresetOverlay(Canvas canvas, int w, int h) {
        float d = getResources().getDisplayMetrics().density;
        text.setColor(0xFFFFFFFF);
        text.setTextSize(20 * d);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        canvas.drawText("Пресеты материала", 18 * d, 150 * d, text);
        for (int i = 0; i < presets.size(); i++) {
            GlassPreset p = presets.get(i);
            float top = 165 * d + i * 74 * d;
            RectF card = new RectF(16 * d, top, w - 16 * d, top + 64 * d);
            paint.setColor(0x1AFFFFFF);
            canvas.drawRoundRect(card, 18 * d, 18 * d, paint);
            paint.setColor(p.accent);
            canvas.drawCircle(card.left + 26 * d, card.top + 32 * d, 8 * d, paint);
            text.setTextSize(14 * d);
            text.setColor(0xFFFFFFFF);
            canvas.drawText(p.title, card.left + 44 * d, card.top + 27 * d, text);
            text.setTextSize(11 * d);
            text.setColor(0xB3FFFFFF);
            canvas.drawText(p.subtitle, card.left + 44 * d, card.top + 45 * d, text);
        }
    }

    private void drawSceneOverlay(Canvas canvas, int w, int h) {
        float d = getResources().getDisplayMetrics().density;
        text.setColor(0xFFFFFFFF);
        text.setTextSize(20 * d);
        text.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        canvas.drawText("Сцена: " + scene.style().title, 18 * d, 150 * d, text);
        text.setTextSize(12 * d);
        text.setColor(0xCCFFFFFF);
        String[] lines = {
                "Regular: адаптируется, тень/тинт/яркость",
                "Clear: opacity 0.62 + слой затемнения 35 %",
                "Reduce Transparency: blur ×2.2, тинт 55 %",
                "Increase Contrast: контрастный hairline",
        };
        for (int i = 0; i < lines.length; i++) {
            canvas.drawText(lines[i], 18 * d, (180 + i * 22) * d, text);
        }
    }

    // --------------------------------------------------------------- touches

    @Override
    public boolean onTouchEvent(MotionEvent event) {
        if (event.getActionMasked() == MotionEvent.ACTION_DOWN) {
            float d = getResources().getDisplayMetrics().density;
            if (menuOpen) {
                setMenuOpen(false);
                return true;
            }
            if (tab == 1) {
                int index = (int) ((event.getY() / d - 165) / 74);
                if (index >= 0 && index < presets.size()
                        && event.getY() / d < 165 + presets.size() * 74) {
                    params.set(presets.get(index).params);
                    lastAction = "Пресет: " + presets.get(index).title;
                    resetControls();
                    invalidate();
                    return true;
                }
            }
            if (tab == 2) {
                // tapping the scene cycles backdrops, long-tap toggles accessibility
                nextScene();
                return true;
            }
        }
        return super.onTouchEvent(event);
    }

    private void resetControls() {
        for (int i = 0; i < controls.length; i++) {
            if (controls[i] != null) controls[i].setValue(norm(i));
        }
        dirty = true;
    }

    public GlassParams params() {
        return params;
    }
}
