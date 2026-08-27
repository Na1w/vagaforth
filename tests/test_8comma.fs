include core/prelude.fs

hex

: cr 10 emit ;

." Testing 8," cr

here 
400078 8,
here swap - . cr

here 8 - c@ . cr
here 7 - c@ . cr
here 6 - c@ . cr
here 5 - c@ . cr
here 4 - c@ . cr
here 3 - c@ . cr
here 2 - c@ . cr
here 1 - c@ . cr

bye
