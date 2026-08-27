include core/prelude.fs
include core/host-ext.fs

: test-if ( n -- )
    if ."  is TRUE" cr else ."  is FALSE" cr then ;

: test-<> ( a b -- )
    2dup . ." <> " dup .
    <> if ."  is TRUE" cr else ."  is FALSE" cr then ;

: main
    ." Testing IF:" cr
    1 dup . test-if
    0 dup . test-if
    -1 dup . test-if

    ." Testing <>:" cr
    1 0 test-<>
    1 1 test-<>
    ;

main
bye
