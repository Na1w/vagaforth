include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs

target-on

hex

: cr 10 emit ;

78 allot

t-vhere constant ENTRY-POINT

    \ exit(42)
    48 c, c7 c, c7 c, 2a c, 00 c, 00 c, 00 c,  \ RDI = 42
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,  \ RAX = 60
    syscall

here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 100000 elf-header
target-base @ BIN-SIZE + target-dp !

s" vagaforth.bin" save-elf

target-off
." Build complete." cr
bye
