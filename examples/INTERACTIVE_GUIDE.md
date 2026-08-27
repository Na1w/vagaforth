# VagaForth Advanced Examples — Interactive Guide

This guide walks you through the advanced example words defined in
[`examples/advanced.fs`](advanced.fs). Each section gives you the exact REPL
input to type, the expected output, and a short explanation of what is being
tested.

> **Note on negative numbers:** the VagaForth REPL does not parse a leading
> `-` as part of a literal (e.g. `-3` is read as the word `-` followed by `3`).
> To feed a negative value, compute it arithmetically first, e.g. `0 1 -`
> leaves `-1` on the stack.

---

## 1. Loading Instructions

The examples are loaded by *piping* the source file(s) into the interpreter
followed by `-` (which switches the interpreter into interactive REPL mode).
Everything after the `-` is read from your keyboard.

### Load `advanced.fs` alone

```bash
cat examples/advanced.fs - | ./vagaforth.bin
```

### Load both `test.fs` and `advanced.fs`

```bash
cat examples/test.fs examples/advanced.fs - | ./vagaforth.bin
```

Loading `test.fs` first also defines a few helper words (`cr`, `space`,
`square`, `cube`, `.s`, `count-to`, `fizzbuzz-check`) that you can use
alongside the advanced examples.

> **What you'll see:** while the file is being read, the REPL echoes each token
> (prefixed with `?`) and prints `compiled` as colon definitions are built.
> Once the file is fully loaded you'll be back at the `[] > ` prompt, ready to
> type your own input.

---

## 2. "Try This" Sessions

### 2.1 Factorial

```forth
5 factorial .
```

**Expected output:**

```
120
```

**What it tests:** `factorial` computes `n!` using a `begin ... until` loop
with repeated multiplication. `5! = 5 × 4 × 3 × 2 × 1 = 120`. The `.` prints
the result and pops it from the stack.

| Input | Expected output |
|-------|-----------------|
| `5 factorial .` | `120` |
| `7 factorial .` | `5040` |
| `0 factorial .` | `1` |

---

### 2.2 Fibonacci

```forth
10 fib .
```

**Expected output:**

```
55
```

**What it tests:** `fib` returns the *n*-th Fibonacci number (0-indexed) using
three scratch cells (`fib-a`, `fib-b`, `fib-n`) and an iterative loop.
`fib(10) = 55`.

You can also print a whole sequence with `fib-seq`:

```forth
5 fib-seq
```

**Expected output:**

```
0 1 1 2 3
```

**What it tests:** `fib-seq` prints `fib(0)` through `fib(n-1)`, separated by
spaces, followed by a newline.

| Input | Expected output |
|-------|-----------------|
| `10 fib .` | `55` |
| `5 fib-seq` | `0 1 1 2 3` |
| `7 fib-seq` | `0 1 1 2 3 5 8` |

---

### 2.3 Times-Table

```forth
3 times-table
```

**Expected output:**

```
1 2 3
2 4 6
3 6 9
```

**What it tests:** `times-table` builds an *n × n* multiplication table using
two nested `begin ... until` loops and the scratch cells `tt-n`, `tt-i`,
`tt-j`. Each row is printed followed by a newline.

| Input | Expected output |
|-------|-----------------|
| `3 times-table` | `1 2 3` / `2 4 6` / `3 6 9` |
| `4 times-table` | `1 2 3 4` / `2 4 6 8` / `3 6 9 12` / `4 8 12 16` |

---

### 2.4 Classify

```forth
5 classify
```

**Expected output:**

```
small
```

**What it tests:** `classify` categorizes a number using nested comparisons:

- `0` → `zero`
- positive and `< 10` → `small`
- positive and `>= 10` → `big`
- negative → `negative`

Try a few values:

```forth
0 classify
```

**Expected output:**

```
zero
```

```forth
0 1 - classify
```

**Expected output:**

```
negative
```

```forth
10 classify
```

**Expected output:**

```
big
```

**What it tests:** the `0 1 -` form computes `-1` arithmetically (see the note
at the top about negative literals) and verifies the negative branch.

| Input | Expected output |
|-------|-----------------|
| `5 classify` | `small` |
| `0 classify` | `zero` |
| `10 classify` | `big` |
| `0 1 - classify` | `negative` |

---

### 2.5 Stack Demos

These words demonstrate stack manipulation and print their own diagnostic
label.

#### swap3 — reorder three values

```forth
1 2 3 swap3
```

**Expected output:**

```
swap3: 1 2 3
```

**What it tests:** `swap3` reverses the order of three stack items
(`a b c -- c b a`) using `rot rot swap`, then prints them.

#### 2dup-sum — duplicate and sum

```forth
4 5 2dup-sum
```

**Expected output:**

```
2dup-sum: 9 5 4
```

**What it tests:** `2dup-sum` duplicates the top two items, sums them, and
leaves `a b a+b` on the stack. The printed order is top-of-stack first, so you
see `a+b` (9), then `b` (5), then `a` (4).

#### nip-demo — keep only the second value

```forth
7 9 nip-demo
```

**Expected output:**

```
nip-demo: 9
```

**What it tests:** `nip-demo` drops the top item and keeps the second
(`a b -- b`), then prints the survivor.

| Input | Expected output |
|-------|-----------------|
| `1 2 3 swap3` | `swap3: 1 2 3` |
| `4 5 2dup-sum` | `2dup-sum: 9 5 4` |
| `7 9 nip-demo` | `nip-demo: 9` |

---

### 2.6 FizzBuzz

```forth
15 fizzbuzz
```

**Expected output:**

```
FizzBuzz
```

**What it tests:** `fizzbuzz` prints `Fizz` for multiples of 3, `Buzz` for
multiples of 5, `FizzBuzz` for multiples of both, and the number itself
otherwise. It uses the `mod` helper for the divisibility checks.

Run the whole 1–15 sequence with:

```forth
fizzbuzz-run
```

**Expected output:**

```
1
2
Fizz
4
Buzz
Fizz
7
8
Fizz
Buzz
11
Fizz
13
14
FizzBuzz
```

**What it tests:** `fizzbuzz-run` loops from 1 to 15, calling `fizzbuzz` on
each value.

| Input | Expected output |
|-------|-----------------|
| `3 fizzbuzz` | `Fizz` |
| `5 fizzbuzz` | `Buzz` |
| `15 fizzbuzz` | `FizzBuzz` |
| `7 fizzbuzz` | `7` |
| `fizzbuzz-run` | the full 1–15 sequence above |

---

## 3. Quick Reference

| Word | Stack effect | Purpose |
|------|--------------|---------|
| `mod` | `( n m -- rem )` | Remainder via repeated subtraction |
| `factorial` | `( n -- n! )` | Factorial via a loop |
| `fib` | `( n -- nth )` | *n*-th Fibonacci number (0-indexed) |
| `fib-seq` | `( n -- )` | Print `fib(0)..fib(n-1)` |
| `times-table` | `( n -- )` | Print an *n × n* multiplication table |
| `classify` | `( n -- )` | Print `zero` / `small` / `big` / `negative` |
| `swap3` | `( a b c -- c b a )` | Reverse three stack items |
| `2dup-sum` | `( a b -- a b a+b )` | Duplicate two items and sum them |
| `nip-demo` | `( a b -- b )` | Keep only the second value |
| `fizzbuzz` | `( n -- )` | Print Fizz / Buzz / FizzBuzz / number |
| `fizzbuzz-run` | `( -- )` | Run `fizzbuzz` for 1..15 |
