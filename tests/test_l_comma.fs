include core/prelude.fs
include core/host-ext.fs

: l! ( n addr -- )
   over 0xff and over c! 1+
   swap 8 rshift swap
   over 0xff and over c! 1+
   swap 8 rshift swap
   over 0xff and over c! 1+
   swap 8 rshift swap
   0xff and swap c! ;

create test-buf 8 allot

hex
12345678 test-buf l!

test-buf c@ .
test-buf 1+ c@ .
test-buf 2 + c@ .
test-buf 3 + c@ .
cr

bye
