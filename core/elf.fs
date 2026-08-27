\ elf.fs - ELF64 Header Generation

hex

\ Constants
400000 constant ELF-ORIGIN
00400000 constant ELF-ORIGIN-LOW \ For clarity if needed
\ 400000 is 4MB.

\ ELF Header Construction
\ Writes the header at the BEGINNING of the target buffer.
\ Assumes target-dp is currently pointing to the END of the code.
\ We need to be able to write to the start.
\ For now, we assume the user allotted space at the start.

: elf-header ( entry-point file-size mem-size -- )
    >r \ Save mem-size to return stack
    swap \ Stack: mem-size file-size entry-point -> file-size entry-point

    \ --- ELF Header (64 bytes) ---
    \ e_ident
    7f c, 45 c, 4c c, 46 c, \ Magic: .ELF
    02 c,                   \ Class: 64-bit
    01 c,                   \ Data: Little Endian
    01 c,                   \ Version: 1
    00 c,                   \ OS ABI: System V
    00 c,                   \ ABI Version
    00 c, 00 c, 00 c, 00 c, 00 c, 00 c, 00 c, \ Padding

    \ e_type
    0002 2,                 \ ET_EXEC (Executable file)

    \ e_machine
    003e 2,                 \ EM_X86_64 (AMD x86-64)

    \ e_version
    00000001 4,             \ EV_CURRENT

    \ e_entry
    8,                      \ Entry point virtual address (consumes entry-point)

    \ e_phoff (Program Header Offset)
    00000040 8,             \ Immediately after ELF header (64 bytes)

    \ e_shoff (Section Header Offset)
    00000000 8,             \ None

    \ e_flags
    00000000 4,

    \ e_ehsize
    0040 2,                 \ 64 bytes

    \ e_phentsize
    0038 2,                 \ 56 bytes

    \ e_phnum
    0001 2,                 \ 1 entry

    \ e_shentsize
    0040 2,                 \ 64 bytes (unused)

    \ e_shnum
    0000 2,                 \ 0

    \ e_shstrndx
    0000 2,                 \ 0

    \ --- Program Header (56 bytes) ---
    \ p_type
    00000001 4,             \ PT_LOAD

    \ p_flags
    00000007 4,             \ RWE (Read|Write|Execute) - Simplest for Forth

    \ p_offset
    00000000 8,             \ Offset in file (0 includes headers)

    \ p_vaddr
    ELF-ORIGIN 8,           \ Virtual Address

    \ p_paddr
    ELF-ORIGIN 8,           \ Physical Address (same)

    \ p_filesz
    dup 8,                  \ Size in file (file-size from stack)

    \ p_memsz
    r> 8,                   \ Size in memory (mem-size from R stack)

    \ p_align
    00001000 8,             \ Alignment (4KB)
    ;

variable filename-buf 256 allot

: save-elf ( filename-addr filename-len -- )
    \ 1. Null-terminate filename
    filename-buf 256 0 fill  \ Clean buffer
    filename-buf swap cmove  \ Copy string
    
    \ 2. Create file (Mode 0755 = rwxr-xr-x = 1ED hex)
    filename-buf 1ED sys-creat
    dup 0 < if
        ." Error creating file." cr drop exit
    then
    
    \ Stack: fd
    dup >r \ Save fd to return stack
    
    \ 3. Write Target Buffer
    target-base @    \ Address
    target-dp @ target-base @ - \ Length
    sys-write
    
    \ Check write result (optional, assuming success for now)
    drop
    
    \ 4. Close
    r> sys-close drop
    ." Saved binary." cr
    ;

