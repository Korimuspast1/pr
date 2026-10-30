.section .text.boot,"ax"
.code64
.global _start
.type _start,@function
_start:
    cli
    cld
    movl $0xC0000080, %ecx
    rdmsr
    testl $0x400, %eax
    jz 1f
    leaq boot_stack_top(%rip), %rsp
    andq $-16, %rsp
    xorq %rbp, %rbp
    call kernel_main
1:  cli
    hlt
    jmp 1b
.size _start, .-_start

.section .text,"ax"
.global cpu_halt
.type cpu_halt,@function
cpu_halt:
    cli
2:  hlt
    jmp 2b

.global cpu_reboot
.type cpu_reboot,@function
cpu_reboot:
    cli
    /* PS/2 controller reset: works on PC/AT, QEMU and VirtualBox. */
    movw $0x64, %dx
3:  inb %dx, %al
    testb $2, %al
    jnz 3b
    movb $0xFE, %al
    outb %al, %dx
    /* If the controller is absent, deliberately triple-fault to reset. */
    lidt invalid_idt(%rip)
    int3
4:  hlt
    jmp 4b

.macro ISR name, vector, has_error=0
.global \name
.type \name,@function
\name:
    .if !\has_error
    pushq $0
    .endif
    pushq $\vector
    jmp interrupt_common
.endm
ISR isr0, 0
ISR isr1, 1
ISR isr2, 2
ISR isr3, 3
ISR isr4, 4
ISR isr5, 5
ISR isr6, 6
ISR isr7, 7
ISR isr8, 8, 1
ISR isr9, 9
ISR isr10, 10, 1
ISR isr11, 11, 1
ISR isr12, 12, 1
ISR isr13, 13, 1
ISR isr14, 14, 1
ISR isr15, 15
ISR isr16, 16
ISR isr17, 17, 1
ISR isr18, 18
ISR isr19, 19
ISR isr20, 20
ISR isr21, 21, 1
ISR isr22, 22
ISR isr23, 23
ISR isr24, 24
ISR isr25, 25
ISR isr26, 26
ISR isr27, 27
ISR isr28, 28
ISR isr29, 29, 1
ISR isr30, 30, 1
ISR isr31, 31
ISR irq0, 32
ISR irq1, 33
ISR irq12, 44
ISR irq_spurious, 255

interrupt_common:
    /* Save the complete interrupted register set; pass a pointer to C. */
    pushq %rax
    pushq %rbx
    pushq %rcx
    pushq %rdx
    pushq %rsi
    pushq %rdi
    pushq %rbp
    pushq %r8
    pushq %r9
    pushq %r10
    pushq %r11
    pushq %r12
    pushq %r13
    pushq %r14
    pushq %r15
    movq %rsp, %rdi
    call interrupt_dispatch
    popq %r15
    popq %r14
    popq %r13
    popq %r12
    popq %r11
    popq %r10
    popq %r9
    popq %r8
    popq %rbp
    popq %rdi
    popq %rsi
    popq %rdx
    popq %rcx
    popq %rbx
    popq %rax
    addq $16, %rsp
    iretq

.section .rodata
.p2align 8
invalid_idt:
    .word 0
    .quad 0

.section .bss
.p2align 12
boot_stack:
    .skip 65536
boot_stack_top:
.section .note.GNU-stack,"",@progbits
