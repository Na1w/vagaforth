\ ==============================================================================
\ build_c_forth.fs - Build a Custom VagaForth Snapshot with Inline-C Pre-loaded
\ ==============================================================================
\ This script demonstrates how to create a custom extended Forth system:
\ 1. Pre-loads the Inline-C compiler addon (addons/inline_c.fs).
\ 2. Defines a custom startup banner word (`c-edition-start`).
\ 3. Serializes the extended system into `vagaforth_c.bin` using `save-app`.
\ ==============================================================================

\ 1. Load the Inline-C compiler addon into the live dictionary
include addons/inline_c.fs

\ 2. Define custom startup banner word that invokes the Forth REPL (START)
: c-edition-start
    cr
    ." +---------------------------------------------------------+" cr
    ." |   VagaForth C-Edition (with Native Inline-C JIT)        |" cr
    ." |   Usage: c-compile with C function strings              |" cr
    ." +---------------------------------------------------------+" cr
    START
    ;

\ 3. Save the snapshot as a runnable executable
save-app c-edition-start vagaforth_c.bin
