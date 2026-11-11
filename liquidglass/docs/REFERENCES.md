# Liquid Glass — как это выглядит в iOS 26

Подборка референсов, по которым настраивался материал в этом репозитории, и что
именно на них видно. Все изображения ниже — внешние источники; ссылки даны рядом.

---

## 1. Пять признаков, по которым узнаётся Liquid Glass

| Признак | Что смотреть | Как воспроизведено в лаборатории |
|---|---|---|
| **Линзирование (главное)** | контент под кромкой изгибается, «заворачивается» вокруг силуэта | SDF-линза: смещение сэмпла вдоль нормали, профиль `circleMap`, `uBevel` + `uLens` |
| **Спекулярная кромка** | светлая дуга по силуэту, со стороны источника света | `pow(dot(normal, light), falloff)` по полосе у кромки + тонкий «волос» света |
| **Однородный слой** | таб-бар, тулбар, меню лежат на одной «плавающей плоскости» | все элементы рисуются в общем window-пространстве от одного backdrop |
| **Толщина** | крупные поверхности выглядят «толще»: тень глубже, рефракция сильнее | `uDepth` + `shadow` + бо́льшая `bevel` для меню/панелей |
| **Жидкость** | нажатие даёт пружину, каплю под пальцем, морф между состояниями | `pressBulge`, растяжение пилюли по скорости, «раскрытие» меню из кнопки |

---

## 2. Скриншоты-референсы (внешние)

### Таб-бар и «пилюля» выделения

<img src="https://cdn.macstories.net/img_1490-1757681717198.jpeg" width="620" alt="Liquid Glass в таб-баре Music, MacStories">

*Таб-бар Music: капсула с отдельной круглой кнопкой поиска, выделенный таб — светлый
пузырь с преломлением по краю. Источник: [macstories.net — iOS and iPadOS 26 review](https://macstories.net/stories/ios-and-ipados-26-the-macstories-review/2).*

<img src="https://i.sstatic.net/QSFr4T2n.jpg" width="620" alt="Таб-бар Music с мини-плеером">

*Тот же бар вблизи: над ним — отдельная «плавающая» капсула мини-плеера, между ними
видно, как фон (обложки) просвечивает и изгибается на кромке.
Источник: [stackoverflow — Liquid Glass bar above tab view](https://stackoverflow.com/questions/79666234/liquid-glass-bar-above-tab-view-like-the-apple-music-app).*

### Меню, которое «раскрывается» из кнопки

<img src="https://raw.githubusercontent.com/ryanashcraft/FabBar/main/Assets/large-text-mode.png" width="320" alt="Меню Liquid Glass раскрывается из кнопки">

*Меню-пузырь: выделенный пункт — отдельная светлая капсула внутри общего стекла,
само меню растёт из точки нажатия. Источник: [ryanashcraft/FabBar](https://github.com/ryanashcraft/FabBar).*

### Тулбар / навигационная панель

<img src="https://cdn.macstories.net/sat-01-jan-0001-11-18-11-1757668706722.png" width="520" alt="Поиск и кнопки в тулбаре iOS 26">

*Круглые стеклянные кнопки в тулбаре и «плавающий» поиск снизу — типичная раскладка
iOS 26. Источник: [macstories.net](https://macstories.net/stories/ios-and-ipados-26-the-macstories-review/3).*

### Тумблеры и слайдеры

<img src="https://b2c-contenthub.com/wp-content/uploads/2025/08/Liquid-Glass-2.jpg?quality=50&strip=all&w=1200" width="620" alt="Liquid Glass тумблер в Настройках">

*Тумблер: в выключенном состоянии ручка — обычный светлый кружок; при касании она
становится стеклянной каплей и растягивается. Источник: [macworld.com](https://www.macworld.com/article/2891233/this-liquid-glass-toggle-is-a-window-into-apples-broken-design-process.html).*

### Clear-вариант

<img src="https://www.cnet.com/wp-content/uploads/sites/2/new_slider.jpg?w=864" width="620" alt="Прозрачность Liquid Glass">

*Настройка прозрачности: слева «Clear» (почти прозрачно, видно контент), справа
«Tinted/Regular». Источник: [cnet.com](https://www.cnet.com/tech/services-and-software/how-to-adjust-your-iphones-liquid-glass-appearance-in-a-few-easy-steps/).*

### Физика, на которую ссылается Apple

<img src="https://blog.logrocket.com/wp-content/uploads/2025/06/8-Lensing-effect-Liquid-Glass.png" width="560" alt="Капля воды как линза">

*Капля воды как линза — метафора материала: не размытие, а преломление.
Источник: [blog.logrocket.com — adopting Liquid Glass](https://blog.logrocket.com/ux-design/adopting-liquid-glass-examples-best-practices/).*

---

## 3. Видео и официальные материалы

* **WWDC25-219 «Meet Liquid Glass»** — определение материала, lensing как главный
  признак, Regular/Clear, адаптивность. <https://developer.apple.com/videos/play/wwdc2025/219/>
  (HD: <https://devstreaming-cdn.apple.com/videos/wwdc/2025/219/4/476dabc8-f3bb-4190-8929-4ba7131c939d/downloads/wwdc2025-219_hd.mp4?dl=1>)
* **WWDC25-323 «Build a SwiftUI app with the new design»** — `.glassEffect()`,
  `GlassEffectContainer`, `.interactive()`, `.buttonStyle(.glass)`.
  <https://developer.apple.com/videos/play/wwdc2025/323/>
* **HIG › Materials** — где вообще допустим материал, Clear + затемнение 35 %.
  <https://developer.apple.com/design/human-interface-guidelines/materials>
* **Adopting Liquid Glass** — что делать с существующим приложением.
  <https://developer.apple.com/documentation/TechnologyOverviews/adopting-liquid-glass>
* **Applying Liquid Glass to custom views** — API и правила.
  <https://developer.apple.com/documentation/SwiftUI/Applying-Liquid-Glass-to-custom-views>
* **Физика преломления в браузере** (CSS + SVG displacement map) — лучшее пошаговое
  объяснение того, как считается смещение: <https://kube.io/blog/liquid-glass-css-svg/>
* **Конспект WWDC-сессии**: <https://wwdcnotes.com/documentation/wwdc25-219-meet-liquid-glass/>

---

## 4. Как это уже повторили на Android (что мы взяли)

| Проект | Что взято |
|---|---|
| [Kyant0/AndroidLiquidGlass](https://github.com/Kyant0/AndroidLiquidGlass) | архитектура «backdrop один раз → RenderEffect на элемент», шейдеры `RoundedRectRefraction` / `WithDispersion`, `circleMap`, параметры `refractionHeight/Amount`, `Highlight.Default` (угол 45°, falloff 1), `Highlight.Ambient`, `Shadow(radius 24, alpha 0.1)`, `InnerShadow(radius 24, alpha 0.15)`, поведение таба (пилюля 56 dp, скейл 78/56 при нажатии) |
| [Abdullajon1881/LiquidGlass](https://github.com/Abdullajon1881/LiquidGlass) | smooth-min объединение силуэтов, «дрожание» под пальцем, RGB-split, dither |
| [QWEA0/Liquid-Glass-Android](https://github.com/QWEA0/Liquid-Glass-Android) | практические числа: `refractionHeight 160 px`, `bevelWidth 48 px`, `falloff 2`, `dispersion 0.10` (>0.25 — уже «радуга»), адаптивный тинт по яркости фона |
| [shufajiaok/LiquidGlass](https://github.com/shufajiaok/LiquidGlass) | компактные значения для мелких контролов: blur 6 dp, edge 1.2 dp, transparency 0.38 |
| [sitong-zhang/apple-liquid-glass-guide](https://github.com/sitong-zhang/apple-liquid-glass-guide) | таблица «рецептов» материалов и разбор архитектуры backdrop |
| [kube.io](https://kube.io/blog/liquid-glass-css-svg/) | профиль линзы (выпуклый squircle), нормализация displacement, направление света −60° |

---

## 5. Что важно НЕ делать (частые ошибки портов)

1. **Размытие вместо преломления.** Если под стеклом нет изгиба содержимого — это
   просто frosted glass, а не Liquid Glass.
2. **Стекло на стекле.** Материал не может сэмплировать другой материал; нужен либо
   общий backdrop, либо `GlassEffectContainer`-подобная общая область сэмплирования.
3. **Слишком сильная дисперсия.** Больше ~0.25–0.3 по амплитуде — и кромка становится
   радужной, как мыльный пузырь.
4. **Liquid Glass в слое контента.** Материал — только для функционального слоя
   (навигация, контролы, меню). Исключение — активированный слайдер/тумблер.
5. **Смешивание Regular и Clear** в одном слое: у Clear нет адаптации, он требует
   затемнения и яркого контента сверху.
