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

    48 c, c7 c, c7 c, 410000 4, 
    48 c, bc c, 420000 8, 
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, 
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, 
    syscall

t-code EXIT
    c3 c,
t-end-code

: test ( -- )
    s" EXIT" t-find
    ." Result of t-find:" cr
    ." Top: " dup . cr
    dup if
        ." Success!" cr
        ." Second: " over . cr
        ." Third: " 2 pick . cr
    else
        ." Not found" cr
    then
    ;

test

bye
