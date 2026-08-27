\ host-ext.fs - Host Extensions needed for Cross Compiler

\ Removed: 2drop, nip, tuck (now primitives in main.c)

: 3drop ( a b c -- )
    drop drop drop ;

: 3dup ( a b c -- a b c a b c )
    2 pick 2 pick 2 pick ;
    
: 2over ( a b c d -- a b c d a b )
    3 pick 3 pick ;

: 1+ 1 + ;
: 1- 1 - ;

: 0= ( n -- flag ) 0 = ;
: <> ( a b -- flag ) = 0= ;
: 0<> ( n -- flag ) 0 <> ;

: cells ( n -- n*8 ) 8 * ;

\ Helper for counted loops using BEGIN/UNTIL
\ ( limit start -- )

: host-string= ( addr1 len1 addr2 len2 -- bool )
    \ Stack: a1 l1 a2 l2
    >r swap r@ <> if
        \ Lengths differ
        r> drop 2drop 0 exit
    then
    \ Lengths equal. Stack: a1 a2 ( l2 is on R-stack )
    
    begin
        r@ 0 >
        if
            over c@ over c@ <> if
                r> drop 2drop 0 exit
            then
            1+ swap 1+ swap \ Increment addrs
            r> 1- >r \ Decrement len
            0 \ Continue
        else
            -1 \ Done
        then
    until
    r> drop 2drop -1 
    ;

: s, ( addr len -- )
    over + swap 
    begin
        2dup >
        if
            dup c@ c,
            1+ 0
        else
            1
        then
    until
    2drop ;

: l! ( val addr -- )
    >r
    dup r@ c!
    dup 8 rshift r@ 1+ c!
    dup 16 rshift r@ 2 + c!
    24 rshift r> 3 + c! ;