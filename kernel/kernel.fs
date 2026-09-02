\ kernel.fs - Target Kernel with Interpreter Loop
\ t-i001: asm-call-sync helper
\ t-i002: arithmetic primitives + - dup drop swap .
\ t-i003: REPL interpreter loop

include core/prelude.fs
include core/host-ext.fs
include core/asm.fs
include core/os.fs
include core/elf.fs
include core/cross.fs

\ --- Helpers (defined BEFORE target-on so they are host words) ---
hex
: asm-call ( dest-virt -- )
    e8 c, 
    t-vhere 4 + - 4, 
    ;

\ t-i001: asm-call-sync - CALL dest then mov rdi, rax (re-sync DSP)
: asm-call-sync ( dest-virt -- )
    e8 c, 
    t-vhere 4 + - 4, 
    48 c, 89 c, c7 c, \ mov rdi, rax
    ;

\ t-immediate - mark the most recently defined target word as IMMEDIATE
\ (sets the 0x80 flag bit in its length byte so FIND returns it).
\ MUST be defined before target-on (host word).
: t-immediate
    target-latest @ virt>host 8 +   \ host addr of the length/flags byte
    dup c@ 80 or swap c!
    ;

\ --- Host helpers to emit RIP-relative target-variable access (t-c002) ---
\ All take a target VIRTUAL address of a variable.
: emit-mov-rax-var ( var-virt -- )  \ mov rax, [rip+disp32]
    48 c, 8b c, 05 c,
    t-vhere 4 + - 4, ;
: emit-store-rax-var ( var-virt -- ) \ mov [rip+disp32], rax
    48 c, 89 c, 05 c,
    t-vhere 4 + - 4, ;
: emit-cmp-rax-var ( var-virt -- )   \ cmp rax, [rip+disp32]
    48 c, 3b c, 05 c,
    t-vhere 4 + - 4, ;
: emit-mov-rax-imm ( imm64 -- )       \ mov rax, imm64
    48 c, b8 c, 8, ;
: emit-test-rax ( -- )                \ test rax, rax
    48 c, 85 c, c0 c, ;
\ push imm64 onto the target DSP (VagaForth stack), used by runtime words.
\ mov rax, imm64 ; add rdi, 8 ; mov [rdi-8], rax
: emit-push-imm ( imm64 -- )
    48 c, b8 c, 8,
    add-rdi-8
    mov-tos-rax ;
\ mov rbx,[rip+disp32]
: emit-mov-rbx-var ( var-virt -- )
    48 c, 8b c, 1d c,
    t-vhere 4 + - 4, ;
\ mov [rip+disp32], rbx
: emit-store-rbx-var ( var-virt -- )
    48 c, 89 c, 1d c,
    t-vhere 4 + - 4, ;
\ mov rbx, imm64
: emit-mov-rbx-imm ( imm64 -- )
    48 c, bb c, 8, ;
\ mov rcx, imm64
: emit-mov-rcx-imm ( imm64 -- )
    48 c, b9 c, 8, ;
\ mov rdx, imm64
: emit-mov-rdx-imm ( imm64 -- )
    48 c, ba c, 8, ;
\ mov rax, rbx
: emit-mov-rax-rbx ( -- ) 48 c, 89 c, d8 c, ;
\ mov rbx, rax
: emit-mov-rbx-rax ( -- ) 48 c, 89 c, c3 c, ;
\ mov rax, rcx
: emit-mov-rax-rcx ( -- ) 48 c, 89 c, c8 c, ;
\ mov rcx, rax
: emit-mov-rcx-rax ( -- ) 48 c, 89 c, c1 c, ;
\ mov rax, rdx
: emit-mov-rax-rdx ( -- ) 48 c, 89 c, d0 c, ;
\ mov rdx, rax
: emit-mov-rdx-rax ( -- ) 48 c, 89 c, c2 c, ;
\ mov rsi, rax
: emit-mov-rsi-rax ( -- ) 48 c, 89 c, c6 c, ;
\ mov rdi, rax
: emit-mov-rdi-rax ( -- ) 48 c, 89 c, c7 c, ;
\ inc rcx
: emit-inc-rcx ( -- ) 48 c, ff c, c1 c, ;
\ mov [rcx], bl  (byte store)
: emit-storb-rbx-rcx ( -- ) 88 c, 19 c, ;
\ mov [rcx], ebx (dword store)
: emit-stord-rbx-rcx ( -- ) 48 c, 89 c, 19 c, ;
\ mov [rcx], rax (qword store)
: emit-storq-rax-rcx ( -- ) 48 c, 89 c, 01 c, ;
\ add rcx, imm8
: emit-add-rcx-imm8 ( n -- ) 48 c, 83 c, c1 c, c, ;
\ mov byte [rcx], imm8
: emit-movb-imm-rcx ( imm8 -- ) c6 c, 01 c, c, ;
\ mov byte [rcx+1], imm8
: emit-movb-imm-rcx1 ( imm8 -- ) c6 c, 41 c, 01 c, c, ;
\ mov byte [rcx+disp8], imm8  : ( disp8 imm8 -- ) c6 c, 41 c, c, c, ;
: emit-movb-imm-rcxdisp ( disp imm8 -- ) c6 c, 41 c, c, c, ;
\ mov byte [rcx+imm8], bl   : ( imm8 -- ) 88 c, 59 c, c, ;
: emit-storb-bl-rcxdisp ( disp -- ) 88 c, 59 c, c, ;

\ t-c1d2: ELF emission scratch buffers (target RAM, above S-BUF-ADDR 0x430400).
\ ELF-HDR-BUF holds the 120-byte ELF header + PT_LOAD program header.
\ ELF-FN-BUF holds the null-terminated output filename for save-elf.
\ ELF-FD holds the open file descriptor across save-elf's sys-write calls.
\ ELF-ZERO-BUF is a 256-byte zero buffer used to pad the output file to a
\ page-aligned offset (the Linux loader requires the first PT_LOAD's p_offset
\ to be a multiple of 0x1000).
\ These live in the 0x430xxx scratch region, ABOVE the emitted image
\ [0x400000, HERE), so writing them never pollutes the image on disk.
430800 constant ELF-HDR-BUF
430B00 constant ELF-FN-BUF
430D00 constant ELF-FD
430D08 constant ELF-ZERO-BUF
\ t-d5a6: include scratch buffers (target RAM, above ELF-ZERO-BUF 0x430D08+0x100).
\ INCLUDE-DEPTH tracks include nesting level.
\ INC-BUF-BASE (0x450000) provides 256KB per nesting level.
430E00 constant INCLUDE-DEPTH
430E08 constant INCLUDE-FD
430E10 constant INCLUDE-LEN
430E18 constant INCLUDE-FN-BUF
431000 constant INCLUDE-SRC-BUF
70000000 constant INC-BUF-BASE

\ t-b1c3: save-elf-at scratch buffers (target RAM, above INCLUDE-SRC-BUF 0x431000).
\ The runtime s" primitive reuses one shared S-BUF-ADDR, so consecutive s"
\ calls overwrite each other. We therefore copy BOTH the word name and the
\ output filename into dedicated 256-byte buffers before consuming the stack.
\ SAVE-NM-BUF = word name, SAVE-FN-BUF = output filename (each 256 bytes).
\ SAVE-NM-LEN / SAVE-FN-LEN hold their lengths; SAVE-XT and SAVE-TRAMP hold
\ the found word's XT and the emitted trampoline start address.
434100 constant SAVE-NM-BUF
434200 constant SAVE-NM-ADDR
434208 constant SAVE-NM-LEN
434210 constant SAVE-XT
434218 constant SAVE-TRAMP
434400 constant SAVE-FN-BUF
434500 constant SAVE-FN-ADDR
434508 constant SAVE-FN-LEN

\ t-i2a1: REPL line buffer (target RAM, 0x43xxxx scratch region above the
\ emitted image [0x400000, HERE)). Holds exactly one input line read from KEY
\ by REPL-REFILL. 0x434600 is well clear of the SAVE-* region (highest use
\ 0x434508), both stacks (DSP 0x410000 / RSP 0x420000), WORD-BUF-ADDR
\ (0x430000), S-BUF-ADDR (0x430400) and INCLUDE-SRC-BUF (0x431000).
\ SOURCE-LINE-SIZE = 512 bytes max line.
434600 constant SOURCE-LINE-BUF
000200 constant SOURCE-LINE-SIZE

\ t-c002: Additional register/var emitters needed by the target compiler words.
\ mov rcx,[rip+disp32]
: emit-mov-rcx-var ( var-virt -- ) 48 c, 8b c, 0d c, t-vhere 4 + - 4, ;
\ mov [rip+disp32], rcx
: emit-store-rcx-var ( var-virt -- ) 48 c, 89 c, 0d c, t-vhere 4 + - 4, ;
\ mov rdx,[rip+disp32]
: emit-mov-rdx-var ( var-virt -- ) 48 c, 8b c, 15 c, t-vhere 4 + - 4, ;
\ mov [rip+disp32], rdx
: emit-store-rdx-var ( var-virt -- ) 48 c, 89 c, 15 c, t-vhere 4 + - 4, ;
\ add rbx, rax   (48 01 c3)
: emit-add-rbx-rax ( -- ) 48 c, 01 c, c3 c, ;
\ add rbx, rcx   (48 01 cb)
: emit-add-rbx-rcx ( -- ) 48 c, 01 c, cb c, ;
\ shl rcx, 3   (48 c1 e1 03)
: emit-shl-rcx-3 ( -- ) 48 c, c1 c, e1 c, 03 c, ;
\ shl rax, 3   (48 c1 e0 03)
: emit-shl-rax-3 ( -- ) 48 c, c1 c, e0 c, 03 c, ;
\ and rax, 0x80  (48 83 e0 80)
: emit-and-rax-80 ( -- ) 48 c, 83 c, e0 c, 80 c, ;

\ Target runtime dictionary base (where colon-defined words are emitted at runtime).
403000 constant T-CODE-START

target-on
target-base @ target-dp !
hex
100 allot \ Headers space
t-vhere constant T-LATEST-VAR  0 8,
t-vhere constant T-STATE-VAR   0 8,
\ t-f8c3: numeric base cell (10=decimal, 16=hex). Default decimal. Read by
\ the `.` word to match C prim_dot (`%ld` decimal vs `%lx` hex when base==16).
t-vhere constant T-BASE-VAR    10 8,
t-vhere constant T-HERE-VAR    T-CODE-START 8,
t-vhere constant T-BSS-VAR     60000000 8,
t-vhere constant T-CDEPTH      0 8,
t-vhere constant SOURCE-PTR    0 8,
t-vhere constant SOURCE-END    0 8,
t-vhere constant SOURCE-ACTIVE 0 8,
\ t-i2a1: 1 = current SOURCE is a REPL line buffer (refill + prompt on
\ exhaustion); 0 = an evaluate/compile-source buffer (EOF -> return).
\ Zero-init so evaluate/compile-source stay in "EOF->return" mode; only
\ START sets it to 1 at boot. Never written to 0 by the refill path.
t-vhere constant LINE-MODE 0 8,
\ temporary scratch cells used by the REPL/compiler (target RAM).
t-vhere constant TMP-FLAGS   0 8,
t-vhere constant TMP-XT      0 8,
t-vhere constant TMP-TOK-ADDR 0 8,
t-vhere constant TMP-TOK-LEN  0 8,
t-vhere constant IS-TTY-FLAG  0 8,
t-vhere constant PROMPT-FLAG  0 8,
\ ABORT-VT holds the target address of the ABORT word (patched at build end),
\ so the REPL/EVALUATE can call it via an indirect call. Forward reference
\ because ABORT is defined after REPL.
t-vhere constant ABORT-VT   0 8,
decimal

\ Abort message buffer (target RAM area).
hex
t-vhere constant ABORT-MSG
200 allot
\ Pre-fill ABORT-MSG with "?abort\n".
ABORT-MSG virt>host
dup 3f swap c! 1+
dup 61 swap c! 1+
dup 62 swap c! 1+
dup 6f swap c! 1+
dup 72 swap c! 1+
dup 74 swap c! 1+
dup 0a swap c! 1+
drop
decimal

\ Control-flow stack array (256 bytes = 32 cells) in target memory.
hex
t-vhere constant T-CSTACK-ARR
200 allot
\ T-CSTACK-BASE points at the base of the control-flow stack array.
t-vhere constant T-CSTACK-BASE T-CSTACK-ARR 8,
decimal

\ --- t-d004: Status-report label strings (target RAM) for the version word. ---
\ Pre-filled string buffers consumed by the native `version` t-code word via
\ the target's `type` primitive. Kept in target RAM (low 0x40xxxx data region),
\ well below WORD-BUF-ADDR(0x430000)/NUM-BUF-END(0x430200)/S-BUF-ADDR(0x430400)
\ so they never collide with parse/source buffers.
hex
t-vhere constant VM-LBL0      \ "vagaforth-kernel: dict " (0x17 chars)
40 allot
t-vhere constant VM-LBL1      \ "bytes, " (0x07 chars)  [host s" strips leading space]
40 allot
t-vhere constant VM-LBL2      \ "words" (0x05 chars)    [host s" strips leading space]
40 allot
\ pre-fill the three labels (host build-time). cmove expects ( src dest len -- ):
\   s" ..."                -> ( src len )
\   VM-LBLx virt>host      -> ( src len dest )
\   swap                   -> ( src dest len )
\   cmove                  -> memmove(dest, src, len)
\ Note: host `s"` trims leading whitespace, so label1/label2 start without a
\ leading space; the `version` word inserts explicit `space` separators.
s" vagaforth-kernel: dict " VM-LBL0 virt>host swap cmove
s" bytes, " VM-LBL1 virt>host swap cmove
s" words" VM-LBL2 virt>host swap cmove
\ t-i001: welcome + prompt label buffers (target RAM).
t-vhere constant VM-LBL3      \ "VagaForth v0.8" (0x0e chars)
40 allot
t-vhere constant VM-LBL4      \ "vagaforth> " (0x0b chars)
40 allot
s" VagaForth v0.8" VM-LBL3 virt>host swap cmove
s" vagaforth> " VM-LBL4 virt>host swap cmove
\ t-c3d4: REPL prompt state-separator label buffers (target RAM).
\   VM-LBL5 = "> " (interpret, 2 chars)  VM-LBL6 = "compiled > " (compile, 11 chars)
\   (host s" strips one leading space; the prompt emits the space itself)
t-vhere constant VM-LBL5      \ "> " (0x02 chars)  [host s" strips leading space]
40 allot
t-vhere constant VM-LBL6      \ "compiled > " (0x0b chars)
40 allot
s" > " VM-LBL5 virt>host swap cmove
s" compiled > " VM-LBL6 virt>host swap cmove
decimal

\ --- Primitives ---
t-code EXIT   hex c3 c, t-end-code

\ t-i002: Arithmetic & stack primitives
t-code + ( a b -- a+b )
    t-vhere constant XT_PLUS
    mov-rax-tos      \ RAX = b (TOS)
    mov-rbx-nos      \ RBX = a (NOS)
    add-rax-rbx      \ RAX = a + b
    sub-rdi-8        \ POP
    mov-tos-rax      \ store result
    mov-rax-rdi      \ return DSP
    hex c3 c,
t-end-code

t-code - ( a b -- a-b )
    t-vhere constant XT_MINUS
    mov-rax-tos      \ RAX = b (TOS)
    mov-rbx-nos      \ RBX = a (NOS)
    hex 48 c, f7 c, d8 c, \ NEG RAX ( -b )
    add-rax-rbx      \ RAX = a - b
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    hex c3 c,
t-end-code

t-code dup ( n -- n n )
    t-vhere constant XT_DUP
    mov-rax-tos      \ RAX = n
    add-rdi-8        \ PUSH
    mov-tos-rax      \ store n at new TOS
    mov-rax-rdi
    hex c3 c,
t-end-code

t-code drop ( n -- )
    t-vhere constant XT_DROP
    sub-rdi-8        \ POP
    mov-rax-rdi
    hex c3 c,
t-end-code

t-code 2drop ( a b -- )
    t-vhere constant XT_2DROP
    48 c, 83 c, ef c, 10 c, \ SUB RDI, 16
    mov-rax-rdi
    hex c3 c,
t-end-code

t-code negate ( n -- -n )
    t-vhere constant XT_NEGATE
    mov-rax-tos
    48 c, f7 c, d8 c, \ NEG RAX
    mov-tos-rax
    mov-rax-rdi
    hex c3 c,
t-end-code

t-code swap ( a b -- b a )
    t-vhere constant XT_SWAP
    mov-rax-tos      \ RAX = b
    mov-rbx-nos      \ RBX = a
    hex 48 c, 89 c, 5f c, f8 c, \ MOV [RDI-8], RBX (TOS = a)
    48 c, 89 c, 47 c, f0 c, \ MOV [RDI-16], RAX (NOS = b)
    mov-rax-rdi
    hex c3 c,
t-end-code

hex
t-code * ( a b -- ab )
    t-vhere constant XT_MUL
    mov-rax-tos      \ RAX = b
    mov-rbx-nos      \ RBX = a
    48 c, 0f c, af c, c3 c, \ IMUL RAX, RBX
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ @ ( addr -- n )  : fetch qword
t-code @ ( addr -- n )
    t-vhere constant XT_FETCH
    mov-rax-tos      \ RAX = addr
    48 c, 8b c, 00 c, \ MOV RAX, [RAX]
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ ! ( n addr -- )  : store qword
t-code ! ( n addr -- )
    t-vhere constant XT_STORE
    mov-rax-tos      \ RAX = addr
    mov-rbx-nos      \ RBX = n
    48 c, 89 c, 18 c, \ MOV [RAX], RBX
    sub-rdi-8
    sub-rdi-8
    mov-rax-rdi
    c3 c,
t-end-code

\ +! ( n addr -- )  : add n to qword at addr
t-code +! ( n addr -- )
    t-vhere constant XT_PLUS_STORE
    mov-rax-tos      \ RAX = addr
    mov-rbx-nos      \ RBX = n
    48 c, 01 c, 18 c, \ ADD [RAX], RBX
    sub-rdi-8
    sub-rdi-8
    mov-rax-rdi
    c3 c,
t-end-code

\ /mod ( a b -- rem quot )
t-code /mod ( a b -- rem quot )
    t-vhere constant XT_SLASH_MOD
    mov-rbx-tos      \ RBX = b (divisor)
    mov-rax-nos      \ RAX = a (dividend)
    48 c, 99 c,      \ CQO
    48 c, f7 c, fb c, \ IDIV RBX (RAX=quot, RDX=rem)
    mov-tos-rax      \ TOS = quot
    48 c, 89 c, 57 c, f0 c, \ MOV [RDI-16], RDX (NOS = rem)
    mov-rax-rdi
    c3 c,
t-end-code

\ / ( a b -- quot )
t-code / ( a b -- quot )
    t-vhere constant XT_SLASH
    mov-rbx-tos      \ RBX = b (divisor)
    mov-rax-nos      \ RAX = a (dividend)
    48 c, 99 c,      \ CQO
    48 c, f7 c, fb c, \ IDIV RBX
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ mod ( a b -- rem )
t-code mod ( a b -- rem )
    t-vhere constant XT_MOD
    mov-rbx-tos      \ RBX = b (divisor)
    mov-rax-nos      \ RAX = a (dividend)
    48 c, 99 c,      \ CQO
    48 c, f7 c, fb c, \ IDIV RBX
    sub-rdi-8
    48 c, 89 c, 57 c, f8 c, \ MOV [RDI-8], RDX
    mov-rax-rdi
    c3 c,
t-end-code

\ c@ ( addr -- ch )  : fetch byte
t-code c@ ( addr -- ch )
    t-vhere constant XT_CFETCH
    mov-rax-tos      \ RAX = addr
    48 c, 0f c, b6 c, 00 c, \ MOVZX RAX, BYTE [RAX]
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ c! ( ch addr -- )  : store byte
t-code c! ( ch addr -- )
    t-vhere constant XT_CSTORE
    mov-rax-tos      \ RAX = addr
    mov-rbx-nos      \ RBX = ch
    48 c, 88 c, 18 c, \ MOV [RAX], BL
    sub-rdi-8
    sub-rdi-8
    mov-rax-rdi
    c3 c,
t-end-code

\ over ( a b -- a b a )
t-code over ( a b -- a b a )
    t-vhere constant XT_OVER
    mov-rbx-nos      \ RBX = a (NOS)
    add-rdi-8        \ PUSH
    mov-tos-rbx
    mov-rax-rdi
    c3 c,
t-end-code

\ 2over ( a b c d -- a b c d a b )
t-code 2over ( a b c d -- a b c d a b )
    t-vhere constant XT_2OVER
    48 c, 8b c, 47 c, e0 c,     \ MOV RAX, [RDI-32] (a)
    48 c, 8b c, 5f c, e8 c,     \ MOV RBX, [RDI-24] (b)
    48 c, 83 c, c7 c, 10 c,     \ ADD RDI, 16
    48 c, 89 c, 47 c, f0 c,     \ MOV [RDI-16], RAX
    48 c, 89 c, 5f c, f8 c,     \ MOV [RDI-8], RBX
    mov-rax-rdi
    c3 c,
t-end-code

\ 3dup ( a b c -- a b c a b c )
t-code 3dup ( a b c -- a b c a b c )
    t-vhere constant XT_3DUP
    48 c, 8b c, 47 c, e8 c,     \ MOV RAX, [RDI-24] (a)
    48 c, 8b c, 5f c, f0 c,     \ MOV RBX, [RDI-16] (b)
    48 c, 8b c, 4f c, f8 c,     \ MOV RCX, [RDI-8]  (c)
    48 c, 83 c, c7 c, 18 c,     \ ADD RDI, 24
    48 c, 89 c, 47 c, e8 c,     \ MOV [RDI-24], RAX
    48 c, 89 c, 5f c, f0 c,     \ MOV [RDI-16], RBX
    48 c, 89 c, 4f c, f8 c,     \ MOV [RDI-8], RCX
    mov-rax-rdi
    c3 c,
t-end-code

\ 3drop ( a b c -- )
t-code 3drop ( a b c -- )
    t-vhere constant XT_3DROP
    48 c, 83 c, ef c, 18 c,     \ SUB RDI, 24
    mov-rax-rdi
    c3 c,
t-end-code

\ pick ( n -- x_n )
t-code pick ( n -- x_n )
    t-vhere constant XT_PICK
    mov-rax-tos                 \ RAX = n
    48 c, ff c, c0 c,           \ INC RAX (n+1)
    48 c, c1 c, e0 c, 03 c,     \ SHL RAX, 3 ((n+1)*8)
    48 c, f7 c, d8 c,           \ NEG RAX (-offset)
    48 c, 8b c, 44 c, 07 c, f8 c, \ MOV RAX, [RDI + RAX - 8]
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ cells ( n -- n*8 )
t-code cells ( n -- n*8 )
    t-vhere constant XT_CELLS
    mov-rax-tos
    48 c, c1 c, e0 c, 03 c,     \ SHL RAX, 3
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

hex
\ 1+ ( n -- n+1 )
t-code 1+ ( n -- n+1 )
    t-vhere constant XT_1PLUS
    mov-rax-tos
    48 c, 83 c, c0 c, 01 c, \ ADD RAX, 1
    mov-tos-rax
    mov-rax-rdi
    hex c3 c,
t-end-code

\ 1- ( n -- n-1 )
t-code 1- ( n -- n-1 )
    t-vhere constant XT_1-
    mov-rax-tos
    48 c, 83 c, e8 c, 01 c, \ SUB RAX, 1
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ > ( a b -- flag )  : TOS=b NOS=a, flag = a > b
t-code > ( a b -- flag )
    t-vhere constant XT_GT
    mov-rax-nos      \ RAX = a
    mov-rbx-tos      \ RBX = b
    cmp-rax-rbx      \ a - b
    48 c, 0f c, 9f c, c0 c, \ SETG AL (a>b)
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ < ( a b -- flag )  : a < b
t-code < ( a b -- flag )
    t-vhere constant XT_LT
    mov-rax-nos      \ RAX = a
    mov-rbx-tos      \ RBX = b
    cmp-rax-rbx      \ a-b
    48 c, 0f c, 9c c, c0 c, \ SETL AL (a<b)
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ = ( a b -- flag )  : a == b
t-code = ( a b -- flag )
    t-vhere constant XT_EQ
    mov-rax-nos      \ RAX = a
    mov-rbx-tos      \ RBX = b
    cmp-rax-rbx      \ a-b
    48 c, 0f c, 94 c, c0 c, \ SETE AL (a==b)
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ 0= ( n -- flag )
t-code 0= ( n -- flag )
    t-vhere constant XT_0=
    mov-rax-tos
    cmp-rax-0
    48 c, 0f c, 94 c, c0 c, \ SETE AL
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ 0<> ( n -- flag )
t-code 0<> ( n -- flag )
    t-vhere constant XT_0NOTEQ
    mov-rax-tos
    cmp-rax-0
    48 c, 0f c, 95 c, c0 c, \ SETNE AL
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ <> ( a b -- flag )
t-code <> ( a b -- flag )
    t-vhere constant XT_NOTEQ
    mov-rax-nos
    mov-rbx-tos
    cmp-rax-rbx
    48 c, 0f c, 95 c, c0 c, \ SETNE AL
    48 c, 0f c, b6 c, c0 c, \ MOVZX RAX, AL
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ true ( -- -1 )
t-code true ( -- -1 )
    t-vhere constant XT_TRUE
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, ff c, ff c, ff c, ff c, \ MOV [RDI-8], -1
    mov-rax-rdi
    c3 c,
t-end-code

\ false ( -- 0 )
t-code false ( -- 0 )
    t-vhere constant XT_FALSE
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, 00 c, 00 c, 00 c, 00 c, \ MOV [RDI-8], 0
    mov-rax-rdi
    c3 c,
t-end-code

\ ============================================================
\ t-b1c2: Native file-I/O syscall words (Linux x86-64)
\ Follow the RDI=DSP-in / RAX=DSP-out convention, using
\ asm-push-rdi/asm-pop-rdi around syscalls (matching type/emit/key).
\ ============================================================

\ sys-write ( fd addr len -- count ) : write(1) syscall
t-code sys-write ( fd addr len -- count )
    t-vhere constant XT_SYS_WRITE
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = len
    mov-rdx-rax                \ RDX = len (arg3)
    mov-rax-nos                \ RAX = addr
    mov-rsi-rax                \ RSI = addr (arg2)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (fd)
    mov-rdi-rax                \ RDI = fd (arg1)
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,   \ RAX = 1 (SYS_WRITE)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop len
    sub-rdi-8                  \ pop addr
    mov-tos-rax                \ store count (overwrites fd slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-read ( fd addr len -- count ) : read(0) syscall
t-code sys-read ( fd addr len -- count )
    t-vhere constant XT_SYS_READ
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = len
    mov-rdx-rax                \ RDX = len (arg3)
    mov-rax-nos                \ RAX = addr
    mov-rsi-rax                \ RSI = addr (arg2)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (fd)
    mov-rdi-rax                \ RDI = fd (arg1)
    48 c, 31 c, c0 c,          \ XOR RAX, RAX (RAX = 0 = SYS_READ)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop len
    sub-rdi-8                  \ pop addr
    mov-tos-rax                \ store count
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-close ( fd -- status ) : close(3) syscall
t-code sys-close ( fd -- status )
    t-vhere constant XT_SYS_CLOSE
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = fd
    mov-rdi-rax                \ RDI = fd (arg1)
    48 c, c7 c, c0 c, 03 c, 00 c, 00 c, 00 c,   \ RAX = 3 (SYS_CLOSE)
    syscall
    asm-pop-rdi
    mov-tos-rax                \ store status (overwrites fd)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-creat ( path-addr mode -- fd ) : creat(85) syscall
t-code sys-creat ( path-addr mode -- fd )
    t-vhere constant XT_SYS_CREAT
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = mode
    mov-rsi-rax                \ RSI = mode (arg2)
    mov-rax-nos                \ RAX = path-addr
    mov-rdi-rax                \ RDI = path-addr (arg1)
    48 c, c7 c, c0 c, 55 c, 00 c, 00 c, 00 c,   \ RAX = 85 (SYS_CREAT)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop mode
    mov-tos-rax                \ store fd (overwrites path-addr slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-open ( path-addr path-len flags mode -- fd ) : open(2) syscall
\ path-addr must point to a null-terminated string; path-len is consumed
\ but not passed (open() requires a NUL-terminated path).
t-code sys-open ( path-addr path-len flags mode -- fd )
    t-vhere constant XT_SYS_OPEN
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = mode
    mov-rdx-rax                \ RDX = mode (arg3)
    mov-rax-nos                \ RAX = flags
    mov-rsi-rax                \ RSI = flags (arg2)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (path-len, discarded)
    48 c, 8b c, 47 c, e0 c,    \ MOV RAX, [RDI-32]  (path-addr)
    mov-rdi-rax                \ RDI = path-addr (arg1)
    48 c, c7 c, c0 c, 02 c, 00 c, 00 c, 00 c,   \ RAX = 2 (SYS_OPEN)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop mode
    sub-rdi-8                  \ pop flags
    sub-rdi-8                  \ pop path-len
    mov-tos-rax                \ store fd (overwrites path-addr slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-ioctl ( fd req arg -- status ) : ioctl(16) syscall
t-code sys-ioctl ( fd req arg -- status )
    t-vhere constant XT_SYS_IOCTL
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = arg (arg3)
    mov-rdx-rax                \ RDX = arg
    mov-rax-nos                \ RAX = req (arg2)
    mov-rsi-rax                \ RSI = req
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (fd)
    mov-rdi-rax                \ RDI = fd (arg1)
    48 c, c7 c, c0 c, 10 c, 00 c, 00 c, 00 c,   \ RAX = 16 (SYS_IOCTL)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop arg
    sub-rdi-8                  \ pop req
    mov-tos-rax                \ store status (overwrites fd slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-nanosleep ( req-addr rem-addr -- status ) : nanosleep(35) syscall
t-code sys-nanosleep ( req-addr rem-addr -- status )
    t-vhere constant XT_SYS_NANOSLEEP
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = rem (arg2)
    mov-rsi-rax                \ RSI = rem
    mov-rax-nos                \ RAX = req (arg1)
    mov-rdi-rax                \ RDI = req
    48 c, c7 c, c0 c, 23 c, 00 c, 00 c, 00 c,   \ RAX = 35 (SYS_NANOSLEEP)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop rem
    mov-tos-rax                \ store status (overwrites req slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-fcntl ( fd cmd arg -- status ) : fcntl(72) syscall
t-code sys-fcntl ( fd cmd arg -- status )
    t-vhere constant XT_SYS_FCNTL
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = arg (arg3)
    mov-rdx-rax                \ RDX = arg
    mov-rax-nos                \ RAX = cmd (arg2)
    mov-rsi-rax                \ RSI = cmd
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (fd)
    mov-rdi-rax                \ RDI = fd (arg1)
    48 c, c7 c, c0 c, 48 c, 00 c, 00 c, 00 c,   \ RAX = 72 (SYS_FCNTL = 0x48)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop arg
    sub-rdi-8                  \ pop cmd
    mov-tos-rax                \ store status (overwrites fd slot)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-lseek ( fd offset whence -- result ) : lseek(8) syscall
hex
t-code sys-lseek ( fd offset whence -- result )
    t-vhere constant XT_SYS_LSEEK
    asm-push-rdi
    mov-rax-tos                \ RAX = whence
    mov-rdx-rax                \ RDX = whence
    mov-rax-nos                \ RAX = offset
    mov-rsi-rax                \ RSI = offset
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (fd)
    mov-rdi-rax                \ RDI = fd
    48 c, c7 c, c0 c, 08 c, 00 c, 00 c, 00 c, \ RAX = 8 (SYS_LSEEK)
    syscall
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8        \ pop whence, offset
    mov-tos-rax                \ TOS = result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-mmap ( addr len prot flags fd offset -- mapped-addr ) : mmap(9) syscall
hex
t-code sys-mmap ( addr len prot flags fd offset -- mapped-addr )
    t-vhere constant XT_SYS_MMAP
    asm-push-rdi
    48 c, 8b c, 4f c, f8 c,    \ mov rcx, [rdi-8]   (offset)
    49 c, 89 c, c9 c,          \ mov r9, rcx        (arg6 = offset)
    48 c, 8b c, 47 c, f0 c,    \ mov rax, [rdi-16]  (fd)
    49 c, 89 c, c0 c,          \ mov r8, rax        (arg5 = fd)
    48 c, 8b c, 4f c, e8 c,    \ mov rcx, [rdi-24]  (flags)
    49 c, 89 c, ca c,          \ mov r10, rcx       (arg4 = flags)
    48 c, 8b c, 57 c, e0 c,    \ mov rdx, [rdi-32]  (arg3 = prot)
    48 c, 8b c, 77 c, d8 c,    \ mov rsi, [rdi-40]  (arg2 = len)
    48 c, 8b c, 7f c, d0 c,    \ mov rdi, [rdi-48]  (arg1 = addr)
    48 c, c7 c, c0 c, 09 c, 00 c, 00 c, 00 c, \ RAX = 9 (SYS_MMAP)
    syscall
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8 \ pop 5 args
    mov-tos-rax                \ TOS = mapped-addr
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-munmap ( addr len -- status ) : munmap(11) syscall
hex
t-code sys-munmap ( addr len -- status )
    t-vhere constant XT_SYS_MUNMAP
    asm-push-rdi
    mov-rax-tos                \ RAX = len
    mov-rsi-rax                \ RSI = len
    mov-rax-nos                \ RAX = addr
    mov-rdi-rax                \ RDI = addr
    48 c, c7 c, c0 c, 0b c, 00 c, 00 c, 00 c, \ RAX = 11 (SYS_MUNMAP)
    syscall
    asm-pop-rdi
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ sys-mprotect ( addr len prot -- status ) : mprotect(10) syscall
hex
t-code sys-mprotect ( addr len prot -- status )
    t-vhere constant XT_SYS_MPROTECT
    asm-push-rdi
    mov-rax-tos                \ RAX = prot
    mov-rdx-rax                \ RDX = prot
    mov-rax-nos                \ RAX = len
    mov-rsi-rax                \ RSI = len
    48 c, 8b c, 47 c, e8 c,    \ mov rax, [rdi-24]  (addr)
    mov-rdi-rax                \ RDI = addr
    48 c, c7 c, c0 c, 0a c, 00 c, 00 c, 00 c, \ RAX = 10 (SYS_MPROTECT)
    syscall
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- Foreign Function Interface (System V AMD64 ABI call0..call6) ---
\ call0 ( fn-ptr -- res )
hex
t-code call0 ( fn-ptr -- res )
    t-vhere constant XT_CALL0
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    asm-push-rdi               \ save DSP
    55 c,                      \ push rbp
    48 c, 89 c, e5 c,          \ mov rbp, rsp
    48 c, 83 c, e4 c, f0 c,    \ and rsp, -16 (align)
    48 c, 31 c, c0 c,          \ xor rax, rax (AL=0)
    ff c, d3 c,                \ call rbx
    48 c, 89 c, ec c,          \ mov rsp, rbp
    5d c,                      \ pop rbp
    asm-pop-rdi                \ restore DSP
    mov-tos-rax                \ store res
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call1 ( a1 fn-ptr -- res )
hex
t-code call1 ( a1 fn-ptr -- res )
    t-vhere constant XT_CALL1
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    mov-rax-nos                \ rax = a1
    asm-push-rdi               \ save DSP
    55 c,                      \ push rbp
    48 c, 89 c, e5 c,          \ mov rbp, rsp
    48 c, 83 c, e4 c, f0 c,    \ and rsp, -16
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,          \ xor rax, rax
    ff c, d3 c,                \ call rbx
    48 c, 89 c, ec c,          \ mov rsp, rbp
    5d c,                      \ pop rbp
    asm-pop-rdi                \ restore DSP
    sub-rdi-8                  \ pop a1
    mov-tos-rax                \ store res
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call2 ( a1 a2 fn-ptr -- res )
hex
t-code call2 ( a1 a2 fn-ptr -- res )
    t-vhere constant XT_CALL2
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    mov-rax-nos                \ rax = a2
    48 c, 89 c, c6 c,          \ mov rsi, rax (arg2)
    48 c, 8b c, 47 c, e8 c,    \ mov rax, [rdi-24] (a1)
    asm-push-rdi
    55 c,
    48 c, 89 c, e5 c,
    48 c, 83 c, e4 c, f0 c,
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,
    ff c, d3 c,
    48 c, 89 c, ec c,
    5d c,
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call3 ( a1 a2 a3 fn-ptr -- res )
hex
t-code call3 ( a1 a2 a3 fn-ptr -- res )
    t-vhere constant XT_CALL3
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    mov-rax-nos                \ rax = a3
    48 c, 89 c, c2 c,          \ mov rdx, rax (arg3)
    48 c, 8b c, 77 c, e8 c,    \ mov rsi, [rdi-24] (a2)
    48 c, 8b c, 47 c, e0 c,    \ mov rax, [rdi-32] (a1)
    asm-push-rdi
    55 c,
    48 c, 89 c, e5 c,
    48 c, 83 c, e4 c, f0 c,
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,
    ff c, d3 c,
    48 c, 89 c, ec c,
    5d c,
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call4 ( a1 a2 a3 a4 fn-ptr -- res )
hex
t-code call4 ( a1 a2 a3 a4 fn-ptr -- res )
    t-vhere constant XT_CALL4
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    48 c, 8b c, 4f c, f0 c,    \ mov rcx, [rdi-16] (arg4=a4)
    48 c, 8b c, 57 c, e8 c,    \ mov rdx, [rdi-24] (arg3=a3)
    48 c, 8b c, 77 c, e0 c,    \ mov rsi, [rdi-32] (arg2=a2)
    48 c, 8b c, 47 c, d8 c,    \ mov rax, [rdi-40] (a1)
    asm-push-rdi
    55 c,
    48 c, 89 c, e5 c,
    48 c, 83 c, e4 c, f0 c,
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,
    ff c, d3 c,
    48 c, 89 c, ec c,
    5d c,
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call5 ( a1 a2 a3 a4 a5 fn-ptr -- res )
hex
t-code call5 ( a1 a2 a3 a4 a5 fn-ptr -- res )
    t-vhere constant XT_CALL5
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    4c c, 8b c, 47 c, f0 c,    \ mov r8, [rdi-16] (arg5=a5)
    48 c, 8b c, 4f c, e8 c,    \ mov rcx, [rdi-24] (arg4=a4)
    48 c, 8b c, 57 c, e0 c,    \ mov rdx, [rdi-32] (arg3=a3)
    48 c, 8b c, 77 c, d8 c,    \ mov rsi, [rdi-40] (arg2=a2)
    48 c, 8b c, 47 c, d0 c,    \ mov rax, [rdi-48] (a1)
    asm-push-rdi
    55 c,
    48 c, 89 c, e5 c,
    48 c, 83 c, e4 c, f0 c,
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,
    ff c, d3 c,
    48 c, 89 c, ec c,
    5d c,
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ call6 ( a1 a2 a3 a4 a5 a6 fn-ptr -- res )
hex
t-code call6 ( a1 a2 a3 a4 a5 a6 fn-ptr -- res )
    t-vhere constant XT_CALL6
    mov-rax-tos                \ rax = fn-ptr
    48 c, 89 c, c3 c,          \ mov rbx, rax
    4c c, 8b c, 4f c, f0 c,    \ mov r9, [rdi-16] (arg6=a6)
    4c c, 8b c, 47 c, e8 c,    \ mov r8, [rdi-24] (arg5=a5)
    48 c, 8b c, 4f c, e0 c,    \ mov rcx, [rdi-32] (arg4=a4)
    48 c, 8b c, 57 c, d8 c,    \ mov rdx, [rdi-40] (arg3=a3)
    48 c, 8b c, 77 c, d0 c,    \ mov rsi, [rdi-48] (arg2=a2)
    48 c, 8b c, 47 c, c8 c,    \ mov rax, [rdi-56] (a1)
    asm-push-rdi
    55 c,
    48 c, 89 c, e5 c,
    48 c, 83 c, e4 c, f0 c,
    48 c, 89 c, c7 c,          \ mov rdi, rax (arg1)
    48 c, 31 c, c0 c,
    ff c, d3 c,
    48 c, 89 c, ec c,
    5d c,
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-b3d4: Native helper words (stack ops, logic, shifts, memory)
\ ============================================================

\ rot ( a b c -- b c a )
t-code rot ( a b c -- b c a )
    t-vhere constant XT_ROT
    hex
    mov-rax-tos                \ RAX = c
    mov-rbx-nos                \ RBX = b
    48 c, 8b c, 4f c, e8 c,    \ MOV RCX, [RDI-24]  (a)
    48 c, 89 c, 4f c, f8 c,    \ MOV [RDI-8], RCX   (TOS = a)
    48 c, 89 c, 47 c, f0 c,    \ MOV [RDI-16], RAX (NOS = c)
    48 c, 89 c, 5f c, e8 c,    \ MOV [RDI-24], RBX (NNOS = b)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ 2dup ( a b -- a b a b )
t-code 2dup ( a b -- a b a b )
    t-vhere constant XT_2DUP
    hex
    mov-rax-tos                \ RAX = b
    mov-rbx-nos                \ RBX = a
    add-rdi-8
    add-rdi-8
    48 c, 89 c, 5f c, f0 c,    \ MOV [RDI-16], RBX ([orig] = a)
    48 c, 89 c, 47 c, f8 c,    \ MOV [RDI-8], RAX  ([orig+8] = b)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ nip ( a b -- b )
t-code nip ( a b -- b )
    t-vhere constant XT_NIP
    hex
    mov-rax-tos                \ RAX = b
    sub-rdi-8                  \ pop b (RDI = orig-8)
    mov-tos-rax                \ store b at [orig-16] (overwrite a)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ and ( a b -- a&b )
t-code and ( a b -- a&b )
    t-vhere constant XT_AND
    hex
    mov-rax-tos                \ RAX = b
    mov-rbx-nos                \ RBX = a
    and-rax-rbx                \ RAX = a & b
    sub-rdi-8                  \ pop b
    mov-tos-rax                \ store result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ or ( a b -- a|b )
t-code or ( a b -- a|b )
    t-vhere constant XT_OR
    hex
    mov-rax-tos                \ RAX = b
    mov-rbx-nos                \ RBX = a
    48 c, 09 c, d8 c,          \ OR RAX, RBX
    sub-rdi-8                  \ pop b
    mov-tos-rax                \ store result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ xor ( a b -- a^b )
t-code xor ( a b -- a^b )
    t-vhere constant XT_XOR
    hex
    mov-rax-tos                \ RAX = b
    mov-rbx-nos                \ RBX = a
    48 c, 31 c, d8 c,          \ XOR RAX, RBX
    sub-rdi-8                  \ pop b
    mov-tos-rax                \ store result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ invert ( n -- ~n )
t-code invert ( n -- ~n )
    t-vhere constant XT_INVERT
    hex
    mov-rax-tos
    48 c, f7 c, d0 c,          \ NOT RAX
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ rshift ( n shift -- n>>shift )
t-code rshift ( n shift -- n>>shift )
    t-vhere constant XT_RSHIFT
    hex
    mov-rax-tos                \ RAX = shift
    48 c, 89 c, c1 c,          \ MOV RCX, RAX (shift count in CL)
    mov-rax-nos                \ RAX = n
    48 c, d3 c, e8 c,          \ SHR RAX, CL
    sub-rdi-8                  \ pop shift
    mov-tos-rax                \ store result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ lshift ( n shift -- n<<shift )
t-code lshift ( n shift -- n<<shift )
    t-vhere constant XT_LSHIFT
    hex
    mov-rax-tos                \ RAX = shift
    48 c, 89 c, c1 c,          \ MOV RCX, RAX
    mov-rax-nos                \ RAX = n
    48 c, d3 c, e0 c,          \ SHL RAX, CL
    sub-rdi-8                  \ pop shift
    mov-tos-rax                \ store result
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ cmove ( src dst len -- ) : copy len bytes from src to dst (forward)
variable cmove-done
t-code cmove ( src dst len -- )
    t-vhere constant XT_CMOVE
    asm-push-rdi               \ save DSP
    hex
    mov-rax-tos                \ RAX = len
    48 c, 89 c, c1 c,          \ MOV RCX, RAX (RCX = len)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24] (src)
    48 c, 89 c, c6 c,          \ MOV RSI, RAX (RSI = src)
    mov-rax-nos                \ RAX = dst
    48 c, 89 c, c7 c,          \ MOV RDI, RAX (RDI = dst)
    \ copy loop
    here constant CMOVE_LOOP
    48 c, 83 c, f9 c, 00 c,    \ CMP RCX, 0
    asm-je cmove-done !
    8a c, 06 c,                \ MOV AL, [RSI]
    88 c, 07 c,                \ MOV [RDI], AL
    48 c, ff c, c6 c,          \ INC RSI
    48 c, ff c, c7 c,          \ INC RDI
    48 c, ff c, c9 c,          \ DEC RCX
    eb c, CMOVE_LOOP here 1 + - c,   \ JMP loop_start
    \ done:
    here constant CMOVE_DONE
    cmove-done @ asm-resolve
    asm-pop-rdi                \ restore DSP
    sub-rdi-8                  \ pop len
    sub-rdi-8                  \ pop dst
    sub-rdi-8                  \ pop src
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ fill ( addr len ch -- ) : fill len bytes at addr with ch
variable fill-done
t-code fill ( addr len ch -- )
    t-vhere constant XT_FILL
    asm-push-rdi               \ save DSP
    hex
    mov-rax-tos                \ RAX = ch
    48 c, 89 c, c3 c,          \ MOV RBX, RAX (RBX = ch)
    mov-rax-nos                \ RAX = len
    48 c, 89 c, c1 c,          \ MOV RCX, RAX (RCX = len)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24] (addr)
    48 c, 89 c, c7 c,          \ MOV RDI, RAX (RDI = addr)
    \ fill loop
    here constant FILL_LOOP
    48 c, 83 c, f9 c, 00 c,    \ CMP RCX, 0
    asm-je fill-done !
    88 c, 1f c,                \ MOV [RDI], BL
    48 c, ff c, c7 c,          \ INC RDI
    48 c, ff c, c9 c,          \ DEC RCX
    eb c, FILL_LOOP here 1 + - c,   \ JMP loop_start
    \ done:
    here constant FILL_DONE
    fill-done @ asm-resolve
    asm-pop-rdi                \ restore DSP
    sub-rdi-8                  \ pop ch
    sub-rdi-8                  \ pop len
    sub-rdi-8                  \ pop addr
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-c1d2: Target ELF emission - elf-header
\ elf-header ( entry-point file-size mem-size -- )
\ Writes the 64-byte ELF64 header + 56-byte PT_LOAD program header into
\ ELF-HDR-BUF (0x430800). Uses a local pointer (RCX) so it does NOT advance
\ the runtime dictionary pointer HERE. Stack args: entry-point file-size mem-size.
\ ============================================================
hex
t-code elf-header ( entry-point file-size mem-size -- )
    t-vhere constant XT_ELF_HEADER
    asm-push-rdi               \ save DSP (RDI) on return stack
    hex
    \ --- Load the three stack args into registers ---
    \ Stack layout (RDI=DSP): [RDI-8]=mem-size [RDI-16]=file-size [RDI-24]=entry-point
    mov-rax-tos                \ RAX = mem-size
    48 c, 89 c, c2 c,          \ MOV RDX, RAX  (RDX = mem-size)
    mov-rax-nos                \ RAX = file-size
    48 c, 89 c, c3 c,          \ MOV RBX, RAX  (RBX = file-size)
    48 c, 8b c, 47 c, e8 c,    \ MOV RAX, [RDI-24]  (entry-point)
    49 c, 89 c, c0 c,          \ MOV R8, RAX  (R8 = entry-point, preserved)
    \ --- RCX = ELF-HDR-BUF (local write pointer) ---
    48 c, b9 c, ELF-HDR-BUF 8, \ MOV RCX, ELF-HDR-BUF
    \ --- e_ident (bytes 0-15) ---
    7f emit-movb-imm-rcx        \ mov byte [rcx], 0x7f
    48 c, ff c, c1 c,           \ inc rcx
    45 emit-movb-imm-rcx        \ 'E'
    48 c, ff c, c1 c,
    4c emit-movb-imm-rcx        \ 'L'
    48 c, ff c, c1 c,
    46 emit-movb-imm-rcx        \ 'F'
    48 c, ff c, c1 c,
    02 emit-movb-imm-rcx        \ EI_CLASS = ELFCLASS64
    48 c, ff c, c1 c,
    01 emit-movb-imm-rcx        \ EI_DATA = ELFDATA2LSB
    48 c, ff c, c1 c,
    01 emit-movb-imm-rcx        \ EI_VERSION = EV_CURRENT
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx        \ EI_OSABI = SYSV
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx        \ EI_ABIVERSION
    48 c, ff c, c1 c,
    \ EI_PAD: 7 zero bytes
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_type (0x10) = 0x0002 ET_EXEC ---
    02 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_machine (0x12) = 0x003e EM_X86_64 ---
    3e emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_version (0x14) = 0x00000001 ---
    01 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_entry (0x18) = entry-point (R8 holds it) ---
    \ store 8 bytes of R8 (entry-point) at [rcx], little-endian
    4c c, 89 c, 01 c,          \ mov [rcx], r8  (entry-point qword)
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ --- e_phoff (0x20) = 0x0000000000000040 ---
    40 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_shoff (0x28) = 0 ---
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_flags (0x30) = 0 ---
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_ehsize (0x34) = 0x0040 ---
    40 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_phentsize (0x36) = 0x0038 ---
    38 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_phnum (0x38) = 0x0001 ---
    01 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_shentsize (0x3A) = 0x0040 ---
    40 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_shnum (0x3C) = 0 ---
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- e_shstrndx (0x3E) = 0 ---
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ --- Program Header (bytes 64-119) ---
    \ p_type (0x40) = 0x00000001 PT_LOAD
    01 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ p_flags (0x44) = 0x00000007 RWE
    07 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ p_offset (0x48) = 0x1000: the image [0x400000, HERE) is written at file
    \ offset 0x1000 (page-aligned), after the 120-byte header + padding. The
    \ Linux ELF loader REQUIRES the first PT_LOAD's p_offset to be page-aligned
    \ (a multiple of 0x1000); a non-aligned offset (e.g. 120) makes execve fail
    \ with EINVAL. The image itself starts with the ELF header (the running
    \ binary loaded the whole file at 0x400000), so p_offset=0x1000 maps the
    \ image correctly at 0x400000.
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    10 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    00 emit-movb-imm-rcx
    48 c, ff c, c1 c,
    \ p_vaddr (0x50) = ELF-ORIGIN 0x400000
    48 c, b8 c, ELF-ORIGIN 8,   \ mov rax, ELF-ORIGIN
    48 c, 89 c, 01 c,          \ mov [rcx], rax
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ p_paddr (0x58) = ELF-ORIGIN 0x400000
    48 c, b8 c, ELF-ORIGIN 8,   \ mov rax, ELF-ORIGIN
    48 c, 89 c, 01 c,          \ mov [rcx], rax
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ p_filesz (0x60) = file-size (RBX)
    48 c, 89 c, 19 c,          \ mov [rcx], rbx
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ p_memsz (0x68) = mem-size (RDX)
    48 c, 89 c, 11 c,          \ mov [rcx], rdx
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ p_align (0x70) = 1 : the image is written at file offset 120 (0x78),
    \ which is NOT page-aligned. With p_align=0x1000 the Linux loader would
    \ reject the binary (p_offset must be congruent to p_vaddr mod p_align).
    \ p_align=1 removes the alignment constraint so the emitted file loads.
    48 c, b8 c, 01 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 1
    48 c, 89 c, 01 c,          \ mov [rcx], rax
    48 c, 83 c, c1 c, 08 c,    \ add rcx, 8
    \ --- Done: restore DSP, pop 3 args, return ---
    asm-pop-rdi                \ restore DSP
    sub-rdi-8                  \ pop mem-size
    sub-rdi-8                  \ pop file-size
    sub-rdi-8                  \ pop entry-point
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-c3e4: Target ELF emission - save-elf
\ save-elf ( filename-addr filename-len -- )
\ 1. Null-terminate filename into ELF-FN-BUF (0x430B00).
\ 2. sys-creat(path, 0755) -> fd
\ 3. sys-write(fd, ELF-HDR-BUF, 120)
\ 4. sys-write(fd, 0x400000, HERE - 0x400000)
\ 5. sys-close(fd)
\ ============================================================
variable fn-copy-done
hex
t-code save-elf ( filename-addr filename-len -- )
    t-vhere constant XT_SAVE_ELF
    asm-push-rdi               \ save DSP (RDI) on return stack
    hex
    \ --- 1. Null-terminate filename into ELF-FN-BUF ---
    \ Stack: [RDI-8]=filename-len [RDI-16]=filename-addr
    mov-rax-tos                \ RAX = filename-len
    48 c, 89 c, c1 c,          \ MOV RCX, RAX  (RCX = len)
    mov-rax-nos                \ RAX = filename-addr
    48 c, 89 c, c6 c,          \ MOV RSI, RAX  (RSI = src)
    48 c, bf c, ELF-FN-BUF 8,  \ MOV RDI, ELF-FN-BUF  (dst)
    \ copy loop: while RCX>0 copy byte
    here constant FN_COPY_LOOP
    48 c, 83 c, f9 c, 00 c,    \ CMP RCX, 0
    asm-je fn-copy-done !
    8a c, 06 c,                \ MOV AL, [RSI]
    88 c, 07 c,                \ MOV [RDI], AL
    48 c, ff c, c6 c,          \ INC RSI
    48 c, ff c, c7 c,          \ INC RDI
    48 c, ff c, c9 c,          \ DEC RCX
    eb c, FN_COPY_LOOP here 1 + - c,  \ JMP FN_COPY_LOOP
    here constant FN_COPY_DONE
    fn-copy-done @ asm-resolve
    \ append NUL terminator
    48 c, c6 c, 07 c, 00 c,    \ MOV BYTE [RDI], 0
    \ --- 2. sys-creat(ELF-FN-BUF, 0755) -> fd ---
    \ push path-addr, mode onto DSP then call sys-creat
    48 c, b8 c, ELF-FN-BUF 8,  \ mov rax, ELF-FN-BUF
    add-rdi-8
    mov-tos-rax                \ push path-addr
    48 c, b8 c, 1ed 8,          \ mov rax, 0755 (0x1ed = 493)
    add-rdi-8
    mov-tos-rax                \ push mode
    e8 c, XT_SYS_CREAT t-vhere 4 + - 4,  \ CALL sys-creat ( path mode -- fd )
    \ fd is now TOS. Save it in ELF-FD (sys-write overwrites the fd slot).
    mov-rax-tos                \ rax = fd
    ELF-FD emit-store-rax-var  \ ELF-FD = fd
    \ --- 3. sys-write(fd, ELF-HDR-BUF, 120) ---
    \ push fd, addr, len then call sys-write
    ELF-FD emit-mov-rax-var     \ rax = fd
    add-rdi-8
    mov-tos-rax                \ push fd
    48 c, b8 c, ELF-HDR-BUF 8, \ mov rax, ELF-HDR-BUF
    add-rdi-8
    mov-tos-rax                \ push addr
    48 c, b8 c, 78 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 120
    add-rdi-8
    mov-tos-rax                \ push len
    e8 c, XT_SYS_WRITE t-vhere 4 + - 4,  \ CALL sys-write ( fd addr len -- count )
    sub-rdi-8                  \ drop count
    \ --- 3b. Pad file to 0x1000 (page-aligned) so the Linux loader accepts it.
    \ The header is 120 bytes; write 0x1000-120 = 0xF88 = 3976 zero bytes from
    \ ELF-ZERO-BUF so the image lands exactly at file offset 0x1000. ---
    \ We write 256-byte chunks: 3976 = 15*256 + 136. Loop 15 times writing 256
    \ bytes (3840), then one final write of 136 bytes (3976 total).
    \ Loop counter in R9: 15 iterations.
    49 c, c7 c, c1 c, 0f c, 00 c, 00 c, 00 c,  \ mov r9, 15
    \ pad_loop:
    here constant PAD_LOOP
    ELF-FD emit-mov-rax-var     \ rax = fd
    add-rdi-8
    mov-tos-rax                \ push fd
    48 c, b8 c, ELF-ZERO-BUF 8, \ mov rax, ELF-ZERO-BUF
    add-rdi-8
    mov-tos-rax                \ push addr
    48 c, b8 c, 00 c, 01 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 256
    add-rdi-8
    mov-tos-rax                \ push len
    e8 c, XT_SYS_WRITE t-vhere 4 + - 4,  \ CALL sys-write ( fd addr len -- count )
    sub-rdi-8                  \ drop count
    49 c, ff c, c9 c,          \ dec r9
    49 c, 83 c, f9 c, 00 c,    \ cmp r9, 0
    75 c, PAD_LOOP here 1 + - c,  \ jne PAD_LOOP
    \ final write of 136 bytes (0x88) to reach 3976 total
    ELF-FD emit-mov-rax-var     \ rax = fd
    add-rdi-8
    mov-tos-rax                \ push fd
    48 c, b8 c, ELF-ZERO-BUF 8, \ mov rax, ELF-ZERO-BUF
    add-rdi-8
    mov-tos-rax                \ push addr
    48 c, b8 c, 88 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 136
    add-rdi-8
    mov-tos-rax                \ push len
    e8 c, XT_SYS_WRITE t-vhere 4 + - 4,  \ CALL sys-write ( fd addr len -- count )
    sub-rdi-8                  \ drop count
    \ --- 4. sys-write(fd, 0x400000, HERE - 0x400000) ---
    \ push fd, addr, len then call sys-write
    ELF-FD emit-mov-rax-var     \ rax = fd
    add-rdi-8
    mov-tos-rax                \ push fd
    48 c, b8 c, ELF-ORIGIN 8,  \ mov rax, ELF-ORIGIN (0x400000)
    add-rdi-8
    mov-tos-rax                \ push addr
    \ len = HERE - 0x400000
    T-HERE-VAR emit-mov-rax-var \ rax = HERE
    48 c, bb c, ELF-ORIGIN 8,  \ mov rbx, ELF-ORIGIN
    sub-rax-rbx                \ rax = HERE - ELF-ORIGIN
    add-rdi-8
    mov-tos-rax                \ push len
    e8 c, XT_SYS_WRITE t-vhere 4 + - 4,  \ CALL sys-write ( fd addr len -- count )
    sub-rdi-8                  \ drop count
    \ --- 5. sys-close(fd) ---
    ELF-FD emit-mov-rax-var     \ rax = fd
    add-rdi-8
    mov-tos-rax                \ push fd
    e8 c, XT_SYS_CLOSE t-vhere 4 + - 4,  \ CALL sys-close ( fd -- status )
    sub-rdi-8                  \ drop status
    \ --- Done: restore DSP, pop 2 args, return ---
    asm-pop-rdi                \ restore DSP
    sub-rdi-8                  \ pop filename-len
    sub-rdi-8                  \ pop filename-addr
    mov-rax-rdi
    c3 c,
    decimal
t-end-code


decimal

\ Number buffer for '.' (decimal output)
hex 430200 constant NUM-BUF-END decimal

\ t-f8c3: Corrected `.` matching C prim_dot. Pops TOS, honors T-BASE-VAR
\ (decimal %ld by default, lowercase hex %lx when base==16), ALWAYS emits a
\ trailing space (0x20), then flushes (raw write syscall acts as flush).
variable dot-dec
variable dot-hexloop
variable dot-hexlt
variable dot-hzero        \ hex n==0 -> done
variable dot-hloopend     \ hex loop end -> done
variable dot-dpos
variable dot-dloop
variable dot-dzero        \ dec n==0 -> done
variable dot-dnosign

\ (.) ( n -- ) : internal helper. Prints n in DECIMAL with NO trailing space.
\ Used by .s / version / REPL prompt which per the C contract must stay
\ base-independent (always decimal) and manage their own separators.
variable pdot-dpos
variable pdot-dloop
variable pdot-nosign
variable pdot-done
t-code (.)
    t-vhere constant XT_PDOT
    mov-rax-tos      \ RAX = n
    sub-rdi-8        \ pop n
    asm-push-rdi     \ save DSP on return stack
    hex
    \ RBX = NUM-BUF-END (0x430200)
    48 c, bb c, 00 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RBX, 0x430200
    4d c, 31 c, c0 c,            \ XOR R8, R8 (sign flag = 0)
    cmp-rax-0
    asm-jge pdot-dpos !
    \ n < 0
    4d c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ MOV R8, 1
    48 c, f7 c, d8 c,            \ NEG RAX
    here constant PDOT_DPOS
    pdot-dpos @ asm-resolve
    \ if n != 0, convert
    cmp-rax-0
    asm-jne pdot-dloop !
    \ n == 0: store '0'
    48 c, c6 c, 03 c, 30 c,      \ MOV BYTE [RBX], '0'
    48 c, ff c, cb c,            \ DEC RBX
    asm-jmp pdot-done !
    here constant PDOT_DLOOP_LBL
    pdot-dloop @ asm-resolve
    48 c, b9 c, 0a c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RCX, 10
    here constant PDOT_DLOOP
    48 c, 31 c, d2 c,            \ XOR RDX, RDX
    48 c, f7 c, f1 c,            \ DIV RCX
    80 c, c2 c, 30 c,            \ ADD DL, '0'
    88 c, 13 c,                  \ MOV [RBX], DL
    48 c, ff c, cb c,            \ DEC RBX
    cmp-rax-0
    75 c, PDOT_DLOOP here 1 + - c,  \ JNE back to loop
    \ insert '-' if negative
    4d c, 83 c, f8 c, 01 c,      \ CMP R8, 1
    asm-jne pdot-nosign !
    48 c, c6 c, 03 c, 2d c,      \ MOV BYTE [RBX], '-'
    48 c, ff c, cb c,            \ DEC RBX
    here constant PDOT_NOSIGN
    pdot-nosign @ asm-resolve
    \ write string at RBX+1, length = 0x430200 - RBX (no trailing space)
    here constant PDOT_DONE_LBL
    pdot-done @ asm-resolve
    48 c, 8d c, 73 c, 01 c,      \ LEA RSI, [RBX+1]
    48 c, b8 c, 00 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RAX, 0x430200
    48 c, 29 c, d8 c,            \ SUB RAX, RBX
    48 c, 89 c, c2 c,            \ MOV RDX, RAX
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c,  \ RDI = 1
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ RAX = 1
    syscall
    asm-pop-rdi
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

t-code . ( n -- )
    t-vhere constant XT_DOT
    mov-rax-tos      \ RAX = n
    sub-rdi-8        \ pop n
    asm-push-rdi     \ save DSP on return stack

    hex
    \ RBX = base value from T-BASE-VAR
    T-BASE-VAR emit-mov-rbx-var   \ RBX = base
    48 c, 83 c, fb c, 10 c,      \ CMP RBX, 16
    asm-jne dot-dec !            \ base != 16 -> decimal path

    \ ===== HEX PATH (base == 16) =====
    \ RCX = 16 (divisor)
    48 c, b9 c, 10 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RCX, 16
    \ RBX = NUM-BUF-END (0x430200)
    48 c, bb c, 00 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RBX, 0x430200
    \ if RAX != 0, convert (unsigned, like C %lx)
    cmp-rax-0
    asm-jne dot-hexloop !
    \ n == 0: store '0'
    48 c, c6 c, 03 c, 30 c,      \ MOV BYTE [RBX], '0'
    48 c, ff c, cb c,            \ DEC RBX
    asm-jmp dot-hzero !
    here constant DOT_HEXLOOP
    dot-hexloop @ asm-resolve
    48 c, 31 c, d2 c,            \ XOR RDX, RDX
    48 c, f7 c, f1 c,            \ DIV RCX  (RDX = remainder 0-15)
    80 c, c2 c, 30 c,            \ ADD DL, '0'
    80 c, fa c, 39 c,            \ CMP DL, '9'
    76 asm-jump-op dot-hexlt !   \ JBE -> digit is 0-9, skip adjust
    80 c, c2 c, 27 c,            \ ADD DL, 0x27  (map 10-15 to 'a'-'f')
    dot-hexlt @ asm-resolve
    88 c, 13 c,                  \ MOV [RBX], DL
    48 c, ff c, cb c,            \ DEC RBX
    cmp-rax-0
    75 c, DOT_HEXLOOP here 1 + - c,  \ JNE back to loop
    asm-jmp dot-hloopend !

    \ ===== DECIMAL PATH (base != 16) =====
    here constant DOT_DEC_LBL
    dot-dec @ asm-resolve
    48 c, b9 c, 0a c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RCX, 10
    \ sign handling (decimal only)
    4d c, 31 c, c0 c,            \ XOR R8, R8 (sign flag = 0)
    cmp-rax-0
    asm-jge dot-dpos !           \ n >= 0 -> positive path
    \ n < 0
    4d c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ MOV R8, 1
    48 c, f7 c, d8 c,            \ NEG RAX
    here constant DOT_DPOS_LBL
    dot-dpos @ asm-resolve
    \ if n != 0, convert
    cmp-rax-0
    asm-jne dot-dloop !
    \ n == 0: store '0'
    48 c, bb c, 00 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RBX, 0x430200
    48 c, c6 c, 03 c, 30 c,      \ MOV BYTE [RBX], '0'
    48 c, ff c, cb c,            \ DEC RBX
    asm-jmp dot-dzero !
    here constant DOT_DLOOP_LBL
    dot-dloop @ asm-resolve
    48 c, bb c, 00 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RBX, 0x430200
    here constant DOT_DLOOP
    48 c, 31 c, d2 c,            \ XOR RDX, RDX
    48 c, f7 c, f1 c,            \ DIV RCX
    80 c, c2 c, 30 c,            \ ADD DL, '0'
    88 c, 13 c,                  \ MOV [RBX], DL
    48 c, ff c, cb c,            \ DEC RBX
    cmp-rax-0
    75 c, DOT_DLOOP here 1 + - c,  \ JNE back to loop
    \ insert '-' if decimal negative (R8 == 1)
    4d c, 83 c, f8 c, 01 c,      \ CMP R8, 1
    asm-jne dot-dnosign !
    48 c, c6 c, 03 c, 2d c,      \ MOV BYTE [RBX], '-'
    48 c, ff c, cb c,            \ DEC RBX
    dot-dnosign @ asm-resolve

    \ ===== DONE: append trailing space at 0x430201 & write once =====
    here constant DOT_DONE_LBL
    dot-hzero @ asm-resolve
    dot-hloopend @ asm-resolve
    dot-dzero @ asm-resolve
    \ trailing space: MOV RCX, 0x430201 ; MOV BYTE [RCX], 0x20
    48 c, b9 c, 01 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RCX, 0x430201
    c6 c, 01 c, 20 c,                                             \ MOV BYTE [RCX], 0x20
    \ RSI = RBX+1 (string start)
    48 c, 8d c, 73 c, 01 c,      \ LEA RSI, [RBX+1]
    \ RDX = 0x430201 - RBX (length, includes trailing space)
    48 c, b8 c, 01 c, 02 c, 43 c, 00 c, 00 c, 00 c, 00 c, 00 c,  \ MOV RAX, 0x430201
    48 c, 29 c, d8 c,            \ SUB RAX, RBX
    48 c, 89 c, c2 c,            \ MOV RDX, RAX
    \ write(1, rsi, rdx)  (unbuffered syscall -> flush)
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c,  \ RDI = 1
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ RAX = 1
    syscall

    asm-pop-rdi      \ restore DSP
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- EMIT and KEY (I/O) ---
t-code EMIT ( char -- )
    t-vhere constant XT_EMIT
    asm-push-rdi
    hex 48 c, c7 c, c2 c, 01 c, 00 c, 00 c, 00 c, \ RDX = 1
    48 c, 8d c, 77 c, f8 c,                   \ RSI = [RDI-8]
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c, \ RDI = 1
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c, \ RAX = 1
    syscall
    asm-pop-rdi sub-rdi-8 mov-rax-rdi c3 c,
t-end-code

t-code KEY ( -- char )
    t-vhere constant XT_KEY
    add-rdi-8 hex
    asm-push-rdi
    48 c, c7 c, c2 c, 01 c, 00 c, 00 c, 00 c, \ RDX = 1
    48 c, 8d c, 77 c, f8 c,                   \ RSI = [RDI-8]
    48 c, 31 c, ff c,                         \ RDI = 0
    48 c, 31 c, c0 c,                         \ RAX = 0
    syscall
    asm-pop-rdi \ Restore RDI (DSP)
    \ Check read return value (RAX) for EOF BEFORE clobbering RAX
    48 c, 83 c, f8 c, 00 c, \ CMP RAX, 0
    75 c, 0c c,             \ JNE got_char (skip EOF handling)
    \ EOF: push 0 as the char
    48 c, c7 c, 47 c, f8 c, 00 c, 00 c, 00 c, 00 c, \ MOV [RDI-8], 0
    mov-rax-rdi c3 c,
    \ got_char: zero-extend the byte read into the 8-byte slot
    48 c, 0f c, b6 c, 47 c, f8 c, \ MOVZX RAX, BYTE [RDI-8]
    48 c, 89 c, 47 c, f8 c,       \ MOV [RDI-8], RAX
    mov-rax-rdi c3 c,
t-end-code

\ --- Buffers ---
hex 430000 constant WORD-BUF-ADDR decimal
\ Dedicated counted-string buffer for s" (must NOT overlap WORD-BUF-ADDR,
\ because the token that follows s" in the REPL/compile-source is parsed into
\ WORD-BUF-ADDR and would clobber the string address otherwise).
hex 430400 constant S-BUF-ADDR decimal
\ t-g7h8: dedicated scratch buffer for ." (compile-time string collection).
\ 0x430300 sits clear of NUM-BUF-END (0x430200, written downward by . / (.)),
\ of S-BUF-ADDR (0x430400) and of INCLUDE-SRC-BUF (0x431000).
hex 430300 constant DOTQ-BUF-ADDR decimal

\ --- t-c001: String-print + newline helpers ---
\ type ( addr len -- )  : write len bytes from addr to stdout via native write syscall.
t-code type
    t-vhere constant XT_TYPE
    asm-push-rdi
    hex
    mov-rax-tos                \ RAX = len
    mov-rdx-rax                \ RDX = len
    mov-rax-nos                \ RAX = addr
    mov-rsi-rax                \ RSI = addr
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c,   \ RDI = 1 (stdout)
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,   \ RAX = 1 (SYS_WRITE)
    syscall
    asm-pop-rdi
    sub-rdi-8                  \ pop addr
    sub-rdi-8                  \ pop len
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ cr ( -- )  : emit newline via native EMIT
t-code cr
    t-vhere constant XT_CR
    hex
    48 c, c7 c, c0 c, 0a c, 00 c, 00 c, 00 c,   \ RAX = 10 ('\n')
    add-rdi-8
    mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,              \ CALL EMIT
    mov-rax-rdi
    c3 c,
    decimal

\ space ( -- )  : emit single space via native EMIT
t-code space
    t-vhere constant XT_SPACE
    hex
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c,   \ RAX = 32 (' ')
    add-rdi-8
    mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,              \ CALL EMIT
    mov-rax-rdi
    c3 c,
    decimal


\ --- getchar ( -- ch ) : read next char from current source (EVALUATE buffer), else stdin. ---
variable gch-src
variable gch-active
variable gch-key-real
hex
t-code getchar
    t-vhere constant XT_GETCHAR
    \ if SOURCE-ACTIVE: read from buffer; if exhausted return 0 (EOF)
    SOURCE-ACTIVE emit-mov-rax-var
    48 c, 83 c, f8 c, 00 c,        \ cmp rax, 0
    asm-je gch-active !
    SOURCE-PTR emit-mov-rax-var
    SOURCE-END emit-mov-rbx-var
    48 c, 39 c, d8 c,              \ CMP RAX, RBX (ptr - end)
    asm-jb gch-src !
    \ source exhausted & active -> push 0 (EOF)
    48 c, 31 c, c0 c,              \ xor rax, rax
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    \ src_avail:
    here constant GCH_AVAIL
    gch-src @ asm-resolve
    48 c, 89 c, c1 c,              \ mov rcx, rax (ptr)
    48 c, 0f c, b6 c, 01 c,        \ movzx rax, byte [rcx]
    48 c, ff c, c1 c,              \ inc rcx
    48 c, 89 c, cb c,              \ mov rbx, rcx
    SOURCE-PTR emit-store-rbx-var
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    \ not_active: -> KEY
    here constant GCH_KEY
    gch-active @ asm-resolve
    LINE-MODE emit-mov-rax-var
    emit-test-rax
    asm-je gch-key-real !          \ LINE-MODE==0 -> normal KEY
    48 c, 31 c, c0 c,              \ xor rax, rax
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    here constant GCH_KEY_REAL
    gch-key-real @ asm-resolve
    e8 c, XT_KEY t-vhere 4 + - 4,
    c3 c,
t-end-code
decimal

\ --- s" ( -- addr len ) : read a quoted string from source into WORD-BUF-ADDR,
\ then push ( addr len ) onto the stack. Uses GETCHAR so it works both from
\ stdin (REPL) and from an EVALUATE/compile-source buffer (SOURCE-ACTIVE).
variable s-q-end1
variable s-q-end2
t-code s"
    t-vhere constant XT_S_QUOTE
    hex
    asm-push-rbx
    48 c, bb c, S-BUF-ADDR 8,                   \ MOV RBX, S-BUF-ADDR
    \ SQ_LOOP:
    here constant SQ_LOOP
    \ t-v3a1: GETCHAR's line-buffer (src_avail) path sets RBX = SOURCE-PTR+1,
    \ which would clobber our write cursor. Preserve RBX across the call.
    asm-push-rbx
    e8 c, XT_GETCHAR t-vhere 4 + - 4,           \ CALL GETCHAR  ( -- ch )
    asm-pop-rbx
    mov-rax-tos
    sub-rdi-8                                   \ pop char
    48 c, 83 c, f8 c, 22 c,                     \ CMP RAX, 34 ('"')
    asm-je s-q-end1 !
    cmp-rax-0
    asm-je s-q-end2 !
    88 c, 03 c,                                 \ MOV [RBX], AL
    48 c, ff c, c3 c,                           \ INC RBX
    eb c, SQ_LOOP here 1 + - c,                 \ JMP SQ_LOOP
    \ SQ_DONE:
    here constant SQ_DONE
    s-q-end1 @ asm-resolve
    s-q-end2 @ asm-resolve
    \ push addr (S-BUF-ADDR)
    48 c, b8 c, S-BUF-ADDR 8,                   \ MOV RAX, S-BUF-ADDR
    add-rdi-8
    mov-tos-rax
    \ push len (RCX = RBX - S-BUF-ADDR)
    48 c, 89 c, d9 c,                           \ MOV RCX, RBX
    48 c, 81 c, e9 c, S-BUF-ADDR 4,             \ SUB RCX, S-BUF-ADDR
    48 c, 89 c, c8 c,                           \ MOV RAX, RCX
    add-rdi-8
    mov-tos-rax
    asm-pop-rbx
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- \ ( -- ) line comment: skip until newline (10) or EOF (0) ---
variable bs-je-eof
variable bs-je-nl
t-code \
    t-vhere constant XT_BACKSLASH
    hex
    here constant BS_LOOP
    e8 c, XT_GETCHAR t-vhere 4 + - 4,   \ CALL getchar ( -- ch )
    mov-rax-tos
    sub-rdi-8                           \ pop char
    48 c, 83 c, f8 c, 00 c,             \ CMP RAX, 0 (EOF)
    asm-je bs-je-eof !
    48 c, 83 c, f8 c, 0a c,             \ CMP RAX, 10 (\n)
    asm-je bs-je-nl !
    eb c, BS_LOOP here 1 + - c,         \ JMP BS_LOOP
    here constant BS_DONE
    bs-je-eof @ asm-resolve
    bs-je-nl @ asm-resolve
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- ( ( -- ) block comment: skip until ')' (41 / 0x29) or EOF (0) ---
variable paren-je-eof
variable paren-je-close
t-code (
    t-vhere constant XT_PAREN
    hex
    here constant PAREN_LOOP
    e8 c, XT_GETCHAR t-vhere 4 + - 4,   \ CALL getchar ( -- ch )
    mov-rax-tos
    sub-rdi-8                           \ pop char
    48 c, 83 c, f8 c, 00 c,             \ CMP RAX, 0 (EOF)
    asm-je paren-je-eof !
    48 c, 83 c, f8 c, 29 c,             \ CMP RAX, 41 (')')
    asm-je paren-je-close !
    eb c, PAREN_LOOP here 1 + - c,      \ JMP PAREN_LOOP
    here constant PAREN_DONE
    paren-je-eof @ asm-resolve
    paren-je-close @ asm-resolve
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- PARSE-NAME ( -- addr len ) ---
variable p-je-eof-skip
variable p-je-eof-collect
variable p-jne-notnl
t-code PARSE-NAME
    t-vhere constant XT_PARSE_NAME
    asm-push-rbx
    asm-push-rsi
    
    \ label: skip_ws
    here constant SKIP_WS_ADDR
    hex e8 c, XT_GETCHAR t-vhere 4 + - 4, \ Manual call getchar
    mov-rax-tos
    sub-rdi-8 \ Pop getchar char
    cmp-rax-0
    asm-je p-je-eof-skip !   \ char==0 means EOF
    \ if char == newline (10), set PROMPT-FLAG = IS-TTY-FLAG
    48 c, 83 c, f8 c, 0a c,  \ cmp rax, 10
    asm-jne p-jne-notnl !
    IS-TTY-FLAG emit-mov-rax-var
    PROMPT-FLAG emit-store-rax-var
    p-jne-notnl @ asm-resolve
    cmp-rax-32
    7e c, SKIP_WS_ADDR here - 1 - c, \ JLE skip_ws
    
    \ Collecting
    48 c, be c, WORD-BUF-ADDR 8, \ MOV RSI, WORD-BUF-ADDR
    48 c, 31 c, db c, \ XOR RBX, RBX (Counter)
    
    \ label: collect
    here constant COLLECT_ADDR
    88 c, 06 c, \ MOV [RSI], AL
    48 c, ff c, c6 c, \ INC RSI
    48 c, ff c, c3 c, \ INC RBX
    
    asm-push-rsi
    asm-push-rbx
    e8 c, XT_GETCHAR t-vhere 4 + - 4,
    asm-pop-rbx
    asm-pop-rsi
    
    mov-rax-tos
    sub-rdi-8 \ Pop char
    cmp-rax-0
    asm-je p-je-eof-collect ! \ char==0 -> EOF after a collected word
    cmp-rax-32
    77 c, COLLECT_ADDR here - 1 - c, \ JA collect ( > 32 )
    
    \ Return (addr len) -- reached on whitespace terminator OR EOF after a
    \ collected word. Returns the collected word with its length (RBX).
    here constant COLLECT_RETURN
    p-je-eof-collect @ asm-resolve
    WORD-BUF-ADDR t-lit
    48 c, 89 c, d8 c, \ MOV RAX, RBX (len)
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    asm-pop-rsi
    asm-pop-rbx
    c3 c,
    
    \ eof_return: push len=0 -- reached only via EOF during whitespace skip
    here constant EOF_RETURN
    p-je-eof-skip @ asm-resolve
    48 c, 31 c, c0 c, \ XOR RAX, RAX (len=0)
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    asm-pop-rsi
    asm-pop-rbx
    c3 c,
t-end-code


\ --- FIND ( addr len -- xt flags true | false ) ---
\ Registers: RSI=addr, RCX=len, RBX=current link, RAX=temp, RDX=dict name ptr, R8=counter
variable f-jne-len
variable f-jne-name
variable f-je-zero
variable f-je-found
variable f-fold-dict-a
variable f-fold-dict-z
variable f-fold-inp-a
variable f-fold-inp-z

t-code FIND
    t-vhere constant XT_FIND
    \ pop len into RCX, addr into RSI
    mov-rax-tos      \ RAX = len
    48 c, 89 c, c1 c, \ MOV RCX, RAX
    sub-rdi-8        \ pop len
    mov-rax-tos      \ RAX = addr
    48 c, 89 c, c6 c, \ MOV RSI, RAX
    49 c, 89 c, f1 c, \ MOV R9, RSI  (save base input addr)
    sub-rdi-8        \ pop addr
    
    \ current = [T-LATEST-VAR]
    48 c, 8b c, 1d c, T-LATEST-VAR t-vhere 4 + - 4, \ MOV RBX, [rip+T-LATEST-VAR]
    
    \ loop_start:
    here constant FIND_LOOP
    \ if current == 0: not found
    48 c, 83 c, fb c, 00 c, \ CMP RBX, 0
    asm-je32 f-je-zero !     \ (near jmp: FIND_NOTFOUND >127 bytes ahead)
    \ len_byte = [rbx+8] & 0x1F
    48 c, 0f c, b6 c, 43 c, 08 c, \ MOVZX RAX, BYTE [RBX+8]
    48 c, 83 c, e0 c, 1f c, \ AND RAX, 0x1F
    48 c, 39 c, c8 c, \ CMP RAX, RCX
    asm-jne f-jne-len !
    \ compare names: RSI = base (R9), RDX = [rbx+9], R8 = counter
    4c c, 89 c, ce c, \ MOV RSI, R9
    48 c, 8d c, 53 c, 09 c, \ LEA RDX, [RBX+9]
    4d c, 31 c, c0 c, \ XOR R8, R8
    \ cmp_loop:
    here constant CMP_LOOP
    49 c, 39 c, c8 c, \ CMP R8, RCX
    asm-je f-je-found !
    8a c, 02 c,                  \ MOV AL, [RDX]      (dict name byte)
    3c c, 41 c,                  \ CMP AL, 'A'
    asm-jb f-fold-dict-a !
    3c c, 5a c,                  \ CMP AL, 'Z'
    asm-ja f-fold-dict-z !
    0c c, 20 c,                  \ OR AL, 0x20         (case-fold dict byte)
    f-fold-dict-a @ asm-resolve
    f-fold-dict-z @ asm-resolve
    44 c, 8a c, 16 c,            \ MOV R10B, [RSI]    (input name byte)
    41 c, 80 c, fa c, 41 c,      \ CMP R10B, 'A'
    asm-jb f-fold-inp-a !
    41 c, 80 c, fa c, 5a c,      \ CMP R10B, 'Z'
    asm-ja f-fold-inp-z !
    41 c, 80 c, ca c, 20 c,      \ OR R10B, 0x20      (case-fold input byte)
    f-fold-inp-a @ asm-resolve
    f-fold-inp-z @ asm-resolve
    44 c, 38 c, d0 c,            \ CMP AL, R10B
    asm-jne f-jne-name !
    48 c, ff c, c2 c, \ INC RDX
    48 c, ff c, c6 c, \ INC RSI
    49 c, ff c, c0 c, \ INC R8
    eb c, CMP_LOOP here 1 + - c, \ JMP cmp_loop
    
    \ found:
    here constant FIND_FOUND
    f-je-found @ asm-resolve
    \ xt = align(rbx + 9 + len)
    48 c, 8d c, 43 c, 09 c, \ LEA RAX, [RBX+9]
    48 c, 01 c, c8 c, \ ADD RAX, RCX
    48 c, 83 c, c0 c, 07 c, \ ADD RAX, 7
    48 c, 83 c, e0 c, f8 c, \ AND RAX, -8
    \ push xt, flags, true
    add-rdi-8
    mov-tos-rax
    \ flags = full length/flags byte at [RBX+8] (preserves immediate bit 0x80)
    48 c, 0f c, b6 c, 43 c, 08 c, \ MOVZX RAX, BYTE [RBX+8]
    add-rdi-8
    mov-tos-rax
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, ff c, ff c, ff c, ff c, \ MOV [RDI-8], -1
    mov-rax-rdi
    c3 c,
    
    \ next_word:
    here constant FIND_NEXT
    f-jne-len @ asm-resolve
    f-jne-name @ asm-resolve
    \ follow link
    48 c, 8b c, 1b c, \ MOV RBX, [RBX]
    e9 c, FIND_LOOP here 4 + - 4, \ JMP loop_start (near jmp: back-edge >127 bytes away)
    
    \ not_found:
    here constant FIND_NOTFOUND
    f-je-zero @ asm-resolve32
    \ push false
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, 00 c, 00 c, 00 c, 00 c, \ MOV [RDI-8], 0
    mov-rax-rdi
    c3 c,
t-end-code

\ --- NUMBER? ( addr len -- n true | false ) ---
\ Registers: RSI=addr, RCX=len, R9=accum, RBX=counter, RDX=sign, RAX=digit
variable n-jne-parse
variable n-jge-done
variable n-notnum1
variable n-notnum2
variable n-notnum3
variable n-notnum4
variable n-notnum5
variable n-notnum6
variable n-jne-notnum
variable n-jne-pos
variable n-chk-lower
variable n-chk-upper
variable n-got-digit1
variable n-got-digit2
variable n-skip-0x1
variable n-skip-0x2
variable n-skip-0x3
variable n-match-0x

t-code NUMBER?
    t-vhere constant XT_NUMBER
    \ pop len into RCX, addr into RSI
    mov-rax-tos      \ RAX = len
    48 c, 89 c, c1 c, \ MOV RCX, RAX
    sub-rdi-8        \ pop len
    mov-rax-tos      \ RAX = addr
    48 c, 89 c, c6 c, \ MOV RSI, RAX
    sub-rdi-8        \ pop addr
    
    \ init: R9=0 (accum), RBX=0 (counter), RDX=0 (sign), R11=base
    4d c, 31 c, c9 c, \ XOR R9, R9
    48 c, 31 c, db c, \ XOR RBX, RBX
    48 c, 31 c, d2 c, \ XOR RDX, RDX
    T-BASE-VAR emit-mov-rax-var
    49 c, 89 c, c3 c, \ MOV R11, RAX (base)
    
    \ check for leading '-'
    48 c, 83 c, f9 c, 00 c, \ CMP RCX, 0
    asm-jle n-notnum1 !
    8a c, 06 c, \ MOV AL, [RSI]
    3c c, 2d c, \ CMP AL, '-'
    asm-jne n-jne-parse !
    48 c, c7 c, c2 c, 01 c, 00 c, 00 c, 00 c, \ MOV RDX, 1
    48 c, ff c, c3 c, \ INC RBX
    
    \ check for 0x / 0X prefix (if remaining len >= 2)
    here constant NUM_PARSE
    n-jne-parse @ asm-resolve
    48 c, 8d c, 43 c, 02 c, \ LEA RAX, [RBX+2]
    48 c, 39 c, c8 c,       \ CMP RAX, RCX
    asm-ja n-skip-0x1 !
    8a c, 04 c, 1e c,       \ MOV AL, [RSI+RBX]
    3c c, 30 c,             \ CMP AL, '0'
    asm-jne n-skip-0x2 !
    8a c, 44 c, 1e c, 01 c, \ MOV AL, [RSI+RBX+1]
    3c c, 78 c,             \ CMP AL, 'x'
    asm-je n-match-0x !
    3c c, 58 c,             \ CMP AL, 'X'
    asm-jne n-skip-0x3 !
    here constant MATCH_0X
    n-match-0x @ asm-resolve
    49 c, c7 c, c3 c, 10 c, 00 c, 00 c, 00 c, \ MOV R11, 16 (hex)
    48 c, 83 c, c3 c, 02 c, \ ADD RBX, 2 (skip '0x')
    
    \ parse_digits loop:
    here constant NUM_DIGIT_LOOP
    n-skip-0x1 @ asm-resolve
    n-skip-0x2 @ asm-resolve
    n-skip-0x3 @ asm-resolve
    48 c, 39 c, cb c, \ CMP RBX, RCX
    asm-jge n-jge-done !
    48 c, 0f c, b6 c, 04 c, 1e c, \ MOVZX RAX, BYTE [RSI+RBX]
    \ '0'..'9'
    48 c, 83 c, f8 c, 30 c, \ CMP RAX, '0'
    asm-jb n-notnum2 !
    48 c, 83 c, f8 c, 39 c, \ CMP RAX, '9'
    asm-ja n-chk-lower !
    48 c, 83 c, e8 c, 30 c, \ SUB RAX, '0'
    asm-jmp n-got-digit1 !
    
    \ 'a'..'f'
    here constant NUM_CHK_LOWER
    n-chk-lower @ asm-resolve
    48 c, 83 c, f8 c, 61 c, \ CMP RAX, 'a'
    asm-jb n-chk-upper !
    48 c, 83 c, f8 c, 66 c, \ CMP RAX, 'f'
    asm-ja n-notnum3 !
    48 c, 83 c, e8 c, 57 c, \ SUB RAX, 0x57 ('a'-10)
    asm-jmp n-got-digit2 !
    
    \ 'A'..'F'
    here constant NUM_CHK_UPPER
    n-chk-upper @ asm-resolve
    48 c, 83 c, f8 c, 41 c, \ CMP RAX, 'A'
    asm-jb n-notnum4 !
    48 c, 83 c, f8 c, 46 c, \ CMP RAX, 'F'
    asm-ja n-notnum5 !
    48 c, 83 c, e8 c, 37 c, \ SUB RAX, 0x37 ('A'-10)
    
    \ got_digit: RAX = digit
    here constant NUM_GOT_DIGIT
    n-got-digit1 @ asm-resolve
    n-got-digit2 @ asm-resolve
    49 c, 39 c, c3 c,       \ CMP R11, RAX (base vs digit)
    asm-jle n-notnum6 !     \ digit >= base -> not number
    4d c, 0f c, af c, cb c, \ IMUL R9, R11 (accum * base)
    4c c, 01 c, c8 c,       \ ADD RAX, R9
    49 c, 89 c, c1 c,       \ MOV R9, RAX
    48 c, ff c, c3 c,       \ INC RBX
    eb c, NUM_DIGIT_LOOP here 1 + - c, \ JMP NUM_DIGIT_LOOP
    
    \ done_parse:
    here constant NUM_DONE
    n-jge-done @ asm-resolve
    48 c, 39 c, cb c, \ CMP RBX, RCX
    asm-jne n-jne-notnum !
    \ apply sign
    48 c, 83 c, fa c, 01 c, \ CMP RDX, 1
    asm-jne n-jne-pos !
    49 c, f7 c, d9 c, \ NEG R9
    \ positive:
    here constant NUM_POS
    n-jne-pos @ asm-resolve
    \ push n, true
    add-rdi-8
    4c c, 89 c, 4f c, f8 c, \ MOV [RDI-8], R9
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, ff c, ff c, ff c, ff c, \ MOV [RDI-8], -1
    mov-rax-rdi
    c3 c,
    
    \ not_number:
    here constant NUM_NOTNUM
    n-notnum1 @ asm-resolve
    n-notnum2 @ asm-resolve
    n-notnum3 @ asm-resolve
    n-notnum4 @ asm-resolve
    n-notnum5 @ asm-resolve
    n-notnum6 @ asm-resolve
    n-jne-notnum @ asm-resolve
    \ push false
    add-rdi-8
    48 c, c7 c, 47 c, f8 c, 00 c, 00 c, 00 c, 00 c, \ MOV [RDI-8], 0
    mov-rax-rdi
    c3 c,
t-end-code

\ hex ( -- ) : set base to 16
hex
t-code hex
    t-vhere constant XT_HEX
    48 c, c7 c, c0 c, 10 c, 00 c, 00 c, 00 c, \ mov rax, 16
    T-BASE-VAR emit-store-rax-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ decimal ( -- ) : set base to 10
hex
t-code decimal
    t-vhere constant XT_DECIMAL
    48 c, c7 c, c0 c, 0a c, 00 c, 00 c, 00 c, \ mov rax, 10
    T-BASE-VAR emit-store-rax-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- EXECUTE ( xt -- ) ---
t-code EXECUTE ( xt -- )
    t-vhere constant XT_EXECUTE
    mov-rax-tos      \ RAX = xt
    sub-rdi-8        \ pop xt
    hex ff c, d0 c, \ CALL RAX
    c3 c,            \ ret (word returned DSP in RAX)
t-end-code

\ --- Runtime dictionary helpers (t-c002) ---
hex
\ here ( -- addr ) : push current runtime dictionary pointer
t-code HERE
    t-vhere constant XT_HERE
    T-HERE-VAR emit-mov-rax-var   \ mov rax,[rip+T-HERE-VAR]
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ c, ( ch -- ) : emit byte into runtime dictionary, advance HERE
t-code c, ( ch -- )
    t-vhere constant XT_CCOMMA
    mov-rax-tos      \ RAX = ch
    sub-rdi-8        \ pop ch
    T-HERE-VAR emit-mov-rbx-var   \ RBX = here
    88 c, 03 c,                  \ mov [rbx], al
    48 c, ff c, c3 c,            \ inc rbx
    T-HERE-VAR emit-store-rbx-var \ here++
    mov-rax-rdi
    c3 c,
t-end-code

\ , ( n -- ) : store qword into high dictionary, advance by 8
t-code C,2 ( n -- )
    t-vhere constant XT_COMMA
    mov-rax-tos      \ RAX = n
    sub-rdi-8        \ pop n
    T-HERE-VAR emit-mov-rbx-var   \ RBX = here
    48 c, 89 c, 03 c,            \ mov [rbx], rax
    48 c, 83 c, c3 c, 08 c,      \ add rbx, 8
    T-HERE-VAR emit-store-rbx-var \ here += 8
    mov-rax-rdi
    c3 c,
t-end-code

\ align8 ( -- ) : round HERE up to 8
t-code align8
    t-vhere constant XT_ALIGN8
    T-HERE-VAR emit-mov-rax-var
    48 c, 83 c, c0 c, 07 c,    \ add rax, 7
    48 c, 83 c, e0 c, f8 c,    \ and rax, -8
    T-HERE-VAR emit-store-rax-var
    mov-rax-rdi
    c3 c,
t-end-code

\ call,  ( xt -- ) -- emit CALL xt + MOV RDI,RAX into code area.
\ The trailing MOV RDI,RAX re-syncs DSP (RAX=DSP-out) into RDI so a compiled
\ body (a chain of native CALLs) correctly propagates DSP between words.
t-code call,
    t-vhere constant XT_CALLCOMMA
    mov-rax-tos      \ RAX = xt
    sub-rdi-8        \ pop xt
    T-HERE-VAR emit-mov-rbx-var
    48 c, c6 c, 03 c, e8 c,    \ mov byte [rbx], 0xE8
    48 c, 89 c, c1 c,          \ mov rcx, rax (xt)
    T-HERE-VAR emit-mov-rax-var \ rax = here
    48 c, 83 c, c0 c, 05 c,    \ add rax, 5
    48 c, 29 c, c1 c,          \ sub rcx, rax  (rcx = rel32)
    89 c, 4b c, 01 c,          \ mov dword [rbx+1], ecx  (4-byte rel32 store)
    48 c, 83 c, c3 c, 05 c,    \ add rbx, 5  (rbx now at end of CALL)
    48 c, c6 c, 03 c, 48 c,    \ mov byte [rbx], 0x48
    48 c, c6 c, 43 c, 01 c, 89 c, \ mov byte [rbx+1], 0x89
    48 c, c6 c, 43 c, 02 c, c7 c, \ mov byte [rbx+2], 0xc7 (mov rdi,rax)
    48 c, 83 c, c3 c, 03 c,    \ add rbx, 3  (total 8)
    T-HERE-VAR emit-store-rbx-var
    mov-rax-rdi
    c3 c,
t-end-code

\ lit, ( n -- ) : emit a runtime literal-push helper call with inline 8-byte payload.
\ Runtime helper c-push-literal is defined elsewhere; here we emit:
\   call c-push-literal ; .data64 n
\ lit, ( n -- )  : emit literal push sequence into code dictionary
t-code lit,
    t-vhere constant XT_LITCOMMA
    mov-rax-tos      \ RAX = n
    sub-rdi-8        \ pop n
    T-HERE-VAR emit-mov-rbx-var   \ RBX = here
    48 c, c6 c, 03 c, 48 c,    \ mov byte [rbx], 0x48
    48 c, c6 c, 43 c, 01 c, b8 c, \ mov byte [rbx+1], 0xb8
    48 c, 89 c, 43 c, 02 c,    \ mov [rbx+2], rax  (imm64)
    48 c, c6 c, 43 c, 0a c, 48 c, \ mov byte [rbx+10], 0x48
    48 c, c6 c, 43 c, 0b c, 83 c, \ mov byte [rbx+11], 0x83
    48 c, c6 c, 43 c, 0c c, c7 c, \ mov byte [rbx+12], 0xc7
    48 c, c6 c, 43 c, 0d c, 08 c, \ mov byte [rbx+13], 0x08
    48 c, c6 c, 43 c, 0e c, 48 c, \ mov byte [rbx+14], 0x48
    48 c, c6 c, 43 c, 0f c, 89 c, \ mov byte [rbx+15], 0x89
    48 c, c6 c, 43 c, 10 c, 47 c, \ mov byte [rbx+16], 0x47
    48 c, c6 c, 43 c, 11 c, f8 c, \ mov byte [rbx+17], 0xf8
    \ trailing: mov rax,rdi (48 89 f8) so DSP is returned in RAX
    48 c, c6 c, 43 c, 12 c, 48 c, \ mov byte [rbx+18], 0x48
    48 c, c6 c, 43 c, 13 c, 89 c, \ mov byte [rbx+19], 0x89
    48 c, c6 c, 43 c, 14 c, f8 c, \ mov byte [rbx+20], 0xf8
    48 c, 83 c, c3 c, 15 c,    \ add rbx, 21
    T-HERE-VAR emit-store-rbx-var
    mov-rax-rdi
    c3 c,
t-end-code

\ ' ( "name" -- xt ) : parse name and return XT
hex
t-code '
    t-vhere constant XT_TICK
    e8 c, XT_PARSE_NAME t-vhere 4 + - 4, \ ( -- addr len )
    e8 c, XT_FIND t-vhere 4 + - 4,       \ ( -- xt flags true | false )
    mov-rax-tos
    cmp-rax-0
    74 c, 09 c,                         \ je not_found
    sub-rdi-8                           \ drop true
    sub-rdi-8                           \ drop flags
    mov-rax-rdi
    c3 c,
    \ not_found:
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ['] ( "name" -- ) : immediate word to compile XT literal
hex
t-code [']
    t-vhere constant XT_BRACKET_TICK
    e8 c, XT_PARSE_NAME t-vhere 4 + - 4, \ ( -- addr len )
    e8 c, XT_FIND t-vhere 4 + - 4,       \ ( -- xt flags true | false )
    sub-rdi-8                           \ drop true
    sub-rdi-8                           \ drop flags
    T-STATE-VAR emit-mov-rax-var
    emit-test-rax
    74 c, 09 c,                         \ je interpret_mode
    e8 c, XT_LITCOMMA t-vhere 4 + - 4,   \ compile mode: lit,
    mov-rax-rdi
    c3 c,
    \ interpret mode:
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ (code) ( -- 0 ) : compatibility cell
hex
t-code (code)
    t-vhere constant XT_PAREN_CODE
    48 c, c7 c, c0 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 0
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-g7h8: (.") ( -- ) : RUNTIME printer for compiled ." strings.
\ Reads len from the inline literal (at return address + 3, after the
\ call, -emitted `mov rdi,rax`), writes exactly len bytes to stdout
\ (NO trailing space, NO newline), then advances the return address
\ past the inline string (cell-aligned) so execution continues after it.
\ Matches C prim_dot_quote_run.
t-code (.")
    t-vhere constant XT_DOT_QUOTE_RUN
    hex
    asm-push-rdi                \ save DSP on hardware stack
    \ r8 = return address (points to the `mov rdi,rax` after the call)
    48 c, 8b c, 44 c, 24 c, 08 c,   \ mov rax, [rsp+8]
    49 c, 89 c, c0 c,               \ mov r8, rax
    49 c, 83 c, c0 c, 03 c,         \ add r8, 3  (r8 = &len literal)
    \ rcx = len = [r8] ; r9 = len (saved across syscall)
    49 c, 8b c, 08 c,               \ mov rcx, [r8]
    49 c, 89 c, c9 c,               \ mov r9, rcx
    \ rsi = string addr = r8 + 8
    49 c, 8d c, 70 c, 08 c,         \ lea rsi, [r8+8]
    \ write(1, rsi, len)
    4c c, 89 c, ca c,               \ mov rdx, r9
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c,  \ mov rdi, 1
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ mov rax, 1
    syscall
    \ advance return address: align8(r8 + 8 + len)
    4c c, 89 c, c0 c,               \ mov rax, r8
    48 c, 83 c, c0 c, 08 c,         \ add rax, 8
    4c c, 01 c, c8 c,               \ add rax, r9
    48 c, 83 c, c0 c, 07 c,         \ add rax, 7
    48 c, 83 c, e0 c, f8 c,         \ and rax, -8
    48 c, 89 c, 44 c, 24 c, 08 c,   \ mov [rsp+8], rax
    asm-pop-rdi                     \ restore DSP
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-g7h8: ." ( -- ) : IMMEDIATE compile-time word matching C prim_dot_quote.
\ Parses the in-line quoted string (via GETCHAR, source-buffer aware) into
\ DOTQ-BUF-ADDR, then:
\   - COMPILING (T-STATE != 0): compiles  call, (.") ; len-literal ;
\     inline string bytes ; align.  The runtime (.") prints the bytes with
\     NO trailing space and NO automatic newline (matching C).
\   - INTERPRETING (T-STATE == 0): pushes (addr len) and CALLs TYPE immediately.
\ Output has NO trailing space and NO automatic newline, matching C.
variable dq7-end1
variable dq7-end2
variable dq7-interp
variable dq7-done
variable dqcopy-done
t-code ."
    t-vhere constant XT_DOT_QUOTE
    hex
    asm-push-rbx
    48 c, bb c, DOTQ-BUF-ADDR 8,                \ MOV RBX, DOTQ-BUF-ADDR
    \ DQ7_LOOP:
    here constant DQ7_LOOP
    \ preserve RBX (write cursor) across GETCHAR (its src_avail path clobbers RBX)
    asm-push-rbx
    e8 c, XT_GETCHAR t-vhere 4 + - 4,           \ CALL GETCHAR  ( -- ch )
    asm-pop-rbx
    mov-rax-tos
    sub-rdi-8                                   \ pop char
    48 c, 83 c, f8 c, 22 c,                     \ CMP RAX, 34 ('"')
    asm-je dq7-end1 !
    cmp-rax-0
    asm-je dq7-end2 !
    88 c, 03 c,                                 \ MOV [RBX], AL
    48 c, ff c, c3 c,                           \ INC RBX
    eb c, DQ7_LOOP here 1 + - c,                \ JMP DQ7_LOOP
    \ DQ7_DONE (string collected):
    here constant DQ7_DONE
    dq7-end1 @ asm-resolve
    dq7-end2 @ asm-resolve
    \ RCX = len = RBX - DOTQ-BUF-ADDR
    48 c, 89 c, d9 c,                           \ MOV RCX, RBX
    48 c, 81 c, e9 c, DOTQ-BUF-ADDR 4,          \ SUB RCX, DOTQ-BUF-ADDR
    \ check T-STATE-VAR: 0=interpret, nonzero=compile
    T-STATE-VAR emit-mov-rax-var
    emit-test-rax
    asm-je dq7-interp !
    \ ===== COMPILE MODE =====
    \ save len in R9 (call, clobbers RCX)
    49 c, 89 c, c9 c,                           \ mov r9, rcx
    \ compile: call, XT_DOT_QUOTE_RUN
    XT_DOT_QUOTE_RUN emit-push-imm
    XT_CALLCOMMA asm-call-sync
    \ compile: len literal (8 bytes) at HERE
    T-HERE-VAR emit-mov-rbx-var                 \ RBX = HERE
    4c c, 89 c, 0b c,                           \ mov [rbx], r9 (len)
    48 c, 83 c, c3 c, 08 c,                     \ add rbx, 8
    T-HERE-VAR emit-store-rbx-var
    \ compile: inline string bytes (copy DOTQ-BUF-ADDR -> HERE)
    48 c, be c, DOTQ-BUF-ADDR 8,                \ mov rsi, DOTQ-BUF-ADDR
    T-HERE-VAR emit-mov-rbx-var                 \ RBX = HERE
    here constant DQCOPY_LOOP
    49 c, 83 c, f9 c, 00 c,                     \ cmp r9, 0
    asm-je dqcopy-done !
    8a c, 06 c,                                 \ mov al, [rsi]
    88 c, 03 c,                                 \ mov [rbx], al
    48 c, ff c, c6 c,                           \ inc rsi
    48 c, ff c, c3 c,                           \ inc rbx
    49 c, ff c, c9 c,                           \ dec r9
    eb c, DQCOPY_LOOP here 1 + - c,            \ JMP DQCOPY_LOOP
    here constant DQCOPY_DONE
    dqcopy-done @ asm-resolve
    T-HERE-VAR emit-store-rbx-var
    \ align HERE to 8
    T-HERE-VAR emit-mov-rax-var
    48 c, 83 c, c0 c, 07 c,                     \ add rax, 7
    48 c, 83 c, e0 c, f8 c,                     \ and rax, -8
    T-HERE-VAR emit-store-rax-var
    asm-jmp dq7-done !
    \ ===== INTERPRET MODE =====
    here constant DQ7_INTERP
    dq7-interp @ asm-resolve
    \ push addr (DOTQ-BUF-ADDR)
    48 c, b8 c, DOTQ-BUF-ADDR 8,                \ MOV RAX, DOTQ-BUF-ADDR
    add-rdi-8
    mov-tos-rax
    \ push len (RCX)
    48 c, 89 c, c8 c,                           \ MOV RAX, RCX
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,              \ CALL TYPE
    \ DQ7_DONE2:
    here constant DQ7_DONE2
    dq7-done @ asm-resolve
    asm-pop-rbx
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ ============================================================
\ t-c002: TARGET-SIDE COMPILER + CONTROL FLOW
\ These are IMMEDIATE words running in the target REPL. When T-STATE
\ is nonzero the REPL compiles a word; otherwise it interprets it.
\ ============================================================
hex

\ --- Control-flow stack ---
\ c-push ( n -- ) : push n onto the target control-flow stack.
\ addr = T-CSTACK-BASE + old_depth*8 ; depth++
t-code c-push ( n -- )
    t-vhere constant XT_CPUSH
    mov-rax-tos                 \ RAX = n
    sub-rdi-8                   \ pop n
    48 c, 89 c, c3 c,           \ mov rbx, rax (save n in RBX)
    T-CDEPTH emit-mov-rcx-var   \ rcx = depth (old)
    48 c, 89 c, c8 c,           \ mov rax, rcx
    48 c, ff c, c0 c,           \ inc rax
    T-CDEPTH emit-store-rax-var \ depth++
    T-CSTACK-BASE emit-mov-rax-var  \ rax = base
    48 c, 48 c, c1 c, e1 c, 03 c,  \ shl rcx, 3  (rcx*8)
    48 c, 01 c, c8 c,              \ add rax, rcx (rax = base + old_depth*8)
    48 c, 89 c, 18 c,              \ mov [rax], rbx  (store n)
    mov-rax-rdi
    c3 c,
t-end-code

\ c-pop ( -- n ) : pop top of control-flow stack.
\ addr = base + (depth-1)*8 ; value=[addr]; depth--
variable cp-jle
variable cp-jmp
t-code c-pop ( -- n )
    t-vhere constant XT_CPOP
    T-CDEPTH emit-mov-rax-var   \ rax = depth
    48 c, 83 c, f8 c, 00 c,     \ cmp rax, 0
    asm-jle cp-jle !
    \ non-empty: addr = base + (depth-1)*8
    T-CSTACK-BASE emit-mov-rbx-var   \ rbx = base
    48 c, 83 c, e8 c, 01 c,     \ dec rax (depth-1)
    48 c, 48 c, c1 c, e0 c, 03 c, \ shl rax,3
    48 c, 01 c, d8 c,           \ add rax, rbx  (rax = addr)
    48 c, 8b c, 18 c,           \ mov rbx, [rax]  (value)
    \ decrement depth
    T-CDEPTH emit-mov-rax-var
    48 c, ff c, c8 c,           \ dec rax
    T-CDEPTH emit-store-rax-var
    48 c, 89 c, d8 c,           \ mov rax, rbx (value)
    asm-jmp cp-jmp !
    \ cpop_empty:
    here constant CPOP_EMPTY
    cp-jle @ asm-resolve
    48 c, 31 c, c0 c,           \ xor rax, rax (0)
    \ cpop_done:
    here constant CPOP_DONE
    cp-jmp @ asm-resolve
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ c-@ ( -- n ) : fetch top of control stack without popping
variable cp-at-jle
t-code c-@ ( -- n )
    t-vhere constant XT_CFETCH_CSTACK
    T-CDEPTH emit-mov-rax-var   \ rax = depth
    48 c, 83 c, f8 c, 00 c,     \ cmp rax, 0
    asm-jle cp-at-jle !
    \ non-empty: addr = base + (depth-1)*8
    T-CSTACK-BASE emit-mov-rbx-var   \ rbx = base
    48 c, 83 c, e8 c, 01 c,     \ dec rax (depth-1)
    48 c, 48 c, c1 c, e0 c, 03 c, \ shl rax,3
    48 c, 01 c, d8 c,           \ add rax, rbx  (rax = addr)
    48 c, 8b c, 18 c,           \ mov rbx, [rax]  (value)
    add-rdi-8
    mov-tos-rbx
    mov-rax-rdi
    c3 c,
    here constant CP_AT_EMPTY
    cp-at-jle @ asm-resolve
    \ underflow: push 0
    48 c, 31 c, c0 c,           \ xor rax, rax
    add-rdi-8
    mov-tos-rax
    mov-rax-rdi
    c3 c,
t-end-code

\ >r ( n -- )
t-code >r ( n -- )
    t-vhere constant XT_TO_R
    e8 c, XT_CPUSH t-vhere 4 + - 4,
    c3 c,
t-end-code

\ r> ( -- n )
t-code r> ( -- n )
    t-vhere constant XT_R_FROM
    e8 c, XT_CPOP t-vhere 4 + - 4,
    c3 c,
t-end-code

\ r@ ( -- n )
t-code r@ ( -- n )
    t-vhere constant XT_R_FETCH
    e8 c, XT_CFETCH_CSTACK t-vhere 4 + - 4,
    c3 c,
t-end-code

\ ============================================================
\ t-c002 + t-c003: TARGET-SIDE COMPILER + CONTROL FLOW + EVALUATE
\ ============================================================
\ Words marked t-immediate execute at compile time (during colon defs).
\ REPL switches on T-STATE-VAR: 0=interpret, 1=compile.
\ Control-flow words push/pop branch-field addresses on the target
\ control-flow stack (T-CSTACK-ARR, depth in T-CDEPTH).

hex

\ --- create-header ( addr len -- ) : create a new dict header at HERE. ---
\ Layout: [link qword][len+flags byte][name bytes][align to 8]
variable ch-jle-copy
t-code create-header
    t-vhere constant XT_CREATEHDR
    mov-rax-tos                  \ RAX = len
    sub-rdi-8                    \ pop len
    48 c, 89 c, c1 c,            \ mov rcx, rax   (rcx = len)
    mov-rax-tos                  \ RAX = name addr
    sub-rdi-8                    \ pop addr
    48 c, 89 c, c3 c,            \ mov rbx, rax   (rbx = name addr)
    \ rdx = here
    T-HERE-VAR emit-mov-rdx-var
    \ [here] = link ; latest = here
    T-LATEST-VAR emit-mov-rax-var
    48 c, 89 c, 02 c,            \ mov [rdx], rax  (link)
    48 c, 89 c, d0 c,            \ mov rax, rdx    (rax = here)
    T-LATEST-VAR emit-store-rax-var
    48 c, 83 c, c2 c, 08 c,      \ add rdx, 8
    \ len byte at [here]
    48 c, 89 c, c8 c,            \ mov rax, rcx
    48 c, 88 c, 02 c,            \ mov [rdx], al
    48 c, ff c, c2 c,            \ inc rdx
    \ name copy loop (rbx=src, rdx=dst, rcx=len)
    here constant NAME_COPY
    48 c, 83 c, f9 c, 00 c,      \ cmp rcx, 0
    asm-jle ch-jle-copy !
    8a c, 03 c,                  \ mov al, [rbx]
    88 c, 02 c,                  \ mov [rdx], al
    48 c, ff c, c3 c,            \ inc rbx
    48 c, ff c, c2 c,            \ inc rdx
    48 c, ff c, c9 c,            \ dec rcx
    eb c, NAME_COPY here 1 + - c, \ jmp NAME_COPY
    here constant NAME_END
    ch-jle-copy @ asm-resolve
    48 c, 83 c, c2 c, 07 c,      \ add rdx, 7
    48 c, 83 c, e2 c, f8 c,      \ and rdx, -8
    T-HERE-VAR emit-store-rdx-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- : ( "name" -- ) : create header and enter compile mode. ---
hex
t-code :
    t-vhere constant XT_COLON
    XT_PARSE_NAME asm-call-sync  \ ( addr len )
    XT_CREATEHDR asm-call-sync   \ ( -- )
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ mov rax, 1
    T-STATE-VAR emit-store-rax-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- ; ( -- ) : emit EXIT (c3) and return to interpret. ---
hex
t-code ;
    \ Emit: mov rax,rdi (48 89 f8) ; ret (c3)   -- so the compiled
    \ body returns DSP in RAX regardless of what the last word left.
    T-HERE-VAR emit-mov-rbx-var
    48 c, c6 c, 03 c, 48 c,      \ mov byte [rbx], 0x48
    48 c, c6 c, 43 c, 01 c, 89 c, \ mov byte [rbx+1], 0x89
    48 c, c6 c, 43 c, 02 c, f8 c, \ mov byte [rbx+2], 0xf8
    48 c, c6 c, 43 c, 03 c, c3 c, \ mov byte [rbx+3], 0xc3 (ret)
    48 c, 83 c, c3 c, 04 c,      \ add rbx, 4
    T-HERE-VAR emit-store-rbx-var
    48 c, 31 c, c0 c,            \ xor rax, rax
    T-STATE-VAR emit-store-rax-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- branch0, ( -- field ) : emit runtime flag-test (pop flag, set ZF) + 0F 84 rel32 placeholder.
\ Emits: mov rax,[rdi-8] ; test rax,rax ; sub rdi,8 ; 0f 84 <rel32> ; push field addr.
hex
t-code branch0,
    t-vhere constant XT_BRANCH0
    T-HERE-VAR emit-mov-rdx-var
    \ FIX (ZF-preserving): sub rdi,8; mov rax,[rdi]; test rax,rax; nop; 0f 84
    \ sub rdi,8 = 48 83 ef 08   (pop flag FIRST so test sets ZF right before je)
    c6 c, 02 c, 48 c,
    c6 c, 42 c, 01 c, 83 c,
    c6 c, 42 c, 02 c, ef c,
    c6 c, 42 c, 03 c, 08 c,
    \ mov rax,[rdi]   = 48 8b 07
    c6 c, 42 c, 04 c, 48 c,
    c6 c, 42 c, 05 c, 8b c,
    c6 c, 42 c, 06 c, 07 c,
    \ test rax,rax    = 48 85 c0
    c6 c, 42 c, 07 c, 48 c,
    c6 c, 42 c, 08 c, 85 c,
    c6 c, 42 c, 09 c, c0 c,
    \ nop = 90  (padding; field still at +0x0d)
    c6 c, 42 c, 0a c, 90 c,
    \ 0f 84
    c6 c, 42 c, 0b c, 0f c,
    c6 c, 42 c, 0c c, 84 c,
    \ field = rdx + 0x0d (rel32 byte)
    48 c, 8d c, 42 c, 0d c,
    \ here += 0x11
    48 c, 83 c, c2 c, 11 c,
    T-HERE-VAR emit-store-rdx-var
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- jump, ( -- field ) : emit E9 rel32 + push field addr. ---
hex
t-code jump,
    t-vhere constant XT_JUMP
    T-HERE-VAR emit-mov-rdx-var
    c6 c, 02 c, e9 c,            \ mov byte [rdx], 0xe9
    48 c, 8d c, 42 c, 01 c,      \ lea rax, [rdx+1]  (rel32 field)
    48 c, 83 c, c2 c, 05 c,      \ add rdx, 5  (e9 + rel32 = 5 bytes)
    T-HERE-VAR emit-store-rdx-var
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- patch ( fieldaddr -- ) : set rel32 at fieldaddr to branch to HERE. ---
hex
t-code patch
    t-vhere constant XT_PATCH
    mov-rax-tos                 \ rax = field
    sub-rdi-8                   \ pop field
    48 c, 89 c, c3 c,           \ mov rbx, rax (field)
    T-HERE-VAR emit-mov-rax-var \ rax = here
    48 c, 89 c, c1 c,           \ mov rcx, rax (here)
    48 c, 83 c, c3 c, 04 c,     \ add rbx, 4 (field+4)
    48 c, 89 c, c8 c,           \ mov rax, rcx
    48 c, 29 c, d8 c,           \ sub rax, rbx
    89 c, 43 c, fc c,           \ mov [rbx-4], eax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- if ( -- ) : emit runtime flag-test + JZ forward placeholder. ---
hex
t-code if
    XT_BRANCH0 asm-call-sync
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- then ( field -- ) resolve forward branch. ---
hex
t-code then
    XT_PATCH asm-call-sync
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- else ( if-field -- else-field ) ---
hex
t-code else
    XT_JUMP asm-call-sync       \ ( if-field jump-field )
    mov-rax-tos
    sub-rdi-8
    asm-push-rax                \ save jump-field
    XT_PATCH asm-call-sync      \ patch if-field -> else start
    asm-pop-rax
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- begin ( -- ) push here to control-flow stack. ---
hex
t-code begin
    T-HERE-VAR emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    XT_CPUSH asm-call-sync
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ --- until ( ... flag ) : pop loop entry, JZ back while flag==0. ---
hex
t-code until
    XT_CPOP asm-call-sync          \ ( ... caddr )  pop begin addr
    mov-rax-tos                 \ rax = caddr
    sub-rdi-8                   \ pop caddr
    48 c, 89 c, c3 c,           \ mov rbx, rax (rbx = begin addr)
    \ emit runtime flag test (mov rax,[rdi-8]; test rax,rax; sub rdi,8)
    T-HERE-VAR emit-mov-rdx-var
    c6 c, 02 c, 48 c,
    c6 c, 42 c, 01 c, 83 c,
    c6 c, 42 c, 02 c, ef c,
    c6 c, 42 c, 03 c, 08 c,
    c6 c, 42 c, 04 c, 48 c,
    c6 c, 42 c, 05 c, 8b c,
    c6 c, 42 c, 06 c, 07 c,
    c6 c, 42 c, 07 c, 48 c,
    c6 c, 42 c, 08 c, 85 c,
    c6 c, 42 c, 09 c, c0 c,
    c6 c, 42 c, 0a c, 90 c,
    \ 0f 84 (JZ rel32)
    c6 c, 42 c, 0b c, 0f c,
    c6 c, 42 c, 0c c, 84 c,
    \ rel = rbx - (rdx + 0x11)
    48 c, 89 c, d8 c,           \ mov rax, rbx (dest=begin)
    48 c, 89 c, d1 c,           \ mov rcx, rdx (here)
    48 c, 83 c, c1 c, 11 c,     \ add rcx, 0x11
    48 c, 29 c, c8 c,           \ sub rax, rcx (rel)
    89 c, 42 c, 0d c,           \ mov [rdx+0x0d], eax
    48 c, 83 c, c2 c, 11 c,     \ add rdx, 0x11
    T-HERE-VAR emit-store-rdx-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
t-immediate

\ ============================================================
\ EVALUATE ( addr len -- ) : process counted string as Forth source (t-c003)
\ ============================================================
\ ============================================================
\ REPL with compile/interpret STATE switch (t-c002)
\ ============================================================
\ T-STATE-VAR==0: interpret. T-STATE-VAR!=0: compile.
\ Compiling: found word -> call, if non-immediate; number -> lit,.
\ Immediate words execute at compile time.
variable r-lab-immed
variable r-lab-immed2
variable r-lab-numim
variable r-lab-exit
variable r-je-eof
variable r-je-notfound
variable r-je-error
variable r-jne-noprompt
variable r-prompt-nonempty
variable r-prompt-jmp32
variable r-prompt-interp
variable r-prompt-done32
variable r-jne-noload
variable r-jne-noload2
variable r-jne-linemode
variable r-je-exit1
variable r-je-exit2

hex
\ t-i2a1: REPL-REFILL ( -- eof-flag ). Read one line from KEY into
\ SOURCE-LINE-BUF, strip trailing newline, set SOURCE-PTR/END/ACTIVE.
\ On real stdin EOF pushes 1 (SOURCE-ACTIVE=0); else 0 and SOURCE-ACTIVE=1.
variable rrf-eof
variable rrf-nl
variable rrf-ovf
t-code REPL-REFILL
    t-vhere constant XT_REPL_REFILL
    \ NOTE: KEY performs a `syscall`, which on x86-64 clobbers RCX and R11
    \ (RCX <- return address), RSI (syscall buffer ptr) and RDX. So the line
    \ counter CANNOT live in RCX/RSI/RDX (they are not preserved across the
    \ CALL KEY). Use RBX (dest cursor, which KEY preserves) as the sole
    \ position tracker: SOURCE-END = RBX at line end.
    asm-push-rbx asm-push-rsi
    SOURCE-LINE-BUF emit-mov-rbx-imm       \ MOV RBX, SOURCE-LINE-BUF (dest cursor)
    here constant RRF_LOOP
    e8 c, XT_KEY t-vhere 4 + - 4,          \ CALL KEY ( -- ch )
    mov-rax-tos                            \ RAX = ch
    sub-rdi-8                              \ pop ch
    cmp-rax-0                              \ ch == 0 ?
    asm-je rrf-eof !                       \   -> real stdin EOF
    48 c, 83 c, f8 c, 0a c,                \ CMP RAX, 10 (newline)
    asm-je rrf-nl !                        \   -> line done, strip newline
    48 c, 81 c, fb c, 434800 4,            \ CMP RBX, SOURCE-LINE-BUF+0x200 (end guard)
    73 asm-jump-op rrf-ovf !               \ JAE rrf-ovf (overflow -> truncate)
    88 c, 03 c,                            \ MOV [RBX], AL
    48 c, ff c, c3 c,                      \ INC RBX
    eb c, RRF_LOOP here 1 + - c,           \ JMP RRF_LOOP
    here constant RRF_NL
    rrf-nl @ asm-resolve
    rrf-ovf @ asm-resolve
    SOURCE-LINE-BUF emit-mov-rax-imm       \ MOV RAX, SOURCE-LINE-BUF
    SOURCE-PTR emit-store-rax-var
    SOURCE-END emit-store-rbx-var          \ SOURCE-END = RBX == SOURCE-LINE-BUF + count
    1 emit-mov-rax-imm                     \ MOV RAX, 1
    SOURCE-ACTIVE emit-store-rax-var
    0 emit-mov-rax-imm                     \ push eof-flag 0 (line refilled OK)
    add-rdi-8 mov-tos-rax
    asm-pop-rsi asm-pop-rbx
    mov-rax-rdi
    c3 c,
    here constant RRF_EOF
    rrf-eof @ asm-resolve
    0 emit-mov-rax-imm                     \ MOV RAX, 0
    SOURCE-ACTIVE emit-store-rax-var
    1 emit-mov-rax-imm                     \ MOV RAX, 1 (eof-flag)
    add-rdi-8 mov-tos-rax
    asm-pop-rsi asm-pop-rbx
    mov-rax-rdi
    c3 c,
t-end-code


variable pp-jne-skip
t-code REPL-PROMPT
    t-vhere constant XT_REPL_PROMPT
    \ t-i2a1: one-shot prompt printer (respects PROMPT-FLAG).
    \ t-i003: print prompt "vagaforth> " only if PROMPT-FLAG is non-zero.
    \ Load PROMPT-FLAG; if zero, skip the prompt print.
    PROMPT-FLAG emit-mov-rax-var   \ mov rax, [rip+PROMPT-FLAG]
    emit-test-rax                  \ test rax, rax
    asm-je32 pp-jne-skip !           \ if zero -> skip prompt
    \ t-c3d4: emit "[ TOS ]" / "[]" prefix, then " > " / " compiled > " separator.
    \ RBX = data-stack base 0x410000 for the empty-stack check.
    48 c, bb c, 410000 8,          \ MOV RBX, 0x410000
    48 c, 39 c, df c,              \ CMP RDI, RBX   (rdi - rbx)
    asm-ja r-prompt-nonempty !     \ if rdi > rbx (non-empty) -> non-empty block
    \ empty stack: emit "[]"
    48 c, c7 c, c0 c, 5b c, 00 c, 00 c, 00 c, \ RAX = '['
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, c7 c, c0 c, 5d c, 00 c, 00 c, 00 c, \ RAX = ']'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    asm-jmp32 r-prompt-jmp32 !     \ skip over non-empty block to state separator
    r-prompt-nonempty @ asm-resolve
    \ non-empty stack: emit "[ TOS ]"
    48 c, c7 c, c0 c, 5b c, 00 c, 00 c, 00 c, \ RAX = '['
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, 8b c, 47 c, f8 c,       \ MOV RAX, [RDI-8]  (TOS)
    add-rdi-8 mov-tos-rax
    e8 c, XT_PDOT t-vhere 4 + - 4, \ CALL (.)  (prints TOS decimal, no space)
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, c7 c, c0 c, 5d c, 00 c, 00 c, 00 c, \ RAX = ']'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ state separator: interpret "> " (VM-LBL5) / compile "compiled > " (VM-LBL6)
    r-prompt-jmp32 @ asm-resolve32
    T-STATE-VAR emit-mov-rax-var
    emit-test-rax
    asm-je r-prompt-interp !
    \ compile: emit ' ' then type VM-LBL6 (11 chars)
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, b8 c, VM-LBL6 8,       \ mov rax, VM-LBL6 (addr)
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 0b c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 0x0b (11)
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,   \ CALL TYPE
    asm-jmp32 r-prompt-done32 !
    r-prompt-interp @ asm-resolve
    \ interpret: emit ' ' then type VM-LBL5 (2 chars)
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, b8 c, VM-LBL5 8,       \ mov rax, VM-LBL5 (addr)
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 02 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 0x02 (2)
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,   \ CALL TYPE
    r-prompt-done32 @ asm-resolve32
    \ Clear PROMPT-FLAG to 0.
    0 emit-mov-rax-imm             \ mov rax, 0
    PROMPT-FLAG emit-store-rax-var
    here constant PP_SKIP
    pp-jne-skip @ asm-resolve32
    c3 c,
t-end-code

t-code REPL
    t-vhere constant XT_REPL
    \ loop_start:
    t-vhere constant REPL_LOOP
    \ t-i2a1: print the one-shot prompt (REPL-PROMPT respects PROMPT-FLAG).
    e8 c, XT_REPL_PROMPT t-vhere 4 + - 4,   \ CALL REPL-PROMPT
    \ t-i2a1 loop-top load guard: if LINE-MODE and SOURCE-ACTIVE==0,
    \ refill the first line before the first PARSE-NAME.
    \ if LINE-MODE==0: skip guard (evaluate/compile-source path)
    LINE-MODE emit-mov-rax-var
    emit-test-rax
    asm-je r-jne-noload !       \ LINE-MODE==0 -> skip
    \ if SOURCE-ACTIVE != 0: buffer already loaded -> skip
    SOURCE-ACTIVE emit-mov-rax-var
    emit-test-rax
    asm-jne r-jne-noload2 !     \ SOURCE-ACTIVE!=0 -> skip
    \ need first line: CALL REPL-REFILL ( -- eof-flag )
    e8 c, XT_REPL_REFILL t-vhere 4 + - 4,
    mov-rax-tos                  \ rax = eof-flag
    sub-rdi-8                    \ pop eof-flag
    cmp-rax-0
    asm-jne32 r-je-exit1 !       \ eof-flag!=0 (real stdin EOF) -> exit(0)
    \ r_jne_noload:
    here constant R_JNE_NOLOAD
    r-jne-noload @ asm-resolve
    r-jne-noload2 @ asm-resolve
    XT_PARSE_NAME asm-call-sync    \ ( addr len )
    \ save parsed token addr/len for the undefined-token echo path
    mov-rax-tos                   \ rax = len
    TMP-TOK-LEN emit-store-rax-var
    48 c, 8b c, 47 c, f0 c,       \ MOV RAX, [RDI-16]  (addr)
    TMP-TOK-ADDR emit-store-rax-var
    mov-rax-tos
    cmp-rax-0
    asm-je32 r-je-eof !
    \ 2dup
    48 c, 8b c, 47 c, f0 c,
    48 c, 8b c, 5f c, f8 c,
    48 c, 83 c, c7 c, 10 c,
    48 c, 89 c, 47 c, f0 c,
    48 c, 89 c, 5f c, f8 c,
    XT_FIND asm-call-sync         \ ( addr len xt flags true | addr len false )
    mov-rax-tos
    cmp-rax-0
    asm-je32 r-je-notfound !
    \ found: ( addr len xt flags true )
    sub-rdi-8                     \ drop true
    mov-rax-tos                   \ rax = flags
    sub-rdi-8                     \ drop flags
    48 c, 89 c, c3 c,             \ mov rbx, rax (rbx = flags)
    mov-rax-tos                   \ rax = xt
    sub-rdi-8                     \ drop xt
    sub-rdi-8                     \ drop len
    sub-rdi-8                     \ drop addr
    \ rax=xt rbx=flags ; check compile state
    T-STATE-VAR emit-mov-rcx-var
    48 c, 83 c, f9 c, 00 c,          \ cmp rcx,0
    asm-je32 r-lab-immed !        \ interpret -> execute
    \ compiling: immediate? (0x80)
    48 c, 83 c, e3 c, 80 c,          \ and ebx,0x80
    48 c, 83 c, fb c, 00 c,          \ cmp rbx,0
    asm-jne32 r-lab-immed2 !       \ immediate -> execute
    \ compiling non-immediate: call,
    add-rdi-8 mov-tos-rax
    XT_CALLCOMMA asm-call-sync
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    here constant REPL_IMMED
    r-lab-immed @ asm-resolve32
    r-lab-immed2 @ asm-resolve32
    add-rdi-8 mov-tos-rax
    XT_EXECUTE asm-call-sync
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    \ not_found:
    here constant REPL_NOTFOUND
    r-je-notfound @ asm-resolve32
    sub-rdi-8                     \ drop false
    XT_NUMBER asm-call-sync       \ ( n true | false )
    mov-rax-tos
    cmp-rax-0
    asm-je32 r-je-error !
    sub-rdi-8                     \ drop true
    mov-rax-tos                   \ rax = n
    sub-rdi-8                     \ pop n
    T-STATE-VAR emit-mov-rcx-var
    48 c, 83 c, f9 c, 00 c,          \ cmp rcx,0
    asm-je32 r-lab-numim !        \ interpret -> keep n
    \ compiling: lit
    add-rdi-8 mov-tos-rax
    XT_LITCOMMA asm-call-sync
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    here constant REPL_NUMINT
    r-lab-numim @ asm-resolve32
    add-rdi-8 mov-tos-rax
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    here constant REPL_ERROR
    r-je-error @ asm-resolve32
    sub-rdi-8
    \ undefined token: echo " ? <token>\n" (spec 5.2) then continue REPL.
    \ emit ' '
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ emit '?'
    48 c, c7 c, c0 c, 3f c, 00 c, 00 c, 00 c, \ RAX = '?'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ emit ' '
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ type saved token ( TMP-TOK-ADDR TMP-TOK-LEN )
    TMP-TOK-ADDR emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    TMP-TOK-LEN emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,
    \ emit newline
    48 c, c7 c, c0 c, 0a c, 00 c, 00 c, 00 c, \ RAX = '\n'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ continue REPL loop
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    \ eof:
    here constant REPL_EOF
    r-je-eof @ asm-resolve32
    \ t-i2a1: dispatch on LINE-MODE.
    \   LINE-MODE==1: REPL line buffer exhausted -> arm prompt, print it,
    \                 refill next line, continue (interactive).
    \   LINE-MODE==0: evaluate/compile-source -> existing EOF behavior
    \                 (clear SOURCE-ACTIVE + return / exit(0)).
    LINE-MODE emit-mov-rax-var
    emit-test-rax
    asm-je r-jne-linemode !      \ LINE-MODE==0 -> existing REPL_EOF path
    \ ---- LINE-MODE: line buffer exhausted ----
    \ PARSE-NAME's EOF path pushes a single len=0 cell (the zero-length
    \ EOF-of-line token). Drop it so it neither corrupts the prompt's TOS
    \ display nor accumulates across line boundaries.
    sub-rdi-8
    \ arm one-shot prompt latch if IS-TTY-FLAG is set, then call REPL-PROMPT.
    IS-TTY-FLAG emit-mov-rax-var
    PROMPT-FLAG emit-store-rax-var
    e8 c, XT_REPL_PROMPT t-vhere 4 + - 4,   \ CALL REPL-PROMPT
    \ refill next line: CALL REPL-REFILL ( -- eof-flag )
    e8 c, XT_REPL_REFILL t-vhere 4 + - 4,
    mov-rax-tos                  \ rax = eof-flag
    sub-rdi-8                    \ pop eof-flag
    cmp-rax-0
    asm-jne32 r-je-exit2 !       \ eof-flag!=0 (real stdin EOF) -> exit(0)
    \ continue with next line
    e9 c, REPL_LOOP t-vhere 4 + - 4,
    \ r_jne_linemode: LINE-MODE==0 -> existing REPL_EOF body
    here constant R_JNE_LINEMODE
    r-jne-linemode @ asm-resolve
    \ if SOURCE-ACTIVE: clear+return (evaluate); else exit(0)
    SOURCE-ACTIVE emit-mov-rax-var
    48 c, 83 c, f8 c, 00 c,          \ cmp rax,0
    asm-je32 r-lab-exit !
    48 c, 31 c, c0 c,               \ xor rax,rax
    SOURCE-ACTIVE emit-store-rax-var
    \ t-v3a1: drop the stray len=0 EOF token that PARSE-NAME pushed, so the
    \ DSP returns to the pre-nested-REPL depth. Without this, the LINE-MODE==0
    \ (nested evaluate/compile-source) EOF path leaves one extra cell on the
    \ stack, misaligning evaluate's LIFO source-state restore by one cell
    \ (verified in gdb: DSP 0x410028 -> 0x410030 after the nested REPL
    \ returned). The LINE-MODE==1 refill path already drops this cell
    \ (sub-rdi-8 before arming the prompt); match it here.
    sub-rdi-8
    mov-rax-rdi
    c3 c,
    here constant REPL_EXIT
    r-lab-exit @ asm-resolve32
    \ t-i2a1: forward targets for boot-time (r-je-exit1) and exhaustion
    \ (r-je-exit2) stdin-EOF paths both land here -> exit(0).
    r-je-exit2 @ asm-resolve32
    r-je-exit1 @ asm-resolve32
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    syscall
    c3 c,
t-end-code
hex
t-code evaluate
    t-vhere constant XT_EVALUATE
    mov-rax-tos                 \ rax = len
    sub-rdi-8                   \ pop len
    48 c, 89 c, c3 c,           \ mov rbx, rax  (rbx = len)
    mov-rax-tos                 \ rax = addr
    sub-rdi-8                   \ pop addr
    48 c, 89 c, c1 c,           \ mov rcx, rax  (rcx = addr)
    \ t-i2b2 fix: SAVE the outer source state (SOURCE-PTR/END/ACTIVE, LINE-MODE,
    \ PROMPT-FLAG) on the hardware stack (RSP) BEFORE any SOURCE-* overwrite below.
    \ Push order on RSP: SOURCE-PTR, SOURCE-END, SOURCE-ACTIVE, LINE-MODE, PROMPT-FLAG.
    SOURCE-PTR emit-mov-rax-var
    50 c,                                 \ push rax
    SOURCE-END emit-mov-rax-var
    50 c,                                 \ push rax
    SOURCE-ACTIVE emit-mov-rax-var
    50 c,                                 \ push rax
    LINE-MODE emit-mov-rax-var
    50 c,                                 \ push rax
    PROMPT-FLAG emit-mov-rax-var
    50 c,                                 \ push rax
    \ Setup the evaluate buffer: overwrite SOURCE-* trio (now safe, after save).
    SOURCE-PTR emit-store-rcx-var
    48 c, 89 c, d8 c,           \ mov rax, rbx (len)
    48 c, 01 c, c8 c,           \ add rax, rcx (end)
    48 c, 89 c, c3 c,           \ mov rbx, rax
    SOURCE-END emit-store-rbx-var
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,  \ mov rax, 1
    SOURCE-ACTIVE emit-store-rax-var
    \ t-i2a1: force LINE-MODE=0 for the duration of the nested REPL so its
    \ buffer exhaustion returns to us (evaluate) instead of refilling from KEY.
    0 emit-mov-rax-imm                    \ mov rax, 0
    LINE-MODE emit-store-rax-var
    \ run REPL over the buffer; REPL clears SOURCE-ACTIVE at its return
    XT_REPL asm-call-sync
    \ Restore the saved outer state from RSP in LIFO order (exact reverse of push order):
    \ PROMPT-FLAG, LINE-MODE, SOURCE-ACTIVE, SOURCE-END, SOURCE-PTR.
    58 c,                                 \ pop rax
    PROMPT-FLAG emit-store-rax-var
    58 c,                                 \ pop rax
    LINE-MODE emit-store-rax-var
    58 c,                                 \ pop rax
    SOURCE-ACTIVE emit-store-rax-var
    58 c,                                 \ pop rax
    SOURCE-END emit-store-rax-var
    58 c,                                 \ pop rax
    SOURCE-PTR emit-store-rax-var
    \ If STATE is still non-zero (compile mode) when REPL returns, the source
    \ was an unterminated definition (missing ';'). Abort so we do NOT leave
    \ the target silently stuck in compile mode (which would swallow later input).
    T-STATE-VAR emit-mov-rax-var
    48 c, 83 c, f8 c, 00 c,       \ cmp rax, 0
    75 c, 04 c,                   \ jne +4 -> skip mov-rax-rdi/ret to abort
    mov-rax-rdi
    c3 c,                         \ STATE==0: normal return
    \ STATE!=0: abort
    ABORT-VT emit-mov-rax-var
    ff c, d0 c,                   \ call abort (never returns)
    decimal
t-end-code

\ ============================================================
\ t-d002: TARGET-SIDE DEFINING WORDS (create/variable/constant/allot)
\ Built entirely on the existing primitives (create-header, PARSE-NAME,
\ c,, C,2, HERE) so the running target can build data structures and
\ named entities from source, without host assistance.
\ ============================================================

\ --- ALLOT / CREATE / VARIABLE / CONSTANT / , / COMPILE-SOURCE ---
\ (t-d002) Target-side defining words.
\
\ BODY LAYOUT NOTE: FIND returns xt = aligned header body start (body_start).
\ EXECUTE / call, jump to body_start and run it as native code. So every
\ created data word's body MUST be native machine code that (a) pushes a value
\ and (b) returns. We emit a standard 22-byte "push-and-return" routine using
\ the existing lit, primitive (21 bytes: MOV RAX,imm64 / ADD RDI,8 / MOV
\ [RDI-8],RAX) followed by RET (c3, 1 byte). The actual data payload lives at
\ body_start + 22.
\   - create   : pushes body_start+22 (its own data address)
\   - variable : pushes body_start+22 and reserves a zero cell there
\   - constant : pushes the constant value n
\ (lit, advances HERE by 21; +1 for RET = 22 bytes of body code.)

\ --- allot ( n -- ) : advance HERE by n bytes. ---
hex
t-code allot ( n -- )
    t-vhere constant XT_ALLOT
    mov-rax-tos                 \ rax = n
    sub-rdi-8                   \ pop n
    48 c, 89 c, c3 c,           \ mov rbx, rax   (rbx = n)
    T-HERE-VAR emit-mov-rdx-var \ rdx = here
    48 c, 01 c, da c,           \ add rdx, rbx   (rdx = here + n)
    T-HERE-VAR emit-store-rdx-var
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- create ( "name" -- ) : create a named data header whose body pushes its
\ own data address (body_start+22). Used as `create buf 10 allot` to reserve a
\ buffer whose address is `buf`. Stack: ( "name" -- ).
hex
t-code create ( "name" -- )
    t-vhere constant XT_CREATE
    XT_PARSE_NAME asm-call-sync  \ ( addr len )
    XT_CREATEHDR asm-call-sync   \ ( -- )  HERE = body_start
    \ push data_addr = HERE + 22, then lit, emits the push-code
    T-HERE-VAR emit-mov-rax-var  \ rax = body_start
    48 c, 83 c, c0 c, 16 c,      \ add rax, 22  (data_addr)
    add-rdi-8 mov-tos-rax        \ push data_addr
    XT_LITCOMMA asm-call-sync    \ emit MOV RAX,data_addr / PUSH (21B) ; HERE+21
    \ emit RET (0xc3) at HERE via c, : push byte then call c,
    c3 emit-mov-rax-imm          \ mov rax, 0xc3 (host imm; pushes at runtime)
    add-rdi-8 mov-tos-rax        \ push 0xc3 at runtime
    XT_CCOMMA asm-call-sync      \ c, stores RET at HERE+1  (HERE=body_start+22)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- variable ( "name" -- ) : CREATE + reserve one zero-initialized cell.
\ Body pushes the cell address (body_start+22). `x @` / `x !` access the cell.
hex
t-code variable ( "name" -- )
    t-vhere constant XT_VARIABLE
    XT_PARSE_NAME asm-call-sync  \ ( addr len )
    XT_CREATEHDR asm-call-sync   \ ( -- )  HERE = body_start
    \ push cell_addr = HERE + 22, then lit, emits the push-code
    T-HERE-VAR emit-mov-rax-var  \ rax = body_start
    48 c, 83 c, c0 c, 16 c,      \ add rax, 22  (cell_addr)
    add-rdi-8 mov-tos-rax        \ push cell_addr
    XT_LITCOMMA asm-call-sync    \ emit push-code (21B) ; HERE+21
    \ emit RET
    c3 emit-mov-rax-imm
    add-rdi-8 mov-tos-rax
    XT_CCOMMA asm-call-sync      \ HERE now body_start+22 (the cell addr)
    \ reserve + zero the 8-byte cell at HERE
    T-HERE-VAR emit-mov-rax-var  \ rax = HERE (cell addr)
    48 c, 31 c, c9 c,            \ xor rcx, rcx (0)
    48 c, 89 c, 08 c,            \ mov [rax], rcx  (zero the cell)
    48 c, 83 c, c0 c, 08 c,      \ add rax, 8
    T-HERE-VAR emit-store-rax-var \ HERE = cell_addr + 8
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- constant ( n "name" -- ) : CREATE + body pushes the constant value n.
\ `n c .` pushes n when executed. Stack: ( n "name" -- ).
hex
t-code constant ( n "name" -- )
    t-vhere constant XT_CONSTANT
    \ Pop n (TOS) into scratch cell TMP-XT before PARSE-NAME clobbers stack.
    mov-rax-tos                 \ rax = n
    sub-rdi-8                   \ pop n
    TMP-XT emit-store-rax-var   \ save n
    XT_PARSE_NAME asm-call-sync \ ( addr len )
    XT_CREATEHDR asm-call-sync  \ ( -- )  HERE = body_start
    \ push n, then lit, emits the push-code for n
    TMP-XT emit-mov-rax-var     \ rax = n
    add-rdi-8 mov-tos-rax       \ push n
    XT_LITCOMMA asm-call-sync   \ emit push-code (21B) ; HERE+21
    \ emit RET
    c3 emit-mov-rax-imm
    add-rdi-8 mov-tos-rax
    XT_CCOMMA asm-call-sync     \ HERE now body_start+22
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- bss-allot ( size -- addr ) : allocates `size` bytes in uninitialized BSS memory ---
hex
t-code bss-allot ( size -- addr )
    t-vhere constant XT_BSS_ALLOT
    mov-rax-tos                 \ rax = size
    T-BSS-VAR emit-mov-rdx-var  \ rdx = old bss addr
    48 c, 89 c, d3 c,           \ mov rbx, rdx
    48 c, 01 c, c3 c,           \ add rbx, rax (rbx = old + size)
    T-BSS-VAR emit-store-rbx-var \ store new bss addr
    48 c, 89 c, 57 c, f8 c,     \ mov [rdi-8], rdx (TOS = old bss addr)
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- buffer: ( size "name" -- ) : allocates `size` bytes in BSS and creates a named constant ---
hex
t-code buffer: ( size "name" -- )
    t-vhere constant XT_BUFFER_COLON
    XT_BSS_ALLOT asm-call-sync  \ ( size -- addr )
    XT_CONSTANT asm-call-sync   \ ( addr "name" -- )
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- , ( n -- ) : conventional alias for the qword-comma C,2.
\ Emits 8 bytes from the stack into the runtime dictionary, advancing HERE.
\ Keeps C,2 for backward compatibility; "," reads naturally in defining words.
hex
t-code , ( n -- )
    t-vhere constant XT_COMMA_ALIAS
    XT_COMMA asm-call-sync      \ ( n -- ) delegate to C,2
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- compile-source ( addr len -- ) : convenience wrapper over EVALUATE.
\ Processes a counted source string through the target's compiler. EVALUATE
\ itself aborts (via ABORT-VT) if T-STATE is left non-zero on return (an
\ unterminated ':' definition), so the target never hangs in compile mode.
hex
t-code compile-source ( addr len -- )
    t-vhere constant XT_COMPILE_SOURCE
    XT_EVALUATE asm-call-sync   \ ( addr len -- ) process source via evaluate
    mov-rax-rdi
    c3 c,
    decimal
t-end-code
\ ============================================================
\ t-d5a6: Target-side include ( c-addr len -- )
\ Opens the file at c-addr (null-terminated copy into INCLUDE-FN-BUF),
\ reads it into INCLUDE-SRC-BUF, then EVALUATEs the accumulated source.
\ Implemented as a native t-code word (mirrors compile-file's logic but
\ as a single primitive). Uses the same RDI=DSP-in / RAX=DSP-out
\ convention and asm-push-rdi/asm-pop-rdi around syscalls.
\ ============================================================
variable inc-copy-done
variable inc-read-done
hex
t-code included ( c-addr len -- )
    t-vhere constant XT_INCLUDED
    asm-push-rdi               \ save real DSP (RDI) on return stack
    hex
    \ --- 1. Calculate buffer and filename addresses based on INCLUDE-DEPTH ---
    \ FN_BUF = INCLUDE-FN-BUF + depth*256
    INCLUDE-DEPTH emit-mov-rax-var   \ RAX = depth
    48 c, 89 c, c3 c,                \ MOV RBX, RAX
    48 c, c1 c, e3 c, 08 c,          \ SHL RBX, 8 (depth*256)
    48 c, 81 c, c3 c, INCLUDE-FN-BUF 4, \ ADD RBX, INCLUDE-FN-BUF (dst)
    
    \ Null-terminate filename into FN_BUF
    mov-rax-tos                      \ RAX = len
    48 c, 89 c, c1 c,                \ MOV RCX, RAX (RCX = len)
    mov-rax-nos                      \ RAX = c-addr
    48 c, 89 c, c6 c,                \ MOV RSI, RAX (RSI = src)
    
    here constant INC_COPY_LOOP
    48 c, 83 c, f9 c, 00 c,          \ CMP RCX, 0
    asm-je inc-copy-done !
    8a c, 06 c,                      \ MOV AL, [RSI]
    88 c, 03 c,                      \ MOV [RBX], AL
    48 c, ff c, c6 c,                \ INC RSI
    48 c, ff c, c3 c,                \ INC RBX
    48 c, ff c, c9 c,                \ DEC RCX
    eb c, INC_COPY_LOOP here 1 + - c, \ JMP INC_COPY_LOOP
    here constant INC_COPY_DONE
    inc-copy-done @ asm-resolve
    48 c, c6 c, 03 c, 00 c,          \ MOV BYTE [RBX], 0
    
    \ --- 2. sys-open(FN_BUF, 0, 0) -> fd ---
    INCLUDE-DEPTH emit-mov-rax-var
    48 c, c1 c, e0 c, 08 c,          \ SHL RAX, 8
    48 c, 05 c, INCLUDE-FN-BUF 4,    \ ADD RAX, INCLUDE-FN-BUF
    add-rdi-8 mov-tos-rax            \ push FN_BUF
    48 c, 31 c, c0 c,                \ XOR RAX, RAX
    add-rdi-8 mov-tos-rax            \ push path-len (0)
    add-rdi-8 mov-tos-rax            \ push flags (0)
    add-rdi-8 mov-tos-rax            \ push mode (0)
    e8 c, XT_SYS_OPEN t-vhere 4 + - 4, \ CALL sys-open
    48 c, 89 c, c7 c,                \ mov rdi, rax (sync DSP)
    mov-rax-tos                      \ RAX = fd
    INCLUDE-FD emit-store-rax-var    \ save fd in INCLUDE-FD
    sub-rdi-8                        \ pop fd
    
    \ --- 3. Read loop: sys-read(fd, INC-BUF-BASE + depth*0x40000 + len, 4096) ---
    48 c, 31 c, c0 c,                \ XOR RAX, RAX
    INCLUDE-LEN emit-store-rax-var   \ INCLUDE-LEN = 0
    
    here constant INC_READ_LOOP
    INCLUDE-FD emit-mov-rax-var      \ RAX = fd
    add-rdi-8 mov-tos-rax            \ push fd
    
    \ addr = INC-BUF-BASE + depth*0x40000 + INCLUDE-LEN
    INCLUDE-DEPTH emit-mov-rax-var
    48 c, c1 c, e0 c, 12 c,          \ SHL RAX, 18 (depth*262144 / 0x40000)
    48 c, 05 c, INC-BUF-BASE 4,      \ ADD RAX, INC-BUF-BASE
    INCLUDE-LEN emit-mov-rbx-var
    48 c, 01 c, d8 c,                \ ADD RAX, RBX
    add-rdi-8 mov-tos-rax            \ push addr
    
    48 c, b8 c, 00 c, 10 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ MOV RAX, 4096
    add-rdi-8 mov-tos-rax            \ push len (4096)
    e8 c, XT_SYS_READ t-vhere 4 + - 4, \ CALL sys-read
    48 c, 89 c, c7 c,                \ mov rdi, rax (sync DSP)
    mov-rax-tos                      \ RAX = count
    sub-rdi-8                        \ pop count
    48 c, 83 c, f8 c, 00 c,          \ CMP RAX, 0
    asm-jle inc-read-done !
    INCLUDE-LEN emit-mov-rbx-var
    48 c, 01 c, c3 c,                \ RBX = len + count
    INCLUDE-LEN emit-store-rbx-var
    eb c, INC_READ_LOOP here 1 + - c,
    here constant INC_READ_DONE
    inc-read-done @ asm-resolve
    
    \ --- 4. sys-close(fd) ---
    INCLUDE-FD emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    e8 c, XT_SYS_CLOSE t-vhere 4 + - 4,
    48 c, 89 c, c7 c,                \ mov rdi, rax (sync DSP)
    sub-rdi-8                        \ drop status
    
    \ --- 5. Increment INCLUDE-DEPTH before EVALUATE ---
    INCLUDE-DEPTH emit-mov-rax-var
    48 c, ff c, c0 c,                \ INC RAX
    INCLUDE-DEPTH emit-store-rax-var
    
    \ --- 6. Restore real DSP, pop (c-addr len) args, push (buf len) ---
    asm-pop-rdi                      \ restore real DSP
    sub-rdi-8                        \ pop original len
    sub-rdi-8                        \ pop original c-addr
    
    \ push buf = INC-BUF-BASE + (depth-1)*0x40000
    INCLUDE-DEPTH emit-mov-rax-var
    48 c, ff c, c8 c,                \ DEC RAX (depth-1)
    48 c, c1 c, e0 c, 12 c,          \ SHL RAX, 18
    48 c, 05 c, INC-BUF-BASE 4,      \ ADD RAX, INC-BUF-BASE
    add-rdi-8 mov-tos-rax            \ push buf
    
    \ push len
    INCLUDE-LEN emit-mov-rax-var
    add-rdi-8 mov-tos-rax            \ push len
    
    \ --- 7. Call EVALUATE ---
    e8 c, XT_EVALUATE t-vhere 4 + - 4, \ CALL evaluate ( buf len -- )
    48 c, 89 c, c7 c,                  \ mov rdi, rax (re-sync DSP from evaluate)
    
    \ --- 8. Decrement INCLUDE-DEPTH after EVALUATE ---
    INCLUDE-DEPTH emit-mov-rax-var
    48 c, ff c, c8 c,                \ DEC RAX
    INCLUDE-DEPTH emit-store-rax-var
    
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ include ( "name" -- ) : parse filename and execute included
hex
t-code include
    t-vhere constant XT_INCLUDE
    e8 c, XT_PARSE_NAME t-vhere 4 + - 4, \ ( -- c-addr len )
    48 c, 89 c, c7 c,                    \ mov rdi, rax
    e8 c, XT_INCLUDED t-vhere 4 + - 4,   \ CALL included
    48 c, 89 c, c7 c,                    \ mov rdi, rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ (here) ( -- addr )
hex
t-code (here)
    t-vhere constant XT_PAREN_HERE
    T-HERE-VAR emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ (allot) ( n -- )
hex
t-code (allot)
    t-vhere constant XT_PAREN_ALLOT
    e8 c, XT_ALLOT t-vhere 4 + - 4,
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ (,) ( n -- )
hex
t-code (,)
    t-vhere constant XT_PAREN_COMMA
    e8 c, XT_COMMA t-vhere 4 + - 4,
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ (c,) ( ch -- )
hex
t-code (c,)
    t-vhere constant XT_PAREN_CCOMMA
    e8 c, XT_CCOMMA t-vhere 4 + - 4,
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ bye ( -- )
hex
t-code bye
    t-vhere constant XT_BYE
    48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c, \ mov rdi, 0
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, \ mov rax, 60
    syscall
    c3 c,
    decimal
t-end-code

\ --- abort ( -- ) : print "?abort\n" to stderr and exit(1) to prevent hangs. ---
hex
t-code abort
    t-vhere constant XT_ABORT
    \ write(2, msg, 7)
    asm-push-rdi
    48 c, c7 c, c2 c, 07 c, 00 c, 00 c, 00 c,   \ mov rdx, 7
    48 c, be c, ABORT-MSG 8,               \ mov rsi, msg
    48 c, c7 c, c7 c, 02 c, 00 c, 00 c, 00 c, \ mov rdi, 2
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c, \ mov rax, 1
    syscall
    asm-pop-rdi
    48 c, c7 c, c7 c, 01 c, 00 c, 00 c, 00 c, \ mov rdi, 1
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, \ mov rax, 60
    syscall
    c3 c,
    decimal
t-end-code
t-immediate

\ ============================================================
\ t-d004: STATUS WORDS (version / word-count / .s)
\ Report the target dictionary's current state: total compiled bytes
\ (HERE - runtime-dict-base) and total number of words in the link chain.
\ ============================================================

\ --- word-count ( -- n ) : count entries in the runtime dictionary link chain.
\ Walks [T-LATEST-VAR] -> link -> link ... until link == 0.
variable wcount-done
hex
t-code word-count ( -- n )
    t-vhere constant XT_WORDCOUNT
    \ rcx = counter (n), rbx = current link
    48 c, 31 c, c9 c,            \ xor rcx, rcx   (counter = 0)
    T-LATEST-VAR emit-mov-rbx-var \ rbx = [T-LATEST-VAR]
    \ loop_start:
    here constant WCOUNT_LOOP
    48 c, 83 c, fb c, 00 c,      \ cmp rbx, 0
    asm-je wcount-done !         \ if current==0 done
    emit-inc-rcx                 \ counter++
    48 c, 8b c, 1b c,            \ mov rbx, [rbx]  (follow link)
    eb c, WCOUNT_LOOP here 1 + - c, \ jmp loop_start
    \ done:
    wcount-done @ asm-resolve
    emit-mov-rax-rcx             \ rax = counter
    add-rdi-8
    mov-tos-rax                  \ push n
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- version ( -- ) : print "vagaforth-kernel: dict <bytes> bytes, <n> words".
\ bytes = HERE - T-CODE-START (runtime dict base = 0x403000).
hex
t-code version ( -- )
    t-vhere constant XT_VERSION
    \ LBL0 "vagaforth-kernel: dict "
    48 c, b8 c, VM-LBL0 8,       \ mov rax, VM-LBL0
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 17 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 0x17 (23)
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,   \ CALL TYPE

    \ bytes = HERE - T-CODE-START
    T-HERE-VAR emit-mov-rax-var  \ rax = HERE
    48 c, bb c, T-CODE-START 8,  \ rbx = 0x403000
    sub-rax-rbx                  \ rax = HERE - 0x403000 (SUB RAX,RBX)
    add-rdi-8
    mov-tos-rax
    e8 c, XT_PDOT t-vhere 4 + - 4,   \ CALL (.)  (prints bytes, no trailing space)
    e8 c, XT_SPACE t-vhere 4 + - 4,  \ CALL space (separator)

    \ LBL1 "bytes, " (0x07 chars, no leading space)
    48 c, b8 c, VM-LBL1 8,
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 07 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 7
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,

    \ word-count
    e8 c, XT_WORDCOUNT t-vhere 4 + - 4,   \ CALL word-count ( -- n )
    e8 c, XT_PDOT t-vhere 4 + - 4,        \ CALL (.)  (prints n, no trailing space)
    e8 c, XT_SPACE t-vhere 4 + - 4,  \ CALL space (separator)

    \ LBL2 "words" (0x05 chars, no leading space)
    48 c, b8 c, VM-LBL2 8,
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 05 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 5
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,

    e8 c, XT_CR t-vhere 4 + - 4,    \ CALL cr
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- .s ( -- ) : stack depth + contents (decimal), matching C prim_dot_s. ---
\ Prints: "<depth> cell0 cell1 ... cellN-1 " + newline. Base-independent (always
\ decimal via the . primitive). Does not pop anything (stack preserved).
variable ds-done
hex
t-code .s ( -- )
    t-vhere constant XT_DOT_S
    \ r12 = saved DSP; r13 = stack base 0xD0000000 (loop pointer)
    49 c, 89 c, fc c,             \ MOV R12, RDI
    49 c, bd c, D0000000 8,         \ MOV R13, 0xD0000000
    \ push depth = (R12 - R13) >> 3
    4c c, 89 c, e0 c,             \ MOV RAX, R12
    4c c, 29 c, e8 c,             \ SUB RAX, R13
    48 c, c1 c, e8 c, 03 c,       \ SHR RAX, 3
    add-rdi-8 mov-tos-rax
    \ emit '<'
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c, \ RAX = '<'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ print depth (decimal, no trailing space)
    e8 c, XT_PDOT t-vhere 4 + - 4,
    \ emit '>' then ' '  -> "<N> "
    48 c, c7 c, c0 c, 3e c, 00 c, 00 c, 00 c, \ RAX = '>'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    \ loop: while R13 < R12, print [R13] + ' '
    here constant DS_LOOP
    4d c, 39 c, ec c,             \ CMP R12, R13  (r12 - r13)
    asm-jle ds-done !
    49 c, 8b c, 45 c, 00 c,       \ MOV RAX, [R13]
    add-rdi-8 mov-tos-rax
    e8 c, XT_PDOT t-vhere 4 + - 4, \ CALL (.)  (decimal, no trailing space)
    48 c, c7 c, c0 c, 20 c, 00 c, 00 c, 00 c, \ RAX = ' '
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    49 c, 83 c, c5 c, 08 c,       \ ADD R13, 8
    eb c, DS_LOOP here 1 + - c,   \ JMP loop
    \ done:
    ds-done @ asm-resolve
    \ emit newline
    48 c, c7 c, c0 c, 0a c, 00 c, 00 c, 00 c, \ RAX = '\n'
    add-rdi-8 mov-tos-rax
    e8 c, XT_EMIT t-vhere 4 + - 4,
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ --- words ( -- ) : print all dictionary word names, newest first, space-separated. ---
\ Walks [T-LATEST-VAR] -> link -> link ... until link == 0. For each word,
\ prints the name (len byte masked with 0x1F) followed by a space, then a
\ trailing newline. Matches C prim_words.
variable words-done
hex
t-code words ( -- )
    t-vhere constant XT_WORDS
    \ rbx = [T-LATEST-VAR]  (current link)
    T-LATEST-VAR emit-mov-rbx-var
    \ loop_start:
    here constant WORDS_LOOP
    48 c, 83 c, fb c, 00 c,      \ cmp rbx, 0
    asm-je words-done !          \ if current==0 done
    \ r12 = current + 8  (len byte addr)
    49 c, 89 c, dc c,            \ mov r12, rbx
    49 c, 83 c, c4 c, 08 c,      \ add r12, 8
    \ r13 = len = byte [r12] & 0x1F
    4d c, 0f c, b6 c, 2c c, 24 c, \ movzx r13, byte [r12]
    49 c, 83 c, e5 c, 1f c,      \ and r13, 0x1F
    \ r12 = name addr = r12 + 1
    49 c, 83 c, c4 c, 01 c,      \ add r12, 1
    \ push name addr (r12)
    4c c, 89 c, e0 c,            \ mov rax, r12
    add-rdi-8
    mov-tos-rax
    \ push len (r13)
    4c c, 89 c, e8 c,            \ mov rax, r13
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,   \ CALL TYPE
    \ print space
    e8 c, XT_SPACE t-vhere 4 + - 4,  \ CALL SPACE
    \ follow link: rbx = [rbx]
    48 c, 8b c, 1b c,            \ mov rbx, [rbx]
    eb c, WORDS_LOOP here 1 + - c, \ jmp loop_start
    \ done:
    words-done @ asm-resolve
    \ print newline
    e8 c, XT_CR t-vhere 4 + - 4,     \ CALL CR
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ t-b1c3: save-elf-at ( name-addr name-len filename-addr filename-len -- )
\ Looks up a runtime word by name via FIND; if found, emits a small
\ self-contained stack-init trampoline at HERE whose e_entry runs that
\ word, then writes an ELF header + saves the image to the given filename.
\ The trampoline uses ONLY register-indirect (absolute) calls - there is no
\ rel32 `4,` primitive - and initializes RSP/DSP before `call rbx`.
\
\ NOTE: the runtime s" primitive shares ONE S-BUF-ADDR, so the two s" results
\ would overwrite each other. We therefore copy BOTH the name and the filename
\ into dedicated 256-byte buffers (SAVE-NM-BUF / SAVE-FN-BUF) up front.
\ ================================================================
variable se-jnd
variable se-nm-done
variable se-fn-done
hex
t-code save-elf-at ( name-addr name-len filename-addr filename-len -- )
    t-vhere constant XT_SAVE_ELF_AT
    asm-push-rdi                 \ save DSP (RDI) on return stack
    hex
    \ --- 1. Capture all four args into scratch cells (RDI=DSP still valid) ---
    \ stack: [rdi-8]=filename-len [rdi-16]=filename-addr
    \        [rdi-24]=name-len   [rdi-32]=name-addr
    mov-rax-tos                  \ rax = filename-len
    SAVE-FN-LEN emit-store-rax-var
    mov-rax-nos                  \ rax = filename-addr
    SAVE-FN-ADDR emit-store-rax-var
    48 c, 8b c, 47 c, e8 c,      \ mov rax,[rdi-24]  (name-len)
    SAVE-NM-LEN emit-store-rax-var
    48 c, 8b c, 47 c, e0 c,      \ mov rax,[rdi-32]  (name-addr)
    SAVE-NM-ADDR emit-store-rax-var
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8   \ pop all 4 args (DSP empty)
    \ --- 2. Copy NAME (SAVE-NM-ADDR/SAVE-NM-LEN) into SAVE-NM-BUF ---
    \ rcx=len, rsi=src, rbx=dst  (RDI=DSP kept intact)
    SAVE-NM-LEN emit-mov-rcx-var
    SAVE-NM-ADDR emit-mov-rax-var
    48 c, 89 c, c6 c,            \ mov rsi, rax
    SAVE-NM-BUF emit-mov-rbx-imm \ rbx = SAVE-NM-BUF
    here constant SE_NM_LOOP
    48 c, 83 c, f9 c, 00 c,      \ cmp rcx, 0
    asm-je se-nm-done !
    8a c, 06 c,                  \ mov al, [rsi]
    88 c, 03 c,                  \ mov [rbx], al
    48 c, ff c, c6 c,            \ inc rsi
    48 c, ff c, c3 c,            \ inc rbx
    48 c, ff c, c9 c,            \ dec rcx
    eb c, SE_NM_LOOP here 1 + - c, \ jmp SE_NM_LOOP
    here constant SE_NM_DONE
    se-nm-done @ asm-resolve
    48 c, c6 c, 03 c, 00 c,      \ mov byte [rbx], 0  (NUL terminator)
    \ --- 3. Copy FILENAME (SAVE-FN-ADDR/SAVE-FN-LEN) into SAVE-FN-BUF ---
    SAVE-FN-LEN emit-mov-rcx-var
    SAVE-FN-ADDR emit-mov-rax-var
    48 c, 89 c, c6 c,            \ mov rsi, rax
    SAVE-FN-BUF emit-mov-rbx-imm \ rbx = SAVE-FN-BUF
    here constant SE_FN_LOOP
    48 c, 83 c, f9 c, 00 c,      \ cmp rcx, 0
    asm-je se-fn-done !
    8a c, 06 c,                  \ mov al, [rsi]
    88 c, 03 c,                  \ mov [rbx], al
    48 c, ff c, c6 c,            \ inc rsi
    48 c, ff c, c3 c,            \ inc rbx
    48 c, ff c, c9 c,            \ dec rcx
    eb c, SE_FN_LOOP here 1 + - c, \ jmp SE_FN_LOOP
    here constant SE_FN_DONE
    se-fn-done @ asm-resolve
    48 c, c6 c, 03 c, 00 c,      \ mov byte [rbx], 0  (NUL terminator)
    \ --- 4. Lookup name: ( SAVE-NM-BUF SAVE-NM-LEN ) FIND ---
    SAVE-NM-BUF emit-mov-rax-imm
    add-rdi-8 mov-tos-rax
    SAVE-NM-LEN emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    XT_FIND asm-call-sync        \ ( xt flags true | false ) -- FIND consumed addr/len
    mov-rax-tos
    48 c, 85 c, c0 c,            \ test rax, rax
    asm-jne32 se-jnd !            \ if found -> jump
    \ --- NOT found: ( false ) -> drop false then abort ---
    sub-rdi-8                    \ drop false
    e8 c, XT_ABORT t-vhere 4 + - 4,  \ CALL abort (never returns)
    \ --- found: ( xt flags true ) ---
    here constant SE_FOUND
    se-jnd @ asm-resolve32
    sub-rdi-8                    \ drop true
    mov-rax-tos                  \ rax = flags
    sub-rdi-8                    \ drop flags
    mov-rax-tos                  \ rax = xt
    SAVE-XT emit-store-rax-var   \ SAVE-XT = xt
    sub-rdi-8                    \ drop xt
    \ --- 5. Emit stack-init trampoline at HERE ---
    \ tramp-start = HERE ; store it
    XT_HERE asm-call-sync
    mov-rax-tos
    SAVE-TRAMP emit-store-rax-var
    sub-rdi-8                    \ pop here
    \ Each byte is pushed then consumed by the c, target word; each qword is
    \ pushed then consumed by the , (C,2) target word. Absolute reg-indirect.
    \ mov rsp, 0x420000  (RSP init) : 48 bc <imm64>
    48 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    bc emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    F0000000 emit-push-imm e8 c, XT_COMMA t-vhere 4 + - 4,
    \ mov rdi, 0xD0000000 (DSP init) : 48 bf <imm64>
    48 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    bf emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    D0000000 emit-push-imm e8 c, XT_COMMA t-vhere 4 + - 4,
    \ mov rbx, <xt>  (absolute word address) : 48 bb <imm64>
    48 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    bb emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    SAVE-XT emit-mov-rax-var      \ rax = xt ; push + ,
    add-rdi-8 mov-tos-rax
    e8 c, XT_COMMA t-vhere 4 + - 4,
    \ call rbx : ff d3
    ff emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    d3 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    \ mov rdi, 0 : 48 c7 c7 00 00 00 00
    48 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    c7 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    c7 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    \ mov rax, 60 : 48 c7 c0 3c 00 00 00
    48 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    c7 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    c0 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    3c emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    00 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    \ syscall (exit 0) : 0f 05
    0f emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    05 emit-push-imm e8 c, XT_CCOMMA t-vhere 4 + - 4,
    \ --- 6. ELF header: ( tramp-start file-size mem-size ) ---
    \ push tramp-start
    SAVE-TRAMP emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    \ push file-size = HERE - ELF-ORIGIN
    XT_HERE asm-call-sync       \ here
    ELF-ORIGIN emit-push-imm    \ here ELF-ORIGIN
    XT_MINUS asm-call-sync      \ file-size
    \ push mem-size 0x100000000 (4GB)
    100000000 emit-push-imm     \ ( tramp file mem )
    XT_ELF_HEADER asm-call-sync \ elf-header ( entry file mem -- )
    \ --- 7. save-elf ( SAVE-FN-BUF SAVE-FN-LEN ) ---
    SAVE-FN-BUF emit-mov-rax-imm
    add-rdi-8 mov-tos-rax
    SAVE-FN-LEN emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    XT_SAVE_ELF asm-call-sync
    \ --- Done: restore DSP, pop 4 args, return ---
    asm-pop-rdi
    sub-rdi-8 sub-rdi-8 sub-rdi-8 sub-rdi-8
    mov-rax-rdi
    c3 c,
    decimal
t-end-code

\ ============================================================
\ save-app ( "entry-name" "filename" -- )
\ Convenience parsing word: parses the entry word and output filename
\ directly from the input stream and calls save-elf-at.
\ Example: save-app main hello.bin
\ ============================================================
variable sa-nm-done
hex
t-code save-app ( "entry-name" "filename" -- )
    t-vhere constant XT_SAVE_APP
    asm-push-rdi                 \ save DSP
    \ 1. Parse first token (entry name)
    XT_PARSE_NAME asm-call-sync  \ stack: [rdi-8]=len [rdi-16]=addr
    mov-rax-tos                  \ rax = entry-len
    SAVE-NM-LEN emit-store-rax-var
    mov-rax-nos                  \ rax = entry-addr
    SAVE-NM-ADDR emit-store-rax-var
    sub-rdi-8 sub-rdi-8          \ pop entry args
    \ Copy entry name into SAVE-NM-BUF: rcx=len, rsi=src, rbx=dst
    SAVE-NM-LEN emit-mov-rcx-var
    SAVE-NM-ADDR emit-mov-rax-var
    48 c, 89 c, c6 c,            \ mov rsi, rax
    SAVE-NM-BUF emit-mov-rbx-imm \ rbx = SAVE-NM-BUF
    here constant SA_NM_LOOP
    48 c, 83 c, f9 c, 00 c,      \ cmp rcx, 0
    asm-je sa-nm-done !
    8a c, 06 c,                  \ mov al, [rsi]
    88 c, 03 c,                  \ mov [rbx], al
    48 c, ff c, c6 c,            \ inc rsi
    48 c, ff c, c3 c,            \ inc rbx
    48 c, ff c, c9 c,            \ dec rcx
    eb c, SA_NM_LOOP here 1 + - c, \ jmp SA_NM_LOOP
    here constant SA_NM_DONE
    sa-nm-done @ asm-resolve
    48 c, c6 c, 03 c, 00 c,      \ mov byte [rbx], 0 (NUL terminator)
    \ 2. Parse second token (output filename)
    XT_PARSE_NAME asm-call-sync  \ stack: [rdi-8]=fn-len [rdi-16]=fn-addr
    \ Stack now has ( fn-addr fn-len ). We need ( entry-addr entry-len fn-addr fn-len )
    mov-rax-tos                  \ rax = fn-len
    48 c, 89 c, c2 c,            \ mov rdx, rax (rdx = fn-len)
    mov-rax-nos                  \ rax = fn-addr
    48 c, 89 c, c1 c,            \ mov rcx, rax (rcx = fn-addr)
    sub-rdi-8 sub-rdi-8          \ pop fn args (DSP clean)
    \ Push entry-addr (SAVE-NM-BUF)
    SAVE-NM-BUF emit-mov-rax-imm
    add-rdi-8 mov-tos-rax
    \ Push entry-len
    SAVE-NM-LEN emit-mov-rax-var
    add-rdi-8 mov-tos-rax
    \ Push fn-addr (RCX)
    48 c, 89 c, c8 c,            \ mov rax, rcx
    add-rdi-8 mov-tos-rax
    \ Push fn-len (RDX)
    48 c, 89 c, d0 c,            \ mov rax, rdx
    add-rdi-8 mov-tos-rax
    \ 3. Call save-elf-at: ( entry-addr entry-len fn-addr fn-len -- )
    XT_SAVE_ELF_AT asm-call-sync
    asm-pop-rdi                  \ restore DSP
    mov-rax-rdi
    c3 c,
    decimal
t-end-code



\ --- START ---
variable real-entry-point
variable r-is-tty
variable r-tty-done
t-code START
    t-vhere real-entry-point !
    \ Check if stdin (fd 0) is a TTY: ioctl(0, TCGETS=0x5401, 0x434800)
    \ rax=16 (sys_ioctl), rdi=0, rsi=0x5401, rdx=0x434800
    hex
    48 c, c7 c, c0 c, 10 c, 00 c, 00 c, 00 c, \ mov rax, 16
    48 c, 31 c, ff c,                         \ xor rdi, rdi (0)
    48 c, c7 c, c6 c, 01 c, 54 c, 00 c, 00 c, \ mov rsi, 0x5401
    48 c, c7 c, c2 c, 00 c, 48 c, 43 c, 00 c, \ mov rdx, 0x434800
    0f c, 05 c,                               \ syscall
    \ if rax == 0 -> tty (1); else -> not tty (0)
    48 c, 85 c, c0 c,                         \ test rax, rax
    asm-je r-is-tty !
    48 c, 31 c, c0 c,                         \ mov rax, 0
    asm-jmp32 r-tty-done !
    r-is-tty @ asm-resolve
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c, \ mov rax, 1
    r-tty-done @ asm-resolve32
    IS-TTY-FLAG emit-store-rax-var
    PROMPT-FLAG emit-store-rax-var
    \ Init stacks
    48 c, bf c, D0000000 8,                   \ MOV RDI, 0xD0000000 (DSP)
    48 c, bc c, F0000000 8,                   \ MOV RSP, 0xF0000000 (RSP)
    decimal
    \ Reset interpreter state at boot
    0 emit-mov-rax-imm
    SOURCE-ACTIVE emit-store-rax-var
    SOURCE-PTR emit-store-rax-var
    SOURCE-END emit-store-rax-var
    T-STATE-VAR emit-store-rax-var
    T-CDEPTH emit-store-rax-var
    10 emit-mov-rax-imm
    T-BASE-VAR emit-store-rax-var
    \ t-i2a1: enable line-oriented REPL mode for interactive input.
    \ LINE-MODE=1 makes the REPL read full lines (refill on exhaustion,
    \ print prompt, continue) and keeps evaluate/compile-source in
    \ "EOF->return" mode (LINE-MODE stays 0 there).
    1 emit-mov-rax-imm        \ mov rax, 1
    LINE-MODE emit-store-rax-var
    \ t-i001: print welcome message "VagaForth v0.8" then newline.
    hex
    \ Push ( addr len ) for VM-LBL3 then CALL TYPE (pops both).
    48 c, b8 c, VM-LBL3 8,       \ mov rax, VM-LBL3 (addr)
    add-rdi-8
    mov-tos-rax
    48 c, b8 c, 0e c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ mov rax, 0x0e (14)
    add-rdi-8
    mov-tos-rax
    e8 c, XT_TYPE t-vhere 4 + - 4,   \ CALL TYPE
    e8 c, XT_CR t-vhere 4 + - 4,      \ CALL cr (newline)
    decimal
    \ Jump to REPL
    XT_REPL asm-call-sync
    \ exit(0) (should not reach here)
    hex 48 c, c7 c, c7 c, 00 c, 00 c, 00 c, 00 c,
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    syscall
    decimal
t-end-code

\ --- Final Patching ---
decimal
t-vhere T-HERE-VAR virt>host !
target-latest @ T-LATEST-VAR virt>host !
\ Patch ABORT-VT with the abort word's target XT so the REPL/EVALUATE
\ indirect call finds it. ABORT-VT is a target cell; store the XT into it.
XT_ABORT ABORT-VT virt>host !
real-entry-point @ constant ENTRY-POINT

hex
here target-base @ - constant BIN-SIZE
target-base @ target-dp !
ENTRY-POINT BIN-SIZE 100000000 elf-header
target-base @ BIN-SIZE + target-dp !

s" vagaforth_new.bin" host-save-elf
target-off
bye
