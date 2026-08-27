include core/prelude.fs
include core/host-ext.fs

target-on

hex

: cr 10 emit ;

: test ( -- )
    1 2
    ." Before swap: " over . dup . cr
    swap
    ." After swap: " over . dup . cr
    ;

test

bye
