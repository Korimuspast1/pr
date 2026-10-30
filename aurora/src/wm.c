#include "kernel.h"
#define MAX_WINDOWS 12
#define SHELL_LINES 80
#define LINE_WIDTH 112
#define INPUT_WIDTH 100
#define TITLE_HEIGHT 38
#define TASK_HEIGHT 48
enum { WINDOW_SHELL, WINDOW_ABOUT, WINDOW_MONITOR };
typedef struct {
    bool used;int id,kind,x,y,w,h;char title[48];
    char lines[SHELL_LINES][LINE_WIDTH];unsigned line_count;
    char input[INPUT_WIDTH];unsigned input_len,cursor;
    char history[8][INPUT_WIDTH];unsigned history_count,history_pos;
} window;
static window windows[MAX_WINDOWS];static int order[MAX_WINDOWS],window_count,next_id=1,focused=-1;
static int pointer_x,pointer_y;static bool pointer_left,previous_left,dragging,menu_open;
static int drag_index=-1,drag_dx,drag_dy;static bool dirty=true,full_dirty=true;
static bool damage_valid;static int damage_x0,damage_y0,damage_x1,damage_y1;
static bool cursor_dirty=true,cursor_valid;static int cursor_x,cursor_y;
static uint32_t cursor_save[18*18];
static uint32_t bg,panel,panel2,ink,muted,accent,green,border,red;
static int screen_w,screen_h;
_Noreturn static void power_halt(void);
static void invalidate(void){dirty=true;full_dirty=true;}
static void damage_add(int x,int y,int w,int h){
    if(w<=0||h<=0)return;
    if(x<0){w+=x;x=0;}if(y<0){h+=y;y=0;}
    if(x>=screen_w||y>=screen_h)return;
    if(w>screen_w-x)w=screen_w-x;if(h>screen_h-y)h=screen_h-y;
    if(!damage_valid){damage_x0=x;damage_y0=y;damage_x1=x+w;damage_y1=y+h;damage_valid=true;return;}
    if(x<damage_x0)damage_x0=x;if(y<damage_y0)damage_y0=y;
    if(x+w>damage_x1)damage_x1=x+w;if(y+h>damage_y1)damage_y1=y+h;
}
bool wm_take_dirty(void){bool d=dirty;dirty=false;return d;}
void wm_present_full(void){gfx_present();damage_valid=false;full_dirty=false;}
void wm_present(void){
    if(full_dirty||!damage_valid)gfx_present();
    else gfx_present_rect(damage_x0,damage_y0,damage_x1-damage_x0,damage_y1-damage_y0);
    damage_valid=false;full_dirty=false;
}
bool wm_take_cursor_dirty(void){bool d=cursor_dirty;cursor_dirty=false;return d;}
static int index_of(int id){for(int i=0;i<MAX_WINDOWS;i++)if(windows[i].used&&windows[i].id==id)return i;return -1;}
static window *focus_window(void){int i=index_of(focused);return i<0?NULL:&windows[i];}
static void raise_window(int index){if(index<0)return;int id=windows[index].id;int n=0;for(int i=0;i<window_count;i++)if(order[i]!=id)order[n++]=order[i];order[n++]=id;window_count=n;focused=id;invalidate();}
static void shell_line(window *w,const char *s){
    /* UTF-8 is not decoded by this tiny terminal; commands and output are ASCII. */
    while(*s){
        if(w->line_count==SHELL_LINES){memmove(w->lines[0],w->lines[1],(SHELL_LINES-1)*LINE_WIDTH);w->line_count--;}
        char *line=w->lines[w->line_count++];unsigned n=0;
        while(*s&&*s!='\n'&&n<LINE_WIDTH-1)line[n++]=*s++;
        line[n]=0;logf("[shell:%d] %s\n",w->id,line);if(*s=='\n')s++;
        if(!*s)break;
    }
    invalidate();
}
static void shell_banner(window *w){shell_line(w,"Aurora Shell / " VERSION);shell_line(w,"Type 'help' for commands.");shell_line(w,"Drag a title bar to move a window.");shell_line(w,"");}
int wm_spawn(const char *title,int kind){
    int slot=-1;for(int i=0;i<MAX_WINDOWS;i++)if(!windows[i].used){slot=i;break;}
    if(slot<0)return -1;
    window *w=&windows[slot];memset(w,0,sizeof *w);w->used=true;w->id=next_id++;w->kind=kind;
    w->w=kind==WINDOW_SHELL?800:520;w->h=kind==WINDOW_SHELL?470:kind==WINDOW_MONITOR?330:350;
    if(w->w>screen_w-32)w->w=screen_w-32;if(w->h>screen_h-TASK_HEIGHT-32)w->h=screen_h-TASK_HEIGHT-32;
    w->x=120+(slot*31)%160;w->y=70+(slot*27)%100;
    if(w->x+w->w>screen_w-12)w->x=screen_w-w->w-12;if(w->y+w->h>screen_h-TASK_HEIGHT-12)w->y=screen_h-TASK_HEIGHT-w->h-12;
    snprintf(w->title,sizeof w->title,"%s",title&&*title?title:kind==WINDOW_SHELL?"Terminal":kind==WINDOW_ABOUT?"About Aurora":"System monitor");
    if(kind==WINDOW_SHELL)shell_banner(w);
    order[window_count++]=w->id;focused=w->id;invalidate();logf("[wm] create id=%d title=%s\n",w->id,w->title);return w->id;
}
bool wm_close(int id){int i=index_of(id);if(i<0)return false;logf("[wm] close id=%d\n",id);windows[i].used=false;for(int j=0;j<window_count;j++)if(order[j]==id){memmove(&order[j],&order[j+1],(size_t)(window_count-j-1)*sizeof(int));window_count--;break;}if(focused==id)focused=window_count?order[window_count-1]:-1;if(drag_index==i)dragging=false;invalidate();return true;}
void wm_list(void (*emit)(const char *)){char b[160];emit("ID  STATE   TITLE");for(int i=0;i<MAX_WINDOWS;i++)if(windows[i].used){snprintf(b,sizeof b,"%-3d %-7s %s",windows[i].id,windows[i].id==focused?"focused":"open",windows[i].title);emit(b);}}
static void insert_text(window *w,char c){if(w->input_len>=INPUT_WIDTH-1)return;memmove(&w->input[w->cursor+1],&w->input[w->cursor],w->input_len-w->cursor);w->input[w->cursor++]=(uint8_t)c;w->input_len++;w->input[w->input_len]=0;invalidate();}
static void erase_at(window *w,unsigned pos){if(pos>=w->input_len)return;memmove(&w->input[pos],&w->input[pos+1],w->input_len-pos-1);w->input_len--;w->input[w->input_len]=0;invalidate();}
static void history_recall(window *w,int direction){if(!w->history_count)return;if(direction<0){if(w->history_pos<w->history_count)w->history_pos++;}else if(w->history_pos)w->history_pos--;if(!w->history_pos){w->input[0]=0;w->input_len=0;}else{snprintf(w->input,sizeof w->input,"%s",w->history[8-w->history_pos]);w->input_len=(unsigned)strlen(w->input);}w->cursor=w->input_len;invalidate();}
static void execute(window *w){
    char command[INPUT_WIDTH];snprintf(command,sizeof command,"%s",w->input);char echo_arg[INPUT_WIDTH]="";if(!strncmp(command,"echo",4)&&(command[4]==' '||command[4]=='\t')){const char *s=command+4;while(*s==' '||*s=='\t')s++;snprintf(echo_arg,sizeof echo_arg,"%s",s);}char echo[128];snprintf(echo,sizeof echo,"aurora> %s",command);shell_line(w,echo);
    if(*command){memmove(w->history[0],w->history[1],7*INPUT_WIDTH);snprintf(w->history[7],INPUT_WIDTH,"%s",command);if(w->history_count<8)w->history_count++;}
    w->input[0]=0;w->input_len=w->cursor=w->history_pos=0;
    char *argv[12]={0};int argc=0;char *p=command;
    while(*p){while(*p==' '||*p=='\t')*p++=0;if(!*p)break;if(argc<12)argv[argc++]=p;while(*p&&*p!=' '&&*p!='\t')p++;}
    if(!argc)return;const char *cmd=argv[0];char b[192];logf("[shell:%d] %s\n",w->id,command);
    if(!strcmp(cmd,"help")){shell_line(w,"help                 Show this help");shell_line(w,"clear                Clear terminal");shell_line(w,"echo <text>          Print text");shell_line(w,"uptime               Time since boot");shell_line(w,"mem                  Used / free RAM");shell_line(w,"windows              List open windows");shell_line(w,"new [title]          Open a terminal");shell_line(w,"monitor              Open system monitor");shell_line(w,"about                Open About window");shell_line(w,"close <id>           Close a window");shell_line(w,"reboot               Restart the machine");shell_line(w,"halt                 Halt the CPU");shell_line(w,"fault-test           Trigger a page-fault panic");}
    else if(!strcmp(cmd,"clear")){w->line_count=0;invalidate();}
    else if(!strcmp(cmd,"echo")){shell_line(w,echo_arg);}
    else if(!strcmp(cmd,"uptime")){uint64_t t=ticks()/timer_hz();snprintf(b,sizeof b,"Up %llu s (%llu timer ticks, %u Hz)",t,ticks(),timer_hz());shell_line(w,b);}
    else if(!strcmp(cmd,"mem")){uint64_t total=pmm_total(),free=pmm_free();snprintf(b,sizeof b,"Managed RAM: %llu MiB total",total>>20);shell_line(w,b);snprintf(b,sizeof b,"Used: %llu MiB   Free: %llu MiB",(total-free)>>20,free>>20);shell_line(w,b);shell_line(w,"Excludes firmware and bootloader reserved regions.");}
    else if(!strcmp(cmd,"windows")){char b[160];shell_line(w,"ID  STATE   TITLE");for(int i=0;i<MAX_WINDOWS;i++)if(windows[i].used){snprintf(b,sizeof b,"%-3d %-7s %s",windows[i].id,windows[i].id==focused?"focused":"open",windows[i].title);shell_line(w,b);}}
    else if(!strcmp(cmd,"new")){int id=wm_spawn(argc>1?argv[1]:"Terminal",WINDOW_SHELL);if(id<0)shell_line(w,"Window limit reached.");else{snprintf(b,sizeof b,"Opened terminal #%d.",id);shell_line(w,b);}}
    else if(!strcmp(cmd,"monitor")){int id=wm_spawn("System monitor",WINDOW_MONITOR);if(id<0)shell_line(w,"Window limit reached.");}
    else if(!strcmp(cmd,"about"))wm_spawn("About Aurora",WINDOW_ABOUT);
    else if(!strcmp(cmd,"close")){if(argc<2)shell_line(w,"Usage: close <id>");else{int id=0;for(char *s=argv[1];*s;s++){if(*s<'0'||*s>'9'){id=-1;break;}id=id*10+*s-'0';}if(id==w->id){logf("[shell] close self id=%d\n",id);wm_close(id);}else if(id>0&&wm_close(id)){snprintf(b,sizeof b,"Closed window #%d.",id);shell_line(w,b);}else shell_line(w,"No such window.");}}
    else if(!strcmp(cmd,"reboot")){logf("[power] reboot requested from shell\n");fill(gfx_canvas(),0,0,screen_w,screen_h,bg);text(gfx_canvas(),48,48,"Restarting Aurora...",ink,3);gfx_present();cpu_reboot();}
    else if(!strcmp(cmd,"halt")){logf("[power] CPU halt requested from shell\n");power_halt();}
    else if(!strcmp(cmd,"fault-test")){logf("[test] deliberate page fault requested\n");volatile uint64_t *p=(volatile uint64_t *)0xffff900000000000ULL;(void)*p;shell_line(w,"Fault did not occur.");}
    else {snprintf(b,sizeof b,"Unknown command: %s. Type 'help'.",cmd);shell_line(w,b);}
}
void wm_key(const key_event *e){if(!e->pressed)return;invalidate();if(e->code==KEY_ESCAPE){menu_open=false;return;}window *w=focus_window();if(!w||w->kind!=WINDOW_SHELL)return;
    if(e->code==KEY_ENTER){execute(w);return;}
    if(e->code==KEY_BACKSPACE){if(w->cursor){w->cursor--;erase_at(w,w->cursor);}return;}
    if(e->code==KEY_DELETE){erase_at(w,w->cursor);return;}
    if(e->code==KEY_LEFT){if(w->cursor)w->cursor--;return;}
    if(e->code==KEY_RIGHT){if(w->cursor<w->input_len)w->cursor++;return;}
    if(e->code==KEY_UP){history_recall(w,-1);return;}
    if(e->code==KEY_DOWN){history_recall(w,1);return;}
    if(e->ascii==3){shell_line(w,"^C");w->input_len=w->cursor=0;w->input[0]=0;return;}
    if(e->ascii==12){w->line_count=0;return;}
    if(e->ascii>=32&&e->ascii<=126)insert_text(w,e->ascii);
}
static bool inside(int x,int y,int w,int h){return pointer_x>=x&&pointer_x<x+w&&pointer_y>=y&&pointer_y<y+h;}
static int hit_window(void){for(int j=window_count-1;j>=0;j--){int i=index_of(order[j]);if(i>=0&&inside(windows[i].x,windows[i].y,windows[i].w,windows[i].h))return i;}return -1;}
void wm_mouse(const mouse_event *e){
    bool clicked=e->left&&!previous_left;bool released=!e->left&&previous_left;
    int old_x=pointer_x,old_y=pointer_y;
    pointer_x=e->x;pointer_y=e->y;pointer_left=e->left;previous_left=e->left;
    if(pointer_x!=old_x||pointer_y!=old_y)cursor_dirty=true;
    if(dragging&&drag_index>=0&&windows[drag_index].used){window *w=&windows[drag_index];
        damage_add(w->x,w->y,w->w,w->h);
        w->x=pointer_x-drag_dx;w->y=pointer_y-drag_dy;if(w->x<0)w->x=0;if(w->y<36)w->y=36;if(w->x+w->w>screen_w)w->x=screen_w-w->w;if(w->y+w->h>screen_h-TASK_HEIGHT)w->y=screen_h-TASK_HEIGHT-w->h;
        damage_add(w->x,w->y,w->w,w->h);dirty=true;
        if(released){dragging=false;logf("[wm] drag id=%d x=%d y=%d\n",w->id,w->x,w->y);}return;}
    if(released)dragging=false;if(clicked||released)invalidate();if(!clicked)return;
    if(menu_open){int mx=16,my=screen_h-TASK_HEIGHT-216;if(inside(mx,my,260,200)){int item=(pointer_y-my-12)/46;if(item==0)wm_spawn("Terminal",WINDOW_SHELL);if(item==1)wm_spawn("System monitor",WINDOW_MONITOR);if(item==2)wm_spawn("About Aurora",WINDOW_ABOUT);if(item==3){logf("[power] reboot requested from menu\n");cpu_reboot();}menu_open=false;return;}menu_open=false;}
    if(inside(12,screen_h-TASK_HEIGHT+7,128,TASK_HEIGHT-14)){menu_open=!menu_open;return;}
    if(pointer_y>=screen_h-TASK_HEIGHT){int x=154;for(int j=0;j<window_count&&x+170<screen_w-180;j++,x+=178){int i=index_of(order[j]);if(i<0)continue;if(inside(x,screen_h-TASK_HEIGHT+8,170,TASK_HEIGHT-16)){raise_window(i);return;}}if(inside(screen_w-104,screen_h-TASK_HEIGHT+8,42,32)){logf("[power] reboot requested from taskbar\n");cpu_reboot();}if(inside(screen_w-54,screen_h-TASK_HEIGHT+8,42,32)){logf("[power] halt requested from taskbar\n");power_halt();}return;}
    int i=hit_window();if(i>=0){window *w=&windows[i];raise_window(i);if(inside(w->x+w->w-38,w->y+5,32,28)){wm_close(w->id);return;}if(pointer_y<w->y+TITLE_HEIGHT){dragging=true;drag_index=i;drag_dx=pointer_x-w->x;drag_dy=pointer_y-w->y;}return;}
    if(inside(24,70,88,92)){wm_spawn("Terminal",WINDOW_SHELL);return;}
    if(inside(24,180,88,92)){wm_spawn("System monitor",WINDOW_MONITOR);return;}
    if(inside(24,290,88,92)){wm_spawn("About Aurora",WINDOW_ABOUT);return;}
}
_Noreturn static void power_halt(void){fill(gfx_canvas(),0,0,screen_w,screen_h,bg);text(gfx_canvas(),48,48,"Aurora halted.",ink,3);text(gfx_canvas(),48,96,"CPU stopped. You may power off the virtual machine.",muted,2);gfx_present();cpu_halt();}
static void button(canvas *c,int x,int y,int w,int h,const char *label,uint32_t color){fill(c,x,y,w,h,color);rect(c,x,y,w,h,border);text(c,x+12,y+(h-16)/2,label,ink,2);}
static void draw_window(window *w){canvas *c=gfx_canvas();bool active=w->id==focused;int x=w->x,y=w->y,ww=w->w,hh=w->h;
    fill(c,x+5,y+6,ww,hh,rgb(9,15,28));fill(c,x,y,ww,hh,panel);rect(c,x,y,ww,hh,active?accent:border);
    fill(c,x+1,y+1,ww-2,TITLE_HEIGHT-1,active?rgb(35,53,80):panel2);text(c,x+16,y+11,w->title,active?ink:muted,2);
    fill(c,x+ww-38,y+5,32,28,active?rgb(68,43,61):panel);text(c,x+ww-29,y+11,"x",red,2);
    clip(c,x+1,y+TITLE_HEIGHT,ww-2,hh-TITLE_HEIGHT-1);
    if(w->kind==WINDOW_SHELL){
        fill(c,x+1,y+TITLE_HEIGHT,ww-2,hh-TITLE_HEIGHT-1,rgb(13,22,38));
        int columns=(ww-32)/16,rows=(hh-TITLE_HEIGHT-66)/20;if(columns>LINE_WIDTH-1)columns=LINE_WIDTH-1;
        unsigned start=w->line_count>(unsigned)rows?w->line_count-(unsigned)rows:0;
        for(int r=0;(unsigned)r<w->line_count-start&&r<rows;r++){char line[LINE_WIDTH];snprintf(line,sizeof line,"%s",w->lines[start+r]);if(columns<LINE_WIDTH)line[columns]=0;text(c,x+16,y+TITLE_HEIGHT+12+r*20,line,rgb(190,220,221),2);}
        int iy=y+hh-42;fill(c,x+1,iy-5,ww-2,46,rgb(21,34,53));
        char line[128];snprintf(line,sizeof line,"> %s",w->input);if(columns<8)columns=8;
        int scroll=(int)w->cursor+2>columns-1?(int)w->cursor+2-(columns-1):0;line[columns]=0;
        text(c,x+16,iy+6,line+scroll,green,2);
        if(active&&(ticks()/50)%2==0)fill(c,x+16+(w->cursor+2-scroll)*16,iy+5,3,20,green);
    }else if(w->kind==WINDOW_ABOUT){
        text(c,x+26,y+65,"AURORA",accent,4);text(c,x+26,y+116,"Small system. Real pixels.",ink,2);
        text(c,x+26,y+160,"Version " VERSION " / x86-64 long mode",muted,2);text(c,x+26,y+190,"Limine BIOS + UEFI / double buffering",muted,2);
        text(c,x+26,y+235,"C + assembly. No hosted OS underneath.",muted,2);text(c,x+26,y+280,"Drag me. Close me. Make something new.",green,2);
    }else{
        uint64_t total=pmm_total(),free=pmm_free();char b[128];int yy=y+65;
        snprintf(b,sizeof b,"Uptime       %llu seconds",ticks()/timer_hz());text(c,x+24,yy,b,ink,2);
        snprintf(b,sizeof b,"Resolution   %d x %d / 32 bpp",screen_w,screen_h);text(c,x+24,yy+34,b,ink,2);
        snprintf(b,sizeof b,"Managed RAM  %llu MiB",total>>20);text(c,x+24,yy+68,b,ink,2);
        snprintf(b,sizeof b,"Free RAM     %llu MiB",free>>20);text(c,x+24,yy+102,b,green,2);
        int barw=ww-48;fill(c,x+24,yy+144,barw,18,panel2);fill(c,x+24,yy+144,total?(int)((total-free)*barw/total):0,18,accent);
        snprintf(b,sizeof b,"Open windows %d / %d",window_count,MAX_WINDOWS);text(c,x+24,yy+184,b,muted,2);
    }
    clip(c,0,0,screen_w,screen_h);
}
#define CURSOR_W 18
#define CURSOR_H 18
static const char *cursor_shape[CURSOR_H]={"X.................","XX................","XoX...............","XooX..............","XoooX.............","XooooX............","XoooooX...........","XooooooX..........","XoooooooX.........","XooooooooX........","XoooooXXXXX.......","XooXooX...........","XoX.XooX..........","XX..XooX..........","X....XooX.........",".....XooX.........","......XX..........",".................."};
static void draw_cursor(canvas *c,int px,int py){for(int y=0;y<CURSOR_H;y++)for(int x=0;x<CURSOR_W;x++)if(cursor_shape[y][x]!='.')fill(c,px+x,py+y,1,1,cursor_shape[y][x]=='X'?rgb(5,10,18):rgb(248,250,252));}
void wm_cursor_update(void){
    canvas *c=gfx_canvas();clip(c,0,0,screen_w,screen_h);
    /* The back buffer stays cursor-free. Restore the old front rectangle,
       then upload only the new 18x18 cursor rectangle. */
    if(cursor_valid)gfx_present_rect(cursor_x,cursor_y,CURSOR_W,CURSOR_H);
    int px=pointer_x,py=pointer_y;
    for(int y=0;y<CURSOR_H;y++)for(int x=0;x<CURSOR_W;x++){
        int sx=px+x,sy=py+y;
        cursor_save[y*CURSOR_W+x]=(sx>=0&&sx<screen_w&&sy>=0&&sy<screen_h)?c->pixels[(size_t)sy*c->stride+sx]:0;
    }
    draw_cursor(c,px,py);
    gfx_present_rect(px,py,CURSOR_W,CURSOR_H);
    for(int y=0;y<CURSOR_H;y++)for(int x=0;x<CURSOR_W;x++){
        int sx=px+x,sy=py+y;
        if(sx>=0&&sx<screen_w&&sy>=0&&sy<screen_h)c->pixels[(size_t)sy*c->stride+sx]=cursor_save[y*CURSOR_W+x];
    }
    cursor_x=px;cursor_y=py;cursor_valid=true;cursor_dirty=false;
}
void wm_render(void){
    uint64_t second=ticks()/timer_hz();canvas *c=gfx_canvas();clip(c,0,0,screen_w,screen_h);
    fill(c,0,0,screen_w,screen_h,bg);
    /* Quiet geometric wallpaper, rendered into the same back buffer. */
    for(int y=36;y<screen_h-TASK_HEIGHT;y+=32)for(int x=16;x<screen_w;x+=32)fill(c,x,y,1,1,rgb(32,47,69));
    fill(c,screen_w-340,110,230,230,rgb(19,34,54));rect(c,screen_w-320,90,230,230,rgb(30,53,75));
    text(c,screen_w-300,150,"A",rgb(45,81,105),8);
    fill(c,0,0,screen_w,36,rgb(12,20,34));text(c,18,10,"AURORA",accent,2);text(c,145,10,"DESKTOP  /  LIVE SESSION",muted,2);
    char clock[64];snprintf(clock,sizeof clock,"UP %02llu:%02llu:%02llu",second/3600,second/60%60,second%60);text(c,screen_w-240,10,clock,muted,2);
    const char *labels[]={"Terminal","Monitor","About"};for(int i=0;i<3;i++){int y=70+i*110;fill(c,32,y,64,60,panel2);rect(c,32,y,64,60,border);text(c,48,y+18,i==0?">_":i==1?"#":"i",i==1?green:accent,3);text(c,28,y+70,labels[i],ink,2);}
    for(int j=0;j<window_count;j++){int i=index_of(order[j]);if(i>=0)draw_window(&windows[i]);}
    fill(c,0,screen_h-TASK_HEIGHT,screen_w,TASK_HEIGHT,rgb(12,20,34));fill(c,0,screen_h-TASK_HEIGHT,screen_w,1,border);
    button(c,12,screen_h-TASK_HEIGHT+7,128,34,"Aurora",menu_open?rgb(40,71,95):panel2);
    int x=154;for(int j=0;j<window_count&&x+170<screen_w-180;j++,x+=178){int i=index_of(order[j]);if(i<0)continue;char label[12];snprintf(label,sizeof label,"#%d %.7s",windows[i].id,windows[i].title);button(c,x,screen_h-TASK_HEIGHT+8,170,32,label,windows[i].id==focused?rgb(35,53,80):panel2);}
    button(c,screen_w-104,screen_h-TASK_HEIGHT+8,42,32,"R",panel2);button(c,screen_w-54,screen_h-TASK_HEIGHT+8,42,32,"H",panel2);
    if(menu_open){int my=screen_h-TASK_HEIGHT-216;fill(c,16,my,260,200,panel);rect(c,16,my,260,200,accent);const char *items[]={"New terminal","System monitor","About Aurora","Restart machine"};for(int i=0;i<4;i++)button(c,28,my+12+i*46,236,38,items[i],panel2);}
    dirty=false;
}
void wm_init(void){screen_w=(int)gfx_width();screen_h=(int)gfx_height();bg=rgb(15,23,42);panel=rgb(23,35,54);panel2=rgb(30,45,66);ink=rgb(230,237,247);muted=rgb(142,163,188);accent=rgb(99,205,218);green=rgb(132,225,177);border=rgb(53,71,95);red=rgb(255,130,147);pointer_x=screen_w/2;pointer_y=screen_h/2;wm_spawn("Terminal",WINDOW_SHELL);wm_spawn("About Aurora",WINDOW_ABOUT);int terminal=order[0];raise_window(index_of(terminal));wm_render();wm_present_full();wm_cursor_update();}
