#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <ctype.h>
#include <unistd.h>
#include <setjmp.h>
#include <dlfcn.h>
#include "vagaforth.h"

jmp_buf error_jmp;

cell_t data_stack[STACK_SIZE];
cell_t *dsp = data_stack;

cell_t return_stack[STACK_SIZE];
cell_t *rsp = return_stack;

uint8_t *dictionary;
uint8_t *here;
cell_t latest = 0;
int state = 0;
cell_t *ip = NULL;
code_t *running_word_cfa = NULL;

FILE *input_fp = NULL;
char input_buffer[1024];
char *input_ptr = NULL;
int base = 10;
int interactive_mode = 1;

static uint8_t last_found_flags = 0;

typedef struct {
    FILE *fp;
    char buffer[1024];
    long offset;
} InputState;

#define INPUT_STACK_DEPTH 16
static InputState input_stack[INPUT_STACK_DEPTH];
static int input_depth = 0;


static char* get_word(void) {
    if (!input_ptr) return NULL;
    while (isspace(*input_ptr)) input_ptr++;
    if (*input_ptr == 0) return NULL;
    char *start = input_ptr;
    while (*input_ptr && !isspace(*input_ptr)) input_ptr++;
    if (*input_ptr) *input_ptr++ = 0;
    return start;
}

static char* parse_until(char delim) {
    if (!input_ptr) return NULL;
    if (delim == '"' && *input_ptr == ' ') input_ptr++;
    
    char *start = input_ptr;
    while (*input_ptr && *input_ptr != delim) input_ptr++;
    if (*input_ptr == delim) *input_ptr++ = 0;
    return start;
}

static char* find_name_by_cfa(code_t *cfa) {
    cell_t current = latest;
    while (current != 0) {
        uint8_t *ptr = (uint8_t*)current;
        ptr += CELL_SIZE;
        uint8_t len_byte = *ptr++;
        uint8_t len = len_byte & MASK_LENGTH;
        uint8_t *name_ptr = ptr;
        ptr += len;
        while ((uintptr_t)ptr % CELL_SIZE != 0) ptr++;
        
        if ((code_t*)ptr == cfa) {
            static char buf[32];
            int n = len > 31 ? 31 : len;
            memcpy(buf, name_ptr, n);
            buf[n] = 0;
            return buf;
        }
        current = *(cell_t*)current;
    }
    return "unknown";
}


void push(cell_t val) {
    if (dsp >= data_stack + STACK_SIZE) {
        printf("Error: Stack Overflow!\n");
        longjmp(error_jmp, 1);
    }
    *dsp++ = val;
}

cell_t pop(void) {
    if (dsp <= data_stack) {
        printf("Error: Stack Underflow in word '%s'!\n", 
               find_name_by_cfa(running_word_cfa));
        longjmp(error_jmp, 1);
    }
    return *(--dsp);
}

void r_push(cell_t val) {
    if (rsp >= return_stack + STACK_SIZE) {
        printf("Error: Return Stack Overflow!\n");
        longjmp(error_jmp, 1);
    }
    *rsp++ = val;
}

cell_t r_pop(void) {
    if (rsp <= return_stack) {
        printf("Error: Return Stack Underflow!\n");
        longjmp(error_jmp, 1);
    }
    return *(--rsp);
}


void align_here(void) {
    while ((uintptr_t)here % CELL_SIZE != 0) *here++ = 0;
}

void comma(cell_t val) {
    *(cell_t*)here = val;
    here += CELL_SIZE;
}

static void create_header(const char *name, uint8_t flags) {
    align_here();
    *(cell_t*)here = latest;
    cell_t current_addr = (cell_t)here;
    here += CELL_SIZE;
    latest = current_addr;

    uint8_t len = strlen(name);
    *here++ = len | flags;
    memcpy(here, name, len);
    here += len;
    align_here();
}

static void create_primitive(const char *name, code_t func) {
    create_header(name, 0);
    *(code_t*)here = func;
    here += sizeof(code_t);
}

static void create_immediate(const char *name, code_t func) {
    create_header(name, FLAG_IMMEDIATE);
    *(code_t*)here = func;
    here += sizeof(code_t);
}

code_t* find_word_with_flags(const char *name) {
    cell_t current = latest;
    while (current != 0) {
        uint8_t *ptr = (uint8_t*)current;
        ptr += CELL_SIZE;
        uint8_t len_byte = *ptr++;
        uint8_t len = len_byte & MASK_LENGTH;
        
        if (len == strlen(name) && strncasecmp((char*)ptr, name, len) == 0) {
            last_found_flags = len_byte;
            ptr += len;
            while ((uintptr_t)ptr % CELL_SIZE != 0) ptr++;
            return (code_t*)ptr;
        }
        current = *(cell_t*)current;
    }
    return NULL;
}


static void prim_docol(void) {
    if (ip != NULL) r_push((cell_t)ip);
    ip = (cell_t*)(running_word_cfa + 1);
}

static void prim_exit(void) {
    if (rsp == return_stack) ip = NULL;
    else ip = (cell_t*)r_pop();
}

static void prim_lit(void) { push(*ip++); }

static void prim_fetch(void) { cell_t addr = pop(); push(*(cell_t*)addr); }
static void prim_store(void) { cell_t addr = pop(); cell_t val = pop(); *(cell_t*)addr = val; }
static void prim_plus_store(void) { cell_t addr = pop(); cell_t val = pop(); *(cell_t*)addr += val; }
static void prim_cfetch(void) { cell_t addr = pop(); push(*(uint8_t*)addr); }
static void prim_cstore(void) { cell_t addr = pop(); uint8_t val = (uint8_t)pop(); *(uint8_t*)addr = val; }

static void prim_and(void) { cell_t b = pop(); push(pop() & b); }
static void prim_or(void) { cell_t b = pop(); push(pop() | b); }
static void prim_xor(void) { cell_t b = pop(); push(pop() ^ b); }
static void prim_invert(void) { push(~pop()); }
static void prim_negate(void) { push(-pop()); }

static void prim_branch(void) {
    cell_t offset = *ip;
    ip = (cell_t*)offset;
}

static void prim_zbranch(void) {
    cell_t dest = *ip++;
    if (pop() == 0) ip = (cell_t*)dest;
}

static void prim_add(void) { push(pop() + pop()); }
static void prim_sub(void) { cell_t b = pop(); push(pop() - b); }
static void prim_mul(void) { push(pop() * pop()); }
static void prim_dup(void) { cell_t a = pop(); push(a); push(a); }
static void prim_2dup(void) { cell_t b = pop(); cell_t a = pop(); push(a); push(b); push(a); push(b); }
static void prim_drop(void) { pop(); }
static void prim_2drop(void) { pop(); pop(); }
static void prim_swap(void) { cell_t a = pop(); cell_t b = pop(); push(a); push(b); }
static void prim_nip(void) { cell_t a = pop(); pop(); push(a); }
static void prim_tuck(void) { cell_t a = pop(); cell_t b = pop(); push(a); push(b); push(a); }
static void prim_over(void) { cell_t b = pop(); cell_t a = pop(); push(a); push(b); push(a); }
static void prim_rot(void) {
    cell_t c = pop(); cell_t b = pop(); cell_t a = pop();
    push(b); push(c); push(a);
}

static void prim_lshift(void) { cell_t n = pop(); push(pop() << n); }
static void prim_rshift(void) { cell_t n = pop(); push((ucell_t)pop() >> n); }
static void prim_div(void) { cell_t b = pop(); push(pop() / b); }
static void prim_mod(void) { cell_t b = pop(); push(pop() % b); }

static void prim_eq(void) { push(pop() == pop() ? -1 : 0); }
static void prim_lt(void) { cell_t b = pop(); push(pop() < b ? -1 : 0); }
static void prim_gt(void) { cell_t b = pop(); push(pop() > b ? -1 : 0); }

static void prim_pick(void) {
    cell_t n = pop();
    if (dsp - n - 1 < data_stack) {
        printf("Error: Stack Underflow on PICK\n");
        longjmp(error_jmp, 1);
    }
    push(*(dsp - n - 1));
}

static void prim_true(void) { push(-1); }
static void prim_false(void) { push(0); }

static void prim_to_r(void) { r_push(pop()); }
static void prim_r_from(void) { push(r_pop()); }
static void prim_r_fetch(void) { push(*(rsp-1)); }

static void prim_fill(void) {
    uint8_t c = (uint8_t)pop();
    cell_t len = pop();
    void *addr = (void*)pop();
    memset(addr, c, len);
}

static void prim_cmove(void) {
    cell_t len = pop();
    void *dest = (void*)pop();
    void *src = (void*)pop();
    memmove(dest, src, len);
}

static void prim_dovar(void) {
    cell_t *base = (cell_t*)running_word_cfa;
    push((cell_t)(base + 2));
}

static void prim_docon(void) {
    cell_t *base = (cell_t*)running_word_cfa;
    push(*(base + 1));
}

static void prim_dodoes(void) {
    cell_t *base = (cell_t*)running_word_cfa;
    cell_t target_ip = *(base + 1);
    cell_t pfa = (cell_t)(base + 2);
    
    push(pfa);
    if (ip != NULL) r_push((cell_t)ip);
    ip = (cell_t*)target_ip;
}

static void prim_dot(void) {
    long val = (long)pop();
    if (base == 16) printf("%lx ", val);
    else printf("%ld ", val);
    fflush(stdout);
}

static void prim_emit(void) { printf("%c", (char)pop()); fflush(stdout); }
static void prim_cr(void) { printf("\n"); fflush(stdout); }
static void prim_space(void) { printf(" "); fflush(stdout); }

static void prim_bye(void) { exit(0); }

static void prim_words(void) {
    cell_t current = latest;
    while (current) {
        uint8_t *ptr = (uint8_t*)current + CELL_SIZE;
        uint8_t len = *ptr & MASK_LENGTH;
        ptr++;
        printf("%.*s ", len, ptr);
        current = *(cell_t*)current;
    }
    printf("\n");
}

static void prim_dot_s(void) {
    printf("<%ld> ", (long)(dsp - data_stack));
    for (cell_t *p = data_stack; p < dsp; p++) printf("%ld ", (long)*p);
    printf("\n");
}

static void prim_hex(void) { base = 16; }
static void prim_decimal(void) { base = 10; }

static void prim_ccomma(void) { *here++ = (uint8_t)pop(); }
static void prim_here(void) { push((cell_t)here); }
static void prim_allot(void) { here += pop(); align_here(); }
static void prim_comma(void) { comma(pop()); }

static void prim_type(void) {
    cell_t len = pop();
    cell_t addr = pop();
    fwrite((void*)addr, 1, len, stdout);
    fflush(stdout);
}

static void prim_backslash(void) {
    if (input_ptr) while (*input_ptr && *input_ptr != '\n') input_ptr++;
}

static void prim_paren(void) { parse_until(')'); }

static void prim_dot_quote_run(void) {
    cell_t len = *ip++;
    char *str = (char*)ip;
    fwrite(str, 1, len, stdout);
    fflush(stdout);
    cell_t cells = (len + 1 + sizeof(cell_t) - 1) / sizeof(cell_t);
    ip += cells;
}

static void prim_dot_quote(void) {
    char *str = parse_until('"');
    if (!str) return;
    cell_t len = strlen(str);
    
    if (state == 1) {
        code_t *cfa = find_word_with_flags("(.\")");
        comma((cell_t)cfa);
        comma(len);
        memcpy(here, str, len);
        here += len;
        align_here();
    } else {
        printf("%s", str);
        fflush(stdout);
    }
}

static void prim_s_quote_run(void) {
    cell_t len = *ip++;
    char *str = (char*)ip;
    push((cell_t)str);
    push(len);
    cell_t cells = (len + 1 + sizeof(cell_t) - 1) / sizeof(cell_t);
    ip += cells;
}

static void prim_s_quote(void) {
    char *str = parse_until('"');
    if (!str) return;
    cell_t len = strlen(str);
    
    if (state == 1) {
        code_t *cfa = find_word_with_flags("(s\")");
        comma((cell_t)cfa);
        comma(len);
        memcpy(here, str, len);
        here += len;
        *here++ = 0;
        align_here();
    } else {
        static char buf[1024];
        if (len >= 1024) len = 1023;
        memcpy(buf, str, len);
        buf[len] = 0;
        push((cell_t)buf);
        push(len);
    }
}

static void prim_tick(void) {
    char *name = get_word();
    if (!name) return;
    code_t *cfa = find_word_with_flags(name);
    if (cfa) push((cell_t)cfa);
    else printf("Error: Word '%s' not found\n", name);
}

static void prim_bracket_tick(void) {
    char *name = get_word();
    if (!name) return;
    code_t *cfa = find_word_with_flags(name);
    if (cfa) {
        code_t *lit_cfa = find_word_with_flags("LIT");
        comma((cell_t)lit_cfa);
        comma((cell_t)cfa);
    } else {
        printf("Error: Word '%s' not found\n", name);
    }
}

static void prim_parse_name(void) {
    char *str = get_word();
    if (str) {
        push((cell_t)str);
        push(strlen(str));
    } else {
        push(0);
        push(0);
    }
}

static void prim_target_find(void) {
    cell_t *target_latest_ptr = (cell_t*)pop();
    cell_t target_base_val = pop();
    cell_t len = pop();
    char *name = (char*)pop();
    cell_t current_virt = *target_latest_ptr;

    printf("TF: %.*s\n", (int)len, name); // DEBUG

    cell_t target_limit_host = target_base_val + 102400; // 100KB

    while (current_virt >= 0x400000 && current_virt < 0x400000 + 102400) {
        uint8_t *host_ptr = (uint8_t*)(current_virt - 0x400000 + target_base_val);
        
        if ((cell_t)host_ptr < target_base_val || (cell_t)host_ptr >= target_limit_host) break;

        uint8_t target_len = host_ptr[8];
        
        if ((target_len & 0x1F) == len) {
            if (strncasecmp((char*)(host_ptr + 9), name, len) == 0) {
                cell_t xt_host = (cell_t)(host_ptr + 9 + len);
                xt_host = (xt_host + 7) & ~7;
                cell_t xt_virt = xt_host - target_base_val + 0x400000;
                
                push(xt_virt);
                push(0); // flags
                push(-1); // true
                return;
            }
        }
        current_virt = *(cell_t*)host_ptr;
        if (current_virt == 0) break;
    }
    push(0); // false
}

static void prim_immediate(void) {
    if (latest == 0) return;
    uint8_t *ptr = (uint8_t*)latest;
    ptr += CELL_SIZE;
    *ptr ^= FLAG_IMMEDIATE;
}

static void prim_dlopen(void) {
    int flags = pop();
    char *name = (char*)pop();
    push((cell_t)dlopen(name, flags));
}

static void prim_dlsym(void) {
    char *name = (char*)pop();
    void *handle = (void*)pop();
    push((cell_t)dlsym(handle, name));
}

static void prim_call0(void) {
    typedef cell_t (*f0)(void);
    push(((f0)pop())());
}

static void prim_call1(void) {
    typedef cell_t (*f1)(cell_t);
    f1 func = (f1)pop();
    cell_t a = pop();
    push(func(a));
}

static void prim_call2(void) {
    typedef cell_t (*f2)(cell_t, cell_t);
    f2 func = (f2)pop();
    cell_t b = pop();
    cell_t a = pop();
    push(func(a, b));
}

static void prim_call3(void) {
    typedef cell_t (*f3)(cell_t, cell_t, cell_t);
    f3 func = (f3)pop();
    cell_t c = pop();
    cell_t b = pop();
    cell_t a = pop();
    push(func(a, b, c));
}

static void prim_call4(void) {
    typedef cell_t (*f4)(cell_t, cell_t, cell_t, cell_t);
    f4 func = (f4)pop();
    cell_t d = pop();
    cell_t c = pop();
    cell_t b = pop();
    cell_t a = pop();
    push(func(a, b, c, d));
}

static void prim_colon(void) {
    char *name = get_word();
    if (!name) return;
    create_header(name, 0);
    *(code_t*)here = prim_docol;
    here += sizeof(code_t);
    state = 1;
}

static void prim_semicolon(void) {
    code_t *exit_cfa = find_word_with_flags("EXIT");
    comma((cell_t)exit_cfa);
    state = 0;
}

static void prim_if(void) {
    code_t *zbranch = find_word_with_flags("0BRANCH");
    comma((cell_t)zbranch);
    push((cell_t)here);
    comma(0);
}

static void prim_then(void) {
    cell_t hole_addr = pop();
    *(cell_t*)hole_addr = (cell_t)here;
}

static void prim_else(void) {
    code_t *branch = find_word_with_flags("BRANCH");
    comma((cell_t)branch);
    cell_t if_hole = pop();
    push((cell_t)here);
    comma(0);
    *(cell_t*)if_hole = (cell_t)here;
}

static void prim_begin(void) { push((cell_t)here); }

static void prim_until(void) {
    code_t *zbranch = find_word_with_flags("0BRANCH");
    comma((cell_t)zbranch);
    comma(pop());
}

static void prim_create(void) {
    char *name = get_word();
    if (!name) return;
    create_header(name, 0);
    *(code_t*)here = prim_dovar;
    here += sizeof(code_t);
    *(cell_t*)here = 0;
    here += sizeof(cell_t);
}

static void prim_variable(void) {
    prim_create();
    comma(0);
}

static void prim_constant(void) {
    char *name = get_word();
    if (!name) return;
    create_header(name, 0);
    *(code_t*)here = prim_docon;
    here += sizeof(code_t);
    comma(pop());
}

static void prim_does_helper(void) {
    cell_t current = latest;
    uint8_t *ptr = (uint8_t*)current;
    ptr += CELL_SIZE;
    uint8_t len = *ptr & MASK_LENGTH;
    ptr += 1 + len;
    while ((uintptr_t)ptr % CELL_SIZE != 0) ptr++;
    
    code_t *child_cfa = (code_t*)ptr;
    *child_cfa = prim_dodoes;
    cell_t *child_param = (cell_t*)(child_cfa + 1);
    *child_param = (cell_t)ip;
    prim_exit();
}

static void prim_include(void) {
    char *filename = get_word();
    if (!filename) {
        printf("Error: include requires a filename\n");
        return;
    }
    
    FILE *fp = fopen(filename, "r");
    if (!fp) {
        printf("Error: Could not open file '%s'\n", filename);
        return;
    }
    
    if (input_depth >= INPUT_STACK_DEPTH) {
        printf("Error: Input stack overflow\n");
        fclose(fp);
        return;
    }
    
    input_stack[input_depth].fp = input_fp;
    memcpy(input_stack[input_depth].buffer, input_buffer, sizeof(input_buffer));
    input_stack[input_depth].offset = input_ptr - input_buffer;
    input_depth++;
    
    input_fp = fp;
    input_buffer[0] = 0;
    input_ptr = input_buffer;
}


void init_memory(void) {
    dictionary = mmap(NULL, DICT_SIZE, PROT_READ | PROT_WRITE | PROT_EXEC,
                      MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
    if (dictionary == MAP_FAILED) exit(1);
    here = dictionary;
    latest = 0;
    printf("VagaForth initialized.\n");
}

void prim_code_runner_stub(void) __attribute__((naked));
void prim_code_runner_stub(void) {
    __asm__ volatile (
        "push %rbx \n\t"           // Save callee-saved RBX
        "mov dsp(%rip), %rdi \n\t" // Load DSP to RDI
        
        "mov running_word_cfa(%rip), %rax \n\t"
        "add $8, %rax \n\t"        // Skip CFA (8 bytes) to find code start
        
        "call *%rax \n\t"          // Call native code
        
        "mov %rax, dsp(%rip) \n\t" // Save updated DSP
        "pop %rbx \n\t"            // Restore RBX
        "ret \n\t"
    );
}

void init_primitives(void) {
    create_primitive("EXIT", prim_exit);
    create_primitive("LIT", prim_lit);
    create_primitive("BRANCH", prim_branch);
    create_primitive("0BRANCH", prim_zbranch);
    
    create_primitive("+", prim_add);
    create_primitive("-", prim_sub);
    create_primitive("*", prim_mul);
    create_primitive("/", prim_div);
    create_primitive("mod", prim_mod);
    create_primitive("dup", prim_dup);
    create_primitive("2dup", prim_2dup);
    create_primitive("drop", prim_drop);
    create_primitive("2drop", prim_2drop);
    create_primitive("swap", prim_swap);
    create_primitive("nip", prim_nip);
    create_primitive("tuck", prim_tuck);
    create_primitive("over", prim_over);
    create_primitive("rot", prim_rot);
    create_primitive("pick", prim_pick);
    create_primitive("lshift", prim_lshift);
    create_primitive("rshift", prim_rshift);
    
    create_primitive("=", prim_eq);
    create_primitive("<", prim_lt);
    create_primitive(">", prim_gt);
    create_primitive("true", prim_true);
    create_primitive("false", prim_false);
    
    create_primitive(">r", prim_to_r);
    create_primitive("r>", prim_r_from);
    create_primitive("r@", prim_r_fetch);
    
    create_primitive("@", prim_fetch);
    create_primitive("!", prim_store);
    create_primitive("+!", prim_plus_store);
    create_primitive("c@", prim_cfetch);
    create_primitive("c!", prim_cstore);
    create_primitive("fill", prim_fill);
    create_primitive("cmove", prim_cmove);
    
    create_primitive("create", prim_create);
    create_primitive("variable", prim_variable);
    create_primitive("constant", prim_constant);
    
    create_primitive(".", prim_dot);
    create_primitive("emit", prim_emit);
    create_primitive("cr", prim_cr);
    create_primitive("space", prim_space);
    create_primitive("type", prim_type);
    create_primitive(".s", prim_dot_s);
    create_primitive("words", prim_words);
    create_primitive("bye", prim_bye);
    
    create_primitive("(here)", prim_here);
    create_primitive("(allot)", prim_allot);
    create_primitive("(,)", prim_comma);
    create_primitive("(c,)", prim_ccomma);
    
    create_primitive("and", prim_and);
    create_primitive("or", prim_or);
    create_primitive("xor", prim_xor);
    create_primitive("invert", prim_invert);
    create_primitive("negate", prim_negate);
    
    create_primitive("hex", prim_hex);
    create_primitive("decimal", prim_decimal);
    
    create_primitive("'", prim_tick);
    create_primitive("parse-name", prim_parse_name);
    create_primitive("target-find", prim_target_find);
    create_primitive("immediate", prim_immediate);
    create_primitive("include", prim_include);
    
    create_primitive("dlopen", prim_dlopen);
    create_primitive("dlsym", prim_dlsym);
    create_primitive("call0", prim_call0);
    create_primitive("call1", prim_call1);
    create_primitive("call2", prim_call2);
    create_primitive("call3", prim_call3);
    create_primitive("call4", prim_call4);
    
    create_immediate("\\", prim_backslash);
    create_immediate("(", prim_paren);
    create_immediate(".\"", prim_dot_quote);
    create_immediate("s\"", prim_s_quote);
    create_immediate("[']", prim_bracket_tick);
    create_immediate(":", prim_colon);
    create_immediate(";", prim_semicolon);
    create_immediate("IF", prim_if);
    create_immediate("THEN", prim_then);
    create_immediate("ELSE", prim_else);
    create_immediate("BEGIN", prim_begin);
    create_immediate("UNTIL", prim_until);
    
    create_primitive("(.\")", prim_dot_quote_run);
    create_primitive("(s\")", prim_s_quote_run);
    create_primitive("(does>)", prim_does_helper);
    create_primitive("(code)", prim_code_runner_stub);
}

static void run_vm(void) {
    while (ip != NULL) {
        code_t *cfa = (code_t*)*ip++;
        running_word_cfa = cfa;
        (*cfa)();
    }
}

void run_repl(void) {
    printf("VagaForth v0.8\n");
    code_t *lit_cfa = find_word_with_flags("LIT");
    
    input_ptr = input_buffer;
    input_buffer[0] = 0;

    if (setjmp(error_jmp) != 0) {
        if (!interactive_mode) {
            fprintf(stderr, "Fatal error. Exiting.\n");
            exit(1);
        }
        dsp = data_stack;
        rsp = return_stack;
        state = 0;
        ip = NULL;
        input_depth = 0;
        if (input_fp != stdin) {
            if (input_fp) fclose(input_fp);
            input_fp = stdin;
        }
        printf("State reset.\n");
        input_buffer[0] = 0;
        input_ptr = input_buffer;
    }

    while (1) {
        char *token = get_word();
        
        if (token == NULL) {
            if (interactive_mode && input_fp == stdin && ip == NULL) {
                if (dsp > data_stack) printf("[ %ld ]", (long)*(dsp-1));
                else printf("[]");
                printf(state ? " compiled > " : " > ");
            }
            
            memset(input_buffer, 0, sizeof(input_buffer));
            
            if (fgets(input_buffer, sizeof(input_buffer), input_fp) == NULL) {
                if (input_depth > 0) {
                    fclose(input_fp);
                    input_depth--;
                    input_fp = input_stack[input_depth].fp;
                    memcpy(input_buffer, input_stack[input_depth].buffer, sizeof(input_buffer));
                    input_ptr = input_buffer + input_stack[input_depth].offset;
                    continue;
                }
                break;
            }
            input_ptr = input_buffer;
            continue;
        }

        code_t *cfa = find_word_with_flags(token);
        
        if (cfa) {
            if (state == 1 && !(last_found_flags & FLAG_IMMEDIATE)) {
                comma((cell_t)cfa);
            } else {
                running_word_cfa = cfa;
                (*cfa)();
                run_vm();
            }
        } else {
            char *end;
            long val = strtol(token, &end, base);
            if (*end == 0) {
                if (state == 1) {
                    comma((cell_t)lit_cfa);
                    comma((cell_t)val);
                } else {
                    push((cell_t)val);
                }
            } else {
                printf(" ? %s\n", token);
            }
        }
    }
}

int main(int argc, char **argv) {
    init_memory();
    init_primitives();
    
    if (argc > 1) {
        input_fp = fopen(argv[1], "r");
        if (!input_fp) {
            fprintf(stderr, "Error: Could not open file '%s'\n", argv[1]);
            return 1;
        }
        interactive_mode = 0;
    } else {
        input_fp = stdin;
        interactive_mode = isatty(STDIN_FILENO);
    }

    run_repl();
    
    if (input_fp != stdin) fclose(input_fp);
    return 0;
}