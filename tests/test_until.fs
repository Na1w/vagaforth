include core/prelude.fs
include core/host-ext.fs

: test-loop ( -- )
    5
    begin
        dup . cr
        1-
        dup 0 = \ Loop until it is 0
    until
    drop
    42 emit cr
    ;

test-loop
bye
