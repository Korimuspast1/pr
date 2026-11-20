package lav.bottleflip.alav;

/**
 * Модели сцены. Бутылка — тело вращения по профилю PET-бутылки
 * (дно, тело, плечо, горлышко, крышка). Мебель собирается из боксов
 * и латхей. Все модели в единичной ширине X — масштабируются при отрисовке.
 */
final class Models {

    // палитра
    static final float[] BOTTLE = {0.45f, 0.78f, 0.95f};
    static final float[] BOTTLE_DARK = {0.30f, 0.62f, 0.85f};
    static final float[] CAP = {0.95f, 0.32f, 0.30f};
    static final float[] WATER = {0.25f, 0.60f, 0.90f};
    static final float[] WOOD = {0.72f, 0.55f, 0.38f};
    static final float[] WOOD_DARK = {0.55f, 0.40f, 0.27f};
    static final float[] WHITE = {0.93f, 0.93f, 0.95f};
    static final float[] DARK = {0.16f, 0.17f, 0.20f};
    static final float[] SCREEN = {0.08f, 0.10f, 0.14f};
    static final float[] GREEN = {0.35f, 0.70f, 0.35f};
    static final float[] LEAF = {0.25f, 0.62f, 0.30f};
    static final float[] POT = {0.80f, 0.42f, 0.30f};
    static final float[] YELLOW = {0.95f, 0.80f, 0.30f};
    static final float[] BLUE = {0.30f, 0.50f, 0.90f};
    static final float[] RED = {0.85f, 0.30f, 0.30f};
    static final float[] FLOOR = {0.85f, 0.76f, 0.62f};
    static final float[] WALL = {0.88f, 0.90f, 0.94f};
    static final float[] SHADOW = {0.05f, 0.05f, 0.08f};

    private Models() {
    }

    /** PET-бутылка: профиль снизу вверх, высота ~1.1, радиус ~0.3. */
    static Mesh bottle() {
        float[][] profile = {
                {0.02f, 0.00f}, {0.20f, 0.005f}, {0.28f, 0.02f},
                {0.30f, 0.06f}, {0.30f, 0.14f}, {0.295f, 0.20f},
                {0.30f, 0.26f}, {0.295f, 0.32f}, {0.30f, 0.38f},
                {0.295f, 0.46f}, {0.28f, 0.54f}, {0.24f, 0.60f},
                {0.17f, 0.66f}, {0.13f, 0.72f}, {0.125f, 0.80f},
                {0.125f, 0.86f}, {0.14f, 0.87f}, {0.14f, 0.98f},
                {0.145f, 1.00f}, {0.12f, 1.02f}, {0.02f, 1.03f},
        };
        float[][] colors = new float[profile.length][];
        for (int i = 0; i < profile.length; i++) {
            float y = profile[i][1];
            if (y >= 0.87f) {
                colors[i] = CAP;
            } else if (y <= 0.30f) {
                colors[i] = BOTTLE_DARK; // «вода» внизу
            } else {
                colors[i] = BOTTLE;
            }
        }
        return new MeshBuilder().lathe(profile, colors, 24, 0, 0, 0).build();
    }

    /** Тень-диск. */
    static Mesh shadow() {
        return new MeshBuilder().disc(0, 0, 0, 0.34f, SHADOW, 24).build();
    }

    static Mesh floor() {
        return new MeshBuilder()
                .box(0, -0.5f, 0, 400, 1f, 24, FLOOR)
                .build();
    }

    static Mesh wall() {
        return new MeshBuilder()
                .box(0, 6f, -5.5f, 400, 14, 0.5f, WALL)
                .build();
    }

    // ── Мебель: начало координат = центр верха посадочной поверхности ──
    // Каждая модель рисуется «вниз» от точки приземления; ширина = 1,
    // масштаб подбирается платформой. Возвращает {mesh, topOffset}.

    static final int TABLE = 0;
    static final int SHELF = 1;
    static final int TV = 2;
    static final int LAMP = 3;
    static final int BOOKS = 4;
    static final int COUCH = 5;
    static final int PLANT = 6;
    static final int FRIDGE = 7;
    static final int TYPE_COUNT = 8;

    /** Естественная высота мебели (нижняя грань под верхом). */
    static float depthOf(int type) {
        switch (type) {
            case TABLE: return 0.78f;
            case SHELF: return 0.10f;
            case TV: return 1.05f;
            case LAMP: return 1.42f;
            case BOOKS: return 0.55f;
            case COUCH: return 0.62f;
            case PLANT: return 1.15f;
            case FRIDGE: return 1.65f;
            default: return 0.8f;
        }
    }

    static Mesh furniture(int type) {
        MeshBuilder b = new MeshBuilder();
        switch (type) {
            case TABLE: {
                b.box(0, -0.045f, 0, 1.0f, 0.09f, 0.95f, WOOD);
                float lx = 0.42f, lz = 0.38f;
                for (int sx = -1; sx <= 1; sx += 2) {
                    for (int sz = -1; sz <= 1; sz += 2) {
                        b.box(sx * lx, -0.42f, sz * lz, 0.08f, 0.78f, 0.08f,
                                WOOD_DARK);
                    }
                }
                break;
            }
            case SHELF: {
                b.box(0, -0.05f, 0, 1.0f, 0.10f, 0.85f, WHITE);
                b.box(-0.42f, -0.16f, -0.30f, 0.06f, 0.24f, 0.20f, DARK);
                b.box(0.42f, -0.16f, -0.30f, 0.06f, 0.24f, 0.20f, DARK);
                break;
            }
            case TV: {
                b.box(0, -0.72f, 0, 1.0f, 0.60f, 0.66f, WOOD_DARK);
                b.box(0, -0.24f, 0, 0.92f, 0.36f, 0.07f, DARK);
                b.box(0, -0.24f, 0.045f, 0.84f, 0.28f, 0.02f, SCREEN);
                b.box(0, -0.44f, 0, 0.16f, 0.05f, 0.20f, DARK);
                break;
            }
            case LAMP: {
                float[][] pole = {{0.20f, -1.38f}, {0.20f, -1.34f},
                        {0.045f, -1.32f}, {0.045f, -0.42f}};
                float[][] poleCol = {DARK, DARK, DARK, DARK};
                b.lathe(pole, poleCol, 14, 0, 0, 0);
                float[][] shade = {{0.34f, -0.42f}, {0.30f, -0.36f},
                        {0.22f, -0.10f}, {0.20f, -0.04f}, {0.02f, -0.02f}};
                float[][] shadeCol = {YELLOW, YELLOW, YELLOW, YELLOW, YELLOW};
                b.lathe(shade, shadeCol, 18, 0, 0, 0);
                break;
            }
            case BOOKS: {
                b.box(0, -0.07f, 0, 1.0f, 0.14f, 0.70f, RED);
                b.box(0.03f, -0.22f, 0, 0.92f, 0.16f, 0.78f, BLUE);
                b.box(-0.04f, -0.40f, 0, 0.96f, 0.20f, 0.82f, GREEN);
                break;
            }
            case COUCH: {
                b.box(0, -0.16f, 0, 1.0f, 0.32f, 0.85f, BLUE);
                b.box(0, -0.34f, -0.36f, 1.0f, 0.56f, 0.16f, BLUE);
                b.box(-0.46f, -0.24f, 0, 0.10f, 0.44f, 0.85f, WOOD_DARK);
                b.box(0.46f, -0.24f, 0, 0.10f, 0.48f, 0.85f, WOOD_DARK);
                break;
            }
            case PLANT: {
                float[][] pot = {{0.20f, -0.42f}, {0.26f, -0.40f},
                        {0.30f, -0.10f}, {0.30f, -0.06f}};
                float[][] potCol = {POT, POT, POT, POT};
                b.lathe(pot, potCol, 14, 0, 0, 0);
                float[][] ball = new float[9][2];
                for (int i = 0; i < 9; i++) {
                    double t = Math.PI * i / 8.0;
                    ball[i][0] = 0.36f * (float) Math.sin(t);
                    ball[i][1] = -0.06f - 0.34f + 0.36f * (float) -Math.cos(t)
                            + 0.34f;
                }
                // сфера листвы: центр на -0.40, верх на ~0.0
                for (int i = 0; i < 9; i++) {
                    double t = Math.PI * i / 8.0;
                    ball[i][0] = 0.36f * (float) Math.sin(t);
                    ball[i][1] = -0.36f + 0.36f * (float) (1 - Math.cos(t)) - 0.06f;
                }
                float[][] ballCol = new float[9][];
                for (int i = 0; i < 9; i++) {
                    ballCol[i] = LEAF;
                }
                b.lathe(ball, ballCol, 16, 0, 0, 0);
                break;
            }
            case FRIDGE: {
                b.box(0, -0.825f, 0, 1.0f, 1.65f, 0.80f, WHITE);
                b.box(0, -0.95f, 0.41f, 0.92f, 0.03f, 0.02f, DARK);
                b.box(-0.40f, -1.20f, 0.42f, 0.05f, 0.35f, 0.04f, DARK);
                break;
            }
            default:
                b.box(0, -0.05f, 0, 1.0f, 0.1f, 0.9f, WOOD);
        }
        return b.build();
    }
}
