# Aurora 1.0.0

Aurora — небольшая учебная 64-битная ОС на C и x86-64 ассемблере. Это **live-ISO**:
она загружается с DVD-образа и работает в оперативной памяти. Установщика на жёсткий
диск нет. Это не замена Windows: нет дисковой файловой системы, сети, USB-стека,
пользовательских процессов и драйверов реального оборудования.

## Скачать

Прямая ссылка на ISO:

<https://github.com/Korimuspast1/pr/raw/refs/tags/aurora-v1.0.0/aurora/dist/aurora.iso>

Контрольная сумма публикуется рядом с ISO в релизе.

## Что есть

- загрузка через Limine: BIOS и UEFI, x86-64 long mode;
- графический рабочий стол 1024×768×32 с двойной буферизацией;
- окна, перетаскивание за заголовок, закрытие, фокус, панель задач;
- PS/2-клавиатура и относительная PS/2-мышь;
- терминал с командами `help`, `clear`, `echo`, `uptime`, `mem`, `windows`,
  `new`, `monitor`, `about`, `close`, `reboot`, `halt`, `fault-test`;
- вывод отладки в COM1 115200 8N1;
- обработчик page fault с экраном паники и логом в COM1.

## Быстрый запуск в Oracle VirtualBox

Нужен x86-64 компьютер с Intel/AMD и включённой аппаратной виртуализацией.
На Apple Silicon / ARM эта ОС не запустится. Рекомендуется VirtualBox 7.x.

1. Скачайте `aurora.iso`.
2. Создайте ВМ: тип `Other`, версия `Other/Unknown (64-bit)`.
3. RAM: **512 МБ**, CPU: **1**.
4. Дисплей: **VBoxSVGA**, видеопамять **128 МБ**.
5. Носители: подключите `aurora.iso` как DVD. Жёсткий диск не нужен.
6. Система → Порядок загрузки: **Оптический диск** первым.
7. Для UEFI: Система → Материнская плата → **Включить EFI**. Для BIOS: EFI выключен.
   Secure Boot должен быть выключен.
8. Последовательный порт: COM1, `0x3F8`, IRQ4, режим «Файл», путь к логу.
9. Запустите ВМ. Должен появиться рабочий стол и окно Terminal.

В окне ВМ выберите Terminal и введите `help`. Мышь должна двигаться; если
VirtualBox предлагает интеграцию мыши, для этой учебной ОС используйте обычный
PS/2-режим, как в командах ниже.

### PowerShell, Windows

```powershell
$ISO = "$HOME\Downloads\aurora.iso"
VBoxManage createvm --name Aurora --ostype Other_64 --register
VBoxManage modifyvm Aurora --memory 512 --cpus 1 --vram 128 --graphicscontroller vboxsvga --mouse ps2 --keyboard ps2 --boot1 dvd --boot2 none --boot3 none --boot4 none --uart1 0x3F8 4 --uartmode1 file "$HOME\aurora-com1.log"
VBoxManage storagectl Aurora --name IDE --add ide --controller PIIX4
VBoxManage storageattach Aurora --storagectl IDE --port 0 --device 0 --type dvddrive --medium $ISO
VBoxManage startvm Aurora
```

UEFI:

```powershell
VBoxManage modifyvm Aurora --firmware efi
VBoxManage startvm Aurora
```

BIOS:

```powershell
VBoxManage modifyvm Aurora --firmware bios
VBoxManage startvm Aurora
```

### bash, Linux/macOS с VirtualBox

```bash
ISO="$HOME/Downloads/aurora.iso"
VBoxManage createvm --name Aurora --ostype Other_64 --register
VBoxManage modifyvm Aurora --memory 512 --cpus 1 --vram 128 --graphicscontroller vboxsvga --mouse ps2 --keyboard ps2 --boot1 dvd --boot2 none --boot3 none --boot4 none --uart1 0x3F8 4 --uartmode1 file "$HOME/aurora-com1.log"
VBoxManage storagectl Aurora --name IDE --add ide --controller PIIX4
VBoxManage storageattach Aurora --storagectl IDE --port 0 --device 0 --type dvddrive --medium "$ISO"
VBoxManage startvm Aurora
```

UEFI: `VBoxManage modifyvm Aurora --firmware efi`.
BIOS: `VBoxManage modifyvm Aurora --firmware bios`.

Если EFI не загружается, отключите Secure Boot и попробуйте BIOS. Если экран
чёрный, проверьте, что DVD подключён и стоит первым в загрузке; также можно
попробовать графический контроллер VBoxVGA.

## Команды терминала

```text
help                 список команд
clear                очистить терминал
echo <text>          напечатать текст
uptime               время с загрузки
mem                  занятая / свободная управляемая RAM
windows              список окон
new [title]          открыть терминал
monitor              системный монитор
about                окно About
close <id>           закрыть окно по ID
reboot               перезагрузка через контроллер PS/2
halt                 остановка CPU (CLI/HLT)
fault-test           намеренный page fault для проверки паники
```

`halt` останавливает процессор, но не выключает питание ВМ. После него закройте
или выключите ВМ средствами VirtualBox.

## Сборка из исходников

Стандартные инструменты: Clang, LLD, GNU make, curl, xorriso, QEMU, OVMF,
Python 3, coreutils. NASM не нужен: ассемблер GNU `.S` собирается Clang.

Debian/Ubuntu:

```bash
sudo apt install clang lld make curl xorriso qemu-system-x86 ovmf python3
cd aurora
make check
make -j"$(nproc)"
make iso
make run        # BIOS, окно QEMU
make run-efi    # UEFI, окно QEMU
make test       # автоматическая проверка BIOS + UEFI
```

Если используется Zig как переносимый Clang:

```bash
make CC="zig cc" TARGET=x86_64-freestanding-none iso
```

Результат: `build/aurora.iso`. Сборка фиксирует Limine по commit и SHA-256,
нормализует даты файлов ISO и отключает ELF build-ID. Побитовая идентичность
гарантируется только при одинаковых версиях компилятора, Limine и xorriso.

## Проверено

Автоматический тест `tests/smoke.py` в QEMU 11.0.2, TCG, 512 МБ:

- BIOS: рабочий стол примерно за 1.1 с;
- UEFI: рабочий стол примерно за 3.3 с;
- клавиатура, команды терминала, создание/закрытие окон;
- перетаскивание окна относительной PS/2-мышью;
- page fault → экран паники и запись в COM1;
- reboot и halt.

**VirtualBox в среде сборки не запускался.** Инструкции выше даны для
VirtualBox, но фактическая проверка выполнена только в QEMU BIOS/UEFI.

Настоящий triple fault аппаратно сбрасывает CPU; ОС не может нарисовать экран
после самого сброса. Проверяемый `fault-test` вызывает перехватываемый page fault.
