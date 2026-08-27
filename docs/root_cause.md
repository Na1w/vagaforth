# Root Cause & Fix: VagaForth Arithmetic Segfaults

**Task IDs:** t-c001, t-c002, t-c003, t-c004 (analysis) / t-i001, t-i002, t-i003 (fix)
**Date:** 2026-08-22
**Synthesized from:** `cloud_coder` (primitive inventory / CFA-patching verification, t-c003) and `cloud_debugger` (GDB analysis & RDI drift trace, t-b002/t-b003, t-c001/t-c002)

---

## 1. The Correct Call Convention

The VagaForth target kernel uses a fixed, documented register-based data-stack contract:

- **RDI = Data Stack Pointer (DSP)** — the *input* to every word. The data stack grows **upward**; `TOS = [RDI-8]`, `NOS = [RDI-16]`.
- **RAX = DSP (out)** — every word returns the *updated* data stack pointer in RAX.
- **RBX** is callee-saved and must be preserved across word calls.

This convention is implemented correctly on the host side by `prim_code_runner_stub` in `src/main.c` (verified in t-c003): it loads `dsp` into RDI, calls the native body, then writes the returned RAX back to `dsp`. The host-side CFA-patching logic (`get-native-runner`, `here 10 - !`, `8 negate allot`) is also correct and is **not** the source of the target crash.

---

## 2. The Root Cause: Missing RDI Re-sync

The target kernel's `START` loop invoked words via a bare `asm-call` (a `CALL rel32`), but **discarded the return value in RAX** — it never re-synced RDI from RAX after the call.

The disassembly of the buggy loop confirms this:

```
400171:  e8 aa ff ff ff   call 0x400120   ; call +  (NO RDI<-RAX after!)
400176:  e9 f6 ff ff ff   jmp  0x400171   ; loop back to call +
```

There is **no `mov %rax,%rdi` (`48 89 c7`)** between the `call` and the loop-back jump. The updated DSP returned in RAX is simply thrown away.

This is fatal because arithmetic words such as `+` mutate RDI **directly** (e.g. `sub $0x8,%rdi` to pop), and then return the updated DSP in RAX:

```
0x400120:  mov    -0x8(%rdi),%rax   ; RAX = TOS (b)
0x400124:  mov    -0x10(%rdi),%rbx  ; RBX = NOS (a)
0x400128:  add    %rbx,%rax         ; RAX = a + b
0x40012b:  sub    $0x8,%rdi         ; POP: RDI -= 8  (MUTATES RDI directly)
0x40012f:  mov    %rax,-0x8(%rdi)   ; store result as new TOS
0x400133:  mov    %rdi,%rax         ; return updated DSP in RAX
0x400136:  ret
```

Because the caller keeps its own (now stale) RDI and ignores the RAX the word returned, the caller's RDI is left 8 bytes too high on every iteration.

---

## 3. The "RDI Drift" Phenomenon → Code Corruption → SIGSEGV

Since the caller never re-syncs RDI from RAX, the DSP **drifts downward by 8 bytes per operation**. The GDB trace (breakpoint at the `+` entry, `0x400120`) shows this monotonic decay:

```
PLUS entry: rdi=0x410010 rax=(nil)     <- 5 3 pushed, first call
PLUS entry: rdi=0x410008 rax=0x410008   <- 5+3=8, RDI drifts down 8
PLUS entry: rdi=0x410000 rax=0x410000
PLUS entry: rdi=0x40fff8 rax=0x40fff8   <- below data stack region
PLUS entry: rdi=0x40fff0 rax=0x40fff0
PLUS entry: rdi=0x40ffe8 rax=0x40ffe8
... (RDI keeps decreasing by 8 each iteration)
PLUS entry: rdi=0x4001a8 rax=0x4001a8   <- into code/ELF region
PLUS entry: rdi=0x400190 rax=0x400190
PLUS entry: rdi=0x400188 rax=0x400188   <- last entry before crash
```

**Total iterations before the crash: 8146.** RDI starts at `0x410010` and decreases by 8 each iteration.

The final crash state (GDB):

```
Program received signal SIGSEGV, Segmentation fault.
0x000000000041fff7 in ?? ()

rip = 0x41fff7
rdi = 0x400180   (DSP has drifted down into the code/ELF header region)
rax = 0x400180
rsp = 0x420000   (return stack intact)
rbx = 0xffffff
rsi = 0x0
```

Two compounding effects produce the SIGSEGV:

1. **Stale DSP reads garbage:** With RDI stale, the next `+` reads from the wrong stack slot, so the arithmetic operates on garbage.
2. **Code-region overwrite:** As RDI drifts down into the code/ELF region, the `+` word's `mov %rax,-0x8(%rdi)` **writes garbage into the program's own code bytes**, corrupting the instruction stream. The CPU eventually executes corrupted bytes at `0x41fff7` that dereference a null pointer (`add %dh,0x1(%rsi)` with `rsi=0`) → **SIGSEGV**.

The echo loop (`KEY`/`EMIT`) happened to survive only because those words preserve RDI via `asm-push-rdi`/`asm-pop-rdi`; any word that mutates RDI directly (arithmetic: `+`, `-`, `dup`, `drop`, `swap`) exposed the bug.

---

## 4. The Technical Fix: `asm-call-sync`

The fix is a reusable **`asm-call-sync`** helper in the target-side code generation (`kernel/kernel.fs`), which emits:

```
CALL <dest>      ; invoke the word (RDI = DSP in)
mov rdi, rax     ; 48 89 c7  — re-sync DSP from the word's return value
```

i.e. a `CALL` followed by `mov rdi, rax` (`48 89 c7`), so the DSP is re-synced from RAX after **every** word call. Every invocation of a target word in the interpreter loop and in `START` now goes through `asm-call-sync` (not the bare `asm-call`), guaranteeing RDI always tracks the DSP returned by the previous word.

The implementation in `kernel/kernel.fs`:

```forth
: asm-call-sync ( dest-virt -- )
    e8 c, 
    t-vhere 4 + - 4, 
    48 c, 89 c, c7 c,   \ mov rdi, rax
    ;
```

This is the single root-cause fix; the host-side runner stub and CFA-patching logic are already correct and require no changes.

---

## 5. Verification

The fix was verified under GDB (t-v004): the REPL loop re-syncs `RDI ← RAX` after every word call (PARSE-NAME, FIND, NUMBER?, and EXECUTE). At every sync point `RDI == RAX` (verified `YES` across all traced iterations for both single- and multi-word inputs). The arithmetic segfault root cause (missing RDI re-sync) is confirmed fixed.

Functional smoke tests pass:
- `5 3 + .` → prints `8`
- `1 2 + . 10 4 - .` → prints `3 6`
- `dup`/`drop`/`swap`/`.` all operate correctly.

---

## 6. Summary Table

| Aspect | Finding |
|--------|---------|
| **Call convention** | RDI = DSP (in), RAX = DSP (out), RBX preserved |
| **Root cause** | `START` loop called words via `asm-call` but discarded RAX; never re-synced RDI |
| **RDI drift** | DSP (RDI) drifted down 8 bytes/op → overwrote code region → SIGSEGV (8146 iters) |
| **Solution** | `asm-call-sync` helper: `CALL dest` + `mov rdi, rax` (`48 89 c7`) after every word call |
| **Host runner** | Correct (t-c003); not the source of the target crash |
