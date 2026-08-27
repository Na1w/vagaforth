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

: cr 10 emit ;

\ Reservera plats för ELF header
78 allot

\ --- Entry Point (_start) ---
t-vhere constant ENTRY-POINT

    \ Init Data Stack Pointer (RDI)
    48 c, c7 c, c7 c, T-DSP-INIT 4, 

    \ Init Return Stack Pointer (RSP)
    48 c, bc c, T-RSP-INIT 8, 

    \ Call COLD
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

t-code DUP
    mov-rax-tos
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

t-code DROP
    sub-rdi-8
    mov-rax-rdi
    c3 c,
t-end-code


\ --- COLD: Simple test that pushes a value and exits ---

t-code COLD
    \ Push 42 on stack
    48 c, c7 c, c7 c, 2a c, 00 c, 00 c, 00 c,  \ RDI = 42 (fusk)
    
    \ We need to properly push on the Forth stack
    \ ADD RDI, 8  (grows upward)
    48 c, 83 c, c7 c, 08 c,
    \ MOV [RDI-8], 42
    48 c, c7 c, 47 c, f8 c, 2a c, 00 c, 00 c, 00 c,
    
    \ Now exit with that value
    48 c, 8b c, 7f c, f8 c,  \ MOV RDI, [RDI-8] (TOS)
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,  \ RAX = 60
    syscall
    
t-end-code


\ --- Patch Entry Point ---
decimal
: patch-cold ( -- )
    s" COLD" t-find 0= if 
        ." Error: Could not find COLD" cr 
        bye 
    then
    drop 
    constant COLD-ADDR
    
    ." COLD Address: " COLD-ADDR . cr
    
    COLD-ADDR COLD-CALL-ADDR 5 + - 
    COLD-CALL-ADDR 1+ virt>host !
    ;

patch-cold


\ --- Save Binary ---
here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 100000 elf-header
target-base @ BIN-SIZE + target-dp !

s" vagaforth.bin" save-elf

target-off
." Kernel build complete." cr
bye
