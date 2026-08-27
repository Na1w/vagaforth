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

." Testing address conversions" cr
." ELF-ORIGIN: " ELF-ORIGIN . cr
." target-base: " target-base @ . cr

400000 dup . ." virt->host: " virt>host . cr

bye
