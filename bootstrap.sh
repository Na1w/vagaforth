#!/usr/bin/env bash
#
# bootstrap.sh - Build the host compiler, cross-compile the target kernel,
# and run the bootstrap source to verify self-hosted compilation.
#
# This script is the entry point for the build/bootstrap verification flow:
#   1. Build the host compiler (`make`).
#   2. Cross-compile the target kernel (`./vagaforth kernel/kernel.fs`).
#   3. Feed the bootstrap source into the target binary under a 10s timeout.
#
# Exit status:
#   0  - Bootstrap completed successfully.
#   1  - Any step failed, timed out, or exited non-zero.

set -euo pipefail

echo 'Building host compiler...'
make

echo 'Cross-compiling target kernel...'
./vagaforth kernel/kernel.fs

echo 'Running bootstrap source...'
printf '%s\n' "$(cat bootstrap.fs)" | timeout 10 ./vagaforth.bin > bootstrap.out

echo 'Bootstrap completed successfully. Output saved to bootstrap.out'
