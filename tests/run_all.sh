#!/usr/bin/env bash
# =============================================================================
#  tests/run_all.sh  --  Vagaforth regression test harness  (t-e003)
#
#  Purpose
#  --------
#  End-to-end regression tests for the Vagaforth self-hosting Forth system.
#
#  The harness:
#    1. Builds the host compiler with `make`            (target: ./vagaforth)
#    2. Cross-compiles the target binary with
#         ./vagaforth kernel/kernel.fs   ->  vagaforth.bin
#    3. Performs ELF validity & execution-safety checks on vagaforth.bin:
#         * `file`       confirms it is an ELF64 x86-64 executable
#         * `readelf -h` confirms ELF64 class, x86-64 machine, and a sane
#                         entry point within the expected [ELF-ORIGIN, ...)
#                         architectural layout (ELF-ORIGIN == 0x400000)
#         * non-empty, reasonably-sized binary
#    4. Feeds a battery of programs to ./vagaforth.bin via stdin, runs each
#       under `timeout` (so a hung kernel can never wedge the test run), and
#       asserts the expected output is *contained* in stdout (substring match,
#       per the t-e002 spec) plus that the process exit code is 0.
#
#  Usage
#  -----
#     ./tests/run_all.sh    # build host, rebuild target, run the full battery
#
#  Exit status: 0 if every test AND every validity check passed; 1 otherwise.
#
#  Each test case reports, on a consistent format line:
#     [PASS] <name> - <description>
#     [FAIL] <name> - <description>
#     expected substring: <escaped>
#     actual output:      <escaped>
#
#  A "battery summary" (including the ELF validity checks) is printed at the end.
# =============================================================================
set -u                                          # fail on undefined vars (not -e: errors handled per-test)
cd "$(dirname "$0")/.."                          # run from repo root

BIN_HOST=./vagaforth
BIN_TARGET=./vagaforth.bin
KERNEL=kernel/kernel.fs

# Expected architectural layout of the generated ELF (see core/elf.fs).
ELF_ORIGIN=0x400000                            # p_vaddr / load base of the image
ELF_ORIGIN_DEC=$((0x400000))                   # decimal form of the same constant

# Guards against a hung kernel during any phase (build, smoke, or per-test).
BUILD_TIMEOUT=120
RUN_TIMEOUT=10

PASS_COUNT=0
FAIL_COUNT=0
FAILED_NAMES=()

# Validity-check tally (reported in the final summary).
CHECK_PASS=0
CHECK_FAIL=0
CHECK_FAILED_NAMES=()

# -----------------------------------------------------------------------------
#  Utilities
# -----------------------------------------------------------------------------

# Escape control chars for safe, unambiguous diff display.
escape() {
    printf '%s' "$1" | sed -n l
}

# fail_build <msg>  -- abort the harness with a hard error.
fail_build() {
    echo "ERROR: $1" >&2
    exit 1
}

# hex_to_dec <0x...|number>  -- normalize a hex/decimal integer to decimal.
hex_to_dec() {
    printf '%d' "$1"
}

# run_validity <name> <desc> <result(0/1)> <detail>
#   Records the outcome of one validity check; FAILURES are non-fatal here
#   (they are counted and surfaced in the final summary) so that a broken
#   ELF does not silently skip the entire battery.
run_validity() {
    local name="$1" desc="$2" ok="$3" detail="$4"
    if [[ "$ok" -eq 0 ]]; then
        echo "[PASS] (check) $name - $desc"
        CHECK_PASS=$((CHECK_PASS + 1))
    else
        echo "[FAIL] (check) $name - $desc"
        echo "       detail: $detail"
        CHECK_FAIL=$((CHECK_FAIL + 1))
        CHECK_FAILED_NAMES+=("$name")
    fi
}

# -----------------------------------------------------------------------------
#  Assertion helpers
#
#  Two styles are provided:
#    run_test            -- exact-match on stdout+stderr
#    run_test_contains   -- substring match: PASS iff expected_out is present
#                           somewhere in the captured stdout+stderr stream.
#
#  Both feed the program to ./vagaforth.bin via stdin, run it under `timeout`
#  (so an infinite loop / blocked KEY can never hang the harness), and check
#  that the process exits with code 0.
#
#  Signature:
#    run_test_contains <name> <description> <input> <expected_substring>
# -----------------------------------------------------------------------------
run_test() {
    local name="$1" desc="$2" input="$3" expected_out="$4"
    local actual_out actual_exit

    actual_out="$(printf '%s\n' "$input" | timeout "$RUN_TIMEOUT" "$BIN_TARGET" 2>&1)"
    actual_exit=$?

    if [[ "$actual_exit" -eq 0 && "$actual_out" == "$expected_out" ]]; then
        echo "[PASS] $name - $desc"
        PASS_COUNT=$((PASS_COUNT + 1))
    else
        echo "[FAIL] $name - $desc"
        echo "       expected exit=0 output=[$(escape "$expected_out")]"
        echo "       actual   exit=$actual_exit output=[$(escape "$actual_out")]"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        FAILED_NAMES+=("$name")
    fi
}

run_test_contains() {
    local name="$1" desc="$2" input="$3" needle="$4"
    local actual_out actual_exit

    actual_out="$(printf '%s\n' "$input" | timeout "$RUN_TIMEOUT" "$BIN_TARGET" 2>&1)"
    actual_exit=$?

    if [[ "$actual_exit" -eq 0 && "$actual_out" == *"$needle"* ]]; then
        echo "[PASS] $name - $desc"
        PASS_COUNT=$((PASS_COUNT + 1))
    else
        echo "[FAIL] $name - $desc"
        echo "       expected substring=[$(escape "$needle")] exit=0"
        echo "       actual      exit=$actual_exit output=[$(escape "$actual_out")]"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        FAILED_NAMES+=("$name")
    fi
}

# -----------------------------------------------------------------------------
#  Phase 1: Build the host compiler
# -----------------------------------------------------------------------------
echo "== [phase] Building host compiler (make) =="
if ! make >/dev/null 2>&1; then
    fail_build "make failed while building ./vagaforth (host compiler)."
fi
[[ -x "$BIN_HOST" ]] || fail_build "'$BIN_HOST' not produced by make."

# -----------------------------------------------------------------------------
#  Phase 2: Rebuild the target binary
# -----------------------------------------------------------------------------
echo "== [phase] Cross-compiling target: $BIN_HOST $KERNEL =="
if ! timeout "$BUILD_TIMEOUT" "$BIN_HOST" "$KERNEL" >/dev/null 2>&1; then
    fail_build "'$BIN_HOST $KERNEL' failed to produce $BIN_TARGET."
fi
[[ -x "$BIN_TARGET" ]] || fail_build "'$BIN_TARGET' not produced."

# -----------------------------------------------------------------------------
#  Phase 2b: ELF validity & execution-safety checks (t-e003)
# -----------------------------------------------------------------------------
echo "== [phase] Validating ELF: $BIN_TARGET =="

# -- 3. Non-empty, reasonable size -------------------------------------------
BIN_SIZE=0
if [[ -f "$BIN_TARGET" ]]; then
    BIN_SIZE=$(stat -c '%s' "$BIN_TARGET" 2>/dev/null || stat -f '%z' "$BIN_TARGET" 2>/dev/null || echo 0)
fi
MIN_SIZE=$((1024))        # well below the ~6KB real binary; catches empty/truncated output
MAX_SIZE=$((16 * 1024 * 1024))  # 16MB; guards against a runaway/bloated image
if [[ "$BIN_SIZE" -ge "$MIN_SIZE" && "$BIN_SIZE" -le "$MAX_SIZE" ]]; then
    run_validity "size" "binary size $BIN_SIZE bytes within [$MIN_SIZE, $MAX_SIZE]" 0 \
        "size=$BIN_SIZE"
else
    run_validity "size" "binary size $BIN_SIZE bytes within [$MIN_SIZE, $MAX_SIZE]" 1 \
        "size=$BIN_SIZE (out of range)"
fi

# -- 1. Valid ELF64 via `file` ------------------------------------------------
FILE_ID=$(file "$BIN_TARGET" 2>&1)
if [[ "$FILE_ID" == *"ELF 64-bit"* && "$FILE_ID" == *"x86-64"* ]]; then
    run_validity "file-elf64" "file reports an ELF64 x86-64 executable" 0 \
        "file: $FILE_ID"
else
    run_validity "file-elf64" "file reports an ELF64 x86-64 executable" 1 \
        "file: $FILE_ID"
fi

# -- 1b. Valid ELF64 via `readelf -h` -----------------------------------------
READELF_H=$(readelf -h "$BIN_TARGET" 2>&1)
if [[ "$READELF_H" == *"ELF64"* && "$READELF_H" == *"X86-64"* && \
      "$READELF_H" == *"EXEC"* ]]; then
    run_validity "readelf-header" "readelf -h confirms ELF64, X86-64, EXEC" 0 \
        "readelf -h ok"
else
    run_validity "readelf-header" "readelf -h confirms ELF64, X86-64, EXEC" 1 \
        "readelf -h: $READELF_H"
fi

# -- 2. Entry point consistent with architectural layout ----------------------
#   Parse "Entry point address: 0x401818" from readelf -h.
ENTRY_RAW=$(printf '%s\n' "$READELF_H" | awk -F: '/Entry point/{print $2; exit}' | tr -d ' ')
ENTRY_DEC=$(hex_to_dec "${ENTRY_RAW:-0}")
#   Sanity: entry point must be within [ELF_ORIGIN, ELF_ORIGIN + file_size),
#   i.e. it must point into the image loaded at 0x400000 (p_vaddr).
ENTRY_MAX=$((ELF_ORIGIN_DEC + BIN_SIZE))
if [[ -n "$ENTRY_RAW" && "$ENTRY_DEC" -ge "$ELF_ORIGIN_DEC" && "$ENTRY_DEC" -lt "$ENTRY_MAX" ]]; then
    run_validity "entry-point" "entry point 0x$(printf '%x' "$ENTRY_DEC") within [0x400000, 0x$(printf '%x' "$ENTRY_MAX"))" 0 \
        "entry=$ENTRY_RAW"
else
    run_validity "entry-point" "entry point within [0x400000, 0x$(printf '%x' "$ENTRY_MAX"))" 1 \
        "entry=$ENTRY_RAW (expected within 0x400000..0x$(printf '%x' "$ENTRY_MAX"))"
fi

# Smoke test: make sure the binary responds at all (guards against a silent hang).
if ! printf '1 1 + . cr\n' | timeout "$RUN_TIMEOUT" "$BIN_TARGET" >/dev/null 2>&1; then
    fail_build "'$BIN_TARGET' did not execute a trivial '1 1 + . cr' program."
fi

# =============================================================================
#  Phase 3: Regression test battery  (t-e002 / t-e003)
# =============================================================================
echo "== [phase] Running regression battery =="

# --- 1. Basic arithmetic ------------------------------------------------
#   5 3 + .  ->  8
run_test_contains \
    "arithmetic-add" \
    "'5 3 + .' prints 8" \
    '5 3 + . cr' \
    '8'

# --- 2. Negative arithmetic ---------------------------------------------
#   1 2 - .  ->  -1
run_test_contains \
    "arithmetic-sub-neg" \
    "'1 2 - .' prints -1" \
    '1 2 - . cr' \
    '-1'

# --- 3. Stack operations: dup / swap ------------------------------------
#   Input: 10 dup . 20 swap . .
#   Trace:  push 10 -> [10]; dup -> [10,10]; '.'
#           prints 10 (pop) -> [10]
#           push 20 -> [10,20]; swap -> [20,10]; '.' prints 10 (top) -> [20]
#           '.' prints 20 (pop) -> []
#   Output: "101020" which contains both '10' and '20'.
#   (A trailing '.' is added to the spec's literal input '10 dup . 20 swap .'
#    so the value that swap moved to the bottom is also revealed; otherwise
#    '20' would never appear in stdout and the substring assertion would fail.)
run_test_contains \
    "stack-dup-swap" \
    "'10 dup . 20 swap . .' prints a stream containing 10 and 20 (dup+swap)" \
    '10 dup . 20 swap . . cr' \
    '10'
run_test_contains \
    "stack-dup-swap-20" \
    "second assertion: the 20 moved by 'swap' also appears" \
    '10 dup . 20 swap . . cr' \
    '20'

# --- 4. Formatting: cr and emit -----------------------------------------
#   Input:  cr 65 EMIT cr
#   'cr' emits a newline, 65 EMIT prints 'A', then another cr.
#   Raw output: "\nA\n"  (assert on 'A' and the newline).
run_test_contains \
    "format-cr-emit" \
    "'cr 65 EMIT cr' prints 'A' between two newlines" \
    'cr 65 EMIT cr' \
    'A'
#   Explicit newline assertion: output must contain a leading \n before 'A'
#   (the trailing \n is stripped by $(...) command substitution, so only the
#   leading newline can be asserted reliably here; the trailing one is covered
#   by the next test's trailing-cr check via a separator word).
run_test_contains \
    "format-newlines" \
    "'cr 65 EMIT cr' emits a leading newline before 'A'" \
    'cr 65 EMIT cr' \
    $'\nA'
#   Verify the trailing 'cr' also produced a newline by appending a visible
#   sentinel ('Z') after the program so the trailing newline is no longer at
#   the very end of stdout and therefore survives command substitution.
run_test_contains \
    "format-trailing-cr" \
    "'cr 65 EMIT cr Z' proves the trailing cr emitted a newline (before Z)" \
    'cr 65 EMIT cr 90 EMIT' \
    $'\nA\nZ'

# --- 5. User-defined words ----------------------------------------------
#   : sq dup * ; 5 sq .  ->  25
run_test_contains \
    "user-words" \
    "': sq dup * ; 5 sq .' prints 25" \
    ': sq dup * ; 5 sq . cr' \
    '25'

# --- 6. Control flow: begin/until counter loop --------------------------
#   : t 0 begin 1+ dup 5 > until . ; t   ->  prints the final value 6
run_test_contains \
    "control-begin-until" \
    "': t 0 begin 1+ dup 5 > until . ; t' prints 6 (final counter value)" \
    ': t 0 begin 1+ dup 5 > until . ; t cr' \
    '6'

#   Variant: a begin/until loop that prints a SEQUENCE of values (0..5)
#   to demonstrate sequence output as well.
run_test_contains \
    "control-begin-until-seq" \
    "': seq 0 begin dup . 1+ dup 6 > until drop ; seq' prints '0 1 2 3 4 5 6' (with spaces; . emits trailing space)" \
    ': seq 0 begin dup . 1+ dup 6 > until drop ; seq cr' \
    '0 1 2 3 4 5 6'

# --- 7. Self-compilation (compile-source at runtime) --------------------
#   s" : dbl dup + ;" compile-source 21 dbl .  ->  42
#   compile-source defines 'dbl' in the target dictionary at runtime, then
#   the newly-defined word is executed: 21 + 21 = 42.
run_test_contains \
    "self-compile" \
    "'s\" : dbl dup + ;\" compile-source 21 dbl .' prints 42" \
    's" : dbl dup + ;" compile-source 21 dbl . cr' \
    '42'

#   Alternative: evaluate-style self-compilation of a colon word.
run_test_contains \
    "self-compile-evaluate" \
    "'s\" : add3 3 + ;\" evaluate 10 add3 .' prints 13" \
    's" : add3 3 + ;" evaluate 10 add3 . cr' \
    '13'

# =============================================================================
#  Phase 4: Summary
# =============================================================================
echo ""
echo "=========================================================="
echo "  ELF validity checks: $CHECK_PASS passed, $CHECK_FAIL failed"
echo "  Battery results:     $PASS_COUNT passed, $FAIL_COUNT failed"
echo "=========================================================="

if [[ "$FAIL_COUNT" -gt 0 ]]; then
    echo "  Failed tests: ${FAILED_NAMES[*]}"
fi
if [[ "$CHECK_FAIL" -gt 0 ]]; then
    echo "  Failed checks: ${CHECK_FAILED_NAMES[*]}"
fi

if [[ "$FAIL_COUNT" -gt 0 || "$CHECK_FAIL" -gt 0 ]]; then
    echo ""
    echo "  OVERALL RESULT: FAIL"
    exit 1
fi
echo "  OVERALL RESULT: PASS"
exit 0
