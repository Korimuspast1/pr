# Credits / Лицензии ассетов

## Kenney assets (CC0 1.0 Universal — общественное достояние, атрибуция не требуется)

Автор: [Kenney](https://kenney.nl) — все наборы распространяются по лицензии **CC0**.
Файлы получены через зеркало glTF-моделей Kenney: https://github.com/shorepine/kenney

### Car Kit (`assets/kenney/car/`)
- sedan.glb, sedan-sports.glb, suv.glb, suv-luxury.glb, hatchback-sports.glb,
  taxi.glb, van.glb, delivery.glb — используются как машины трафика.
- Textures/colormap.png — общая текстура-атлас для моделей набора.

### Racing Kit (`assets/kenney/racing/`)
- rail.glb — сегменты отбойника вдоль обочин.
- lightPostModern.glb — уличные фонари.
- billboard.glb — рекламные щиты вдоль дороги.

Оригинальный источник Kenney: https://kenney.nl/assets/car-kit и https://kenney.nl/assets/racing-kit

### Starter Kit: Racing (`assets/kenney/moto/`, `assets/audio/`)
Официальный репозиторий Kenney на GitHub: https://github.com/KenneyNL/Starter-Kit-Racing
(лицензия репозитория — MIT, 3D-модели и звуки внутри отдельно помечены автором как **CC0**).

- `vehicle-motorcycle.glb` + `Textures/colormap.png` — готовая, не процедурная
  модель мотоцикла (колёса, рама, бак, вилка отдельными узлами для анимации
  качения колёс и поворота руля). Используется как основная 3D-модель байка
  игрока в `scripts/motorcycle.gd`.
- `engine-motorcycle.ogg`, `skid.ogg` — звук двигателя и юза шин, подключены
  к `AudioStreamPlayer3D` на байке (громкость/питч зависят от скорости и газа).

## Сгенерированные текстуры (`assets/textures/`)

`asphalt.jpg`, `grass.jpg`, `building_facade.jpg`, `birch_bark.jpg` — созданы отдельно как AI-генерируемые бесшовные текстуры для этого проекта.

## Собственные модели и код

Наездник на мотоцикле (капсулы/сферы поверх готовой модели байка — в комплекте
Kenney фигуры человека нет), дорога, разметка, барьеры, портальные указатели,
километровые столбики, здания и деревья — процедурная геометрия, написанная с
нуля для этого проекта на GDScript.
