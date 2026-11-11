package dev.arena.liquidglasslab;

import android.app.Activity;
import android.graphics.Color;
import android.graphics.Typeface;
import android.os.Bundle;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

/**
 * In-app documentation: the short version of docs/REFERENCES.md and
 * docs/HOW-TO-REPLICATE.md, so the research ships together with the lab.
 */
public class DocActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        boolean dark = true;
        int bg = dark ? 0xFF0E0F13 : 0xFFFFFFFF;
        int fg = dark ? 0xFFEDEFF4 : 0xFF14161B;
        int dim = dark ? 0xFF9AA0AA : 0xFF5B6069;

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(bg);
        LinearLayout column = new LinearLayout(this);
        column.setOrientation(LinearLayout.VERTICAL);
        int pad = dp(20);
        column.setPadding(pad, pad, pad, pad * 2);
        scroll.addView(column);

        add(column, "Liquid Glass Lab", 26, fg, true, 0);
        add(column, "Как повторить материал iOS 26 один в один", 14, dim, false, 4);

        section(column, "1. Что это за материал", fg, dim,
                "Liquid Glass — не «размытие с прозрачностью», а цифровой мета-материал, "
                        + "который преломляет свет. Главный определитель — линзирование (lensing): "
                        + "свет изгибается, собирается и перемещается по кромке. Размытие — лишь "
                        + "подготовка фона. Материал живёт в отдельном функциональном слое над "
                        + "контентом: панели, меню, таб-бары, контролы.");

        section(column, "2. Оптическая модель (что рисуем)", fg, dim,
                "1) Силуэт: суперэллипс/скруглённый прямоугольник, концентричный углам экрана.\n"
                        + "2) Фон: backdrop захватывается один раз и проходит blur + насыщение.\n"
                        + "3) Линза: смещение сэмпла вдоль нормали SDF, профиль circleMap, "
                        + "амплитуда падает от кромки внутрь (bevel).\n"
                        + "4) Дисперсия: три сэмпла R/G/B с разным смещением.\n"
                        + "5) Блик: pow(dot(нормаль, свет), falloff) по полосе у кромки.\n"
                        + "6) Внутренняя тень: толщина материала, сильнее снизу.\n"
                        + "7) Зерно: 1–2 % шума, убирает бандинг.");

        section(column, "3. Параметры (дефолт iOS 26)", fg, dim,
                "bevel ≈ 14 dp · lens ≈ 16 dp · lensProfile 0.55 · chroma 0.10 · "
                        + "blur ≈ 10 dp · saturation 1.55 · highlight 1.0 · highlightWidth ≈ 1.1 dp · "
                        + "innerShadow 0.30 · shadow 12 dp @ 18 % · tint white @ 6 %.\n"
                        + "Крупные поверхности (меню, шторки): bevel 22–26 dp, lens 24–34 dp, "
                        + "depth 0.8 — «толще» стекло, сильнее тень и рефракция.");

        section(column, "4. Regular vs Clear", fg, dim,
                "Regular — адаптивный: тень/тинт/светлота подстраиваются под фон, годится везде. "
                        + "Clear — постоянно прозрачнее (opacity ≈ 0.62), без адаптации, требует "
                        + "затемняющего слоя (35 % чёрного по HIG) и жирного яркого контента сверху. "
                        + "Смешивать Regular и Clear в одном слое нельзя.");

        section(column, "5. Адаптивность и доступность", fg, dim,
                "Средняя яркость фона (5×5 thumbnail backdrop'а) переключает светлый/тёмный "
                        + "вариант глифов и контента. Reduce Transparency — сильнее blur, тинт до 55 %, "
                        + "блик слабее. Increase Contrast — контрастный hairline по силуэту. "
                        + "Reduce Motion — без пружин, короткие fade.");

        section(column, "6. Анимация", fg, dim,
                "Появление: не fade, а «материализация» — амплитуда линзы и блик растут от 0. "
                        + "Нажатие: масштаб +1–3 %, капля-бурление под пальцем (pressBulge ≈ 10 dp), "
                        + "блик ярче. Таб-бар: пилюля выделения скользит с пружиной и растягивается "
                        + "по скорости. Меню «раскрывается» из кнопки.");

        section(column, "7. Как это сделано в приложении", fg, dim,
                "Scene рисует фон в RenderNode. GlassRender.Backdrop применяет "
                        + "RenderEffect.createBlurEffect + ColorMatrix (насыщение/яркость). "
                        + "Каждый элемент — свой RenderNode, содержимое = сдвинутая копия backdrop'а, "
                        + "RenderEffect.createRuntimeShaderEffect(Shaders.LIQUID_GLASS, \"content\"). "
                        + "Шейдер — AGSL, тот же файл, что и оффлайн-рендер в tools/skialab.py. "
                        + "Требуется API 33+ (RuntimeShader); на 31–32 — fallback «морозное стекло».");

        section(column, "8. Референсы", fg, dim,
                "WWDC25-219 «Meet Liquid Glass» · HIG › Materials · WWDC25-323 «Build a SwiftUI app "
                        + "with the new design» · Apple Docs «Applying Liquid Glass to custom views» · "
                        + "Kyant0/AndroidLiquidGlass (AGSL-движок) · Abdullajon1881/LiquidGlass · "
                        + "QWEA0/Liquid-Glass-Android · kube.io/blog/liquid-glass-css-svg (физика). "
                        + "Полные ссылки и разбор — в docs/REFERENCES.md в репозитории.");

        setContentView(scroll);
    }

    private void section(LinearLayout parent, String title, int fg, int dim, String body) {
        add(parent, title, 16, fg, true, 22);
        add(parent, body, 13, dim, false, 6);
    }

    private void add(LinearLayout parent, String text, int sp, int color, boolean bold, int top) {
        TextView tv = new TextView(this);
        tv.setText(text);
        tv.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        tv.setTextColor(color);
        tv.setLineSpacing(dp(4), 1f);
        if (bold) tv.setTypeface(Typeface.create("sans-serif-medium", Typeface.NORMAL));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(top);
        tv.setLayoutParams(lp);
        tv.setGravity(Gravity.START);
        parent.addView(tv);
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
