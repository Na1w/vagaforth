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

\ --- Entry Point (_start) ---
t-vhere constant ENTRY-POINT

    \ Init Data Stack Pointer (RDI)
    48 c, c7 c, c7 c, 
    T-DSP-INIT 4, \ MOV RDI, 0x410000

    \ Init Return Stack Pointer (RSP)
    48 c, bc c, 
    T-RSP-INIT 8, \ MOV RSP, 0x420000

    \ Call Main Word (COLD)
    t-vhere constant COLD-CALL-ADDR
    e8 c, 00 00 00 00 4, \ CALL 0 (Will patch later)

    \ Exit(0)
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, \ RDI=0
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, \ RAX=60
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

\ --- High Level Logic (COLD) ---

t-code COLD
    parse-name DUP t-find 
    if 
        drop 
        t-call 
    else
        2drop ." Error: DUP not found!" cr
    then
    
    hex
    here target-base @ - ELF-ORIGIN + 
    t-vhere 4 + -
    e8 c, 4,
    
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
