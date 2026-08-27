\ test.fs - VagaForth test library

: cr 10 emit ; \ Newline

: space 32 emit ; \ Space

: square ( n -- n*n )
    dup * ;

: cube ( n -- n*n*n )
    dup square * ;

: .s ." [ " dup . ." ]" ; \ Enkel visning av toppen (vi har redan prompten men kul ändå)

\ En loop som räknar från 0 till n-1
: count-to ( n -- )
    0 begin
        dup . space
        1 +
        over over =
    until
    drop drop
    cr ;

\ En enkel villkorstest
: fizzbuzz-check ( n -- )
    dup 3 = if ." Three!" else ." Not three!" then cr drop ;

." --- test.fs loaded ---" cr
." Try: 5 cube ." cr
." Try: 10 count-to" cr
