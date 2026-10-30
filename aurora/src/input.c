#include "kernel.h"
#define KEY_QUEUE 256
static volatile key_event keys[KEY_QUEUE];static volatile unsigned key_head,key_tail;
static volatile mouse_event mouse;static volatile bool mouse_pending;
static unsigned prefix;static bool shift,caps,ctrl;static int mx,my;static bool left,right;
static unsigned packet_index;static uint8_t packet[3];static bool mouse_ok,keyboard_ok;
static const char map[128]={0,27,'1','2','3','4','5','6','7','8','9','0','-','=','\b','\t','q','w','e','r','t','y','u','i','o','p','[',']','\n',0,'a','s','d','f','g','h','j','k','l',';','\'','`',0,'\\','z','x','c','v','b','n','m',',','.','/',0,'*',0,' ',0};
static const char shifted[128]={0,27,'!','@','#','$','%','^','&','*','(',')','_','+','\b','\t','Q','W','E','R','T','Y','U','I','O','P','{','}','\n',0,'A','S','D','F','G','H','J','K','L',':','"','~',0,'|','Z','X','C','V','B','N','M','<','>','?',0,'*',0,' ',0};
static void push_key(uint16_t code,char c,bool pressed){unsigned next=(key_head+1)%KEY_QUEUE;if(next!=key_tail){keys[key_head]=(key_event){code,c,pressed};key_head=next;}}
void ps2_keyboard_byte(uint8_t sc) {
    if(sc==0xe0){prefix=1;return;}
    if(sc==0xe1){prefix=2;return;}
    if(prefix==2){prefix--;return;}
    bool released=!!(sc&0x80);unsigned code=sc&0x7f;bool extended=prefix==1;prefix=0;
    if(code==0x2a||code==0x36){shift=!released;return;}
    if(code==0x1d){ctrl=!released;return;}
    if(code==0x3a&&!released){caps=!caps;return;}
    if(released)return;
    if(extended){uint16_t k=code==0x4b?KEY_LEFT:code==0x4d?KEY_RIGHT:code==0x48?KEY_UP:code==0x50?KEY_DOWN:code==0x53?KEY_DELETE:0;if(k)push_key(k,0,true);return;}
    if(code==0x1c){push_key(KEY_ENTER,'\n',true);return;}
    if(code==0x0e){push_key(KEY_BACKSPACE,'\b',true);return;}
    if(code==0x01){push_key(KEY_ESCAPE,27,true);return;}
    if(code<128){char c=(shift?shifted:map)[code];if(c>='a'&&c<='z'&&caps)c-=32;else if(c>='A'&&c<='Z'&&caps)c+=32;if(ctrl&&c>='a'&&c<='z')c-=96;if(ctrl&&c>='A'&&c<='Z')c-=64;if(c)push_key((uint8_t)c,c,true);}
}
void ps2_mouse_byte(uint8_t b) {
    /* Bit 3 is the PS/2 first-byte marker; discard impossible button combinations. */
    if(!packet_index&&(!(b&8)||(b&0xc0)))return;
    packet[packet_index++]=b;if(packet_index<3)return;packet_index=0;
    uint8_t flags=packet[0];if(flags&0xc0)return;
    int dx=(int)packet[1]-((flags&0x10)?256:0),dy=(int)packet[2]-((flags&0x20)?256:0);
    mx+=dx;my-=dy;if(mx<0)mx=0;if(my<0)my=0;if(mx>=(int)gfx_width())mx=(int)gfx_width()-1;if(my>=(int)gfx_height())my=(int)gfx_height()-1;
    left=!!(flags&1);right=!!(flags&2);mouse=(mouse_event){mx,my,left,right,true};mouse_pending=true;
}
bool key_pop(key_event *e){if(key_tail==key_head)return false;*e=keys[key_tail];key_tail=(key_tail+1)%KEY_QUEUE;return true;}
bool mouse_pop(mouse_event *e){if(!mouse_pending)return false;__asm__ volatile("":::"memory");*e=mouse;mouse_pending=false;return true;}
static bool wait_write(void){for(unsigned i=0;i<100000;i++)if(!(inb(0x64)&2))return true;return false;}
static bool wait_read(void){for(unsigned i=0;i<100000;i++)if(inb(0x64)&1)return true;return false;}
static void flush(void){while(inb(0x64)&1)(void)inb(0x60);}
static bool command(uint8_t c){if(!wait_write())return false;outb(0x60,c);return true;}
static bool response(uint8_t *v){if(!wait_read())return false;*v=inb(0x60);return true;}
static bool mouse_command(uint8_t c,uint8_t *reply){if(!wait_write())return false;outb(0x64,0xd4);if(!wait_write())return false;outb(0x60,c);return response(reply)&&*reply==0xfa;}
void input_init(void) {
    mx=(int)gfx_width()/2;my=(int)gfx_height()/2;mouse=(mouse_event){mx,my,false,false,true};mouse_pending=true;
    if(!wait_write())PANIC("PS/2 controller unavailable");outb(0x64,0xad);outb(0x64,0xa7);flush();
    if(!wait_write())PANIC("PS/2 configuration timeout");outb(0x64,0x20);uint8_t config=0;response(&config);
    config=(uint8_t)((config|0x43)&~0x30);if(!wait_write())PANIC("PS/2 write timeout");outb(0x64,0x60);if(!wait_write())PANIC("PS/2 write timeout");outb(0x60,config);
    if(!wait_write())PANIC("PS/2 timeout");outb(0x64,0xae);
    uint8_t keyboard_reply=0;keyboard_ok=command(0xf4)&&response(&keyboard_reply)&&keyboard_reply==0xfa;
    if(!wait_write())PANIC("PS/2 timeout");outb(0x64,0xa8);
    if(!wait_write())PANIC("PS/2 timeout");outb(0x64,0x20);uint8_t cfg=0;response(&cfg);cfg|=2;cfg&=~0x20;
    if(!wait_write())PANIC("PS/2 timeout");outb(0x64,0x60);if(!wait_write())PANIC("PS/2 timeout");outb(0x60,cfg);
    uint8_t r;mouse_ok=mouse_command(0xf6,&r)&&mouse_command(0xf4,&r);
    flush();logf("[input] PS/2 keyboard=%s mouse=%s; relative packets, set-1 keyboard\n",keyboard_ok?"ACK":"unconfirmed",mouse_ok?"ACK":"unconfirmed");
}
