\ test_redef.fs

: hilbert ." Gamla Hilbert" cr ;

: run-test 
    ." Anropar Hilbert inifrån run-test: " 
    hilbert \ Detta kompilerar adressen till Gamla Hilbert
    ;

\ Nu omdefinierar vi Hilbert
: hilbert ." NYA Hilbert" cr ;

." 1. Kör Hilbert direkt: " hilbert
." 2. Kör run-test:       " run-test

bye
