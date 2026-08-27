: cr 10 emit ;
include core/prelude.fs
include core/asm.fs

code native-inc ( n -- n+1 )
    mov-rax-tos
    inc-rax
    mov-tos-rax
    mov-rax-rdi
end-code

code native-plus ( a b -- a+b )
    mov-rax-tos
    mov-rbx-nos
    add-rax-rbx
    sub-rdi-8
    mov-tos-rax
    mov-rax-rdi
end-code

decimal \ Tillbaka till decimalt för testet

." Testing native-inc with 10..." cr
10 native-inc 
." Result: " . cr

." Testing native-plus with 20 30..." cr
20 30 native-plus
." Result: " . cr
