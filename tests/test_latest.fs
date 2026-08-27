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

\ Reservera plats för ELF header
78 allot

\ --- Entry Point (_start) ---
t-vhere constant ENTRY-POINT

    48 c, c7 c, c7 c, 
    410000 4, 
    48 c, bc c, 
    420000 8, 
    e8 c, 00 00 00 00 4, 
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, 
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, 
    syscall

\ --- Primitives ---

t-code EXIT
    c3 c,
t-end-code

." After EXIT:" cr
." target-latest: " target-latest @ . cr

t-code DUP
    mov-rax-tos
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

." After DUP:" cr
." target-latest: " target-latest @ . cr

\ Försök hitta EXIT
s" EXIT" 
t-find if
    ." Found EXIT at: " . cr
    drop
else
    ." EXIT not found" cr
    drop drop

bye
