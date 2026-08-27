include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs
." After cross.fs" cr
target-on
." After target-on" cr
hex
410000 constant T-DSP-INIT
." After constants" cr
bye
