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

t-create TEST-WORD
c3 c,

: test-match ( -- )
    s" TEST-WORD" 
    target-latest @ 
    t-match?
    if
        ." Match!" cr
    else
        ." No match" cr
    then
    ;

test-match

bye
