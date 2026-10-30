# Aurora 1.0.0 — отчёт проверки

Среда: QEMU 11.0.2, TCG, x86-64, 512 МБ RAM, 1024×768×32.

| Режим | Результат | Время до READY |
|---|---:|---:|
| BIOS | PASS | ~1.0 с |
| UEFI (EDK2) | PASS | ~3.4 с |

Проверено автоматическим тестом `tests/smoke.py`:

- long mode, framebuffer, PS/2 keyboard/mouse ACK;
- клавиатурный ввод и команды `help`, `echo`, `uptime`, `mem`, `windows`, `new`, `close`, `monitor`;
- перетаскивание окна относительной PS/2-мышью;
- `fault-test`: page fault, экран паники, запись в COM1;
- `reboot`: повторная загрузка;
- `halt`: остановка CPU.

VirtualBox в этой среде не запускался; проверка выполнена только в QEMU.
