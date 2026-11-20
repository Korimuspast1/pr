package lav.bottleflip.alav;

import java.util.ArrayList;
import java.util.Iterator;
import java.util.List;
import java.util.Random;

/**
 * Игровая логика: физика бутылки и бесконечный процедурный уровень.
 * Тап = один переворот (короткий прыжок), тап в полёте = второй переворот
 * (длинный прыжок). Приземление только дном вниз и на платформу.
 */
final class Game {

    static final int EV_JUMP = 1;
    static final int EV_LAND = 2;
    static final int EV_FAIL = 3;

    interface Listener {
        void onEvent(int event);
    }

    // физика
    private static final float GRAVITY = 15.7f;
    private static final float JUMP_VY = 4.35f;
    private static final float VX = 4.0f;
    static final float FLIP_SHORT = 2.30f;  // расстояние одного переворота
    static final float FLIP_LONG = 3.45f;   // двойного
    private static final float LAND_ANGLE = 0.50f;   // допуск вертикальности
    private static final float EDGE_MARGIN = 0.16f;  // запас от края платформы

    static final int READY = 0;
    static final int FLYING = 1;
    static final int LANDED = 2;
    static final int FALLEN = 3;
    static final int OVER = 4;

    static final class Platform {
        float centerX;
        float width;
        float topY;
        int type;
        boolean moving;
        float phase;
    }

    final List<Platform> platforms = new ArrayList<>();
    final Random random = new Random();
    Listener listener;

    int state = READY;
    int score;
    int best;
    float bottleX, bottleY, bottleVy, angle, angleVel;
    private int currentIndex;
    private boolean doubleTapped;
    private float fallenTimer;
    private float time;
    private long seed;

    void reset(long newSeed) {
        seed = newSeed;
        random.setSeed(seed);
        platforms.clear();
        Platform first = new Platform();
        firstAt(first);
        platforms.add(first);
        currentIndex = 0;
        bottleX = first.centerX;
        bottleY = first.topY;
        bottleVy = 0;
        angle = 0;
        angleVel = 0;
        score = 0;
        state = READY;
        doubleTapped = false;
        fallenTimer = 0;
        time = 0;
        while (lastX() < bottleX + 30) {
            genNext();
        }
    }

    private void firstAt(Platform p) {
        p.centerX = 0;
        p.width = 1.9f;
        p.topY = 0.78f;
        p.type = Models.TABLE;
    }

    private float lastX() {
        return platforms.get(platforms.size() - 1).centerX;
    }

    /** Процедурная генерация следующей платформы. */
    private void genNext() {
        Platform prev = platforms.get(platforms.size() - 1);
        Platform p = new Platform();
        float difficulty = Math.min(1f, score / 45f);
        boolean twoFlips = random.nextFloat() < 0.28f + 0.35f * difficulty;
        float dist = (twoFlips ? FLIP_LONG : FLIP_SHORT)
                * (0.97f + random.nextFloat() * 0.06f);
        p.centerX = prev.centerX + dist;
        float natural = 1.6f;
        float scale = (1.05f - 0.30f * difficulty)
                * (0.92f + random.nextFloat() * 0.16f);
        p.width = natural * scale;
        float maxUp = twoFlips ? 1.15f : 0.50f;
        p.topY = prev.topY + (random.nextFloat() * 2f - 1f) * 0.85f;
        p.topY = Math.min(p.topY, prev.topY + maxUp);
        p.topY = Math.max(0.55f, Math.min(3.1f, p.topY));
        p.type = random.nextInt(Models.TYPE_COUNT);
        p.moving = score >= 14 && random.nextFloat() < 0.22f;
        p.phase = random.nextFloat() * 6.28f;
        platforms.add(p);
    }

    /** Верх платформы с учётом движения. */
    float platformTop(Platform p) {
        if (!p.moving) {
            return p.topY;
        }
        return p.topY + 0.22f * (float) Math.sin(time * 2.1f + p.phase);
    }

    void tap() {
        if (state == READY || state == LANDED) {
            launch();
        } else if (state == FLYING && !doubleTapped) {
            // второй переворот: новый импульс вверх, ещё одно вращение
            doubleTapped = true;
            bottleVy = JUMP_VY;
            Platform target = nextPlatform();
            float ty = target != null ? platformTop(target) : bottleY;
            float air = airtime(bottleY, ty, bottleVy);
            angleVel = (float) (-2.0 * Math.PI) / Math.max(0.2f, air);
            emit(EV_JUMP);
        }
    }

    private void launch() {
        state = FLYING;
        doubleTapped = false;
        bottleVy = JUMP_VY;
        Platform target = nextPlatform();
        float ty = target != null ? platformTop(target) : bottleY;
        float air = airtime(bottleY, ty, bottleVy);
        angleVel = (float) (-2.0 * Math.PI) / Math.max(0.2f, air);
        emit(EV_JUMP);
    }

    /** Время полёта до высоты ty (больший корень баллистики). */
    private static float airtime(float y0, float ty, float vy) {
        double disc = vy * vy - 2.0 * GRAVITY * (ty - y0);
        if (disc <= 0) {
            return 2f * vy / GRAVITY;
        }
        return (float) ((vy + Math.sqrt(disc)) / GRAVITY);
    }

    private Platform nextPlatform() {
        if (currentIndex + 1 < platforms.size()) {
            return platforms.get(currentIndex + 1);
        }
        return null;
    }

    void update(float dt) {
        time += dt;
        if (state == FLYING) {
            bottleVy -= GRAVITY * dt;
            bottleY += bottleVy * dt;
            bottleX += VX * dt;
            angle += angleVel * dt;
            if (bottleVy < 0) {
                tryLand();
            }
            if (state == FLYING && bottleY <= 0.05f) {
                fail(0.05f);
            }
        } else if (state == FALLEN) {
            fallenTimer += dt;
            float target = (float) (-Math.PI / 2.0);
            if (angle > target) {
                angle = Math.max(target, angle - 9f * dt);
            }
            if (fallenTimer > 0.65f) {
                state = OVER;
            }
        }
        // генерация вперёд и очистка позади
        while (lastX() < bottleX + 30) {
            genNext();
        }
        Iterator<Platform> it = platforms.iterator();
        int removed = 0;
        while (it.hasNext() && removed < currentIndex - 2) {
            it.next();
            it.remove();
            removed++;
        }
        if (removed > 0) {
            currentIndex -= removed;
        }
    }

    private void tryLand() {
        for (int i = 0; i < platforms.size(); i++) {
            Platform p = platforms.get(i);
            float top = platformTop(p);
            if (bottleY > top || bottleY < top - 0.6f) {
                continue;
            }
            float half = p.width / 2f;
            if (Math.abs(bottleX - p.centerX) > half + 0.05f) {
                continue;
            }
            boolean onSurface = Math.abs(bottleX - p.centerX)
                    <= half - EDGE_MARGIN;
            float a = normalizeAngle(angle);
            if (onSurface && Math.abs(a) <= LAND_ANGLE) {
                // успех
                bottleY = top;
                angle = 0;
                angleVel = 0;
                bottleVy = 0;
                currentIndex = i;
                score++;
                state = LANDED;
                emit(EV_LAND);
            } else {
                fail(top);
            }
            return;
        }
    }

    private void fail(float surfaceY) {
        bottleY = Math.max(surfaceY, 0.05f);
        bottleVy = 0;
        angleVel = 0;
        state = FALLEN;
        fallenTimer = 0;
        emit(EV_FAIL);
    }

    private static float normalizeAngle(float a) {
        float twoPi = (float) (2.0 * Math.PI);
        a = a % twoPi;
        if (a > Math.PI) {
            a -= twoPi;
        }
        if (a < -Math.PI) {
            a += twoPi;
        }
        return a;
    }

    /** Поверхность прямо под бутылкой (для тени). */
    float surfaceUnder() {
        float bestY = 0f;
        for (Platform p : platforms) {
            if (Math.abs(bottleX - p.centerX) <= p.width / 2f) {
                bestY = Math.max(bestY, platformTop(p));
            }
        }
        return bestY;
    }

    private void emit(int event) {
        if (listener != null) {
            listener.onEvent(event);
        }
    }
}
