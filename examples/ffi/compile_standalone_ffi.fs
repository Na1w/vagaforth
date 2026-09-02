include addons/dynlink.fs

create so-path 64 allot
s" examples/ffi/libdemo.so" so-path swap cmove

create sym-add 16 allot
s" demo_add" sym-add swap cmove

create sym-fact 16 allot
s" demo_fact" sym-fact swap cmove

create sym-sum6 16 allot
s" demo_sum6" sym-sum6 swap cmove

variable mylib
variable fn-add
variable fn-fact
variable fn-sum6

: ffi-app-main
    cr
    ." Standalone FFI Executable Running!" cr
    so-path 23 dlopen mylib !
    mylib @ 0= if
        ." Failed to open shared library!" cr exit
    then
    mylib @ sym-add 8 dlsym fn-add !
    mylib @ sym-fact 9 dlsym fn-fact !
    mylib @ sym-sum6 9 dlsym fn-sum6 !

    ." C add(40, 2) = " 40 2 fn-add @ call2 decimal . cr
    ." C fact(7)   = " 7 fn-fact @ call1 decimal . cr
    ." C sum6(1,2,3,4,5,6) = " 1 2 3 4 5 6 fn-sum6 @ call6 decimal . cr
    ." Done!" cr
    ;

save-app ffi-app-main ffi_app.bin
