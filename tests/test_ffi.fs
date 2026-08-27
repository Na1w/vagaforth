include core/prelude.fs
include core/host-ext.fs
include core/ffi.fs

: main
    s" Hello from puts" puts
    s" ls -l Makefile" system drop ;

main
bye
