variable div-a
variable div-b
variable div-q

: /mod
    div-b ! div-a ! 0 div-q !
    begin
        div-a @ div-b @ < if
            1
        else
            div-a @ div-b @ - div-a !
            div-q @ 1+ div-q !
            0
        then
    until
    div-a @ div-q @
    ;

: mod /mod drop ;
: / /mod nip ;

variable rng-seed
variable urand-fd
create urand-path 47 c, 100 c, 101 c, 118 c, 47 c, 117 c, 114 c, 97 c, 110 c, 100 c, 111 c, 109 c, 0 c,

: init-random
    urand-path 0 0 0 sys-open urand-fd !
    urand-fd @ 0 > if
        urand-fd @ rng-seed 8 sys-read drop
        urand-fd @ sys-close drop
    then
    rng-seed @ 0 < if 0 rng-seed @ - rng-seed ! then
    rng-seed @ 0= if 12345 rng-seed ! then
    ;

: rand
    rng-seed @ 1103515245 * 12345 +
    dup rng-seed !
    16 rshift 32767 and
    ;

variable rng-max
: random-range
    rng-max !
    rand rng-max @ mod 1+
    ;

: add-entropy
    rng-seed @ 31 * + 17 + rng-seed !
    ;

variable num-acc
variable num-ch
variable has-digits

: read-num
    0 num-acc !
    0 has-digits !
    begin
        KEY num-ch !
        num-ch @ add-entropy
        num-ch @ 10 = num-ch @ 13 = + if
            has-digits @ if 1 else 0 then
        else
            num-ch @ 47 > num-ch @ 58 < and if
                1 has-digits !
                num-acc @ 10 * num-ch @ 48 - + num-acc !
            then
            0
        then
    until
    num-acc @
    ;

variable secret
variable guess
variable attempts
variable playing
variable choice-ch

: print-banner
    cr
    ." ========================================" cr
    ."    * GUESS THE SECRET NUMBER (1-100) * " cr
    ." ========================================" cr
    ." I have picked a random number (1..100)." cr
    ." Try to guess it in as few tries as possible!" cr
    cr
    ;

: print-rating
    attempts @ 5 < if
        ." Rating: LEGENDARY! Incredible intuition!" cr
    else
        attempts @ 8 < if
            ." Rating: EXCELLENT! Great strategy!" cr
        else
            attempts @ 12 < if
                ." Rating: GOOD JOB! Well played!" cr
            else
                ." Rating: YOU GOT IT! Practice makes perfect!" cr
            then
        then
    then
    ;

: play-round
    100 random-range secret !
    0 attempts !
    0 playing !
    print-banner
    begin
        ." Enter your guess: "
        read-num guess !
        attempts @ 1+ attempts !

        guess @ secret @ = if
            cr
            ." >>> CORRECT! The number was " secret @ . ." <<<" cr
            ." You solved it in " attempts @ . ." attempt(s)!" cr
            print-rating
            1 playing !
        else
            guess @ secret @ < if
                ." [-] Too LOW! Try a higher number." cr cr
            else
                ." [+] Too HIGH! Try a lower number." cr cr
            then
            0 playing !
        then
        playing @
    until
    ;

: ask-replay
    cr
    ." Would you like to play again? (y/n): "
    KEY choice-ch !
    choice-ch @ add-entropy
    choice-ch @ 121 = choice-ch @ 89 = + if
        cr
        1
    else
        cr
        ." Thanks for playing! Goodbye." cr
        0
    then
    ;

: game
    init-random
    cr
    ." Booting Guess Game..." cr
    begin
        play-round
        ask-replay 0=
    until
    ;

create entry-name 16 allot
s" game" entry-name swap cmove
entry-name 4 s" guess_game.bin" save-elf-at
