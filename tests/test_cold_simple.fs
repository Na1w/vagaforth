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

    \ Init DSP
    48 c, c7 c, c7 c, 00 c, 00 c, 41 c, 00 c,
    \ Init RSP  
    48 c, bc c, 00 c, 00 c, 42 c, 00 c, 00 c, 00 c, 00 c,
    \ Call COLD
    e8 c, 0a c, 00 c, 00 c, 00 c,  \ CALL offset 0x0a (10 bytes framåt)
    \ Exit(0)
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    syscall

\ COLD: Gör bara exit(88)
t-code COLD
    48 c, c7 c, c7 c, 58 c, 00 c, 00 c, 00 c,  \ RDI = 88
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,  \ RAX = 60
    syscall
    
t-end-code

here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 100000 elf-header
target-base @ BIN-SIZE + target-dp !

s" vagaforth.bin" save-elf

target-off
." Build complete." cr
bye
