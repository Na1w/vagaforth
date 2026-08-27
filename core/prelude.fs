\ prelude.fs - Bootstrap environment

\ --- Target Memory ---
variable target-base
variable target-dp
variable emitting-to-target 
0 emitting-to-target !

\ Initiera target minne (100KB)
(here) target-base !
102400 (allot)
target-base @ target-dp !

\ --- Wrappers ---

: here 
    emitting-to-target @ if target-dp @ else (here) then ;

: allot 
    emitting-to-target @ if target-dp +! else (allot) then ;

: , 
    emitting-to-target @ if 
        target-dp @ ! 8 target-dp +! 
    else 
        (,) 
    then ;

: c, 
    emitting-to-target @ if 
        target-dp @ c! 1 target-dp +! 
    else 
        (c,) 
    then ;

: align
    here 7 + -8 and here - allot ;

\ --- Mode Switch ---
: target-on  1 emitting-to-target ! ;
: target-off 0 emitting-to-target ! ;

hex
: 2, ( n -- )
    dup c,
    8 rshift c,
    ;

: 4, ( n -- )
    dup c,
    dup 08 rshift c,
    dup 10 rshift c,
    18 rshift c, 
    ;

: 8, ( n -- )
    dup c,
    dup 08 rshift c,
    dup 10 rshift c,
    dup 18 rshift c,
    dup 20 rshift c, 
    dup 28 rshift c, 
    dup 30 rshift c, 
    38 rshift c, 
    ;
decimal

." Prelude loaded. Memory abstracted." cr
