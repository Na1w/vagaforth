# Vagaforth

**Vagaforth** is a self-hosting implementation of the [Forth](https://en.wikipedia.org/wiki/Forth_(programming_language)) programming language. It combines a small, hand-written C interpreter (the *host*) with a complete Forth language layer that is capable of **cross-compiling** and **self-compiling** a native x86-64 ELF binary.

The project demonstrates a full bootstrap chain: a C interpreter builds a first-generation Forth kernel, which in turn compiles itself into a standalone, runnable executable — a genuine proof of self-hosting.

---

## Overview

Forth is a stack-based, concatenative programming language known for its extreme simplicity and low-level control. Vagaforth embraces that philosophy:

- **Tiny host core** — a minimal C interpreter that provides the primitive words, memory model, and REPL.
- **Forth-defined everything else** — the dictionary, compiler, assembler, ELF emitter, and cross-compiler are all written *in Forth itself*.
- **Self-hosting** — the target kernel can compile its own source into a fresh native binary, closing the bootstrap loop.

The architecture is split into two distinct layers:

| Layer | Language | Role |
|-------|----------|------|
| **Host interpreter** | C (`src/main.c`) | Provides primitives, stacks, dictionary memory, and the REPL. |
| **Forth kernel** | Forth (`kernel/`, `core/`) | Implements the compiler, assembler, cross-compiler, and self-hosting logic. |

---

## Repository Layout

```
.
├── Makefile              # Builds the host C interpreter
├── bootstrap.sh          # Build host → cross-compile kernel → run bootstrap source
├── selfhost.sh           # Full self-hosting bootstrap + verification sequence
├── bootstrap.fs          # Bootstrap source exercising core words
├── trampoline.fs         # Host-side extension loader / assembler smoke test
├── include/
│   └── vagaforth.h       # Host interpreter header (types, globals, flags)
├── src/
│   └── main.c            # Host C interpreter (human-written)
├── core/                 # Forth core libraries
│   ├── prelude.fs        # Target memory abstraction & mode switching
│   ├── host-ext.fs       # Host-side helper words
│   ├── asm.fs            # Target assembler
│   ├── core-asm.fs       # Core assembler helpers
│   ├── cross.fs          # Cross-compiler utilities
│   ├── elf.fs            # ELF64 header/program-header emitter
│   ├── os.fs             # OS / syscall interface
│   ├── ffi.fs            # Foreign function interface helpers
│   ├── struct.fs         # Structure helpers
│   ├── does.fs           # DOES> support
│   └── debug.fs          # Debug helpers
├── kernel/               # Target kernel sources
│   ├── kernel.fs         # Main target kernel (interpreter loop, primitives)
│   ├── kernel_self.fs    # Forth library compiled by self-hosting stage
│   └── selfhost.fs       # Self-hosting driver (compile-file, save-elf)
├── addons/               # VagaForth Addon modules
│   └── inline_c.fs       # Inline C compiler (x86-64 native JIT + Forth bridge)
├── examples/             # Example Forth programs
│   ├── test.fs
│   ├── advanced.fs
│   ├── guess_game.fs     # Interactive number guessing game (emits standalone binary)
│   ├── dungeon.fs        # Classic text adventure dungeon game (emits standalone binary)
│   ├── platformer.fs     # 2D ASCII platformer runner game (emits standalone binary)
│   ├── c_inline/         # Inline C compilation demo
│   │   └── demo_inline_c.fs
│   ├── bf/               # Brainfuck-to-x86-64 native JIT compiler & examples
│   │   ├── bf_compiler.fs
│   │   ├── compile_hello_bf.fs # Ahead-of-Time compiler emitting hello_bf.bin
│   │   ├── hello.bf
│   │   ├── alphabet.bf
│   │   ├── rot13.bf
│   │   └── README.md
│   └── INTERACTIVE_GUIDE.md
├── tests/                # Test suites
└── docs/
    └── root_cause.md     # Root-cause analysis of arithmetic segfaults
```

---

## How It Works

### 1. The Host Interpreter (C)

`src/main.c` implements a minimal Forth interpreter in C. It provides:

- A **data stack** and **return stack** (`cell_t` arrays).
- A **dictionary** region (`16 MB`) for words and generated code.
- Primitive words (`+`, `-`, `dup`, `drop`, `swap`, `.`, `emit`, `key`, etc.).
- A REPL with interpret/compile state switching.
- A `code_t` function-pointer model so Forth words can call native C bodies.

The host uses a documented register-based calling convention: **RDI** holds the data-stack pointer on entry, and **RAX** returns the updated pointer on exit (see `docs/root_cause.md` for the full convention and a historical bug fix).

### 2. Cross-Compilation

The host interpreter loads the Forth kernel sources and, via `core/cross.fs`, emits native x86-64 machine code into a **target memory buffer**. Key mechanisms:

- `target-on` / `target-off` switch the memory abstraction between host and target space.
- `t-create`, `t-find`, and `t-match?` build and search a *target* dictionary with virtual-address links.
- `core/asm.fs` provides an assembler that emits raw x86-64 opcodes.
- `core/elf.fs` writes a complete ELF64 header and program header, producing a standalone executable.

The result is a **first-generation** target binary (`vagaforth.bin`).

### 3. Self-Compilation

`kernel/selfhost.fs` implements the self-hosting driver. The first-generation binary:

1. Reads its own kernel source from disk (`compile-file`).
2. Re-compiles that source into a fresh target image.
3. Emits a new ELF64 executable (`vagaforth_new.bin`) via `save-elf`.

Because the second-generation binary is produced *by* the first-generation binary from the same source, the compiler is proven to be self-hosting.

### 4. Bootstrap Sequence

`selfhost.sh` orchestrates the full proof:

```
Stage 1: make                                 → build host C interpreter (vagaforth)
Stage 2: ./vagaforth kernel/kernel.fs         → cross-compile target kernel (vagaforth.bin)
Stage 3: ./vagaforth.bin < kernel/selfhost.fs → self-compile (vagaforth_new.bin)
Stage 4: verify vagaforth_new.bin             → valid ELF64 x86-64 AND runs
```

---

## Building

### Prerequisites

- A C compiler (`gcc` or compatible)
- `make`
- A Linux x86-64 environment (the target emits ELF64 x86-64 binaries)

### Build the host interpreter

```bash
make
```

This produces the `vagaforth` host binary.

### Run the full bootstrap

```bash
./bootstrap.sh     # build host, cross-compile kernel, run bootstrap source
./selfhost.sh      # full self-hosting bootstrap + verification
```

---

## Usage

### Interactive REPL

Run the C host interpreter REPL:
```bash
./vagaforth
```

Or run the native self-hosted x86-64 Forth REPL:
```bash
./vagaforth_new.bin
```

### Run a Forth source file

```bash
cat examples/test.fs - | ./vagaforth_new.bin
```

The trailing `-` switches the interpreter into interactive REPL mode after the file is loaded.

### Example session

```forth
5 square .        \ → 25
3 cube .          \ → 27
6 fib .           \ → 8
```

---

## Inline C Compiler Addon (`addons/inline_c.fs`)

VagaForth includes a built-in **Inline C Compiler** addon that parses C function syntax directly from a string and JIT-compiles it into native x86-64 machine code, automatically registering bridge words in the Forth dictionary. It includes a built-in C standard I/O runtime (`printf`, `puts`, `gets`, `putchar`, `getchar`).

### Quick Example

```forth
include addons/inline_c.fs

\ 1. Arithmetic & recursion in C:
s" int add(int a, int b) { return a + b; }" c-compile
20 30 add .   \ Outputs 50

s" int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); }" c-compile
10 fib .      \ Outputs 55

\ 2. Formatted output with printf() and string literals:
s" void greet(char *name, int score) { printf('Hello %s! Score: %d (0x%x)\n', name, score, score); }" c-compile
: c-str 2dup + 0 swap c! drop ;
s" Fredrik" c-str 100 greet

\ 3. Interactive input with gets():
create user-buf 256 allot drop
s" void ask(char *buf) { printf('Your name: '); gets(buf); printf('Welcome, %s!\n', buf); }" c-compile
user-buf ask
```

Run the inline C demo:
```bash
make demo-c
```

---

## 100% Native Compilation & Standalone ELF Executables

VagaForth is a **true native compiler** for x86-64 Linux. It does **not** rely on bytecode interpreters, virtual machines, or C runtime libraries (`libc`).

### Architecture: How 100% Native Code Works

1. **Direct Machine Code Emission:**
   - When a Forth word is defined with `:` (colon), machine code (`CALL`, `RET`, stack manipulations) is assembled directly into executable memory.
   - Inline C functions compiled via `c-compile` are translated on the fly to native x86-64 instructions.
   - All I/O operations (`EMIT`, `KEY`, `type`, `included`) perform Linux kernel system calls directly via the `syscall` instruction (`0f 05`).

2. **Standalone Binary Serialization (`save-elf-at`):**
   - When `save-elf-at` is called, VagaForth constructs an ELF64 header and program header pointing directly to a native startup trampoline.
   - The startup trampoline initializes the hardware return stack (`RSP`), sets up the Forth data stack pointer (`RDI`), and calls the application entry point via an absolute machine call (`call rbx`).
   - When the entry word finishes and returns, the trampoline invokes Linux `sys_exit` (`syscall 60`), cleanly terminating the process without requiring any external runtime.

---

### 1. `save-elf-at` (Custom Application Entry Point)

`save-elf-at` creates a standalone Linux x86-64 ELF executable whose entry point immediately executes a specified Forth word when launched:

**Stack effect:** `( entry-addr entry-len filename-addr filename-len -- )`

```forth
\ Define the application logic:
: main
    cr
    ." =======================================" cr
    ."   Standalone VagaForth Binary Running! " cr
    ." =======================================" cr
    ." 7 * 8 = " 7 8 * . cr
    ;

\ Save as a standalone executable 'hello.bin':
create entry-nm 16 allot
s" main" entry-nm swap cmove
entry-nm 4 s" hello.bin" save-elf-at
```

> **Note on `s"` buffers:** Because Forth's `s"` word uses a single shared temporary buffer (`S-BUF-ADDR`), copy the entry word name into a dedicated buffer (e.g. `s" main" entry-nm swap cmove`) before passing the output filename `s" ..."` to `save-elf-at`.

#### Building & Running from Bash:

```bash
# Feed the Forth script to the self-hosted compiler:
./vagaforth_new.bin < my_app.fs

# Run the generated binary directly:
chmod +x hello.bin
./hello.bin
```

---

### 2. Standalone Binaries with Inline C

You can seamlessly combine Forth definitions and Inline C functions into a standalone ELF binary:

```forth
include addons/inline_c.fs

\ Compile C logic into native machine code:
s" int fib(int n) { if (n <= 1) return n; return fib(n-1) + fib(n-2); } void c_greet() { printf('Hello from C!\nFibonacci(10) = %d\n', fib(10)); }" c-compile

\ Forth main word calling both C and Forth words:
: main
    c_greet
    ." Forth computation: 100 * 25 = " 100 25 * . cr
    ;

create entry-nm 16 allot
s" main" entry-nm swap cmove
entry-nm 4 s" c_app.bin" save-elf-at
```

---

### 3. Verifying the Native Binary

Because generated binaries are 100% static native x86-64 machine code emitted without bloated ELF section tables (only Program Headers `PT_LOAD`), you can inspect them with standard Linux binary analysis tools:

- **Check static linkage (no libc / ld.so dependencies):**
  ```bash
  ldd hello.bin
  # Output: not a dynamic executable
  ```

- **Inspect disassembled machine code (raw binary mode with 0x400000 base):**
  ```bash
  objdump -D -b binary -m i386:x86-64 --adjust-vma=0x400000 hello.bin | head -n 40
  ```

- **Disassemble starting directly from the entry trampoline:**
  ```bash
  ENTRY=$(readelf -h hello.bin | awk '/Entry point/{print $4}')
  objdump -D -b binary -m i386:x86-64 --adjust-vma=0x400000 --start-address=$ENTRY hello.bin
  ```

- **Trace direct kernel system calls:**
  ```bash
  strace ./hello.bin
  ```

---

### 4. `save-elf` (Interactive REPL Snapshot)

`save-elf` saves the entire Forth dictionary and memory state, setting the entry point to the standard interactive Forth REPL (`START`).

**Stack effect:** `( filename-addr filename-len -- )`

```forth
\ Define new words in the dictionary:
: double 2 * ;
: square dup * ;

\ Save the extended system snapshot:
s" my_custom_forth.bin" save-elf
```

When `./my_custom_forth.bin` is executed, it boots directly into the interactive Forth REPL with all custom words pre-loaded.

---

## Testing

Run the full automated test suite (unit tests, regression battery, differential testing, PTY interactive test, and inline C demo):

```bash
make test
```

Individual unit test files in `tests/` can also be loaded into the interpreter:

```bash
cat tests/test_dot.fs - | ./vagaforth_new.bin
```

---

## Documentation

- [`docs/root_cause.md`](docs/root_cause.md) — Root-cause analysis of a historical arithmetic segfault, documenting the register-based calling convention and the fix for a missing RDI re-sync in the target kernel's `START` loop.
- [`examples/INTERACTIVE_GUIDE.md`](examples/INTERACTIVE_GUIDE.md) — Interactive walkthrough of the example programs.

---

## License

This project is released under the MIT License. See the `LICENSE` file for details.

---

## Acknowledgments
- **Fredrik Andersson** — Author of the original Forth C interpreter.
- **`kvaser-cli`** — AI tooling used to generate the Forth language implementation, including the cross-compilation and self-compilation logic.
- **Google Gemini** — Forth examples (dungeon, brainfuck compiler, platformer) and the inline C compiler addon.
