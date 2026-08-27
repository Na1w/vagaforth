\ os.fs - Linux Syscalls using Native Assembler

hex

\ Linux x86-64 Syscall Numbers
01 constant SYS_WRITE
3c constant SYS_EXIT

\ : sys-exit ( code -- )
code sys-exit
    mov-rax-tos    \ RAX = code
    mov-rdi-rax    \ RDI = code (Arg 1)
    
    \ Ladda syscall nummer (60 = 0x3c)
    48 c, c7 c, c0 c, 3c c, 00 c, 00 c, 00 c,
    
    syscall
end-code

\ : sys-write ( fd addr len -- count )
code sys-write
    \ Spara undan DSP (RDI) eftersom syscall behöver RDI-registret
    asm-push-rdi
    
    mov-rax-tos    \ RAX = len
    mov-rdx-rax    \ RDX = len (Arg 3)
    
    sub-rdi-8      \ Pop len. RDI pekar nu på addr.
    mov-rax-tos    \ RAX = addr
    mov-rsi-rax    \ RSI = addr (Arg 2)
    
    sub-rdi-8      \ Pop addr. RDI pekar nu på fd.
    mov-rax-tos    \ RAX = fd
    mov-rdi-rax    \ RDI = fd (Arg 1) -- RDI överskrivet!
    
    \ Ladda syscall nummer (1 = SYS_WRITE)
    48 c, c7 c, c0 c, 01 c, 00 c, 00 c, 00 c,
    
    syscall
    
    \ Återställ DSP
    asm-pop-rdi
    
    \ Justera DSP (Vi har konsumerat 2 argument: len, addr). 
    \ FD skrivs över av resultatet.
    sub-rdi-8
    sub-rdi-8
    
    \ Spara resultatet
    mov-tos-rax
    mov-rax-rdi    \ Returnera DSP
end-code

\ : sys-creat ( path mode -- fd )
code sys-creat
    asm-push-rdi   \ Save DSP
    
    mov-rax-tos    \ RAX = mode
    mov-rsi-rax    \ RSI = mode (Arg 2)
    
    sub-rdi-8      \ Pop mode
    mov-rax-tos    \ RAX = path
    mov-rdi-rax    \ RDI = path (Arg 1)
    
    \ Syscall 85 (creat)
    48 c, c7 c, c0 c, 55 c, 00 c, 00 c, 00 c,
    
    syscall
    
    asm-pop-rdi    \ Restore DSP
    sub-rdi-8      \ Pop path
    mov-tos-rax    \ Store FD
    mov-rax-rdi
end-code

\ : sys-close ( fd -- status )
code sys-close
    asm-push-rdi
    
    mov-rax-tos    \ RAX = fd
    mov-rdi-rax    \ RDI = fd (Arg 1)
    
    \ Syscall 3 (close)
    48 c, c7 c, c0 c, 03 c, 00 c, 00 c, 00 c,
    
    syscall
    
    asm-pop-rdi
    mov-tos-rax
    mov-rax-rdi
end-code

decimal

variable char-buf 0 char-buf !

: os-emit ( char -- )
    char-buf c!    \ Spara tecknet i minnet
    1              \ stdout
    char-buf       \ address
    1              \ length
    sys-write      \ anropa
    drop           \ kasta resultatet
    ;

." OS Syscalls loaded." cr