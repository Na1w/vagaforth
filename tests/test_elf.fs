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

\ Skapa några byte
c3 c, c3 c, c3 c,

here target-base @ - ELF-ORIGIN + constant MY-ENTRY

." Entry point should be: " MY-ENTRY . cr

here target-base @ - constant BIN-SIZE

." Binary size: " BIN-SIZE . cr

." Before elf-header, stack: " .s cr

MY-ENTRY BIN-SIZE 100000 elf-header

." After elf-header" cr

bye
