\ tests/test_parser_prims.fs

include core/prelude.fs
include core/host-ext.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/cross.fs

target-on
target-base @ target-dp !
hex
78 allot 
t-vhere constant T-LATEST-VAR 0 8,

\ --- Helpers ---
: resolve ( addr -- ) here over - 1 - swap c! ;

\ --- Primitives ---
t-code EXIT   c3 c, t-end-code

t-code DROP ( n -- )
    sub-rdi-8 mov-rax-rdi c3 c, 
t-end-code

t-code DUP ( n -- n n )
    mov-rax-tos add-rdi-8 mov-tos-rax mov-rax-rdi c3 c, 
t-end-code

t-code OVER ( a b -- a b a )
    mov-rax-nos add-rdi-8 mov-tos-rax mov-rax-rdi c3 c,
t-end-code

t-code 0= ( n -- bool )
    mov-rax-tos cmp-rax-0
    74 c, 05 c, \ JE +5
    48 c, 31 c, c0 c, \ XOR RAX, RAX
    eb c, 07 c, \ JMP +7
    48 c, c7 c, c0 c, ff ff ff ff c, \ MOV RAX, -1
    mov-tos-rax mov-rax-rdi c3 c,
t-end-code

t-code TYPE ( addr len -- )
    asm-push-rdi
    48 c, 8b c, 57 c, f8 c, \ RDX = [RDI-8] (len)
    48 c, 8b c, 77 c, f0 c, \ RSI = [RDI-16] (addr)
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c, \ RDI = 1
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c, \ RAX = 1
    syscall
    asm-pop-rdi sub-rdi-8 sub-rdi-8 mov-rax-rdi c3 c,
t-end-code

t-vhere constant HELLO-ADDR
s" Hello" s,

variable real-entry-point

t-code START
    t-vhere real-entry-point !
    48 c, c7 c, c7 c, 00 c, 00 c, 41 c, 00 c, \ MOV RDI, 410000 (DSP)
    48 c, bc c, 420000 8,                     \ MOV RSP, 420000

    \ Test TYPE
    HELLO-ADDR t-lit
    5 t-lit
    t-call TYPE

    \ Exit
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    syscall
t-end-code

\ --- Final Patching ---
decimal
target-latest @ T-LATEST-VAR virt>host !
real-entry-point @ constant ENTRY-POINT

hex
here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 1000000 elf-header
target-base @ BIN-SIZE + target-dp !

s" test_prims.bin" save-elf
target-off
bye
