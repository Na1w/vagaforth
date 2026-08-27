\ test_does.fs
include core/prelude.fs
include core/host-ext.fs
include core/does.fs

\ Helpers
: cells 8 * ;

." Defining CONSTANT..." cr
: MY-CONSTANT ( n -- )
    create , 
    DOES> @ ;

123 MY-CONSTANT VAL
." VAL should be 123: " VAL . cr

." Defining ARRAY..." cr
: ARRAY ( size -- )
    create
    cells allot
    DOES> ( index base -- addr )
    swap cells + ;

5 ARRAY my-array
11 0 my-array !
22 1 my-array !

." my-array[0] (11): " 0 my-array @ . cr
." my-array[1] (22): " 1 my-array @ . cr

bye
