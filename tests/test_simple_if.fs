include core/prelude.fs

: cr 10 emit ;

: test-if ( n -- )
    dup 0 = if 42 emit cr else 45 emit cr then
    drop
    ;

." Calling test-if with 0:" cr
0 test-if

." Calling test-if with 1:" cr
1 test-if

bye
