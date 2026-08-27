include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs

target-on

hex

: cr 10 emit ;

variable cold-addr

78 allot

t-vhere constant ENTRY-POINT

    48 c, c7 c, c7 c, 410000 4, 
    48 c, bc c, 420000 8, 
    e8 c, 00 00 00 00 4, 
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, 
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, 
    syscall

t-code EXIT
    c3 c,
t-end-code

t-code COLD
    48 c, c7 c, c7 c, 2a c, 00 c, 00 c, 00 c,  
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,  
    syscall
    
t-end-code

: find-cold ( -- )
    s" COLD" t-find
    ." t-find returned" cr
    ." Top of stack: " dup . cr
    if
        ." Found! virt-addr: " dup . cr
        ." flags: " . cr
        cold-addr !
        ." Stored in cold-addr" cr
    else
        ." Not found" cr
        drop drop
    then ;

find-cold
cold-addr @ ." cold-addr contains: " . cr

bye
