#ifndef EDITOR1_ANIMATION_H
#define EDITOR1_ANIMATION_H
/* Time-based cubic easing, independent of frame rate. */
typedef struct { float value, from, target; unsigned long long started; unsigned duration; int active; } Animation;
static int animation_tick(Animation *a, unsigned long long now) {
 if (!a->active) return 0;
 float t = now <= a->started ? 0.0f : (float)(now - a->started) / (float)a->duration;
 if (t >= 1.0f) { a->value = a->target; a->active = 0; return 1; }
 if (t < 0.0f) t = 0.0f;
 float inverse = 1.0f - t;
 a->value = a->from + (a->target - a->from) * (1.0f - inverse * inverse * inverse);
 return 1;
}
static void animation_start(Animation *a, float target, unsigned long long now, unsigned duration, int enabled) {
 animation_tick(a, now);
 if (!enabled || !duration) { a->value = a->from = a->target = target; a->active = 0; return; }
 if (a->target == target && a->active) return;
 a->from = a->value; a->target = target; a->started = now; a->duration = duration;
 a->active = (a->value != target);
}
#endif
