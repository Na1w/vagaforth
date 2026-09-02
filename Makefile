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
BF_BIN = bf_compiler.bin
BF_HELLO_BIN = hello_bf.bin
PLATFORMER_BIN = platformer.bin

CORE_SRCS = $(wildcard core/*.fs)
KERNEL_SRCS = kernel/kernel.fs $(CORE_SRCS)

all: $(HOST_BIN) $(TARGET_BIN) $(SELFHOST_BIN) $(GAME_BIN) $(DUNGEON_BIN) $(BF_BIN) $(BF_HELLO_BIN) $(PLATFORMER_BIN)

$(HOST_BIN): $(OBJ)
	$(CC) $(OBJ) -o $(HOST_BIN) $(LDFLAGS)

%.o: %.c include/vagaforth.h
	$(CC) $(CFLAGS) -c $< -o $@

$(TARGET_BIN): $(HOST_BIN) $(KERNEL_SRCS)
	./$(HOST_BIN) kernel/kernel.fs
	cp $(SELFHOST_BIN) $(TARGET_BIN)

$(SELFHOST_BIN): $(TARGET_BIN) kernel/selfhost.fs
	./$(TARGET_BIN) < kernel/selfhost.fs

$(GAME_BIN): $(SELFHOST_BIN) examples/guess_game.fs
	./$(SELFHOST_BIN) < examples/guess_game.fs

$(DUNGEON_BIN): $(SELFHOST_BIN) examples/dungeon.fs
	./$(SELFHOST_BIN) < examples/dungeon.fs

$(BF_BIN): $(SELFHOST_BIN) examples/bf/bf_compiler.fs
	./$(SELFHOST_BIN) < examples/bf/bf_compiler.fs

$(BF_HELLO_BIN): $(SELFHOST_BIN) examples/bf/compile_hello_bf.fs
	./$(SELFHOST_BIN) < examples/bf/compile_hello_bf.fs

$(PLATFORMER_BIN): $(SELFHOST_BIN) examples/platformer.fs
	./$(SELFHOST_BIN) < examples/platformer.fs

test: all
	./tests/run_all.sh
	./tests/diff_test.sh
	./tests/diff_dot_dotquote.sh
	python3 tests/pty_interactive_test.py
	./$(SELFHOST_BIN) < examples/c_inline/demo_inline_c.fs

demo-c: $(SELFHOST_BIN)
	./$(SELFHOST_BIN) < examples/c_inline/demo_inline_c.fs

clean:
	rm -f $(OBJ) $(HOST_BIN) $(TARGET_BIN) $(SELFHOST_BIN) $(GAME_BIN) $(DUNGEON_BIN) $(BF_BIN) $(BF_HELLO_BIN) $(PLATFORMER_BIN) hello.bin tests/stage_*.txt src/*.o

.PHONY: all test clean demo-c
