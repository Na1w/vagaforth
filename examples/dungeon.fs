variable cur-room
variable has-key
variable has-sword
variable has-treasure
variable door-open
variable troll-alive
variable game-state
variable cmd-ch
variable last-action

variable in-char
variable first-char

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

: read-cmd
    0 first-char !
    begin
        KEY in-char !
        in-char @ 10 = in-char @ 13 = + if
            1
        else
            first-char @ 0= if
                in-char @ 32 > if
                    in-char @ first-char !
                then
            then
            0
        then
    until
    first-char @
    ;

: print-header
    c-cyan
    ." +----------------------------------------------------------------------+" cr
    ." |                THE DUNGEON OF VAGAFORTH (x86-64 Native)              |" cr
    ." +----------------------------------------------------------------------+" cr
    c-reset
    ;

: print-map
    c-yellow
    ." [ WORLD MAP ]" cr
    cur-room @ 2 = if
        c-white ."        [ CAVE (@) ]" cr
    else
        c-gray  ."        [   CAVE   ]" cr
    then
    c-gray ."              |        " cr
    cur-room @ 1 = if
        c-white ."   [CABIN(@)]-"
    else
        c-gray  ."   [ CABIN  ]-"
    then
    cur-room @ 0 = if
        c-white ." [CLEARING(@)]" cr
    else
        c-gray  ." [ CLEARING ]" cr
    then
    c-gray ."              |        " cr
    door-open @ if
        cur-room @ 3 = if
            c-white ."        [CATACOMB(@)]-"
        else
            c-gray  ."        [ CATACOMB ]-"
        then
        cur-room @ 4 = if
            c-white ." [TREASURY(@)]" cr
        else
            c-gray  ." [ TREASURY ]" cr
        then
    else
        c-red ."        [ (LOCKED) ]" cr
    then
    c-reset
    ;

: art-clearing
    c-green
    ."       /\\       /\\       /\\       " cr
    ."      /  \\     /  \\     /  \\      " cr
    ."     / /\\ \\   / /\\ \\   / /\\ \\     " cr
    ."    / /  \\ \\ / /  \\ \\ / /  \\ \\    " cr
    ."       ||       ||       ||       " cr
    c-reset
    ;

: art-cabin
    c-yellow
    ."             /\\                   " cr
    ."            /  \\    ___________   " cr
    ."           / /\\ \\  /          /\\  " cr
    ."          / ____ \\/__________/  \\ " cr
    ."          |  []  |   [][]   |   | " cr
    ."          |  __  |          |   | " cr
    c-reset
    ;

: art-cave
    c-gray
    ."          .------------------.    " cr
    ."         /    ____________    \\   " cr
    ."        /    /  ________  \\    \\  " cr
    ."       |    /  /  DARK  \\  \\    | " cr
    ."       |   |  |   CAVE   |  |   | " cr
    ."       |   |  |          |  |   | " cr
    c-reset
    ;

: art-dungeon
    troll-alive @ if
        c-red
        ."           [ CAVE TROLL ]         " cr
        ."             (o)  (o)             " cr
        ."              \\ -- /    <==[CLUB] " cr
        ."              /|  |\\              " cr
        ."             / |  | \\             " cr
        ."               /  \\               " cr
    else
        c-green
        ."          ( Defeated Troll )      " cr
        ."           ___....-------....___  " cr
        ."          [__RIP___Troll___RIP__] " cr
    then
    c-reset
    ;

: art-treasury
    c-yellow
    ."              .--------.          " cr
    ."             (  CHALICE )         " cr
    ."              \\~~~~~~~~/          " cr
    ."               \\  **  /           " cr
    ."                 |  |             " cr
    ."               __|__|__           " cr
    c-reset
    ;

: draw-scene
    cur-room @ 0 = if art-clearing then
    cur-room @ 1 = if art-cabin then
    cur-room @ 2 = if art-cave then
    cur-room @ 3 = if art-dungeon then
    cur-room @ 4 = if art-treasury then
    ;

: desc-room
    c-bold
    cur-room @ 0 = if
        c-green ." LOCATION: Forest Clearing" cr c-reset
        ." The dark pine trees sway in the cold wind." cr
        ." Exits: (N)orth to Cave, (E)ast to Cabin." cr
    then
    cur-room @ 1 = if
        c-yellow ." LOCATION: Old Wooden Cabin" cr c-reset
        has-key @ 0= if
            ." You see a shiny " c-yellow ." BRASS KEY " c-reset ." lying on a wooden table." cr
        else
            ." An old rustic shelter, dusty and quiet." cr
        then
        ." Exits: (W)est to Forest Clearing." cr
    then
    cur-room @ 2 = if
        c-gray ." LOCATION: Dark Cliffside Cave" cr c-reset
        door-open @ 0= if
            ." A giant locked " c-red ." IRON DOOR " c-reset ." blocks the way down." cr
        else
            ." The heavy iron door stands wide open leading down." cr
        then
        ." Exits: (S)outh to Clearing, (D)own to Catacombs." cr
    then
    cur-room @ 3 = if
        c-magenta ." LOCATION: Ancient Catacombs" cr c-reset
        troll-alive @ if
            c-red ." A menacing CAVE TROLL guards the archway to the East!" cr c-reset
        else
            ." The troll lies defeated on the stone floor." cr
        then
        has-sword @ 0= if
            ." You spot an " c-cyan ." ELVEN BLADE " c-reset ." beside a fallen skeleton." cr
        then
        ." Exits: (U)p to Cave, (E)ast to Treasury." cr
    then
    cur-room @ 4 = if
        c-yellow ." LOCATION: The King's Lost Treasury" cr c-reset
        has-treasure @ 0= if
            ." On an altar sparkles " c-yellow ." THE GOLDEN CHALICE OF FORTH! " cr c-reset
        else
            ." The altar stands empty. You hold the sacred chalice!" cr
        then
        ." Exits: (W)est to Catacombs." cr
    then
    c-reset
    ;

: print-hud
    c-cyan
    ." +----------------------------------------------------------------------+" cr
    c-reset
    ." INVENTORY: "
    has-key @ has-sword @ + has-treasure @ + 0= if
        c-gray ." [Empty]" c-reset
    else
        has-key @ if c-yellow ." [Brass Key] " c-reset then
        has-sword @ if c-cyan ." [Elven Blade] " c-reset then
        has-treasure @ if c-yellow ." [Golden Chalice] " c-reset then
    then
    cr
    c-cyan
    ." +----------------------------------------------------------------------+" cr
    c-reset
    ;

: print-last-action
    last-action @ 1 = if
        c-yellow ." >> [!] You picked up the Brass Key." cr c-reset
    then
    last-action @ 2 = if
        c-cyan ." >> [!] You equip the glowing Elven Blade!" cr c-reset
    then
    last-action @ 3 = if
        c-green ." >> [*] You unlocked the heavy Iron Door with the Brass Key!" cr c-reset
    then
    last-action @ 4 = if
        c-green ." >> [⚔] You slay the ferocious Cave Troll with your blade!" cr c-reset
    then
    last-action @ 5 = if
        c-red ." >> [!] The heavy door is locked. You need a key." cr c-reset
    then
    last-action @ 6 = if
        c-red ." >> [!] The Cave Troll blocks your path with a roar!" cr c-reset
    then
    last-action @ 7 = if
        c-red ." >> [!] You cannot move in that direction." cr c-reset
    then
    last-action @ 8 = if
        c-gray ." >> Nothing to take here." cr c-reset
    then
    last-action @ 9 = if
        c-gray ." >> Nothing to use or attack here." cr c-reset
    then
    0 last-action !
    ;

: render-ui
    cls
    print-header
    cr
    draw-scene
    cr
    desc-room
    cr
    print-map
    cr
    print-hud
    print-last-action
    ;

: init-game
    0 cur-room !
    0 has-key !
    0 has-sword !
    0 has-treasure !
    0 door-open !
    1 troll-alive !
    0 game-state !
    0 last-action !
    ;

: do-move-n
    cur-room @ 0 = if
        2 cur-room !
    else
        7 last-action !
    then
    ;

: do-move-s
    cur-room @ 2 = if
        0 cur-room !
    else
        7 last-action !
    then
    ;

: do-move-e
    cur-room @ 0 = if
        1 cur-room !
    else
        cur-room @ 3 = if
            troll-alive @ if
                6 last-action !
            else
                4 cur-room !
            then
        else
            7 last-action !
        then
    then
    ;

: do-move-w
    cur-room @ 1 = if
        0 cur-room !
    else
        cur-room @ 4 = if
            3 cur-room !
        else
            7 last-action !
        then
    then
    ;

: do-move-d
    cur-room @ 2 = if
        door-open @ if
            3 cur-room !
        else
            5 last-action !
        then
    else
        7 last-action !
    then
    ;

: do-move-u
    cur-room @ 3 = if
        2 cur-room !
    else
        7 last-action !
    then
    ;

: do-take
    cur-room @ 1 = if
        has-key @ if
            8 last-action !
        else
            1 has-key !
            1 last-action !
        then
    else
        cur-room @ 3 = if
            has-sword @ if
                8 last-action !
            else
                1 has-sword !
                2 last-action !
            then
        else
            cur-room @ 4 = if
                has-treasure @ if
                    8 last-action !
                else
                    1 has-treasure !
                    1 game-state !
                then
            else
                8 last-action !
            then
        then
    then
    ;

: do-action
    cur-room @ 2 = if
        door-open @ if
            9 last-action !
        else
            has-key @ if
                1 door-open !
                3 last-action !
            else
                5 last-action !
            then
        then
    else
        cur-room @ 3 = if
            troll-alive @ if
                has-sword @ if
                    0 troll-alive !
                    4 last-action !
                else
                    ." You need a weapon to fight the Troll!" cr
                then
            else
                9 last-action !
            then
        else
            9 last-action !
        then
    then
    ;

: print-help
    cls
    c-yellow
    ." ========================== COMMAND HELP ==========================" cr
    c-reset
    ."  N, S, E, W, U, D  : Navigate in directions (North, South, etc.)" cr
    ."  T                 : Take / Pick up item in room" cr
    ."  A                 : Action (Unlock door with key, attack enemies)" cr
    ."  H, ?              : Show this help screen" cr
    ."  Q                 : Quit game" cr
    c-yellow
    ." ===================================================================" cr
    c-reset
    ." Press any key to return to game..." cr
    KEY drop
    ;

: handle-cmd
    cmd-ch @ 96 > cmd-ch @ 123 < and if
        cmd-ch @ 32 - cmd-ch !
    then

    cmd-ch @ 78 = if do-move-n then
    cmd-ch @ 83 = if do-move-s then
    cmd-ch @ 69 = if do-move-e then
    cmd-ch @ 87 = if do-move-w then
    cmd-ch @ 85 = if do-move-u then
    cmd-ch @ 68 = if do-move-d then
    cmd-ch @ 84 = if do-take then
    cmd-ch @ 65 = if do-action then
    cmd-ch @ 72 = cmd-ch @ 63 = + if print-help then
    cmd-ch @ 81 = if 2 game-state ! then
    ;

: play-game
    init-game
    begin
        render-ui
        c-cyan ." Command [N/S/E/W/U/D, T=Take, A=Action, H=Help, Q=Quit] > " c-reset
        read-cmd cmd-ch !
        handle-cmd
        game-state @ 0= 0=
    until

    cls
    game-state @ 1 = if
        c-yellow
        ."   ***********************************************************" cr
        ."   *                                                         *" cr
        ."   *     VICTORY! YOU HAVE CLAIMED THE GOLDEN CHALICE!       *" cr
        ."   *         You are the Hero of the VagaForth Realm!        *" cr
        ."   *                                                         *" cr
        ."   ***********************************************************" cr
        art-treasury
        c-reset
        cr
    else
        c-gray
        ." You abandoned the quest. Darkness consumes the dungeon." cr
        c-reset
    then
    ;

create entry-nm 112 c, 108 c, 97 c, 121 c, 45 c, 103 c, 97 c, 109 c, 101 c,
entry-nm 9 s" dungeon.bin" save-elf-at
