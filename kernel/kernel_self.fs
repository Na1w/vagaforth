variable dq
variable dr
variable dd
variable fa
variable fb
variable fi
variable ft
: square dup * ;
: cube dup dup * * ;
: negate 0 swap - ;
: abs dup 0 < if 0 swap - then ;
: min 2dup > if swap then drop ;
: max 2dup < if swap then drop ;
: divmod dd ! dq ! 0 dr ! begin dq @ dd @ < if 1 else dq @ dd @ - dq ! dr @ 1+ dr ! 0 then until dr @ dq @ ;
: spaces begin dup 0 > if space 1- then dup 0 = until drop ;
: fib fi ! 0 fa ! 1 fb ! begin fi @ 0 = if fa @ 1 else fa @ fb @ + ft ! fb @ fa ! ft @ fb ! fi @ 1- fi ! 0 then until ;
: demo 5 square . space 3 cube . space 10 3 divmod . . space 6 fib . cr ;
