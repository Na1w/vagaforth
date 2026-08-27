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
├── examples/             # Example Forth programs
│   ├── test.fs
│   ├── advanced.fs
│   ├── guess_game.fs     # Interactive number guessing game (emits standalone binary)
│   ├── dungeon.fs        # Classic text adventure dungeon game (emits standalone binary)
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
Stage 1: make                          → build host C interpreter (vagaforth)
Stage 2: ./vagaforth kernel/kernel.fs  → cross-compile target kernel (vagaforth.bin)
Stage 3: ./vagaforth.bin < selfhost.fs → self-compile (vagaforth_new.bin)
Stage 4: verify vagaforth_new.bin      → valid ELF64 x86-64 AND runs
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

```bash
./vagaforth
```

### Run a Forth source file

```bash
cat examples/test.fs - | ./vagaforth.bin
```

The trailing `-` switches the interpreter into interactive REPL mode after the file is loaded.

### Example session

```forth
5 square .        \ → 25
3 cube .          \ → 27
6 fib .           \ → 8
```

See `examples/INTERACTIVE_GUIDE.md` for a detailed walkthrough of the example programs.

---

## Testing

The `kernel/` directory contains focused unit tests for individual words and subsystems (e.g. `test_plus.fs`, `test_emit.fs`, `test_resolve32.fs`, `test_start.fs`). These are loaded into the interpreter to verify behavior:

```bash
cat kernel/test_plus.fs - | ./vagaforth.bin
```

The `tests/` directory holds additional integration test suites.

---

## Documentation

- [`docs/root_cause.md`](docs/root_cause.md) — Root-cause analysis of a historical arithmetic segfault, documenting the register-based calling convention and the fix for a missing RDI re-sync in the target kernel's `START` loop.
- [`examples/INTERACTIVE_GUIDE.md`](examples/INTERACTIVE_GUIDE.md) — Interactive walkthrough of the example programs.

---

## License

This project is released under the MIT License. See the `LICENSE` file for details.

---

## Acknowledgments

- **`kvaser-cli`** — the AI tooling used to generate the Forth language implementation, including the cross-compilation and self-compilation logic.
