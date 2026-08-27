\ core-asm.fs - Native replacements for core words

." Replacing core words with native code..." cr
hex

code + ( a b -- a+b )
    mov-rax-tos
    mov-rbx-nos
    add-rax-rbx
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
end-code

\ Notera: SUB är icke-kommutativ. a b - -> a - b.
\ Stack: TOS=b, NOS=a.
\ Vi vill: NOS - TOS.
\ SUB RAX, RBX gör RAX = RAX - RBX. (b - a). FEL.
\ Vi vill ha a - b.
\ Alt 1: SUB RBX, RAX -> RBX = a - b. Flytta RBX till RAX.
\ Alt 2: NEG RAX. ADD RAX, RBX.
\ Låt oss använda SUB RBX, RAX (48 29 c3).
\ Vi saknar den opcoden i asm.fs.
\ Vi kör Alt 2: neg rax, add rax, rbx.
code - ( a b -- a-b )
    mov-rax-tos    \ RAX = b
    mov-rbx-nos    \ RBX = a
    48 c, f7 c, d8 c, \ NEG RAX ( -b )
    add-rax-rbx    \ RAX = -b + a
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
end-code

code dup ( n -- n n )
    mov-rax-tos    \ RAX = n
    add-rdi-8      \ PUSH
    mov-tos-rax    \ Spara n på nya toppen
    mov-rax-rdi
end-code

code drop ( n -- )
    sub-rdi-8      \ POP
    mov-rax-rdi
end-code

code swap ( a b -- b a )
    mov-rax-tos    \ RAX = b
    mov-rbx-nos    \ RBX = a
    mov-tos-rax    \ TOS = a (fel reg, vi vill ha RBX)
    \ Vi har inte mov-tos-rbx definierat i asm.fs
    \ Manuell: MOV [RDI-8], RBX (48 89 5f f8)
    48 c, 89 c, 5f c, f8 c,
    \ MOV [RDI-16], RAX (48 89 47 f0)
    48 c, 89 c, 47 c, f0 c,
    mov-rax-rdi
end-code

decimal
." Core optimized." cr
