# Mandelbrot Explorer — User Guide

An interactive Mandelbrot-set explorer written in VagaForth. It renders the
fractal to your terminal in **24-bit ANSI truecolor**, lets you zoom, pan, and
tune the iteration depth live, and compiles down to a **standalone native ELF
binary** — no interpreter needed at runtime.

Everything is computed with **signed 30.34 fixed-point arithmetic**
(`value = int × 2^30`), because VagaForth has no native floating-point
support. The result is a self-contained, dependency-free fractal viewer that
fits in a single Forth source file.

---

## 1. Overview

`examples/mandelbrot.fs` implements a complete interactive explorer:

- **Renders the Mandelbrot set** on an 80 × 40 character grid.
- **24-bit ANSI truecolor** output (`ESC[38;2;R;G;Bm`) with a smooth
  HSV-style gradient, so each pixel is a full-color block.
- **Live controls** for zoom, pan, and iteration depth, read in raw terminal
  mode.
- **Standalone native binary** produced by `save-app`, so the finished
  `mandelbrot.bin` runs on its own without the VagaForth interpreter.

The default viewport is centered on `re = -0.5, im = 0` with a scale of
`0.05` and `100` max iterations — a classic overview of the set.

---

## 2. How to Build

From the workspace root, pipe the source file into the VagaForth interpreter:

```bash
./vagaforth_new.bin < examples/mandelbrot.fs
```

This compiles the program and, via the `save-app` word at the end of the
file, emits a standalone native ELF binary named **`mandelbrot.bin`** in the
**workspace root** (not inside `examples/`).

Make it executable:

```bash
chmod +x mandelbrot.bin
```

> **Note:** the build emits `mandelbrot.bin` in the current working
> directory. If you run the build from elsewhere, the binary lands there.

---

## 3. How to Run

```bash
./mandelbrot.bin
```

The program switches the terminal into raw mode, hides the cursor, and starts
rendering the fractal immediately. It redraws the frame roughly every 20 ms
and polls for keypresses between frames.

### Requirements

- A **terminal that supports ANSI truecolor (24-bit color)**. Most modern
  terminals do (e.g. GNOME Terminal, Konsole, iTerm2, Windows Terminal, and
  `tmux`/`screen` with the right settings). If colors look wrong or the
  gradient is missing, your terminal is falling back to 8/16-color mode.
- A terminal at least **80 columns × 40 rows** for the full view.

### Automated testing

Because the program reads keys in raw mode, you can drive it to quit cleanly
by piping a `q`:

```bash
printf 'q' | ./mandelbrot.bin
```

The `q` is read as the quit key, the program restores the terminal, clears
the screen, and exits with status `0`. This is handy for smoke tests and CI.

---

## 4. Controls

Keys are **case-insensitive** (lowercase input is uppercased internally), so
`W` and `w` both work. The `+` key and the `=` key (shift-`+`) both zoom in.

| Key | Action |
|-----|--------|
| `+` / `=` | **Zoom in** — halve the scale (down to a minimum of `1`) |
| `-` | **Zoom out** — double the scale (up to a maximum of `1.0`) |
| `w` | **Pan up** — move the imaginary center up by `8 × scale` |
| `s` | **Pan down** — move the imaginary center down by `8 × scale` |
| `a` | **Pan left** — move the real center left by `4 × scale` |
| `d` | **Pan right** — move the real center right by `4 × scale` |
| `i` | **Increase** max iterations by 10 |
| `o` | **Decrease** max iterations by 10 |
| `r` | **Reset** the viewport to the default view |
| `q` | **Quit** — restore the terminal and exit |

A heads-up display (HUD) below the fractal shows the current center
(`re`/`im`), the zoom scale, and the max-iteration count, plus a reminder of
the key bindings.

---

## 5. Fixed-Point Math

VagaForth has **no native floating-point arithmetic**, so the explorer uses
**signed 30.34 fixed-point**: every real number is stored as an integer that
represents the value scaled by `2^30`.

```
value = int × 2^30
```

So the integer `1073741824` means `1.0`, and `-536870912` means `-0.5`. The
"30.34" name reflects the layout: 30 bits of integer part and 34 bits of
fractional part (the fractional bits are the low 30 bits of the scaled
integer, with the sign in the top bit).

### Why Forth needs it

Forth's native arithmetic is integer-only. To do smooth, continuous math like
the Mandelbrot iteration, we pick a fixed scaling factor (`2^30`) and do all
arithmetic on scaled integers. This keeps everything fast and deterministic
— no float library required.

### `fxmul` — fixed-point multiply

Multiplying two scaled values naively over-scales the result:

```
(a × 2^30) × (b × 2^30) = a × b × 2^60
```

To recover `a × b × 2^30`, we shift right by 30 bits:

```
fxmul(a, b) = (a × b) >> 30
```

The tricky part is **sign handling**. Forth's `rshift` is a *logical*
(unsigned) shift, so a negative operand would corrupt the result. `fxmul`
therefore:

1. Records the sign of each operand.
2. Takes the absolute value of both.
3. Multiplies and shifts right by 30.
4. Re-applies the sign (XOR of the two signs) with `negate` if the result
   should be negative.

### `fxdiv` — fixed-point divide

Division is the inverse: to compute `a / b` in fixed point, we scale the
numerator up first:

```
fxdiv(a, b) = (a × 2^30) / b
```

The same sign-handling dance applies: take absolute values, do the integer
division, then re-apply the sign.

### The Mandelbrot iteration in fixed point

The Mandelbrot set is the set of complex `c` for which the recurrence

```
z₀ = 0
zₙ₊₁ = zₙ² + c
```

never diverges. In practice we iterate and bail out as soon as
`|z|² > 4`, because once the magnitude exceeds 2 it can never return.

Splitting `z = zre + i·zim` and `c = cre + i·cim`, one iteration is:

```
zre' = zre² − zim² + cre
zim' = 2·zre·zim + cim
```

and the bailout test is:

```
zre² + zim² > 4
```

In the code, `FX-4` is the fixed-point constant for `4.0`
(`4294967296 = 4 × 2^30`), and the squares are computed with `fxmul`:

```forth
zre @ zre @ fxmul zre2 !      \ zre²
zim @ zim @ fxmul zim2 !      \ zim²
zre2 @ zim2 @ + FX-4 > if     \ |z|² > 4  → escaped
    ...
else
    zre @ zim @ fxmul zreim ! \ zre·zim
    zre2 @ zim2 @ - cre @ + zre !   \ zre' = zre² − zim² + cre
    zreim @ 2 * cim @ + zim !       \ zim' = 2·zre·zim + cim
    ...
then
```

If a point reaches `max-iter` without escaping, it is treated as being
**inside** the set and rendered black. Otherwise the iteration count maps to
a color.

---

## 6. How It Works Under the Hood

### Raw-mode terminal

The program takes over the terminal so it can read single keypresses without
waiting for Enter and render without line buffering:

- **`sys-ioctl`** with `TCGETS`/`TCSETS` (21505/21506) reads and writes the
  terminal's termios structure. It clears the `ICANON` and `ECHO` bits
  (the `2` and `8` flags) to disable canonical (line) mode and echo.
- **`sys-fcntl`** with `F_GETFL`/`F_SETFL` (3/4) sets the `O_NONBLOCK` flag
  (2048) on stdin, so `poll-key` never blocks waiting for input.
- **`sys-nanosleep`** (via the `msleep` word) paces the render loop to ~20 ms
  per frame, keeping CPU use reasonable.
- On exit, `raw-mode-off` restores the original termios and file flags and
  re-shows the cursor.

### ANSI truecolor gradient

Each pixel's iteration count is mapped to a color through an **HSV-style
gradient**. The count is scaled into a 0–1535 hue range, split into six
sectors, and interpolated between the primary colors (red → yellow → green →
cyan → blue → magenta → red). The result is emitted as a 24-bit truecolor
SGR sequence:

```
ESC[38;2;R;G;Bm
```

followed by the block character `█` (ASCII 219) to fill the cell. Points
inside the set (that never escaped) are rendered black.

### `save-app` — the standalone ELF

At the end of the file:

```forth
save-app mandelbrot-main mandelbrot.bin
```

`save-app` snapshots the compiled Forth image — including the `mandelbrot-main`
word and all the words it calls — and wraps it in a native ELF executable.
The result is a self-contained binary that runs the program directly, with no
interpreter or source file needed at runtime. That's why the build command
produces a runnable `mandelbrot.bin` in the workspace root.

---

## Quick Reference

| Item | Value |
|------|-------|
| Source | `examples/mandelbrot.fs` |
| Build | `./vagaforth_new.bin < examples/mandelbrot.fs` |
| Output | `mandelbrot.bin` (workspace root) |
| Run | `./mandelbrot.bin` |
| Test | `printf 'q' \| ./mandelbrot.bin` |
| Grid | 80 × 40 |
| Fixed point | signed 30.34 (`value = int × 2^30`) |
| Color | ANSI truecolor, HSV-style gradient |
| Default view | center `(-0.5, 0)`, scale `0.05`, `100` iterations |
