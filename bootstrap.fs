: square dup * ;
: cube dup dup * * ;
: abs dup 0 swap < if else 0 swap - then ;
: min over over < if drop else swap drop then ;
: max over over < if swap drop else drop then ;
: emit-cr cr ;
: spaces dup 0 swap < if begin 32 EMIT 1- dup 0= until then drop ;
variable N
variable A
variable B
: fib N ! 0 1 B ! A ! 0 begin 1+ dup N @ > if else A @ B @ + B @ A ! B ! then dup N @ > until drop A @ ;
5 square . cr
3 cube . cr
5 abs . cr
-5 abs . cr
3 5 min . cr
3 5 max . cr
." stage-a " 5 square . cr
." stage-b " 3 spaces 7 square . cr
." stage-b-z " 0 spaces 65 EMIT cr
." fib6 " 6 fib . cr
