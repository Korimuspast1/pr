# Aurora: freestanding x86-64 kernel + hybrid BIOS/UEFI ISO.
# Override tool paths on the command line if your distribution uses versioned names.
CC       ?= clang
HOSTCC   ?= cc
CURL     ?= curl
XORRISO  ?= xorriso
QEMU     ?= qemu-system-x86_64
PYTHON   ?= python3
TARGET   ?= x86_64-elf
BUILD    := build
ISO      := $(BUILD)/aurora.iso
KERNEL   := $(BUILD)/aurora.elf
LIMINE_REV := aad3edd370955449717a334f0289dee10e2c5f01
LIMINE_SHA := 9c23ca261778cbe015041c02220ce26f45e05f38fe3e0c9913be71961f9023e5
LIMINE_DIR := $(BUILD)/limine
LIMINE_TOOL := $(BUILD)/limine-host
OVMF_CODE ?= /usr/share/OVMF/OVMF_CODE_4M.fd
OVMF_VARS ?= /usr/share/OVMF/OVMF_VARS_4M.fd
QEMU_EXTRA ?=
export SOURCE_DATE_EPOCH := 1735689600
export LC_ALL := C
CFLAGS := --target=$(TARGET) -std=c11 -O2 -g0 -Wall -Wextra -Werror -ffreestanding \
  -fno-builtin -fno-stack-protector -fno-pic -fno-pie \
  -fno-asynchronous-unwind-tables -fno-unwind-tables -fno-ident -mno-red-zone \
  -mno-mmx -mno-sse -mno-sse2 -mcmodel=kernel -I$(LIMINE_DIR) -Isrc
ASFLAGS := --target=$(TARGET) -ffreestanding -fno-pic -fno-pie -g0 -c
LDFLAGS := --target=$(TARGET) -fuse-ld=lld -nostdlib -static -Wl,-z,max-page-size=0x1000 \
  -Wl,--build-id=none -Wl,-T,kernel.ld -Wl,-z,noexecstack
SOURCES := src/core.c src/cpu.c src/input.c src/graphics.c src/wm.c
OBJECTS := $(BUILD)/boot.o $(patsubst src/%.c,$(BUILD)/%.o,$(SOURCES))
QEMU_ARGS := -machine pc -accel tcg -cpu qemu64 -smp 1 -m 512M -cdrom $(ISO) \
  -boot order=d -vga std -serial file:$(BUILD)/serial.log -no-shutdown $(QEMU_EXTRA)
.PHONY: all iso run run-efi check clean test
all: $(KERNEL)
iso: $(ISO)
$(BUILD):
	mkdir -p $@
$(BUILD)/limine.tar.gz: | $(BUILD)
	$(CURL) --fail --location --retry 3 https://codeload.github.com/Limine-Bootloader/Limine/tar.gz/$(LIMINE_REV) -o $@.tmp
	printf '%s  %s\n' '$(LIMINE_SHA)' '$@.tmp' | sha256sum -c -
	mv $@.tmp $@
$(LIMINE_DIR)/limine.h: $(BUILD)/limine.tar.gz
	mkdir -p $(LIMINE_DIR)
	tar -xzf $< --strip-components=1 -C $(LIMINE_DIR)
$(LIMINE_TOOL): $(LIMINE_DIR)/limine.h
	$(HOSTCC) -O2 -std=c99 $(LIMINE_DIR)/limine.c -o $@
$(BUILD)/boot.o: src/boot.S | $(BUILD)
	$(CC) $(ASFLAGS) $< -o $@
$(BUILD)/%.o: src/%.c src/kernel.h $(LIMINE_DIR)/limine.h Makefile | $(BUILD)
	$(CC) $(CFLAGS) -c $< -o $@
$(BUILD)/graphics.o: src/graphics.c src/font.h src/kernel.h $(LIMINE_DIR)/limine.h Makefile | $(BUILD)
	$(CC) $(CFLAGS) -c $< -o $@
$(KERNEL): $(OBJECTS) kernel.ld
	$(CC) $(LDFLAGS) $(OBJECTS) -o $@
$(ISO): $(KERNEL) $(LIMINE_TOOL) limine.conf
	rm -rf $(BUILD)/iso-root
	mkdir -p $(BUILD)/iso-root/boot/limine $(BUILD)/iso-root/EFI/BOOT
	cp $(KERNEL) $(BUILD)/iso-root/boot/aurora.elf
	cp limine.conf $(BUILD)/iso-root/limine.conf
	cp $(LIMINE_DIR)/limine-bios.sys $(LIMINE_DIR)/limine-bios-cd.bin $(LIMINE_DIR)/limine-uefi-cd.bin $(BUILD)/iso-root/boot/limine/
	cp $(LIMINE_DIR)/LICENSE $(BUILD)/iso-root/boot/limine/LICENSE
	cp $(LIMINE_DIR)/BOOTX64.EFI $(BUILD)/iso-root/EFI/BOOT/
	find $(BUILD)/iso-root -exec touch -h -d @$(SOURCE_DATE_EPOCH) {} +
	$(XORRISO) -as mkisofs -R -r -J -iso-level 3 -volid AURORA \
	  -uid 0 -gid 0 -dir-mode 0755 -file-mode 0644 \
	  --set_all_file_dates =$(SOURCE_DATE_EPOCH) --modification-date=2025010100000000 \
	  -b boot/limine/limine-bios-cd.bin -no-emul-boot -boot-load-size 4 -boot-info-table \
	  --efi-boot boot/limine/limine-uefi-cd.bin -efi-boot-part --efi-boot-image \
	  --protective-msdos-label $(BUILD)/iso-root -o $@
	$(LIMINE_TOOL) bios-install $@
	sha256sum $@ > $@.sha256
run: $(ISO)
	$(QEMU) $(QEMU_ARGS) -display gtk
run-efi: $(ISO)
	cp $(OVMF_VARS) $(BUILD)/efi-vars.fd
	$(QEMU) $(QEMU_ARGS) -display gtk \
	  -drive if=pflash,format=raw,readonly=on,file=$(OVMF_CODE) \
	  -drive if=pflash,format=raw,file=$(BUILD)/efi-vars.fd
check:
	$(CC) --version
	$(CC) --target=$(TARGET) -dM -E -x c /dev/null | grep '^#define __x86_64__ 1'
	$(HOSTCC) --version | head -1
	$(XORRISO) --version | head -1
	$(QEMU) --version | head -1
	$(PYTHON) --version
clean:
	rm -rf $(BUILD)
test: $(ISO)
	$(PYTHON) tests/smoke.py --qemu "$(QEMU)" --iso "$(ISO)" --ovmf-code "$(OVMF_CODE)" --ovmf-vars "$(OVMF_VARS)"
