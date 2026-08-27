\ tests/repro_fragility.fs

include core/prelude.fs
include core/host-ext.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/cross.fs

target-on
target-base @ target-dp !
hex
78 allot 
t-vhere constant T-LATEST-VAR 0 8,

\ Duplicate t-create but taking name from stack
: t-create-named ( name-addr name-len -- )
    t-align
    
    t-vhere >r               
    target-latest @ 8,       
    r> target-latest !       
    
    dup c,                   
    t-here swap dup allot    
    cmove                    
    
    t-align
    ;

\ Duplicate t-call but taking name from stack
: t-call-named ( name-addr name-len -- )
    t-find
    if \ ( flags virt-addr )
        swap drop \ virt-addr
        e8 c,
        t-vhere 4 + - 4,
    else
        ." Error: Word not found!" cr
    then
    ;

t-code dummy-word c3 c, t-end-code

: stress-redefine
    ." Redefining 'temp' 100 times..." cr
    100
    begin
        dup 0 >
        if
            s" temp" t-create-named
            c3 c, \ RET
            1- 0
        else
            1
        then
    until
    drop
    ." Done." cr
;

: stress-calls
    ." Creating word with 100 calls..." cr
    s" big-caller" t-create-named
    
    deadbeef \ Marker
    
    100
    begin
        dup 0 >
        if
            s" dummy-word" t-call-named
            1- 0
        else
            1
        then
    until
    drop
    
    dup deadbeef <> if
        ." LEAK DETECTED! TOS is: " . cr
        ." Expected: DEADBEEF" cr
    else
        ." Stack clean." cr
        drop
    then
    
    c3 c, 
    ." Done." cr
;

stress-redefine
stress-calls

bye
