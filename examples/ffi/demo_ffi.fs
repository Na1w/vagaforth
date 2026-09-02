\ ==============================================================================
\ demo_ffi.fs - Demonstration of Native Dynamic Linking & FFI in VagaForth
\ ==============================================================================
\ This script demonstrates:
\ 1. Pure Forth dynamic loading of an ELF64 shared object (.so) via `dlopen`.
\ 2. Looking up exported C functions via ELF symbol table parsing with `dlsym`.
\ 3. Calling C functions with 1 to 6 arguments using `call1` .. `call6`.
\ 4. Passing strings and arrays between Forth and C shared libraries.
\ ==============================================================================

include addons/dynlink.fs

\ ANSI color helpers
: c-bold  27 EMIT ." [1m" ;
: c-green 27 EMIT ." [32m" ;
: c-cyan  27 EMIT ." [36m" ;
: c-reset 27 EMIT ." [0m" ;

\ String paths
create so-path 64 allot
s" examples/ffi/libdemo.so" so-path swap cmove

create sym-add 16 allot
s" demo_add" sym-add swap cmove

create sym-fact 16 allot
s" demo_fact" sym-fact swap cmove

create sym-sum6 16 allot
s" demo_sum6" sym-sum6 swap cmove

create sym-strlen 16 allot
s" demo_strlen" sym-strlen swap cmove

create sym-rev 16 allot
s" demo_reverse" sym-rev swap cmove

create sym-hash 16 allot
s" demo_hash_djb2" sym-hash swap cmove

create sym-arr 16 allot
s" demo_sum_array" sym-arr swap cmove

\ String and Array buffers
create str-test 64 allot
s" VagaForth Dynamic FFI" str-test swap cmove
0 str-test 21 + c!

create arr-test 40 allot
10 arr-test !
20 arr-test 8 + !
30 arr-test 16 + !
40 arr-test 24 + !
50 arr-test 32 + !

\ Load shared library and resolve symbols at load time
so-path 23 dlopen constant mylib

mylib sym-add 8 dlsym constant fn-add
mylib sym-fact 9 dlsym constant fn-fact
mylib sym-sum6 9 dlsym constant fn-sum6
mylib sym-strlen 11 dlsym constant fn-strlen
mylib sym-rev 12 dlsym constant fn-rev
mylib sym-hash 14 dlsym constant fn-hash
mylib sym-arr 14 dlsym constant fn-arr

: run-ffi-demo
    cr
    c-bold c-cyan ." ========================================================" cr
    ."   VagaForth Native ELF64 Dynamic Linker & FFI Demo" cr
    ." ========================================================" c-reset cr
    cr

    c-green ." [1] Successfully loaded 'examples/ffi/libdemo.so' into memory!" c-reset cr
    ."     Memory Base Address: 0x" mylib @ hex . decimal cr
    cr

    \ 2. Test basic arithmetic and factorial
    c-green ." [2] Testing C arithmetic & recursion:" c-reset cr
    ."     demo_add(100, 250) = " 100 250 fn-add call2 decimal . cr
    ."     demo_fact(10)      = " 10 fn-fact call1 decimal . cr
    cr

    \ 3. Test 6-argument call
    c-green ." [3] Testing 6-argument System V AMD64 function call:" c-reset cr
    ."     demo_sum6(1, 2, 3, 4, 5, 6) = " 1 2 3 4 5 6 fn-sum6 call6 decimal . cr
    cr

    \ 4. Test String processing
    c-green ." [4] Testing string length and in-place reversal in C:" c-reset cr
    ."     Original string: '" str-test 21 type ." '" cr
    ."     demo_strlen:     " str-test fn-strlen call1 decimal . cr
    str-test fn-rev call1 drop
    ."     Reversed in C:   '" str-test 21 type ." '" cr
    str-test fn-rev call1 drop \ reverse back
    cr

    \ 5. Test Cryptographic/Hashing Algorithm
    c-green ." [5] Testing DJB2 hash calculation in C:" c-reset cr
    ."     demo_hash_djb2('VagaForth Dynamic FFI') = 0x" str-test fn-hash call1 hex . decimal cr
    cr

    \ 6. Test Array processing
    c-green ." [6] Testing Array aggregation [10, 20, 30, 40, 50]:" c-reset cr
    ."     demo_sum_array(arr, 5) = " arr-test 5 fn-arr call2 decimal . cr
    cr

    c-bold c-cyan ." Dynamic FFI calls completed successfully!" c-reset cr
    cr
    ;

run-ffi-demo
bye
