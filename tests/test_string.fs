include core/prelude.fs
include core/host-ext.fs

: test ( addr1 len1 addr2 len4 -- )
    string= if ."  - match" cr else ."  - no match" cr then ;

: main
    ." Testing string=..." cr
    s" hello" s" hello" 2over type test
    s" hello" s" world" 2over type test
    s" hi" s" hello" 2over type test
    ;

main
bye