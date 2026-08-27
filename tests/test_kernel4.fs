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

t-code SWAP
    mov-rax-tos
    mov-rbx-nos
    mov-tos-rax
    48 c, 89 c, 5f c, f0 c,
    mov-rax-rdi
    c3 c,
t-end-code

t-code +
    mov-rax-tos
    mov-rbx-nos
    add-rax-rbx
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

t-code -
    mov-rax-tos
    mov-rbx-nos
    48 c, f7 c, d8 c,
    add-rax-rbx
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
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

t-code KEY
    add-rdi-8 
    asm-push-rdi
    48 c, 8b c, 34 c, 24 c,
    48 c, 83 c, ee c, 08 c,
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c2 c, 01 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 00 c, 00 c, 00 c, 00 c,
    syscall
    asm-pop-rdi
    mov-rax-rdi
    c3 c,
t-end-code

." Testing COLD..." cr

t-code COLD
    ." Inside COLD definition" cr
    here target-base @ - ELF-ORIGIN + constant LOOP-START
    ." LOOP-START defined" cr
    parse-name KEY t-find 
    ." KEY found" cr
    if 
        drop
        t-call 
    else
        2drop ." Error: KEY not found!" cr
    then
    
    parse-name EMIT t-find 
    if 
        drop 
        t-call 
    else
        2drop ." Error: EMIT not found!" cr
    then

    hex
    e9 c,
    LOOP-START t-vhere 4 + -
    4,
    
t-end-code

." COLD defined" cr
bye
