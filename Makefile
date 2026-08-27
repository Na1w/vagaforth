CC = gcc
CFLAGS = -Wall -Wextra -std=c99 -Iinclude -g
LDFLAGS = -ldl

SRC = src/main.c
OBJ = $(SRC:.c=.o)
HOST_BIN = vagaforth
TARGET_BIN = vagaforth.bin
SELFHOST_BIN = vagaforth_new.bin
GAME_BIN = guess_game.bin
DUNGEON_BIN = dungeon.bin

CORE_SRCS = $(wildcard core/*.fs)
KERNEL_SRCS = kernel/kernel.fs $(CORE_SRCS)

all: $(HOST_BIN) $(TARGET_BIN) $(SELFHOST_BIN) $(GAME_BIN) $(DUNGEON_BIN)

$(HOST_BIN): $(OBJ)
	$(CC) $(OBJ) -o $(HOST_BIN) $(LDFLAGS)

%.o: %.c include/vagaforth.h
	$(CC) $(CFLAGS) -c $< -o $@

$(TARGET_BIN): $(HOST_BIN) $(KERNEL_SRCS)
	./$(HOST_BIN) kernel/kernel.fs

$(SELFHOST_BIN): $(TARGET_BIN) kernel/selfhost.fs kernel/kernel_self.fs
	./$(TARGET_BIN) < kernel/selfhost.fs

$(GAME_BIN): $(TARGET_BIN) examples/guess_game.fs
	./$(TARGET_BIN) < examples/guess_game.fs

$(DUNGEON_BIN): $(TARGET_BIN) examples/dungeon.fs
	./$(TARGET_BIN) < examples/dungeon.fs

test: all
	./tests/run_all.sh
	./tests/diff_test.sh
	./tests/diff_dot_dotquote.sh
	python3 tests/pty_interactive_test.py

clean:
	rm -f $(OBJ) $(HOST_BIN) $(TARGET_BIN) $(SELFHOST_BIN) $(GAME_BIN) $(DUNGEON_BIN) hello.bin tests/stage_*.txt src/*.o

.PHONY: all test clean
