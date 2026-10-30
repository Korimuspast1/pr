#include <assert.h>
#include "../animation.h"
int main(void) {
 Animation a = {0};
 animation_start(&a, 1.0f, 100, 200, 1);
 assert(a.active && a.value == 0.0f);
 animation_tick(&a, 200); assert(a.value > 0.5f && a.value < 1.0f);
 float mid = a.value;
 animation_start(&a, 0.0f, 200, 200, 1); assert(a.value == mid);
 animation_tick(&a, 300); assert(a.value < mid && a.value > 0.0f);
 animation_tick(&a, 400); assert(a.value == 0.0f && !a.active);
 animation_start(&a, 1.0f, 500, 200, 0); assert(a.value == 1.0f && !a.active);
 animation_start(&a, 0.0f, 700, 0, 1); assert(a.value == 0.0f && !a.active);
 animation_start(&a, 1.0f, 800, 200, 1); animation_tick(&a, 900);
 mid=a.value; animation_start(&a, 1.0f, 900, 200, 1); assert(a.value==mid);
 animation_tick(&a, 1000); assert(a.value==1.0f && !a.active);
 animation_start(&a, 0.0f, 1100, 200, 1); animation_tick(&a, 1000); assert(a.value==1.0f);
 return 0;
}
