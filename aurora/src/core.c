#include "kernel.h"
#include <stdarg.h>
#include <limine.h>

__attribute__((used, section(".limine_requests_start_marker")))
static volatile uint64_t start_marker[] = {0xf6b8f4b39de7d1aeULL,0xfab91a6940fcb9cfULL,0x785c6ed015d3e316ULL,0x181e920a7852b9d9ULL};
__attribute__((used, section(".limine_requests_end_marker")))
static volatile uint64_t end_marker[] = {0xadc0e0531bb10d03ULL,0x9572709f31764c62ULL};
__attribute__((used, section(".limine_requests")))
static volatile uint64_t base_revision[] = {0xf9562b2d5c95a6c8ULL,0x6a7b384944536bdcULL,3};
#define REQUEST __attribute__((used, section(".limine_requests")))
REQUEST static volatile struct limine_hhdm_request hhdm_req = {.id=LIMINE_HHDM_REQUEST};
REQUEST static volatile struct limine_memmap_request map_req = {.id=LIMINE_MEMMAP_REQUEST};
REQUEST static volatile struct limine_firmware_type_request fw_req = {.id=LIMINE_FIRMWARE_TYPE_REQUEST};
REQUEST static volatile struct limine_bootloader_info_request bl_req = {.id=LIMINE_BOOTLOADER_INFO_REQUEST};
REQUEST static volatile struct limine_kernel_address_request addr_req = {.id=LIMINE_KERNEL_ADDRESS_REQUEST};

void outb(uint16_t p, uint8_t v) { __asm__ volatile("outb %0,%1" : : "a"(v), "Nd"(p)); }
uint8_t inb(uint16_t p) { uint8_t v; __asm__ volatile("inb %1,%0" : "=a"(v) : "Nd"(p)); return v; }
void io_wait(void) { outb(0x80,0); }
void *memset(void *d, int c, size_t n) { uint8_t *p=d; for(size_t i=0;i<n;i++) p[i]=(uint8_t)c; return d; }
void *memcpy(void *d, const void *s, size_t n) { uint8_t *a=d; const uint8_t *b=s; for(size_t i=0;i<n;i++) a[i]=b[i]; return d; }
void *memmove(void *d, const void *s, size_t n) { uint8_t *a=d; const uint8_t *b=s; if(a<b) return memcpy(d,s,n); for(size_t i=n;i;i--) a[i-1]=b[i-1]; return d; }
int memcmp(const void *a, const void *b, size_t n) { const uint8_t *x=a,*y=b; for(size_t i=0;i<n;i++) if(x[i]!=y[i]) return (int)x[i]-y[i]; return 0; }
size_t strlen(const char *s) { size_t n=0; while(s[n]) n++; return n; }
int strcmp(const char *a,const char *b) { while(*a && *a==*b){a++;b++;} return (unsigned char)*a-(unsigned char)*b; }
int strncmp(const char *a,const char *b,size_t n) { for(size_t i=0;i<n;i++){if(a[i]!=b[i] || !a[i]) return (unsigned char)a[i]-(unsigned char)b[i];} return 0; }
char *strcpy(char *d,const char *s) { char *r=d; while((*d++=*s++)) {} return r; }

void serial_init(void) {
    outb(0x3f9,0); outb(0x3fb,0x80); outb(0x3f8,1); outb(0x3f9,0);
    outb(0x3fb,3); outb(0x3fa,0xc7); outb(0x3fc,0x0b);
}
static void serial_char(char c) {
    unsigned timeout=100000;
    while(!(inb(0x3fd)&0x20) && --timeout) __asm__ volatile("pause");
    outb(0x3f8,(uint8_t)c);
}
void serial_write(const char *s) { while(*s){if(*s=='\n') serial_char('\r'); serial_char(*s++);} }
static void number(char **p,char *end,uint64_t v,unsigned base,int width,int zero,int upper,int left) {
    char tmp[66]; int n=0; const char *digits=upper?"0123456789ABCDEF":"0123456789abcdef";
    do {tmp[n++]=digits[v%base];v/=base;} while(v);
    if(!left) while(n<width && *p<end) { *(*p)++=zero?'0':' '; n++; }
    while(n && *p<end) *(*p)++=tmp[--n];
    if(left) while(n<width && *p<end) { *(*p)++=' '; n++; }
}
static void format(char *out,size_t cap,const char *fmt,va_list ap) {
    char *p=out,*end=out+(cap?cap-1:0);
    for(;*fmt;fmt++) {
        if(*fmt!='%'){if(p<end)*p++=*fmt;continue;}
        fmt++; int left=*fmt=='-'; if(left)fmt++; int zero=*fmt=='0'; int width=0; while(*fmt>='0'&&*fmt<='9') width=width*10+*fmt++-'0';
        int precision=-1; if(*fmt=='.'){fmt++;precision=0;while(*fmt>='0'&&*fmt<='9')precision=precision*10+*fmt++-'0';}
        int lng=0; while(*fmt=='l'){lng++;fmt++;}
        switch(*fmt) {
        case 's': {const char *s=va_arg(ap,const char*);if(!s)s="(null)";char *start=p;int count=0;while(*s&&p<end&&(precision<0||count<precision)){*p++=*s++;count++;}if(left)while(count<width&&p<end){*p++=' ';count++;}(void)start;break;}
        case 'c': if(p<end)*p++=(char)va_arg(ap,int);break;
        case 'd': {int64_t v=lng?va_arg(ap,int64_t):va_arg(ap,int);if(v<0){if(p<end)*p++='-';v=-v;}number(&p,end,(uint64_t)v,10,width,zero,0,left);break;}
        case 'u': number(&p,end,lng?va_arg(ap,uint64_t):va_arg(ap,unsigned),10,width,zero,0,left);break;
        case 'x': number(&p,end,lng?va_arg(ap,uint64_t):va_arg(ap,unsigned),16,width,zero,0,left);break;
        case 'X': number(&p,end,lng?va_arg(ap,uint64_t):va_arg(ap,unsigned),16,width,zero,1,left);break;
        case 'p': if(p<end)*p++='0';if(p<end)*p++='x';number(&p,end,(uint64_t)va_arg(ap,void*),16,16,1,0,0);break;
        case '%': if(p<end)*p++='%';break;
        default: if(p<end)*p++='%';if(*fmt&&p<end)*p++=*fmt;
        }
    }
    if(cap)*p=0;
}
void logf(const char *fmt,...) {char b[1024];va_list ap;va_start(ap,fmt);format(b,sizeof b,fmt,ap);va_end(ap);serial_write(b);}
void snprintf(char *out,size_t cap,const char *fmt,...) {va_list ap;va_start(ap,fmt);format(out,cap,fmt,ap);va_end(ap);}

static uint64_t hhdm;
static uint8_t *bitmap;
static uint64_t bitmap_pages, free_pages, total_pages, alloc_hint;
uint64_t hhdm_offset(void){return hhdm;}
uint64_t pmm_total(void){return total_pages*4096;}
uint64_t pmm_free(void){return free_pages*4096;}
static void bit_set(uint64_t i){bitmap[i/8]|=(uint8_t)(1u<<(i%8));}
static void bit_clear(uint64_t i){bitmap[i/8]&=(uint8_t)~(1u<<(i%8));}
static bool bit_test(uint64_t i){return bitmap[i/8]&(1u<<(i%8));}
void pmm_init(void) {
    if(!map_req.response || !hhdm_req.response) PANIC("Limine memory map / HHDM missing");
    hhdm=hhdm_req.response->offset;
    struct limine_memmap_response *m=map_req.response;
    uint64_t top=0, bm_size=0;
    for(uint64_t i=0;i<m->entry_count;i++) {
        struct limine_memmap_entry *e=m->entries[i];
        if(e->type==LIMINE_MEMMAP_USABLE && e->base+e->length>top) top=e->base+e->length;
    }
    bitmap_pages=(top+4095)/4096;
    bm_size=(bitmap_pages+7)/8;
    if(!bm_size || top>UINT64_MAX-4096) PANIC("Invalid physical memory map");
    for(uint64_t i=0;i<m->entry_count;i++) {
        struct limine_memmap_entry *e=m->entries[i];
        if(e->type==LIMINE_MEMMAP_USABLE && e->length>=bm_size+4096 && e->base>=0x100000) {bitmap=(void*)(hhdm+e->base);break;}
    }
    if(!bitmap) PANIC("No RAM for physical page bitmap");
    memset(bitmap,0xff,bm_size);
    for(uint64_t i=0;i<m->entry_count;i++) {
        struct limine_memmap_entry *e=m->entries[i];
        if(e->type!=LIMINE_MEMMAP_USABLE)continue;
        uint64_t begin=(e->base+4095)/4096, end=(e->base+e->length)/4096;
        if(begin<256)begin=256;
        for(uint64_t p=begin;p<end;p++){bit_clear(p);free_pages++;total_pages++;}
    }
    uint64_t bp=(uint64_t)bitmap-hhdm;
    for(uint64_t p=bp/4096;p<(bp+bm_size+4095)/4096;p++) if(!bit_test(p)){bit_set(p);free_pages--;}
    logf("[pmm] usable=%llu MiB free=%llu MiB bitmap=%p\n",total_pages/256,free_pages/256,(void*)bitmap);
}
void *pmm_alloc_pages(size_t count) {
    if(!count)return NULL;
    for(uint64_t pass=0;pass<2;pass++) {
        uint64_t begin=pass?256:alloc_hint, end=pass?alloc_hint:bitmap_pages, run=0;
        for(uint64_t p=begin;p<end;p++) {
            if(bit_test(p)){run=0;continue;}
            if(++run==count) {uint64_t first=p-count+1;for(uint64_t j=first;j<=p;j++)bit_set(j);free_pages-=count;alloc_hint=p+1;void *v=(void*)(hhdm+first*4096);memset(v,0,count*4096);return v;}
        }
    }
    return NULL;
}
void pmm_free_pages(void *v,size_t count) {uint64_t p=((uint64_t)v-hhdm)/4096;for(uint64_t i=p;i<p+count;i++){if(bit_test(i)){bit_clear(i);free_pages++;}}}

_Noreturn void panic_at(const char *file,int line,const char *fmt,...) {
    __asm__ volatile("cli"); char reason[512];va_list ap;va_start(ap,fmt);format(reason,sizeof reason,fmt,ap);va_end(ap);
    logf("[PANIC] %s:%d: %s\n",file,line,reason);
    uint64_t cr2=0,rip=0;__asm__ volatile("mov %%cr2,%0":"=r"(cr2));__asm__ volatile("mov (%%rsp),%0":"=r"(rip));
    panic_screen(reason,cr2,rip,0);cpu_halt();
}

static bool graphics_ready;
void gfx_mark_ready(void);
void gfx_mark_ready(void){graphics_ready=true;}
void panic_screen(const char *reason,uint64_t cr2,uint64_t rip,uint64_t error) {
    if(!graphics_ready)return;
    canvas *c=gfx_canvas();clip(c,0,0,c->width,c->height);fill(c,0,0,c->width,c->height,rgb(25,15,35));
    fill(c,0,0,c->width,8,rgb(255,83,112));text(c,48,60,"AURORA / KERNEL PANIC",rgb(255,120,140),3);
    text(c,48,125,reason,rgb(245,235,245),2);
    char b[256];snprintf(b,sizeof b,"RIP 0x%016llx   CR2 0x%016llx",rip,cr2);text(c,48,190,b,rgb(200,190,210),2);
    snprintf(b,sizeof b,"Error code 0x%llx | CPU halted; VM reset is safe.",error);text(c,48,225,b,rgb(200,190,210),2);
    text(c,48,290,"Details were sent to COM1. Restart the virtual machine.",rgb(200,190,210),2);gfx_present();
}

extern char __kernel_phys_start[], __kernel_phys_end[];
void kernel_main(void) {
    serial_init();serial_write("\n[boot] Aurora " VERSION " COM1 115200 8N1\n");
    uint64_t efer;uint32_t lo,hi;__asm__ volatile("rdmsr":"=a"(lo),"=d"(hi):"c"(0xC0000080));efer=((uint64_t)hi<<32)|lo;
    if(!(efer&0x400))PANIC("Not in long mode");
    if(base_revision[2]!=0)PANIC("Unsupported Limine base revision");
    logf("[boot] long mode confirmed EFER=0x%llx\n",efer);
    if(bl_req.response)logf("[boot] bootloader=%s %s\n",bl_req.response->name,bl_req.response->version);
    if(fw_req.response)logf("[boot] firmware=%s\n",fw_req.response->firmware_type==LIMINE_FIRMWARE_TYPE_UEFI64?"UEFI64":"BIOS");
    pmm_init();
    if(addr_req.response)logf("[boot] kernel physical=0x%llx virtual=0x%llx\n",addr_req.response->physical_base,addr_req.response->virtual_base);
    gfx_init();cpu_init();interrupts_init();timer_init(100);input_init();wm_init();
    logf("[boot] READY desktop=%ux%u firmware=%s\n",gfx_width(),gfx_height(),fw_req.response&&fw_req.response->firmware_type==2?"UEFI64":"BIOS");
    __asm__ volatile("sti");
    uint64_t last=UINT64_MAX;
    for(;;) {
        mouse_event me;while(mouse_pop(&me))wm_mouse(&me);
        key_event ke;while(key_pop(&ke))wm_key(&ke);
        uint64_t now=ticks();
        bool changed=wm_take_dirty();
        if(changed || last==UINT64_MAX || now-last>=timer_hz()/2) {wm_render();gfx_present();last=now;}
        __asm__ volatile("sti; hlt");
    }
}
