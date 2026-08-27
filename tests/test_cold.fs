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

: cr 10 emit ;

\ Reservera plats för ELF header
78 allot

\ --- Entry Point (_start) ---
t-vhere constant ENTRY-POINT

    \ Init Data Stack Pointer (RDI)
    48 c, c7 c, c7 c, 
    T-DSP-INIT 4, 

    \ Init Return Stack Pointer (RSP)
    48 c, bc c, 
    T-RSP-INIT 8, 

    \ Call Main Word (COLD)
    t-vhere constant COLD-CALL-ADDR
    e8 c, 00 00 00 00 4, 

    \ Exit(0)
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, 
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, 
    syscall


\ --- Primitives ---

t-code EXIT
    c3 c,
t-end-code

t-code EMIT
    mov-rax-tos
    mov-rdi-rax
    asm-push-rdi 
    48 c, 8b c, 34 c, 24 c,
    48 c, 83 c, ee c, 08 c,
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c2 c, 01 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,
    syscall
    asm-pop-rdi
    sub-rdi-8
    mov-rax-rdi
    c3 c,
t-end-code


\ --- COLD ---

t-code COLD
    \ Just print 'X' and exit
    58 t-lit   \ Push 'X' (0x58)
    
    s" EMIT" drop  
    target-latest @ 
    begin
        dup 0 <>
    while
        dup virt>host 8 + dup c@ 
        rot swap 
        5 pick 5 pick 
        string= if
            drop
            dup virt>host 8 + dup c@ 1+ + 7 + -8 and host>virt
            t-call
            exit
        then
        drop
        virt>host @
    repeat
    drop
    2drop
    
    3c t-lit   \ Push exit syscall number
    syscall
    
t-end-code


\ --- Patch Entry Point ---
decimal
." Patching Entry Point..." cr
s" COLD" 
t-find 0= if ." Error: Could not find COLD" cr bye then
drop 
constant COLD-ADDR

." COLD Address: " COLD-ADDR . cr

COLD-ADDR COLD-CALL-ADDR 5 + - 
COLD-CALL-ADDR 1+ virt>host !


\ --- Save Binary ---
here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 100000 elf-header
target-base @ BIN-SIZE + target-dp !

s" vagaforth.bin" save-elf

target-off
." Kernel build complete." cr
bye
