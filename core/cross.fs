\ cross.fs - Cross-Compiler Utilities

decimal

\ --- Configuration ---
variable target-latest 
0 target-latest !

\ --- Address Conversion ---
: host>virt ( host-addr -- virt-addr )
    target-base @ - ELF-ORIGIN + ;

: virt>host ( virt-addr -- host-addr )
    ELF-ORIGIN - target-base @ + ;

: t-here ( -- host-addr ) here ;
: t-vhere ( -- virt-addr ) t-here host>virt ;

\ --- Target Dictionary Header ---
: t-align
    here 7 + -8 and here - allot ;

: t-create ( "name" -- )
    t-align
    
    t-vhere >r               \ Save current virtual address (start of link)
    target-latest @ 8,       \ Write Link (Virtual Addr of previous word)
    r> target-latest !       \ Update Latest to point to the link we just wrote
    
    \ Name
    parse-name               \ ( addr len )
    ." Defining target word: " 2dup type space ." at " target-latest @ . cr
    dup c,                   \ Len byte (at latest + 8)
    t-here swap dup allot    \ Allocate space
    cmove                    \ Copy name
    
    t-align
    ;

\ --- Target Lookup ---
: t-match? ( name-addr name-len link-virt-addr -- bool )
    virt>host 8 + \ host addr of len byte
    dup c@ 255 and \ target len
    3 pick <> if 
        drop drop drop drop false exit
    then
    1+ \ host addr of name (addr2)
    swap 2 pick \ addr2 len (addr1 already on stack)
    
    \ Stack: a1 l1 a2 l2
    host-string= 
    ;

: t-find ( name-addr name-len -- virt-addr flags true | false )
    target-latest @ 
    begin
        dup 0 = if
            drop 2drop false exit
        then
        
        \ Stack: name-addr name-len link-virt-addr
        3dup t-match? if
            \ Found it! Calculate XT.
            dup virt>host \ host-link-addr
            8 + \ host-len-addr
            dup c@ 1+ + \ skip link and name
            7 + -8 and \ align
            host>virt \ virt-xt
            
            \ Clean up and return
            nip nip nip
            0 \ flags
            true
            exit
        then
        virt>host @ \ Follow Link
        0 
    until
    drop 2drop false
    ;

\ --- Code Generation Helpers ---
hex
: t-call-dest ( dest-virt-addr -- )
    e8 c, \ CALL opcode
    t-vhere 4 + - 
    4, \ Write 32-bit offset
    ;

: t-lit ( n -- )
    48 c, b8 c, 8,          \ MOV RAX, n
    48 c, 83 c, c7 c, 08 c, \ ADD RDI, 8
    48 c, 89 c, 47 c, f8 c, \ MOV [RDI-8], RAX
    ;

: t-2dup ( -- )
    \ Generates code for 2DUP on target
    48 c, 8b c, 47 c, f0 c, \ mov rax, [rdi-16]
    48 c, 8b c, 5f c, f8 c, \ mov rbx, [rdi-8]
    48 c, 83 c, c7 c, 10 c, \ add rdi, 16
    48 c, 89 c, 47 c, f0 c, \ mov [rdi-16], rax
    48 c, 89 c, 5f c, f8 c, \ mov [rdi-8], rbx
    ;

: t-lea-rsi ( "name" -- )
    parse-name 2dup t-find if
        drop nip nip \ ( virt-addr )
        8 - 9 + 
        48 c, 8d c, 35 c,
        t-vhere 4 + - 4, 
    else
        ." Error: LEA target not found: " type cr
        2drop
    then
    ;

: t-call ( "name" -- )
    parse-name 2dup t-find if
        \ virt-addr flags true
        drop nip nip \ ( virt-addr )
        e8 c, \ CALL
        t-vhere 4 + - 4, \ 32-bit relative offset
    else
        ." Error: Target word not found: " type cr
        2drop \ Clean up name
        1 0 = if exit then \ force exit
    then
    ;

\ --- Compiler Loop Words ---
: t-code t-create ;
: t-end-code ;
: t-: t-create ;
: t-; c3 c, ;

: t-constant ( n "name" -- )
    t-create
    t-lit
    c3 c,
    ;

decimal
." Cross-compiler loaded." cr