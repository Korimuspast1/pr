#include "kernel.h"

/* QEMU/VMware VMMouse absolute pointer protocol (vmport 0x5658).
   This is optional: on VirtualBox/real hardware the OS keeps the PS/2 path. */
#define VMPORT_MAGIC 0x564D5868u
#define VMPORT_PORT 0x5658
#define VMPORT_CMD_GETVERSION 10
#define VMMOUSE_CMD_DATA 39
#define VMMOUSE_CMD_STATUS 40
#define VMMOUSE_CMD_COMMAND 41
#define VMMOUSE_READ_ID 0x45414552u
#define VMMOUSE_REQUEST_ABSOLUTE 0x53424152u
#define VMMOUSE_DISABLE 0x000000f5u
#define VMMOUSE_VERSION 0x3442554au
#define VMMOUSE_LEFT 0x20
#define VMMOUSE_RIGHT 0x10
#define VMMOUSE_RELATIVE 0x00010000u

static bool active;

static uint32_t vmport_call(uint32_t command, uint32_t ebx, uint32_t out[6]) {
    uint32_t eax = VMPORT_MAGIC, ecx = command, edx = VMPORT_PORT, esi = 0, edi = 0;
    __asm__ volatile(
        "outl %%eax, %%dx"
        : "+a"(eax), "+b"(ebx), "+c"(ecx), "+d"(edx), "+S"(esi), "+D"(edi)
        :
        : "memory");
    if (out) { out[0]=eax; out[1]=ebx; out[2]=ecx; out[3]=edx; out[4]=esi; out[5]=edi; }
    return eax;
}

bool vmmouse_active(void) { return active; }

void vmmouse_init(void) {
    uint32_t out[6];
    vmport_call(VMPORT_CMD_GETVERSION, 0, out);
    if (out[1] != VMPORT_MAGIC) return;
    vmport_call(VMMOUSE_CMD_COMMAND, VMMOUSE_READ_ID, out);
    vmport_call(VMMOUSE_CMD_DATA, 1, out);
    if (out[0] != VMMOUSE_VERSION) return;
    vmport_call(VMMOUSE_CMD_COMMAND, VMMOUSE_REQUEST_ABSOLUTE, out);
    active = true;
    logf("[input] VMMouse absolute pointer enabled (vmport 0x%x)\n", VMPORT_PORT);
}

void vmmouse_poll(void) {
    if (!active) return;
    uint32_t out[6];
    for (unsigned guard = 0; guard < 64; guard++) {
        uint32_t status = vmport_call(VMMOUSE_CMD_STATUS, 0, out);
        unsigned words = status & 0xffff;
        if (status >> 16) { active = false; return; }
        if (words < 4) return;
        vmport_call(VMMOUSE_CMD_DATA, 4, out);
        uint32_t buttons = out[0];
        if (buttons & VMMOUSE_RELATIVE) continue;
        uint64_t x = ((uint64_t)out[1] * (gfx_width() - 1) + 32767) / 65535;
        uint64_t y = ((uint64_t)out[2] * (gfx_height() - 1) + 32767) / 65535;
        mouse_event e = { (int)x, (int)y, !!(buttons & VMMOUSE_LEFT), !!(buttons & VMMOUSE_RIGHT), true };
        wm_mouse(&e);
    }
}
