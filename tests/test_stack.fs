include core/prelude.fs
include core/host-ext.fs

target-on

hex

: cr 10 emit ;

: test ( -- )
    1234 0 swap
    ." After swap: " 
    dup . cr     \ Top
    dup . cr     \ Second
    ;

test

bye
