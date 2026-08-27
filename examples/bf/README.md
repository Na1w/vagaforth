# Brainfuck Native JIT Compiler in VagaForth

A complete Brainfuck-to-x86-64 native Just-In-Time (JIT) compiler and interactive runner written in pure VagaForth.

---

## Overview

This example demonstrates the meta-compilation power of VagaForth. When loaded into VagaForth, it compiles into a standalone native ELF64 executable (`bf_compiler.bin`). 

At runtime, `bf_compiler.bin` parses Brainfuck source code, translates each instruction directly into native x86-64 machine code instructions in an executable memory buffer, and executes the compiled code directly on the CPU using `EXECUTE`!

---

## Machine Code Generation

| Brainfuck Opcode | Semantic | Generated x86-64 Machine Code |
| :--- | :--- | :--- |
| `>` | Pointer Increment (`ptr++`) | `48 ff c3` (`inc rbx`) |
| `<` | Pointer Decrement (`ptr--`) | `48 ff cb` (`dec rbx`) |
| `+` | Cell Increment (`(*ptr)++`) | `fe 03` (`inc byte [rbx]`) |
| `-` | Cell Decrement (`(*ptr)--`) | `fe 0b` (`dec byte [rbx]`) |
| `.` | Output Byte | `sys_write(1, rbx, 1)` syscall (`48 c7 c7 01... 0f 05`) |
| `,` | Input Byte | `sys_read(0, rbx, 1)` syscall (`48 c7 c7 00... 0f 05`) |
| `[` | Conditional Jump Forward | `80 3b 00 0f 84 <rel32>` (`cmp byte [rbx], 0; je rel32`) |
| `]` | Conditional Jump Backward | `80 3b 00 0f 85 <rel32>` (`cmp byte [rbx], 0; jne rel32`) |

---

## Building and Running

1. **Build all binaries via Makefile:**
   ```bash
   make
   ```

2. **Run the interactive Brainfuck compiler:**
   ```bash
   ./bf_compiler.bin
   ```

3. **Menu Options:**
   - `1`: Run built-in **"Hello, World!"**
   - `2`: Run built-in **"A-Z Alphabet Generator"**
   - `3`: Input and execute your own custom Brainfuck code in real-time (end input with `!`).
   - `Q`: Quit

---

## Included Examples

- `hello.bf`: The standard "Hello World!" program.
- `alphabet.bf`: Generates ASCII uppercase letters A-Z.
- `rot13.bf`: Classic ROT13 cipher implementation.
