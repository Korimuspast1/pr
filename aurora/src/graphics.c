#include "kernel.h"
#include <limine.h>
#define REQUEST __attribute__((used, section(".limine_requests")))
REQUEST static volatile struct limine_framebuffer_request graphics_request = {.id=LIMINE_FRAMEBUFFER_REQUEST};
static canvas back;
static volatile struct limine_framebuffer *front;
static uint32_t front_width,front_height;
void gfx_mark_ready(void);
uint32_t rgb(uint8_t r,uint8_t g,uint8_t b){return ((uint32_t)r<<16)|((uint32_t)g<<8)|b;}
uint32_t gfx_width(void){return front_width;}
uint32_t gfx_height(void){return front_height;}
canvas *gfx_canvas(void){return &back;}
void clip(canvas *c,int x,int y,int w,int h){if(x<0){w+=x;x=0;}if(y<0){h+=y;y=0;}if(x>c->width)x=c->width;if(y>c->height)y=c->height;if(w>c->width-x)w=c->width-x;if(h>c->height-y)h=c->height-y;c->cx=x;c->cy=y;c->cw=w<0?0:w;c->ch=h<0?0:h;}
static bool clipped(int *x,int *y,int *w,int *h,canvas *c){if(*x<c->cx){*w-=c->cx-*x;*x=c->cx;}if(*y<c->cy){*h-=c->cy-*y;*y=c->cy;}if(*x>c->cx+c->cw||*y>c->cy+c->ch)return false;if(*w>c->cx+c->cw-*x)*w=c->cx+c->cw-*x;if(*h>c->cy+c->ch-*y)*h=c->cy+c->ch-*y;return *w>0&&*h>0;}
void fill(canvas *c,int x,int y,int w,int h,uint32_t color){
    if(!clipped(&x,&y,&w,&h,c))return;
    const uint64_t pair=((uint64_t)color<<32)|color;
    for(int row=0;row<h;row++){
        uint32_t *p=c->pixels+(size_t)(y+row)*c->stride+x;int col=0;
        if(((uintptr_t)p)&4){p[col++]=color;}
        uint64_t *q=(uint64_t*)(p+col);
        for(;col+1<w;col+=2)*q++=pair;
        if(col<w)p[col]=color;
    }
}
void rect(canvas *c,int x,int y,int w,int h,uint32_t color){fill(c,x,y,w,1,color);fill(c,x,y+h-1,w,1,color);fill(c,x,y,1,h,color);fill(c,x+w-1,y,1,h,color);}
void blit(canvas *c,int x,int y,int w,int h,const uint32_t *p){int sx=x,sy=y;if(!clipped(&x,&y,&w,&h,c))return;for(int row=0;row<h;row++)memcpy(c->pixels+(size_t)(y+row)*c->stride+x,p+(size_t)(y-sy+row)*w+(x-sx),(size_t)w*4);}
#include "font.h"
void text(canvas *c,int x,int y,const char *s,uint32_t color,int scale){
    if(scale<1)scale=1;int ox=x;
    for(;*s;s++){
        unsigned char ch=(unsigned char)*s;
        if(ch=='\n'){x=ox;y+=10*scale;continue;}
        if(ch==' '){x+=8*scale;continue;}
        if(ch>127)ch='?';
        for(int row=0;row<8;row++){
            uint8_t bits=font8x8_basic[ch][row];int col=0;
            while(col<8){
                if(!(bits&(1u<<col))){col++;continue;}
                int start=col;while(col<8&&(bits&(1u<<col)))col++;
                fill(c,x+start*scale,y+row*scale,(col-start)*scale,scale,color);
            }
        }
        x+=8*scale;
    }
}
void gfx_init(void){
    if(!graphics_request.response||!graphics_request.response->framebuffer_count)PANIC("Bootloader did not provide a framebuffer");
    front=graphics_request.response->framebuffers[0];
    if(front->bpp!=32||front->memory_model!=LIMINE_FRAMEBUFFER_RGB)PANIC("Framebuffer must be 32-bit RGB");
    if(!front->width||!front->height||front->width>4096||front->height>4096||front->pitch<front->width*4)PANIC("Invalid framebuffer dimensions");
    front_width=(uint32_t)front->width;front_height=(uint32_t)front->height;
    back.width=(int)front_width;back.height=(int)front_height;back.stride=back.width;
    size_t bytes=(size_t)back.width*back.height*4;back.pixels=pmm_alloc_pages((bytes+4095)/4096);
    if(!back.pixels)PANIC("Not enough contiguous RAM for compositor");clip(&back,0,0,back.width,back.height);
    fill(&back,0,0,back.width,back.height,rgb(15,23,42));gfx_mark_ready();gfx_present();
    logf("[gfx] %ux%u bpp=%u pitch=%llu front=%p back=%p double-buffered\n",front_width,front_height,front->bpp,front->pitch,(void*)front->address,(void*)back.pixels);
}
static bool front_native(void){
    return front->red_mask_size==8&&front->red_mask_shift==16&&front->green_mask_size==8&&front->green_mask_shift==8&&front->blue_mask_size==8&&front->blue_mask_shift==0;
}
void gfx_present_rect(int x,int y,int w,int h){
    if(x<0){w+=x;x=0;}if(y<0){h+=y;y=0;}
    if(x>=(int)front_width||y>=(int)front_height)return;
    if(w>(int)front_width-x)w=(int)front_width-x;
    if(h>(int)front_height-y)h=(int)front_height-y;
    if(w<=0||h<=0)return;
    uint8_t *dst=front->address;bool native=front_native();
    for(int row=0;row<h;row++){
        uint32_t *src=back.pixels+(size_t)(y+row)*back.stride+x;
        uint8_t *d=dst+(size_t)(y+row)*front->pitch+(size_t)x*4;
        if(native)memcpy(d,src,(size_t)w*4);
        else for(int col=0;col<w;col++){
            uint32_t p=src[col],r=(p>>16)&255,g=(p>>8)&255,b=p&255;
            uint32_t out=(((r*((1u<<front->red_mask_size)-1)+127)/255)<<front->red_mask_shift)|(((g*((1u<<front->green_mask_size)-1)+127)/255)<<front->green_mask_shift)|(((b*((1u<<front->blue_mask_size)-1)+127)/255)<<front->blue_mask_shift);
            memcpy(d+(size_t)col*4,&out,4);
        }
    }
    __asm__ volatile("sfence":::"memory");
}
void gfx_present(void){gfx_present_rect(0,0,(int)front_width,(int)front_height);}
