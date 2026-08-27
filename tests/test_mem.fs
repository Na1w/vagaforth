include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs

target-on

hex

: cr 10 emit ;

t-create TEST-WORD
c3 c,

." Testing memory access to target dictionary" cr
." target-latest: " target-latest @ . cr

." Converting to host addr..." cr
target-latest @ virt>host 
dup ." Host addr: " . cr

dup @ ." Link value: " . cr

8 + 
dup ." After skip link: " . cr
dup c@ ." Len byte: " . cr

1+ 
dup ." Name addr: " . cr
5 type cr

bye
