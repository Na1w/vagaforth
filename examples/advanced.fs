\ advanced.fs VagaForth advanced examples
\ Uses only the confirmed target vocabulary. String literals inside colon
\ definitions are not reliable (they read from stdin at runtime), so we
\ build string constants at load time, reference them.

\ --- String constants (built at load time) ---
create fizz-str 70 c, 105 c, 122 c, 122 c,          \ "Fizz"
create buzz-str 66 c, 117 c, 122 c, 122 c,          \ "Buzz"
create fizzbuzz-str 70 c, 105 c, 122 c, 122 c, 66 c, 117 c, 122 c, 122 c,  \ "FizzBuzz"
create pos-str 112 c, 111 c, 115 c, 105 c, 116 c, 105 c, 118 c, 101 c,    \ "positive"
create neg-str 110 c, 101 c, 103 c, 97 c, 116 c, 105 c, 118 c, 101 c,     \ "negative"
create zero-str 122 c, 101 c, 114 c, 111 c,          \ "zero"
create small-str 115 c, 109 c, 97 c, 108 c, 108 c,  \ "small"
create big-str 98 c, 105 c, 103 c,                  \ "big"
create swap3-str 115 c, 119 c, 97 c, 112 c, 51 c, 58 c, 32 c,   \ "swap3: "
create sum-str 50 c, 100 c, 117 c, 112 c, 45 c, 115 c, 117 c, 109 c, 58 c, 32 c,  \ "2dup-sum: "
create nip-str 110 c, 105 c, 112 c, 45 c, 100 c, 101 c, 109 c, 111 c, 58 c, 32 c,  \ "nip-demo: "

\ --- [t-c3d4] remainder helper, n-factorial ---
\ remainder of n/m via repeated subtraction.
: mod ( n m -- rem )
    begin
        over over <
        if
            drop
            1
        else
            swap over - swap
            0
        then
    until ;

\ n-factorial via a loop.
: factorial ( n -- n! )
    1 swap
    begin
        dup 0=
        if
            drop
            1
        else
            swap over * swap
            1-
            0
        then
    until ;

\ --- [t-d4e5] fibonacci sequence ---
\ Iterative Fibonacci using three scratch cells.
create fib-a 8 allot drop
create fib-b 8 allot drop
create fib-n 8 allot drop
create fib-cnt 8 allot drop
: fib ( n -- nth )
    fib-n !
    0 fib-a !
    1 fib-b !
    0
    begin
        1+
        dup fib-n @ >
        if
            drop
            1
        else
            fib-a @ fib-b @ + fib-b @ fib-a ! fib-b !
            0
        then
    until
    fib-a @ ;

\ print fibonacci(0) .. fibonacci(n-1).
: fib-seq ( n -- )
    fib-cnt !
    0
    begin
        dup fib . space
        1+
        dup fib-cnt @ < 0=
    until
    drop cr ;

\ --- [t-e5f6] multiplication table, category ---
\ nested loops for an n x n table.
create tt-n 8 allot drop
create tt-i 8 allot drop
create tt-j 8 allot drop
: times-table ( n -- )
    tt-n !
    1 tt-i !
    begin
        tt-i @ tt-n @ > 0=
        if
            1 tt-j !
            begin
                tt-j @ tt-n @ > 0=
                if
                    tt-i @ tt-j @ * . space
                    tt-j @ 1+ tt-j !
                    0
                else
                    1
                then
            until
            cr
            tt-i @ 1+ tt-i !
            0
        else
            1
        then
    until ;

\ print category via comparisons.
: classify ( n -- )
    dup 0 =
    if
        zero-str 4 type cr
    else
        dup 0 >
        if
            dup 10 <
            if
                small-str 5 type cr
            else
                big-str 3 type cr
            then
        else
            neg-str 8 type cr
        then
    then
    drop ;

\ --- [t-f6g7] stack manipulation demos ---
\ reorder three values.
: swap3 ( a b c -- c b a )
    rot rot swap
    swap3-str 7 type
    . space . space . cr ;

\ duplicate two values, sum them.
: 2dup-sum ( a b -- a b a+b )
    2dup +
    sum-str 10 type
    . space . space . cr ;

\ keep only the second value.
: nip-demo ( a b -- b )
    nip
    nip-str 10 type
    . cr ;

\ --- [t-g7h8] fizz-buzz ---
\ print Fizz/Buzz/FizzBuzz or the number.
: fizzbuzz ( n -- )
    dup 15 mod 0 =
    if
        fizzbuzz-str 8 type cr
    else
        dup 5 mod 0 =
        if
            buzz-str 4 type cr
        else
            dup 3 mod 0 =
            if
                fizz-str 4 type cr
            else
                dup . cr
            then
        then
    then
    drop ;

\ run the check for 1..15.
: fizzbuzz-run ( -- )
    1
    begin
        dup fizzbuzz
        1+
        dup 15 >
    until
    drop ;
