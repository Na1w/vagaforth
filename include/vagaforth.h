#ifndef VAGAFORTH_H
#define VAGAFORTH_H

#include <stdint.h>
#include <stddef.h>

// Grundläggande typer (Cell size = machine word)
typedef intptr_t cell_t;
typedef uintptr_t ucell_t;

// Konstanter
#define STACK_SIZE 256
#define DICT_SIZE  (16 * 1024 * 1024) // 16 MB ordbok

// Globala strukturer (deklareras i main.c)
extern cell_t data_stack[STACK_SIZE];
extern cell_t *dsp; // Data Stack Pointer

extern cell_t return_stack[STACK_SIZE];
extern cell_t *rsp; // Return Stack Pointer

extern uint8_t *dictionary; // Minne för ordbok och kod
extern uint8_t *here;       // Pekare till nästa lediga byte
extern cell_t latest;       // Offset/Pekare till senaste ordet (0 om tomt)
extern int state;           // 0 = Interpret, 1 = Compile
extern cell_t *ip;          // Instruction Pointer (för Forth-kod)

// Funktionstyp för primitiver
typedef void (*code_t)(void);

// Helper för att läsa/skriva celler i minnet
#define CELL_SIZE sizeof(cell_t)

#define FLAG_IMMEDIATE 0x80
#define FLAG_HIDDEN    0x40
#define MASK_LENGTH    0x1F

#endif // VAGAFORTH_H
