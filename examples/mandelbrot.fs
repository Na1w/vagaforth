\ ============================================================
\ mandelbrot.fs -- Interactive Mandelbrot-set explorer for VagaForth
\
\ Standalone native ELF (built via save-app). Renders the Mandelbrot
\ set with ANSI truecolor (24-bit) using signed 30.34 fixed-point
\ arithmetic (value = int * 2^30). Interactive zoom/pan/iteration
\ controls in raw terminal mode.
\
\ Build:  ./vagaforth_new.bin < examples/mandelbrot.fs
\ Run:    ./mandelbrot.bin
\ Test:   printf 'q' | ./mandelbrot.bin
\ ============================================================

\ ---------- Screen geometry ----------
80 constant W
40 constant H

\ ---------- Fixed-point constants (30.34) ----------
1073741824 constant FX-1    \ 1.0  = 2^30
4294967296 constant FX-4    \ 4.0  = 4 * 2^30

\ ---------- Words NOT in the target kernel (define ourselves) ----------
\ abs ( n -- |n| )
: abs dup 0 < if 0 swap - then ;
\ min ( a b -- min )
: min 2dup > if swap then drop ;
\ max ( a b -- max )
: max 2dup < if swap then drop ;
\ >= ( a b -- flag )  a >= b  ==  not ( a < b )
: >= < 0= ;

\ ---------- Fixed-point multiply / divide ----------
variable fx-sa
variable fx-sb
variable fx-aa
variable fx-ab

\ fxmul ( a b -- a*b/2^30 )  signed fixed-point multiply.
\ * is 64-bit signed multiply; rshift is LOGICAL (unsigned), so we
\ take abs of both operands, multiply, shift right 30, then re-apply
\ the sign explicitly.
: fxmul ( a b -- a*b/2^30 )
    fx-ab ! fx-aa !
    fx-aa @ 0 < fx-sa !
    fx-ab @ 0 < fx-sb !
    fx-aa @ abs fx-aa !
    fx-ab @ abs fx-ab !
    fx-aa @ fx-ab @ * 30 rshift
    fx-sa @ fx-sb @ xor if negate then
    ;

\ fxdiv ( a b -- a/b )  signed fixed-point divide = a*2^30 / b.
: fxdiv ( a b -- a/b )
    fx-ab ! fx-aa !
    fx-aa @ 0 < fx-sa !
    fx-ab @ 0 < fx-sb !
    fx-aa @ abs fx-aa !
    fx-ab @ abs fx-ab !
    fx-aa @ 30 lshift fx-ab @ /
    fx-sa @ fx-sb @ xor if negate then
    ;

\ ---------- Number printer (no trailing space) ----------
create num-buf 16 allot
variable num-pos
: emit-num ( n -- )
    dup 0 < if 45 EMIT negate then
    15 num-pos !
    begin
        dup 0 > if
            10 /mod
            swap 48 + num-buf num-pos @ + c!
            num-pos @ 1- num-pos !
            0
        else
            1
        then
    until
    drop
    num-pos @ 15 = if
        48 num-buf 15 + c!
        14 num-pos !
    then
    num-buf num-pos @ 1+ + 15 num-pos @ - type
    ;

\ print-fx ( n -- )  print a 30.34 fixed-point value as decimal.
: print-fx ( n -- )
    dup 0 < if 45 EMIT negate then
    dup 30 rshift emit-num
    46 EMIT
    dup 30 rshift 30 lshift - 1000000 * 30 rshift emit-num
    ;

\ ---------- Raw-mode terminal (copied verbatim from platformer.fs) ----------
21505 constant TCGETS
21506 constant TCSETS
3 constant F_GETFL
4 constant F_SETFL
2048 constant O_NONBLOCK

create orig-termios 64 allot
create raw-termios 64 allot
create sleep-req 16 allot
create sleep-rem 16 allot
create in-buf 8 allot
variable orig-flags

: esc 27 EMIT ;
: c-reset esc ." [0m" ;
: cls esc ." [2J" esc ." [H" ;
: cursor-home esc ." [H" ;
: hide-cursor esc ." [?25l" ;
: show-cursor esc ." [?25h" ;

: raw-mode-on
    orig-termios 64 0 fill
    0 TCGETS orig-termios sys-ioctl drop
    orig-termios raw-termios 64 cmove
    raw-termios 12 + @
    dup 2 and if 2 - then
    dup 8 and if 8 - then
    raw-termios 12 + !
    0 raw-termios 22 + c!
    0 raw-termios 23 + c!
    0 TCSETS raw-termios sys-ioctl drop
    0 F_GETFL 0 sys-fcntl orig-flags !
    0 F_SETFL orig-flags @ O_NONBLOCK + sys-fcntl drop
    hide-cursor
    ;

: raw-mode-off
    0 F_SETFL orig-flags @ sys-fcntl drop
    0 TCSETS orig-termios sys-ioctl drop
    show-cursor
    ;

: msleep
    1000000 *
    0 sleep-req !
    sleep-req 8 + !
    sleep-req sleep-rem sys-nanosleep drop
    ;

: poll-key
    0 in-buf 1 sys-read
    1 = if in-buf c@ else 0 then
    ;

\ ---------- Viewport state ----------
variable center-re
variable center-im
variable scale
variable max-iter
variable quit-flag

: init-view
    -536870912 center-re !   \ -0.5
    0 center-im !
    53687091 scale !         \ 0.05
    100 max-iter !
    0 quit-flag !
    ;

\ ---------- Mandelbrot iteration ----------
variable zre
variable zim
variable cre
variable cim
variable it
variable zre2
variable zim2
variable zreim

\ mandel-iter ( cre cim -- n )  returns iteration count; n == max-iter
\ means the point never escaped (inside the set).
: mandel-iter ( cre cim -- n )
    cim ! cre !
    0 zre ! 0 zim ! 0 it !
    begin
        zre @ zre @ fxmul zre2 !
        zim @ zim @ fxmul zim2 !
        zre2 @ zim2 @ + FX-4 > if
            it @
            1
        else
            zre @ zim @ fxmul zreim !
            zre2 @ zim2 @ - cre @ + zre !
            zreim @ 2 * cim @ + zim !
            it @ 1+ it !
            it @ max-iter @ >= if
                max-iter @
                1
            else
                0
            then
        then
    until
    ;

\ ---------- Color mapping (HSV-style gradient -> RGB) ----------
variable hue256
variable sector
variable frac

: color-for ( n -- r g b )
    dup max-iter @ >= if
        drop 0 0 0
    else
        dup 1536 * max-iter @ / hue256 !
        hue256 @ 256 / sector !
        hue256 @ 256 mod frac !
        drop
        sector @ 0 = if 255 frac @ 0 then
        sector @ 1 = if 255 frac @ - 255 0 then
        sector @ 2 = if 0 255 frac @ then
        sector @ 3 = if 0 255 frac @ - 255 then
        sector @ 4 = if frac @ 0 255 then
        sector @ 5 = if 255 0 255 frac @ - then
    then
    ;

\ truecolor ( r g b -- )  emit ESC[38;2;R;G;Bm
: truecolor ( r g b -- )
    27 EMIT ." [38;2;"
    emit-num 59 EMIT
    emit-num 59 EMIT
    emit-num
    109 EMIT
    ;

\ pixel ( cre cim -- )  render one colored block
: pixel ( cre cim -- )
    mandel-iter
    color-for
    truecolor
    219 EMIT
    ;

\ ---------- Rendering ----------
variable row
variable col

: render
    cursor-home
    0 row !
    begin
        row @ H < if
            0 col !
            begin
                col @ W < if
                    col @ W 2 / - scale @ * center-re @ + cre !
                    row @ H 2 / - scale @ * 2 * center-im @ + cim !
                    cre @ cim @ pixel
                    col @ 1+ col !
                    0
                else
                    1
                then
            until
            cr
            row @ 1+ row !
            0
        else
            1
        then
    until
    ;

: print-hud
    cr
    ." center: re=" center-re @ print-fx
    ."  im=" center-im @ print-fx
    ."  zoom=" scale @ print-fx
    ."  max-iter=" max-iter @ emit-num
    cr
    ." + / - zoom   w/a/s/d pan   i/o iter   r reset   q quit"
    cr
    ;

\ ---------- Input handling ----------
variable cur-key

: zoom-in
    scale @ 2 / dup 1 > if scale ! else drop then
    ;
: zoom-out
    scale @ 2 * dup FX-1 < if scale ! else drop then
    ;

: handle-input ( key -- )
    dup 96 > over 123 < and if 32 - then
    cur-key !
    cur-key @ 43 = if zoom-in then
    cur-key @ 61 = if zoom-in then
    cur-key @ 45 = if zoom-out then
    cur-key @ 87 = if center-im @ scale @ 8 * + center-im ! then
    cur-key @ 83 = if center-im @ scale @ 8 * - center-im ! then
    cur-key @ 65 = if center-re @ scale @ 4 * - center-re ! then
    cur-key @ 68 = if center-re @ scale @ 4 * + center-re ! then
    cur-key @ 73 = if max-iter @ 10 + max-iter ! then
    cur-key @ 79 = if max-iter @ 10 - max-iter ! then
    cur-key @ 82 = if init-view then
    cur-key @ 81 = if 1 quit-flag ! then
    ;

\ ---------- Main ----------
: mandelbrot-main
    init-view
    raw-mode-on
    cls
    begin
        render
        print-hud
        poll-key handle-input
        20 msleep
        quit-flag @ if 1 else 0 then
    until
    raw-mode-off
    cls
    ;

save-app mandelbrot-main mandelbrot.bin
