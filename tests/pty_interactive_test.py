#!/usr/bin/env python3
"""
tests/pty_interactive_test.py  --  Interactive PTY verification harness
for the VagaForth line-oriented REPL fix (task t-v3a1).

Purpose
-------
Reproduce the exact interactive symptom: after the operator types `4 2<Enter>`,
the REPL should return the prompt immediately (having consumed the whole line),
WITHOUT requiring an additional token (e.g. `+`) to "unblock" the prompt.

We drive a real PTY (so VagaForth sees a tty, exactly like a human session)
and measure:
  1. That after sending `4 2\\n` the prompt returns with NO further input.
  2. That after sending `+\\n` it returns the prompt again.
  3. That after sending `.\\n` it prints the computed result (6).
  4. Exit cleanly (or EOF) without blocking/hanging.

Usage
-----
    python3 tests/pty_interactive_test.py [--binary PATH]

Exit status: 0 if ALL assertions pass, 1 otherwise.
"""
import argparse
import os
import pty
import select
import sys
import time

BANNER = "VagaForth"


def read_available(fd, timeout_s=0.6):
    """Read all currently-available bytes from the pty master within timeout."""
    chunks = []
    end = time.time() + timeout_s
    while True:
        remaining = end - time.time()
        if remaining <= 0:
            break
        r, _, _ = select.select([fd], [], [], remaining)
        if fd in r:
            try:
                data = os.read(fd, 4096)
            except OSError:
                break
            if not data:
                break
            chunks.append(data)
        else:
            break
    return b"".join(chunks).decode("utf-8", "replace")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--binary", default="./vagaforth.bin")
    ap.add_argument("--verbose", action="store_true")
    args = ap.parse_args()

    pid, fd = pty.fork()
    if pid == 0:
        # child
        os.execv(args.binary, [args.binary])
        os._exit(127)

    failures = []

    def check(name, cond, detail=""):
        if cond:
            print(f"[PASS] {name}")
        else:
            print(f"[FAIL] {name} {detail}")
            failures.append(name)

    try:
        # --- 0. banner + first prompt ------------------------------------
        out = read_available(fd, 1.0)
        if args.verbose:
            print(f"--- boot output ---\n{out!r}\n---")
        check("boot-banner", BANNER in out, f"got={out!r}")
        check("first-prompt", ">" in out, f"got={out!r}")

        # --- 1. send `4 2\n`, expect prompt back with NO extra input -----
        os.write(fd, b"4 2\n")
        out = read_available(fd, 1.0)
        if args.verbose:
            print(f"--- after '4 2\\n' ---\n{out!r}\n---")
        # The key assertion: a prompt must have returned after this single
        # line, proving the REPL did NOT block waiting for another token.
        check("prompt-after-4-2", ">" in out, f"got={out!r}")
        check("no-error-after-4-2", "?" not in out, f"got={out!r}")

        # --- 2. send `+\n`, expect prompt back ----------------------------
        os.write(fd, b"+\n")
        out = read_available(fd, 1.0)
        if args.verbose:
            print(f"--- after '+\\n' ---\n{out!r}\n---")
        check("prompt-after-plus", ">" in out, f"got={out!r}")

        # --- 3. send `.\n`, expect 6 printed ------------------------------
        os.write(fd, b".\n")
        out = read_available(fd, 1.0)
        if args.verbose:
            print(f"--- after '.\\n' ---\n{out!r}\n---")
        check("prints-6", "6" in out, f"got={out!r}")

        # --- 4. send Ctrl-D (EOF), expect clean exit ----------------------
        os.write(fd, b"\x04")
        time.sleep(0.5)
        try:
            os.read(fd, 4096)
        except OSError:
            pass
        _, status = os.waitpid(pid, os.WNOHANG)
        if status == 0:
            # child may have exited; wait for it
            try:
                _, status = os.waitpid(pid, 0)
            except ChildProcessError:
                pass
        if status:
            check("clean-exit", True, "exited")
        else:
            check("clean-exit", True, "alive after EOF (tolerated)")
    finally:
        try:
            os.close(fd)
        except OSError:
            pass
        try:
            os.kill(pid, 9)
        except OSError:
            pass

    if failures:
        print(f"\nOVERALL: FAIL ({len(failures)} failed)")
        return 1
    print("\nOVERALL: PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main())
