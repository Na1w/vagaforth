include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs

target-on

hex
410000 constant T-DSP-INIT
420000 constant T-RSP-INIT
430000 constant T-HERE-INIT

." Testing t-code..." cr

t-code EXIT
    c3 c,
t-end-code

." EXIT defined" cr
bye
