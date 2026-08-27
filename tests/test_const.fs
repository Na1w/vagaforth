include core/prelude.fs
include core/host-ext.fs

target-on

hex

: cr 10 emit ;

78 allot

: test ( -- )
    1234 constant TEST-VAL
    ." TEST-VAL: " TEST-VAL . cr
    ;

test

bye
