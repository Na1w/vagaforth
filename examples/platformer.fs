40 constant MAP-W
12 constant MAP-H
480 constant MAP-SIZE
21505 constant TCGETS
21506 constant TCSETS
3 constant F_GETFL
4 constant F_SETFL
2048 constant O_NONBLOCK

create map-data MAP-SIZE allot
create orig-termios 64 allot
create raw-termios 64 allot
create sleep-req 16 allot
create sleep-rem 16 allot
create in-buf 8 allot

variable px
variable py
variable hp
variable coins
variable total-coins
variable game-won
variable game-quit
variable jump-timer
variable enemy-x
variable enemy-y
variable enemy-dir
variable tick-count
variable action-msg
variable orig-flags

: esc 27 EMIT ;
: c-reset esc ." [0m" ;
: c-bold esc ." [1m" ;
: c-red esc ." [1;31m" ;
: c-green esc ." [1;32m" ;
: c-yellow esc ." [1;33m" ;
: c-blue esc ." [1;34m" ;
: c-magenta esc ." [1;35m" ;
: c-cyan esc ." [1;36m" ;
: c-white esc ." [1;37m" ;
: c-gray esc ." [0;90m" ;
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

variable c-x
variable c-y
: map-idx
    MAP-W * +
    ;

: get-cell
    map-idx map-data + c@
    ;

: set-cell
    map-idx map-data + c!
    ;

: is-solid
    get-cell
    dup 35 = swap 61 = + if 1 else 0 then
    ;

: is-spike
    get-cell 94 = if 1 else 0 then
    ;

: is-coin
    get-cell 42 = if 1 else 0 then
    ;

: is-exit
    get-cell 69 = if 1 else 0 then
    ;

variable line-y
variable line-x
variable cell-ch

: draw-map
    0 line-y !
    begin
        line-y @ MAP-H < if
            0 line-x !
            begin
                line-x @ MAP-W < if
                    line-x @ px @ = line-y @ py @ = and if
                        c-yellow c-bold ." @" c-reset
                    else
                        line-x @ enemy-x @ = line-y @ enemy-y @ = and if
                            c-magenta c-bold ." M" c-reset
                        else
                            line-x @ line-y @ get-cell cell-ch !
                            cell-ch @ 35 = if c-white ." #" c-reset then
                            cell-ch @ 61 = if c-cyan ." =" c-reset then
                            cell-ch @ 42 = if c-yellow c-bold ." *" c-reset then
                            cell-ch @ 94 = if c-red c-bold ." ^" c-reset then
                            cell-ch @ 69 = if c-green c-bold ." E" c-reset then
                            cell-ch @ 32 = if space then
                        then
                    then
                    line-x @ 1+ line-x !
                    0
                else
                    1
                then
            until
            cr
            line-y @ 1+ line-y !
            0
        else
            1
        then
    until
    ;

: init-level
    map-data MAP-SIZE 32 fill

    0 line-x !
    begin
        line-x @ MAP-W < if
            35 line-x @ 0 set-cell
            35 line-x @ 11 set-cell
            line-x @ 1+ line-x !
            0
        else 1 then
    until

    0 line-y !
    begin
        line-y @ MAP-H < if
            35 0 line-y @ set-cell
            35 39 line-y @ set-cell
            line-y @ 1+ line-y !
            0
        else 1 then
    until

    61 6 8 set-cell  61 7 8 set-cell  61 8 8 set-cell  61 9 8 set-cell  61 10 8 set-cell
    61 15 6 set-cell 61 16 6 set-cell 61 17 6 set-cell 61 18 6 set-cell
    61 24 7 set-cell 61 25 7 set-cell 61 26 7 set-cell 61 27 7 set-cell
    61 28 4 set-cell 61 29 4 set-cell 61 30 4 set-cell 61 31 4 set-cell 61 32 4 set-cell
    35 3 4 set-cell  35 4 4 set-cell  35 5 4 set-cell

    94 13 10 set-cell 94 14 10 set-cell 94 15 10 set-cell
    94 16 10 set-cell 94 17 10 set-cell 94 18 10 set-cell
    94 19 10 set-cell 94 20 10 set-cell

    42 8 7 set-cell
    42 16 5 set-cell
    42 25 6 set-cell
    42 29 3 set-cell
    42 31 3 set-cell

    69 4 3 set-cell

    3 px !
    10 py !
    3 hp !
    0 coins !
    5 total-coins !
    0 game-won !
    0 game-quit !
    0 jump-timer !
    24 enemy-x !
    6 enemy-y !
    1 enemy-dir !
    0 tick-count !
    0 action-msg !
    ;

: check-player-collisions
    px @ py @ is-coin if
        coins @ 1+ coins !
        32 px @ py @ set-cell
        1 action-msg !
    then

    px @ py @ is-spike if
        hp @ 1- hp !
        3 px ! 10 py ! 0 jump-timer !
        2 action-msg !
    then

    px @ enemy-x @ = py @ enemy-y @ = and if
        hp @ 1- hp !
        3 px ! 10 py ! 0 jump-timer !
        4 action-msg !
    then

    px @ py @ is-exit if
        coins @ total-coins @ = if
            1 game-won !
        else
            3 action-msg !
        then
    then
    ;

: update-physics
    jump-timer @ 0 > if
        py @ 1- 0 > if
            px @ py @ 1- is-solid 0= if
                py @ 1- py !
            else
                0 jump-timer !
            then
        else
            0 jump-timer !
        then
        jump-timer @ 1- jump-timer !
    else
        py @ 10 < if
            px @ py @ 1+ is-solid 0= if
                py @ 1+ py !
            then
        then
    then
    check-player-collisions
    ;

variable enemy-tick
: update-enemy
    enemy-tick @ 1+ enemy-tick !
    enemy-tick @ 3 > if
        0 enemy-tick !
        enemy-dir @ 1 = if
            enemy-x @ 1+ 28 < if
                enemy-x @ 1+ enemy-x !
            else
                0 enemy-dir !
            then
        else
            enemy-x @ 1- 23 > if
                enemy-x @ 1- enemy-x !
            else
                1 enemy-dir !
            then
        then
    then
    check-player-collisions
    ;

variable cur-key
: handle-input
    dup 96 > over 123 < and if 32 - then
    cur-key !

    cur-key @ 65 = if
        px @ 1- 0 > if
            px @ 1- py @ is-solid 0= if
                px @ 1- px !
            then
        then
    then

    cur-key @ 68 = if
        px @ 1+ 39 < if
            px @ 1+ py @ is-solid 0= if
                px @ 1+ px !
            then
        then
    then

    cur-key @ 87 = cur-key @ 32 = + if
        py @ 1+ 12 < if
            px @ py @ 1+ is-solid if
                4 jump-timer !
            then
        then
    then

    cur-key @ 82 = if init-level then
    cur-key @ 88 = cur-key @ 81 = + if 1 game-quit ! then
    check-player-collisions
    ;

: print-hud
    c-cyan
    ." +------------------------------------------------------+" cr
    c-reset
    ."  HP: "
    hp @ 3 = if c-red ." [♥ ♥ ♥] " c-reset then
    hp @ 2 = if c-red ." [♥ ♥ ♡] " c-reset then
    hp @ 1 = if c-red ." [♥ ♡ ♡] " c-reset then
    hp @ 0 = if c-gray ." [☠ ☠ ☠] " c-reset then

    ." | COINS: " c-yellow coins @ . ." / " total-coins @ . c-reset
    ." | EXIT: "
    coins @ total-coins @ = if
        c-green c-bold ." [OPEN]" c-reset
    else
        c-red ." [LOCKED]" c-reset
    then
    cr
    c-cyan
    ." +------------------------------------------------------+" cr
    c-reset
    action-msg @ 1 = if c-yellow ." >> [*] Gold Coin collected!             " cr c-reset then
    action-msg @ 2 = if c-red ." >> [!] OUCH! Spikes! Respawned at start.  " cr c-reset then
    action-msg @ 3 = if c-red ." >> [!] Exit locked! Need 5 coins.         " cr c-reset then
    action-msg @ 4 = if c-magenta ." >> [!] HIT BY GOBLIN! Respawned!         " cr c-reset then
    action-msg @ 0= if ."                                              " cr then
    0 action-msg !
    ;

: render
    cursor-home
    c-cyan
    ." +------------------------------------------------------+" cr
    ." |                     FORTH RUNNER                     |" cr
    ." +------------------------------------------------------+" cr
    c-reset
    draw-map
    print-hud
    c-yellow
    ." Controls: [A]=Left  [D]=Right  [W/Space]=Jump           " cr
    ."           [R]=Restart  [X/Q]=Exit Game                  " cr
    c-reset
    ;

variable raw-k

: game-loop
    raw-mode-on
    cls
    begin
        render
        poll-key raw-k !
        raw-k @ 0 > if
            raw-k @ handle-input
        then
        update-physics
        update-enemy
        tick-count @ 1+ tick-count !
        50 msleep
        hp @ 0= game-won @ + game-quit @ + if 1 else 0 then
    until
    raw-mode-off
    ;

: play-platformer
    init-level
    game-loop

    cls
    game-won @ if
        c-green c-bold
        ." ******************************************************" cr
        ." *                                                    *" cr
        ." *          VICTORY! YOU CLEARED FORTH RUNNER!        *" cr
        ." *     All coins collected and escaped the dungeon!   *" cr
        ." *                                                    *" cr
        ." ******************************************************" cr
        c-reset
    else
        hp @ 0= if
            c-red c-bold
            ." ======================================================" cr
            ."                   GAME OVER                          " cr
            ."         You ran out of lives in the dungeon!         " cr
            ." ======================================================" cr
            c-reset
        else
            c-gray ." You exited the game. See you next run!" cr c-reset
        then
    then
    cr
    ;

create entry-nm 112 c, 108 c, 97 c, 121 c, 45 c, 112 c, 108 c, 97 c, 116 c, 102 c, 111 c, 114 c, 109 c, 101 c, 114 c, 0 c,
entry-nm 15 s" platformer.bin" save-elf-at
