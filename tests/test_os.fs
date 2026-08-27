\ test_os.fs
." Loading extensions..." cr
include core/asm.fs
include core/os.fs

: test-write
    here 65 swap c! \ 'A'
    1 here 1 sys-write drop
    ;

test-write
0 sys-exit
