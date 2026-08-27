\ trampoline.fs
: cr 10 emit ;
include core/prelude.fs
." Loading extensions..." cr
include core/asm.fs
include core/core-asm.fs

decimal

." Testing host assembler..." cr
10 20 + . cr \ 30

target-on
." Target mode active. Writing to target memory..." cr
\ Nu skriver vi maskinkod till target buffer!
hex
90 c, \ NOP
c3 c, \ RET
decimal

." Target pointer moved: " target-dp @ target-base @ - . cr

target-off
." Back to host." cr

bye
