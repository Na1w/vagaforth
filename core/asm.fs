\ asm.fs - Minimal x86-64 Assembler (Fixed for Upward Stack)

hex

: get-native-runner ['] (code) @ ;

: code
    create
    get-native-runner 
    here 10 - !    \ Backa 16 bytes (0x10) för att patcha CFA
    8 negate allot \ Backa över Body-fältet
    ;

: end-code
    c3 c,
    ;

\ --- Instructions ---
\ RDI = DSP (Next Free). Stack grows UP.
\ TOS = [RDI - 8]
\ NOS = [RDI - 16]

: mov-rax-rdi ( -- ) 48 c, 89 c, f8 c, ;
: sub-rdi-8   ( -- ) 48 c, 83 c, ef c, 08 c, ; \ POP
: add-rdi-8   ( -- ) 48 c, 83 c, c7 c, 08 c, ; \ PUSH

\ Access TOS [RDI - 8]
\ mov rax, [rdi - 8] -> 48 8b 47 f8
: mov-rax-tos ( -- ) 48 c, 8b c, 47 c, f8 c, ;

\ lea rsi, [rdi - 8] -> 48 8d 77 f8
: lea-rsi-tos ( -- ) 48 c, 8d c, 77 c, f8 c, ;

\ mov [rdi - 8], rax -> 48 89 47 f8
: mov-tos-rax ( -- ) 48 c, 89 c, 47 c, f8 c, ;

\ mov [rdi - 8], rbx -> 48 89 5f f8
: mov-tos-rbx ( -- ) 48 c, 89 c, 5f c, f8 c, ;

\ mov rbx, [rdi - 8] -> 48 8b 5f f8
: mov-rbx-tos ( -- ) 48 c, 8b c, 5f c, f8 c, ;

\ Access NOS [RDI - 16]
\ mov rax, [rdi - 16] -> 48 8b 47 f0
: mov-rax-nos ( -- ) 48 c, 8b c, 47 c, f0 c, ;

\ lea rsi, [rdi - 16] -> 48 8d 77 f0
: lea-rsi-nos ( -- ) 48 c, 8d c, 77 c, f0 c, ;

\ mov rbx, [rdi - 16] -> 48 8b 5f f0
: mov-rbx-nos ( -- ) 48 c, 8b c, 5f c, f0 c, ;

\ Math
: inc-rax     ( -- ) 48 c, ff c, c0 c, ;
: add-rax-rbx ( -- ) 48 c, 01 c, d8 c, ; \ ADD RAX, RBX
: sub-rax-rbx ( -- ) 48 c, 29 c, d8 c, ; \ SUB RAX, RBX (RAX -= RBX)

\ Syscalls & Registers
: syscall     ( -- ) 0f c, 05 c, ;

\ Hardware Stack (RSP)
: asm-push-rax ( -- ) 50 c, ;
: asm-push-rcx ( -- ) 51 c, ;
: asm-push-rdx ( -- ) 52 c, ;
: asm-push-rbx ( -- ) 53 c, ;
: asm-push-rsi ( -- ) 56 c, ;
: asm-push-rdi ( -- ) 57 c, ;

: asm-pop-rax ( -- ) 58 c, ;
: asm-pop-rcx ( -- ) 59 c, ;
: asm-pop-rdx ( -- ) 5a c, ;
: asm-pop-rbx ( -- ) 5b c, ;
: asm-pop-rsi ( -- ) 5e c, ;
: asm-pop-rdi ( -- ) 5f c, ;

\ Flytta från RAX (temp) till Argument-register
: mov-rdi-rax ( -- ) 48 c, 89 c, c7 c, ; \ Arg 1
: mov-rsi-rax ( -- ) 48 c, 89 c, c6 c, ; \ Arg 2
: mov-rdx-rax ( -- ) 48 c, 89 c, c2 c, ; \ Arg 3
: mov-rcx-rax ( -- ) 48 c, 89 c, c1 c, ; \ Arg 4

\ LEA RSI, [RIP + offset32] -> 48 8d 35 [offset32]
: lea-rsi-rip ( offset -- )
    48 c, 8d c, 36 c, 4, ;

\ Logic
: and-rax-rbx ( -- ) 48 c, 21 c, d8 c, ;
: or-rax-rbx  ( -- ) 48 c, 09 c, d8 c, ;
: xor-rax-rbx ( -- ) 48 c, 31 c, d8 c, ;

\ Compare
: cmp-rax-rbx ( -- ) 48 c, 39 c, d8 c, ; \ CMP RAX, RBX (RAX - RBX)
: cmp-rax-0   ( -- ) 48 c, 83 c, f8 c, 00 c, ; \ CMP RAX, 0
: cmp-rax-32  ( -- ) 48 c, 83 c, f8 c, 20 c, ; \ CMP RAX, 32

\ --- Control Flow (Short Jumps) ---

\ Kompilera opcode och reservera en byte för offset
: asm-jump-op ( opcode -- addr ) c, here 0 c, ;

: asm-je  ( -- addr ) 74 asm-jump-op ;
: asm-jne ( -- addr ) 75 asm-jump-op ;
: asm-jg  ( -- addr ) 7f asm-jump-op ;
: asm-jge ( -- addr ) 7d asm-jump-op ;
: asm-jl  ( -- addr ) 7c asm-jump-op ;
: asm-jle ( -- addr ) 7e asm-jump-op ;
: asm-ja  ( -- addr ) 77 asm-jump-op ;
: asm-jb  ( -- addr ) 72 asm-jump-op ;
: asm-jmp ( -- addr ) eb asm-jump-op ;

\ Lös upp hoppet (Patcha offset)
: asm-resolve ( addr -- )
    here over - 1 - \ Offset = HERE - Addr - 1
    swap c!
    ;

\ --- Near (32-bit) Control Flow ---
\ For jumps that exceed the short (rel8) range of +/-127 bytes.
\ JE rel32 = 0f 84 <4-byte offset>
: asm-jump-op32 ( opcode -- addr ) c, here 0 c, 0 c, 0 c, 0 c, ;

: asm-je32  ( -- addr ) 0f c, 84 asm-jump-op32 ;
: asm-jne32 ( -- addr ) 0f c, 85 asm-jump-op32 ;
: asm-jg32  ( -- addr ) 0f c, 8f asm-jump-op32 ;
: asm-jge32 ( -- addr ) 0f c, 8d asm-jump-op32 ;
: asm-jl32  ( -- addr ) 0f c, 8c asm-jump-op32 ;
: asm-jle32 ( -- addr ) 0f c, 8e asm-jump-op32 ;
: asm-ja32  ( -- addr ) 0f c, 87 asm-jump-op32 ;
: asm-jb32  ( -- addr ) 0f c, 82 asm-jump-op32 ;
: asm-jmp32 ( -- addr ) e9 asm-jump-op32 ;

\ Resolve a 32-bit relative jump. offset = HERE - addr - 4
\ addr is a HOST address (here returns target-dp, a host pointer, in target-on mode).
\ Uses a host variable (defined before target-on) to hold addr.
variable _jmp32-adr
: asm-resolve32 ( addr -- )
    _jmp32-adr !
    here _jmp32-adr @ - 4 -
    dup _jmp32-adr @ c!
    dup 8 rshift _jmp32-adr @ 1+ c!
    dup 10 rshift _jmp32-adr @ 2 + c!
    18 rshift _jmp32-adr @ 3 + c!
    ;

\ --- Structured Control Flow ---

: asm-then ( addr -- ) asm-resolve ;

: asm-else ( addr -- addr2 )
    asm-jmp      \ 1. Kompilera JMP framåt (addr2)
    swap         \ 2. Byt plats så vi kommer åt IF-adressen (addr)
    asm-resolve  \ 3. Lös upp IF-hoppet så det landar här (starten av ELSE)
    ;            \ 4. Lämna addr2 på stacken för THEN

\ --- Examples ---

\ : native-abs ( n -- |n| )
code native-abs
    mov-rax-tos
    cmp-rax-0
    7d asm-jump-op \ JGE
    
    48 c, f7 c, d8 c, \ NEG RAX
    
    asm-then
    
    mov-tos-rax
    mov-rax-rdi
end-code

\ : native-max ( a b -- max )
code native-max
    mov-rax-tos    \ RAX = b
    mov-rbx-nos    \ RBX = a
    
    cmp-rax-rbx
    asm-jg         \ Om b > a (JG), hoppa till ELSE
        \ Fallthrough: b <= a. Dvs a (RBX) är max.
        48 c, 89 c, d8 c, \ MOV RAX, RBX
    asm-else
        \ Jump target: b > a. Dvs b (RAX) är max.
        \ Gör inget, RAX är redan b.
    asm-then
    
    sub-rdi-8      \ Pop NOS
    mov-tos-rax    \ Spara resultatet
    mov-rax-rdi
end-code

decimal
." Assembler loaded (Control Flow)." cr
