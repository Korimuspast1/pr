#ifndef AURORA_KERNEL_H
#define AURORA_KERNEL_H
#include <stdint.h>
#include <stddef.h>
#include <stdbool.h>

#define VERSION "1.0.0"
#define PANIC(...) panic_at(__FILE__, __LINE__, __VA_ARGS__)
void outb(uint16_t port, uint8_t value);
uint8_t inb(uint16_t port);
void io_wait(void);
void serial_init(void);
void serial_write(const char *s);
void logf(const char *fmt, ...);
_Noreturn void panic_at(const char *file, int line, const char *fmt, ...);
_Noreturn void cpu_halt(void);
_Noreturn void cpu_reboot(void);
void *memset(void *s, int c, size_t n);
void *memcpy(void *dest, const void *src, size_t n);
void *memmove(void *dest, const void *src, size_t n);
int memcmp(const void *a, const void *b, size_t n);
size_t strlen(const char *s);
int strcmp(const char *a, const char *b);
int strncmp(const char *a, const char *b, size_t n);
char *strcpy(char *d, const char *s);
void snprintf(char *out, size_t size, const char *fmt, ...);

void pmm_init(void);
void *pmm_alloc_pages(size_t count);
void pmm_free_pages(void *memory, size_t count);
uint64_t pmm_total(void);
uint64_t pmm_free(void);
uint64_t hhdm_offset(void);
void cpu_init(void);
void interrupts_init(void);
void timer_init(unsigned hz);
uint64_t ticks(void);
unsigned timer_hz(void);
void input_init(void);

typedef struct { int x, y; bool left, right; bool changed; } mouse_event;
typedef struct { uint16_t code; char ascii; bool pressed; } key_event;
bool mouse_pop(mouse_event *event);
bool key_pop(key_event *event);
#define KEY_ENTER 0x100
#define KEY_BACKSPACE 0x101
#define KEY_LEFT 0x102
#define KEY_RIGHT 0x103
#define KEY_DELETE 0x104
#define KEY_UP 0x105
#define KEY_DOWN 0x106
#define KEY_ESCAPE 0x107

typedef struct {
    uint32_t *pixels;
    int width, height, stride;
    int cx, cy, cw, ch;
} canvas;
void gfx_init(void);
void gfx_present(void);
canvas *gfx_canvas(void);
uint32_t gfx_width(void);
uint32_t gfx_height(void);
uint32_t rgb(uint8_t r, uint8_t g, uint8_t b);
void fill(canvas *c, int x, int y, int w, int h, uint32_t color);
void rect(canvas *c, int x, int y, int w, int h, uint32_t color);
void text(canvas *c, int x, int y, const char *s, uint32_t color, int scale);
void blit(canvas *c, int x, int y, int w, int h, const uint32_t *pixels);
void clip(canvas *c, int x, int y, int w, int h);
void panic_screen(const char *reason, uint64_t cr2, uint64_t rip, uint64_t error);

void wm_init(void);
void wm_key(const key_event *event);
void wm_mouse(const mouse_event *event);
void wm_render(void);
bool wm_take_dirty(void);
int wm_spawn(const char *title, int kind);
bool wm_close(int id);
void wm_list(void (*emit)(const char *));

#endif
