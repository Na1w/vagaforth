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

." TEST-WORD created" cr
." target-latest: " target-latest @ . cr

: do-find ( -- )
    parse-name TEST-WORD 
    t-find
    if
        ." Found! Virt: " . cr
    else
        ." Not found" cr
        drop drop
    then
    ;

do-find

bye
