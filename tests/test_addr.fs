include core/prelude.fs
include core/asm.fs
include core/core-asm.fs
include core/os.fs
include core/elf.fs
include core/host-ext.fs
include core/cross.fs

target-on

hex

." ELF-ORIGIN: " ELF-ORIGIN . cr
." target-base: " target-base @ . cr
." target-dp: " target-dp @ . cr

." Testing t-create..." cr

t-create TEST-WORD
c3 c,

." TEST-WORD created" cr
." target-latest: " target-latest @ . cr

." Converting virt to host..." cr
target-latest @ virt>host . cr

." Reading from host addr..." cr
target-latest @ virt>host @ . cr

bye
