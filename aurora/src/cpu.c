#include "kernel.h"

typedef struct __attribute__((packed)) {uint16_t limit;uint64_t base;} descriptor_reg;
typedef struct __attribute__((packed)) {uint16_t low,selector;uint8_t ist,flags;uint16_t middle;uint32_t high,zero;} gate;
typedef struct __attribute__((packed)) {
    uint32_t reserved0;uint64_t rsp[3],reserved1,ist[7];uint64_t reserved2;uint16_t reserved3,iomap;
} tss_t;
static uint64_t gdt[7] __attribute__((aligned(16)));
static gate idt[256] __attribute__((aligned(16)));
static tss_t tss __attribute__((aligned(16)));
static uint8_t fault_stack[16384] __attribute__((aligned(16)));
static volatile uint64_t tick_count;
static unsigned frequency=100;
typedef struct {uint64_t r15,r14,r13,r12,r11,r10,r9,r8,rbp,rdi,rsi,rdx,rcx,rbx,rax,vector,error,rip,cs,rflags,rsp,ss;} frame_t;
extern void isr0(void),isr1(void),isr2(void),isr3(void),isr4(void),isr5(void),isr6(void),isr7(void),isr8(void),isr9(void),isr10(void),isr11(void),isr12(void),isr13(void),isr14(void),isr15(void),isr16(void),isr17(void),isr18(void),isr19(void),isr20(void),isr21(void),isr22(void),isr23(void),isr24(void),isr25(void),isr26(void),isr27(void),isr28(void),isr29(void),isr30(void),isr31(void),irq0(void),irq1(void),irq12(void),irq_spurious(void);
static void set_gate(unsigned n,void (*handler)(void),unsigned ist) {
    uint64_t p=(uint64_t)handler;idt[n]=(gate){(uint16_t)p,0x08,(uint8_t)ist,0x8e,(uint16_t)(p>>16),(uint32_t)(p>>32),0};
}
void cpu_init(void) {
    gdt[0]=0;gdt[1]=0x00af9a000000ffffULL;gdt[2]=0x00cf92000000ffffULL;
    gdt[3]=0x00cff2000000ffffULL;gdt[4]=0x00cffa000000ffffULL;
    uint64_t tb=(uint64_t)&tss;
    tss.iomap=sizeof tss;tss.ist[0]=(uint64_t)(fault_stack+sizeof fault_stack);
    gdt[5]=0x0000890000000000ULL|((tb&0xffffff)<<16)|(((tb>>24)&0xff)<<56)|(sizeof(tss)-1);
    gdt[6]=tb>>32;
    descriptor_reg gd={sizeof gdt-1,(uint64_t)gdt};
    __asm__ volatile("lgdt %0; pushq $0x08; lea 1f(%%rip),%%rax; pushq %%rax; lretq; 1: movw $0x10,%%ax; movw %%ax,%%ds; movw %%ax,%%es; movw %%ax,%%ss; movw %%ax,%%fs; movw %%ax,%%gs" : : "m"(gd) : "rax","memory");
    __asm__ volatile("ltr %0" : : "r"((uint16_t)0x28));
    logf("[cpu] own GDT/TSS loaded, NX page tables supplied by Limine\n");
}
static void pic_send(uint8_t p,uint8_t v){outb(p,v);io_wait();}
void interrupts_init(void) {
    void (*handlers[])(void)={isr0,isr1,isr2,isr3,isr4,isr5,isr6,isr7,isr8,isr9,isr10,isr11,isr12,isr13,isr14,isr15,isr16,isr17,isr18,isr19,isr20,isr21,isr22,isr23,isr24,isr25,isr26,isr27,isr28,isr29,isr30,isr31};
    for(unsigned i=0;i<32;i++)set_gate(i,handlers[i],i==8||i==14?1:0);
    for(unsigned i=32;i<256;i++)set_gate(i,irq_spurious,0);
    set_gate(32,irq0,0);set_gate(33,irq1,0);set_gate(44,irq12,0);
    descriptor_reg id={sizeof idt-1,(uint64_t)idt};__asm__ volatile("lidt %0": :"m"(id));
    /* Remap the legacy PIC away from CPU exception vectors 0..31. */
    uint8_t a=inb(0x21),b=inb(0xa1);
    pic_send(0x20,0x11);pic_send(0xa0,0x11);pic_send(0x21,0x20);pic_send(0xa1,0x28);
    pic_send(0x21,4);pic_send(0xa1,2);pic_send(0x21,1);pic_send(0xa1,1);
    pic_send(0x21,0xf8);pic_send(0xa1,0xef); /* timer, keyboard, cascade, mouse only */
    (void)a;(void)b;logf("[irq] IDT loaded; PIC vectors 32..47; IRQ 0/1/12 unmasked\n");
}
void timer_init(unsigned hz) {frequency=hz;uint16_t divisor=(uint16_t)(1193182/hz);outb(0x43,0x36);outb(0x40,(uint8_t)divisor);outb(0x40,(uint8_t)(divisor>>8));logf("[timer] PIT %u Hz\n",hz);}
uint64_t ticks(void){__asm__ volatile("":::"memory");return tick_count;}
unsigned timer_hz(void){return frequency;}
void ps2_keyboard_byte(uint8_t);void ps2_mouse_byte(uint8_t);
void interrupt_dispatch(frame_t *f) {
    if(f->vector<32) {
        static const char *names[]={"Divide error","Debug","NMI","Breakpoint","Overflow","Bound range","Invalid opcode","Device unavailable","Double fault","Coprocessor segment","Invalid TSS","Segment absent","Stack fault","General protection","Page fault","Reserved","x87 exception","Alignment check","Machine check","SIMD exception","Virtualization","Control protection","Reserved","Reserved","Reserved","Reserved","Reserved","Reserved","Hypervisor","VMM communication","Security","Reserved"};
        uint64_t cr2=0;__asm__ volatile("mov %%cr2,%0":"=r"(cr2));
        logf("[PANIC] exception=%llu (%s) rip=0x%llx cr2=0x%llx error=0x%llx cs=0x%llx rsp=0x%llx\n",f->vector,names[f->vector],f->rip,cr2,f->error,f->cs,f->rsp);
        char reason[160];snprintf(reason,sizeof reason,"%s (exception %llu)",names[f->vector],f->vector);
        __asm__ volatile("cli");panic_screen(reason,cr2,f->rip,f->error);cpu_halt();
    }
    if(f->vector==32)tick_count++;
    else if(f->vector==33||f->vector==44) {
        uint8_t status=inb(0x64);
        if(status&1){uint8_t data=inb(0x60);if(status&0x20)ps2_mouse_byte(data);else ps2_keyboard_byte(data);}
    }
    if(f->vector>=32&&f->vector<48){if(f->vector>=40)outb(0xa0,0x20);outb(0x20,0x20);}
}
