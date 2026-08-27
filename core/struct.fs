\ struct.fs - Structure support
include core/does.fs

\ Align offset to a power of 2
: align-to ( offset boundary -- aligned-offset )
    1- tuck + swap negate and ;

: struct ( -- 0 ) 0 ;

: field ( offset size "name" -- next-offset )
    create 
        over , \ Spara nuvarande offset
        +      \ Beräkna nästa offset
    DOES> 
        @ + ;  \ Addera offset till basadressen

\ Helpers for common types
: cell% 8 ;
: int%  4 ;
: char% 1 ;
: ptr%  8 ;

\ Aligned fields (aligns offset to size before defining)
: a-field ( offset size "name" -- next-offset )
    over over align-to \ Justera offset
    swap field ;       \ Definiera fältet

: end-struct ( offset "name" -- )
    constant ;

." Struct support loaded." cr
