#!/usr/bin/env bash
# =============================================================================
#  tests/test_save_elf_at.sh  --  Entry-word executable test for save-elf-at
#
#  Purpose
#  --------
#  Prove that the target-runtime `save-elf-at` word creates a functioning,
#  standalone ELF64 executable with a CUSTOM entry point (a user-defined
#  Forth word), NOT the normal START/REPL entry.
#
#  The test:
#    1. Builds the host compiler (`make`).
#    2. (Re)generates the target binary `vagaforth.bin` from `kernel/kernel.fs`
#       using the host compiler.
#    3. Feeds the running `vagaforth.bin` (via stdin) a session that:
#         - defines a word `hello` that emits char 42 ('*') then a newline;
#         - calls `save-elf-at` to emit a NEW standalone ELF64 (`hello.bin`)
#           whose e_entry points at the `hello` word body behind a stack-init
#           trampoline.
#    4. Runs `readelf -h hello.bin` and extracts/logs the `Entry point address`.
#    5. Executes `./hello.bin` under `timeout` and asserts:
#         - stdout is exactly `*` followed by a newline (0x2A 0x0A);
#         - exit code is 0;
#         - `hello.bin` is a valid ELF64 executable.
#
#  All stdout/stderr of the build+emission phases are captured into
#  tests/stage_stdout.txt and tests/stage_stderr.txt for auditing.
#
#  Usage:
#     ./tests/test_save_elf_at.sh
#
#  Exit status: 0 if every assertion passes, 1 otherwise.
# =============================================================================
set -u                       # fail on undefined vars (errors handled per-step)
cd "$(dirname "$0")/.."      # run from repo root

BIN_HOST=./vagaforth
BIN_TARGET=./vagaforth.bin
KERNEL=kernel/kernel.fs
OUT_BIN=./hello.bin

BUILD_TIMEOUT=120
RUN_TIMEOUT=10

STAGE_OUT=tests/stage_stdout.txt
STAGE_ERR=tests/stage_stderr.txt

PASS_COUNT=0
FAIL_COUNT=0

# ---------------------------------------------------------------------------
#  Utilities
# ---------------------------------------------------------------------------

# pass <desc>
pass() { echo "[PASS] $1"; PASS_COUNT=$((PASS_COUNT + 1)); }

# fail <desc>  (non-fatal: continue, but report at end)
fail() {
    echo "[FAIL] $1"
    FAIL_COUNT=$((FAIL_COUNT + 1))
}

# fail_build <msg>  -- hard abort
fail_build() {
    echo "ERROR: $1" >&2
    exit 1
}

# ---------------------------------------------------------------------------
#  Phase 1: Build the host compiler (make)
# ---------------------------------------------------------------------------
echo "== [phase] Building host compiler (make) =="
if ! make >"$STAGE_OUT" 2>"$STAGE_ERR"; then
    fail_build "make failed while building ./vagaforth (host compiler)."
fi
[[ -x "$BIN_HOST" ]] || fail_build "'$BIN_HOST' not produced by make."
pass "make produced ./vagaforth"

# ---------------------------------------------------------------------------
#  Phase 2: Rebuild the target binary (vagaforth.bin)
# ---------------------------------------------------------------------------
echo "== [phase] Cross-compiling target: $BIN_HOST $KERNEL =="
if ! timeout "$BUILD_TIMEOUT" "$BIN_HOST" "$KERNEL" >>"$STAGE_OUT" 2>>"$STAGE_ERR"; then
    fail_build "'$BIN_HOST $KERNEL' failed to produce $BIN_TARGET."
fi
[[ -x "$BIN_TARGET" ]] || fail_build "'$BIN_TARGET' not produced."
pass "cross-compiled $BIN_TARGET"

# ---------------------------------------------------------------------------
#  Phase 3: Feed save-elf-at session via stdin to the running target
# ---------------------------------------------------------------------------
echo "== [phase] Running save-elf-at session =="

# IMPORTANT (case sensitivity): the target Forth defines the print-char word
# as `EMIT` (UPPERCASE). Lowercase `emit` is NOT defined in the kernel and would
# abort. The word therefore uses `42 EMIT 10 EMIT` to print '*' then newline.
#
# Forth session:
#   : hello 42 EMIT 10 EMIT ;      -- define word: emit '*', newline
#   s" hello" s" hello.bin" save-elf-at
#                                    -- find 'hello', emit a standalone ELF
#                                       whose e_entry runs it.
SESSION=$': hello 42 EMIT 10 EMIT ;\ns" hello" s" hello.bin" save-elf-at\n'

if ! printf '%s' "$SESSION" | timeout "$RUN_TIMEOUT" "$BIN_TARGET" \
        >>"$STAGE_OUT" 2>>"$STAGE_ERR"; then
    fail_build "save-elf-at session did not run to completion (see $STAGE_ERR)."
fi
pass "save-elf-at session ran"

[[ -f "$OUT_BIN" ]] || fail_build "'$OUT_BIN' was not produced by save-elf-at."
pass "hello.bin produced by save-elf-at"

# ---------------------------------------------------------------------------
#  Phase 4: ELF analysis (readelf -h) -- log the entry point
# ---------------------------------------------------------------------------
echo "== [phase] ELF analysis: readelf -h $OUT_BIN =="
READELF_H=$(readelf -h "$OUT_BIN" 2>&1)
printf '%s\n' "$READELF_H" >>"$STAGE_OUT"

ENTRY_RAW=$(printf '%s\n' "$READELF_H" | awk -F: '/Entry point/{print $2; exit}' | tr -d ' ')
printf 'Entry point address: %s\n' "${ENTRY_RAW:-<missing>}"
echo "  (logged to $STAGE_OUT)"

# --- valid ELF64 executable ---
if [[ "$READELF_H" == *"ELF64"* && "$READELF_H" == *"X86-64"* && \
      "$READELF_H" == *"EXEC"* ]]; then
    pass "hello.bin is a valid ELF64 x86-64 EXEC executable"
else
    fail "hello.bin is NOT a valid ELF64 EXEC executable"
    printf '%s\n' "$READELF_H"
fi

# e_entry present and in-range (>= ELF_ORIGIN 0x400000)
if [[ -n "$ENTRY_RAW" ]]; then
    ENTRY_DEC=$((ENTRY_RAW))
    if [[ "$ENTRY_DEC" -ge $((0x400000)) ]]; then
        pass "entry point ${ENTRY_RAW} within loaded image (>= 0x400000)"
    else
        fail "entry point ${ENTRY_RAW} below ELF origin 0x400000"
    fi
else
    fail "could not extract entry point from readelf -h"
fi

# ---------------------------------------------------------------------------
#  Phase 5: Functional test -- run ./hello.bin under timeout
# ---------------------------------------------------------------------------
echo "== [phase] Functional test: timeout $RUN_TIMEOUT $OUT_BIN =="

HELLO_OUT_FILE=tests/stage_hello_stdout.txt
HELLO_ERR_FILE=tests/stage_hello_stderr.txt

timeout "$RUN_TIMEOUT" "$OUT_BIN" >"$HELLO_OUT_FILE" 2>"$HELLO_ERR_FILE"
HELLO_EXIT=$?

# exit code must be 0
if [[ "$HELLO_EXIT" -eq 0 ]]; then
    pass "hello.bin exited with code 0"
else
    fail "hello.bin exited with code $HELLO_EXIT (expected 0)"
fi

echo "hello.bin stdout bytes:" >>"$STAGE_OUT"
od -c "$HELLO_OUT_FILE" >>"$STAGE_OUT"

# stdout must be exactly '*' followed by a newline (0x2A 0x0A). Compare raw
# bytes with od so trailing newlines are preserved exactly.
HELLO_BYTES=$(od -An -tx1 "$HELLO_OUT_FILE" | tr -d ' \n')
if [[ "$HELLO_BYTES" == "2a0a" ]]; then
    pass "hello.bin stdout is exactly '*\\n' (0x2A 0x0A)"
else
    fail "hello.bin stdout bytes [${HELLO_BYTES:-<empty>}] (expected '2a0a')"
fi

# ---------------------------------------------------------------------------
#  Summary
# ---------------------------------------------------------------------------
echo ""
echo "============================================================="
echo " save-elf-at entry-word executable test summary"
echo "   PASS: $PASS_COUNT   FAIL: $FAIL_COUNT"
echo "   entry point: ${ENTRY_RAW:-<none>}"
echo "   audit logs: $STAGE_OUT / $STAGE_ERR"
echo "============================================================="

if [[ "$FAIL_COUNT" -gt 0 ]]; then
    exit 1
fi
exit 0
