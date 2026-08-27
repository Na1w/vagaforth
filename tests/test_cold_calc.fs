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
constant CODE-START

    \ Init DSP (7 bytes)
    48 c, c7 c, c7 c, 00 c, 00 c, 41 c, 00 c,
    \ Init RSP (10 bytes)
    48 c, bc c, 00 c, 00 c, 42 c, 00 c, 00 c, 00 c, 00 c,
    
    \ Här är CALL (5 bytes). Vi fyller i offset senare.
    t-vhere constant CALL-ADDR
    e8 c, 00 c, 00 c, 00 c, 00 c,
    
    \ Exit(0) (7+7+2=16 bytes)
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    syscall

t-vhere constant COLD-ADDR

\ Beräkna offset: COLD - (CALL + 5)
COLD-ADDR CALL-ADDR 5 + - constant CALL-OFFSET

." CODE-START: " CODE-START . cr
." CALL-ADDR: " CALL-ADDR . cr  
." COLD-ADDR: " COLD-ADDR . cr
." CALL-OFFSET: " CALL-OFFSET . cr

\ Patcha CALL-instruktionen
target-off
call-offset call-addr 1+ !
target-on

\ Nu definiera COLD
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
