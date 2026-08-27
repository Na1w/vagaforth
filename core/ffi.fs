\ ffi.fs - Foreign Function Interface

: libc-handle ( -- handle )
    0 2 dlopen ; \ Use RTLD_NOW

variable _libc
libc-handle _libc !

: c-sym ( addr len -- addr )
    drop _libc @ swap dlsym ;

s" puts" c-sym constant libc-puts
s" system" c-sym constant libc-system
s" getenv" c-sym constant libc-getenv

: puts ( addr len -- )
    drop libc-puts call1 drop ;

: system ( addr len -- res )
    drop libc-system call1 ;

: getenv ( addr len -- val )
    drop libc-getenv call1 ;

." FFI loaded." cr
