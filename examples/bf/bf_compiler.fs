create bf-tape 30000 allot
create bf-code 65536 allot
create bf-src 30000 allot
create bf-loop-stack 800 allot

variable bf-dp
variable bf-src-len
variable bf-loop-sp
variable open-fwd-addr
variable rel-fwd
variable bf-ip
variable bf-end
variable bf-cur-c
variable in-ch
variable menu-ch
variable menu-loop

: esc 27 EMIT ;
: c-reset esc ." [0m" ;
: c-bold esc ." [1m" ;
: c-red esc ." [1;31m" ;
: c-green esc ." [1;32m" ;
: c-yellow esc ." [1;33m" ;
: c-cyan esc ." [1;36m" ;
: c-gray esc ." [0;90m" ;

: bf-push
    bf-loop-stack bf-loop-sp @ + !
    bf-loop-sp @ 8 + bf-loop-sp !
    ;

: bf-pop
    bf-loop-sp @ 8 - bf-loop-sp !
    bf-loop-stack bf-loop-sp @ + @
    ;

: bf-c,
    bf-dp @ c!
    bf-dp @ 1+ bf-dp !
    ;

: 4comma
    dup bf-c,
    dup 8 rshift bf-c,
    dup 16 rshift bf-c,
    24 rshift bf-c,
    ;

: 8comma
    dup 4comma
    32 rshift 4comma
    ;

: emit-init
    87 bf-c,
    83 bf-c,
    72 bf-c, 187 bf-c, bf-tape 8comma
    ;

: emit-ret
    91 bf-c,
    95 bf-c,
    72 bf-c, 137 bf-c, 248 bf-c,
    195 bf-c,
    ;

: emit-inc-ptr 72 bf-c, 255 bf-c, 195 bf-c, ;
: emit-dec-ptr 72 bf-c, 255 bf-c, 203 bf-c, ;
: emit-inc-val 254 bf-c, 3 bf-c, ;
: emit-dec-val 254 bf-c, 11 bf-c, ;

: emit-dot
    72 bf-c, 199 bf-c, 199 bf-c, 1 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    72 bf-c, 137 bf-c, 222 bf-c,
    72 bf-c, 199 bf-c, 194 bf-c, 1 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    72 bf-c, 199 bf-c, 192 bf-c, 1 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    15 bf-c, 5 bf-c,
    ;

: emit-comma
    72 bf-c, 199 bf-c, 199 bf-c, 0 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    72 bf-c, 137 bf-c, 222 bf-c,
    72 bf-c, 199 bf-c, 194 bf-c, 1 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    72 bf-c, 199 bf-c, 192 bf-c, 0 bf-c, 0 bf-c, 0 bf-c, 0 bf-c,
    15 bf-c, 5 bf-c,
    ;

: emit-bracket-open
    128 bf-c, 59 bf-c, 0 bf-c,
    15 bf-c, 132 bf-c,
    bf-dp @ bf-push
    0 4comma
    ;

: emit-bracket-close
    bf-pop open-fwd-addr !
    128 bf-c, 59 bf-c, 0 bf-c,
    15 bf-c, 133 bf-c,
    open-fwd-addr @ 4 + bf-dp @ 4 + - 4comma
    bf-dp @ open-fwd-addr @ 4 + - rel-fwd !
    rel-fwd @ open-fwd-addr @ c!
    rel-fwd @ 8 rshift open-fwd-addr @ 1+ c!
    rel-fwd @ 16 rshift open-fwd-addr @ 2 + c!
    rel-fwd @ 24 rshift open-fwd-addr @ 3 + c!
    ;

: compile-bf
    0 bf-loop-sp !
    bf-tape 30000 0 fill
    bf-code bf-dp !
    emit-init
    bf-src bf-ip !
    bf-src bf-src-len @ + bf-end !
    begin
        bf-ip @ bf-end @ < if
            bf-ip @ c@ bf-cur-c !
            bf-ip @ 1+ bf-ip !
            bf-cur-c @ 62 = if emit-inc-ptr then
            bf-cur-c @ 60 = if emit-dec-ptr then
            bf-cur-c @ 43 = if emit-inc-val then
            bf-cur-c @ 45 = if emit-dec-val then
            bf-cur-c @ 46 = if emit-dot then
            bf-cur-c @ 44 = if emit-comma then
            bf-cur-c @ 91 = if emit-bracket-open then
            bf-cur-c @ 93 = if emit-bracket-close then
            0
        else
            1
        then
    until
    emit-ret
    ;

create hello-bf-str
43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 91 c, 62 c, 43 c, 43 c, 43 c, 43 c,
43 c, 43 c, 43 c, 62 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 62 c, 43 c,
43 c, 43 c, 62 c, 43 c, 60 c, 60 c, 60 c, 60 c, 45 c, 93 c, 62 c, 43 c, 43 c, 46 c, 62 c, 43 c,
46 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 46 c, 46 c, 43 c, 43 c, 43 c, 46 c, 62 c, 43 c,
43 c, 46 c, 60 c, 60 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c, 43 c,
43 c, 43 c, 43 c, 46 c, 62 c, 46 c, 43 c, 43 c, 43 c, 46 c, 45 c, 45 c, 45 c, 45 c, 45 c, 45 c,
46 c, 45 c, 45 c, 45 c, 45 c, 45 c, 45 c, 45 c, 45 c, 46 c, 62 c, 43 c, 46 c, 62 c, 46 c, 10 c,

create alpha-bf-str 200 allot
: init-alpha-bf
    alpha-bf-str 65 43 fill
    46 alpha-bf-str 65 + c!
    0
    begin
        dup 25 < if
            43 over 2 * alpha-bf-str + 66 + c!
            46 over 2 * alpha-bf-str + 67 + c!
            1+
            0
        else
            drop 1
        then
    until
    ;

: run-hello
    hello-bf-str bf-src 107 cmove
    107 bf-src-len !
    compile-bf
    cr
    c-green ." [JIT] Executing compiled Hello World machine code:" cr c-reset
    bf-code EXECUTE
    cr
    ;

: run-alpha
    init-alpha-bf
    alpha-bf-str bf-src 116 cmove
    116 bf-src-len !
    compile-bf
    cr
    c-green ." [JIT] Executing compiled Alphabet machine code:" cr c-reset
    bf-code EXECUTE
    cr
    ;

variable custom-len
: read-custom-bf
    0 custom-len !
    cr
    c-yellow ." Enter Brainfuck code (end with !): " c-reset
    begin
        KEY in-ch !
        in-ch @ 33 = if
            1
        else
            in-ch @ bf-src custom-len @ + c!
            custom-len @ 1+ custom-len !
            0
        then
    until
    custom-len @ bf-src-len !
    custom-len @ 0 > if
        compile-bf
        cr
        c-green ." [JIT] Execution Result:" cr c-reset
        bf-code EXECUTE
        cr
    else
        c-gray ." (No code entered)" cr c-reset
    then
    ;

: print-menu
    cr
    c-cyan
    ." +----------------------------------------------------------------------+" cr
    ." |             BRAINFUCK NATIVE JIT COMPILER (VagaForth x86-64)         |" cr
    ." +----------------------------------------------------------------------+" cr
    c-reset
    ."  1) Run 'Hello World!' via native JIT" cr
    ."  2) Run 'A-Z Alphabet Generator' via native JIT" cr
    ."  3) Enter custom Brainfuck code & execute JIT" cr
    ."  Q) Quit" cr
    c-cyan
    ." +----------------------------------------------------------------------+" cr
    c-reset
    ." Select option > "
    ;

: bf-main
    1 menu-loop !
    begin
        print-menu
        KEY menu-ch !
        cr
        menu-ch @ 49 = if run-hello then
        menu-ch @ 50 = if run-alpha then
        menu-ch @ 51 = if read-custom-bf then
        menu-ch @ 81 = menu-ch @ 113 = + if
            0 menu-loop !
            c-gray ." Exiting Brainfuck Compiler. Goodbye!" cr c-reset
        then
        menu-loop @ 0=
    until
    ;

create entry-nm 98 c, 102 c, 45 c, 109 c, 97 c, 105 c, 110 c, 0 c,
entry-nm 7 s" bf_compiler.bin" save-elf-at
