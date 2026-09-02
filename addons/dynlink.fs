\ ==============================================================================
\ addons/dynlink.fs - Pure Forth ELF64 Dynamic Library Loader & FFI
\ ==============================================================================
\ Provides native dlopen, dlsym, and dlclose for Linux x86-64 without requiring
\ external libdl or dynamic linkers. Loads .so files directly into memory,
\ parses their ELF64 symbol tables, and enables calling C functions via call0..call6.
\ ==============================================================================

decimal

\ --- Memory & File Constants ---
7 constant PROT-RWX
34 constant MAP-PRIV-ANON \ MAP_PRIVATE (2) | MAP_ANONYMOUS (0x20) = 34 (0x22)
0 constant SEEK-SET

\ --- Buffers ---
create elf-hdr-buf 64 allot
create phdr-buf 56 allot
create dl-fn-buf 256 allot

\ --- Dynamic Linker Variables ---
variable dl-fd
variable dl-base
variable dl-span
variable dl-phoff
variable dl-phnum
variable dl-dyn-vaddr
variable dl-dyn-sz
variable dl-strtab
variable dl-symtab
variable dl-strsz
variable dl-handle

variable sym-search-name
variable sym-search-len
variable sym-curr-idx
variable sym-found-addr
variable sym-loop-flag

\ --- Helper: string comparison ( addr1 len1 addr2 len2 -- bool ) ---
: dl-str-eq ( a1 l1 a2 l2 -- bool )
    2 pick <> if
        2drop 2drop 0
    else
        0 >r
        begin
            over 0 > if
                2 pick c@ over c@ = if
                    swap 1- swap
                    1+
                    rot 1+ rot rot
                    0
                else
                    drop 2drop
                    r> drop 0 >r
                    1
                then
            else
                drop 2drop
                r> drop 1 >r
                1
            then
        until
        r>
    then
    ;

\ --- Helper: C null-terminated string length ---
: dl-c-strlen ( c-str -- len )
    0
    begin
        over over + c@ 0 > if
            1+ 0
        else
            nip 1
        then
    until
    ;

\ ==============================================================================
\ dlopen ( filename-addr filename-len -- handle | 0 )
\ ==============================================================================
: dlopen ( name-addr len -- handle )
    dup 255 > if 2drop 0 exit then
    dl-fn-buf 256 0 fill
    2dup dl-fn-buf swap cmove
    dl-fn-buf + 0 swap c!
    
    dl-fn-buf 256 0 0 sys-open dl-fd !
    dl-fd @ 0 < if 0 exit then

    dl-fd @ elf-hdr-buf 64 sys-read 64 < if
        dl-fd @ sys-close drop
        0 exit
    then

    elf-hdr-buf c@ 127 <> if dl-fd @ sys-close drop 0 exit then
    elf-hdr-buf 1+ c@ 69 <> if dl-fd @ sys-close drop 0 exit then
    elf-hdr-buf 2 + c@ 76 <> if dl-fd @ sys-close drop 0 exit then
    elf-hdr-buf 3 + c@ 70 <> if dl-fd @ sys-close drop 0 exit then

    elf-hdr-buf 32 + @ dl-phoff !
    elf-hdr-buf 56 + c@ dl-phnum !

    0 dl-span !
    0 dl-dyn-vaddr !
    0 dl-dyn-sz !

    0
    begin
        dup dl-phnum @ < if
            dl-fd @ dl-phoff @ 2 pick 56 * + SEEK-SET sys-lseek drop
            dl-fd @ phdr-buf 56 sys-read drop

            phdr-buf @ 4294967295 and 1 = if \ PT_LOAD
                phdr-buf 16 + @ phdr-buf 40 + @ +
                dup dl-span @ > if dl-span ! else drop then
            then

            phdr-buf @ 4294967295 and 2 = if \ PT_DYNAMIC
                phdr-buf 16 + @ dl-dyn-vaddr !
                phdr-buf 40 + @ dl-dyn-sz !
            then

            1+ 0
        else
            drop 1
        then
    until

    dl-span @ 4095 + 4095 invert and dl-span !
    dl-span @ 0= if dl-fd @ sys-close drop 0 exit then

    0 dl-span @ PROT-RWX MAP-PRIV-ANON -1 0 sys-mmap dl-base !
    dl-base @ 0 < if dl-fd @ sys-close drop 0 exit then

    0
    begin
        dup dl-phnum @ < if
            dl-fd @ dl-phoff @ 2 pick 56 * + SEEK-SET sys-lseek drop
            dl-fd @ phdr-buf 56 sys-read drop

            phdr-buf @ 4294967295 and 1 = if \ PT_LOAD
                dl-fd @ phdr-buf 8 + @ SEEK-SET sys-lseek drop
                dl-fd @ dl-base @ phdr-buf 16 + @ + phdr-buf 32 + @ sys-read drop
            then

            1+ 0
        else
            drop 1
        then
    until

    0 dl-strtab !
    0 dl-symtab !
    0 dl-strsz !

    dl-dyn-vaddr @ if
        dl-base @ dl-dyn-vaddr @ +
        begin
            dup @ \ d_tag (8 bytes)
            dup 0 <> if \ not DT_NULL
                dup 5 = if \ DT_STRTAB
                    over 8 + @ dl-base @ + dl-strtab !
                then
                dup 6 = if \ DT_SYMTAB
                    over 8 + @ dl-base @ + dl-symtab !
                then
                dup 10 = if \ DT_STRSZ
                    over 8 + @ dl-strsz !
                then
                drop
                16 + 0
            else
                drop drop 1
            then
        until
    then

    dl-fd @ sys-close drop

    40 bss-allot dl-handle !
    dl-base @   dl-handle @ !
    dl-strtab @ dl-handle @ 8 + !
    dl-symtab @ dl-handle @ 16 + !
    dl-strsz @  dl-handle @ 24 + !
    dl-span @   dl-handle @ 32 + !

    dl-handle @
    ;

\ ==============================================================================
\ dlsym ( handle symbol-name-addr symbol-name-len -- func-addr | 0 )
\ ==============================================================================
: dlsym ( handle name-addr len -- func-addr )
    sym-search-len !
    sym-search-name !
    dl-handle !

    dl-handle @ 0= if 0 exit then
    dl-handle @ @ dl-base !
    dl-handle @ 8 + @ dl-strtab !
    dl-handle @ 16 + @ dl-symtab !
    dl-handle @ 24 + @ dl-strsz !

    dl-symtab @ 0= dl-strtab @ 0= or if 0 exit then

    0 sym-found-addr !
    0 sym-curr-idx !
    0 sym-loop-flag !

    begin
        \ sym_ptr = dl-symtab + idx * 24 (Elf64_Sym is 24 bytes)
        dl-symtab @ sym-curr-idx @ 24 * +
        
        \ st_name is 4 bytes at offset 0
        @ 4294967295 and \ st_name offset into strtab
        dup dl-strsz @ < if
            dup 0 > if
                dl-strtab @ + \ str pointer
                dup dl-c-strlen \ ( str-addr str-len )
                sym-search-name @ sym-search-len @ dl-str-eq if
                    \ Found! Read st_value (8 bytes at offset 8)
                    dl-symtab @ sym-curr-idx @ 24 * + 8 + @
                    dup 0 > if
                        dl-base @ + sym-found-addr !
                        1 sym-loop-flag !
                    then
                then
            then
        else
            1 sym-loop-flag !
        then

        sym-curr-idx @ 1+ sym-curr-idx !
        sym-curr-idx @ 1000 > if 1 sym-loop-flag ! then
        sym-loop-flag @
    until

    sym-found-addr @
    ;

\ ==============================================================================
\ dlclose ( handle -- status )
\ ==============================================================================
: dlclose ( handle -- status )
    dup if
        dup @ swap 32 + @ sys-munmap
    else
        drop 0
    then
    ;
