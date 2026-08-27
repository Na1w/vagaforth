." -- self_compile_test: on-target compilation --" cr
." [1] Compile helper 'add5' from source string, then execute it:" cr
s" : add5 5 + ;" compile-source
10 add5 . cr
." [2] Newly compiled word is reusable / composed (add10 = add5 add5):" cr
s" : add10 add5 add5 ;" compile-source
0 add10 . cr
." [3] Control flow (if/else/then) compiled from a source string:" cr
s" : sign dup 0 < if -1 else 1 then ;" compile-source
5 sign . cr
-5 sign . cr
." [4] Defining words (constant / variable) compiled from source:" cr
s" 42 constant answer" compile-source
answer . cr
s" variable counter  7 counter !" compile-source
counter @ . cr
." [5] Runtime dictionary pointer advanced by self-compilation:" cr
." runtime HERE="
HERE . cr
." [6] Status word reports dictionary bytes + word count:" cr
version
." BOUNDARY OF SELF-COMPILATION" cr
."  - Native kernel provides: REPL, dictionary, : ; create variable constant" cr
."    , c, allot, evaluate/compile-source, control flow, all arithmetic." cr
."  - Host bootstrap provides: ELF emit (save-elf), cross-assembler, t-code." cr
."    The target CANNOT re-emit a new ELF; it compiles only counted source." cr
