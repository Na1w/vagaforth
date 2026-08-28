include addons/inline_c.fs

cr
." ========================================================" cr
."   VagaForth Inline C Compiler Addon Demo" cr
."   (With printf, puts, malloc, free & full stdio)" cr
." ========================================================" cr
cr

." [1] Compiling basic C arithmetic functions..." cr
s" int add(int a, int b) { return a + b; }" c-compile
s" int mul(int a, int b) { return a * b; }" c-compile

."     25 17 add = " 25 17 add . cr
."     12 12 mul = " 12 12 mul . cr
cr

." [2] Compiling recursive C functions (Factorial & Fibonacci)..." cr
s" int fact(int n) { if (n <= 1) return 1; return n * fact(n - 1); }" c-compile
s" int fib(int n) { if (n <= 1) return n; return fib(n - 1) + fib(n - 2); }" c-compile

."     6 fact = " 6 fact . cr
."     10 fib = " 10 fib . cr
."     20 fib = " 20 fib . cr
cr

." [3] Compiling iterative algorithms with while loops and modulo..." cr
s" int sum_range(int n) { int total = 0; int i = 1; while (i <= n) { total = total + i; i = i + 1; } return total; }" c-compile
s" int is_prime(int n) { if (n <= 1) return 0; int d = 2; while (d * d <= n) { if (n % d == 0) return 0; d = d + 1; } return 1; }" c-compile

."     sum_range(100) = " 100 sum_range . cr
."     is_prime(97)   = " 97 is_prime . cr
."     is_prime(100)  = " 100 is_prime . cr
cr

." [4] Compiling Collatz conjecture steps calculator in C..." cr
s" int collatz_steps(int n) { int steps = 0; while (n > 1) { if (n % 2 == 0) { n = n / 2; } else { n = 3 * n + 1; } steps = steps + 1; } return steps; }" c-compile

."     collatz_steps(27) = " 27 collatz_steps . cr
cr

." [5] Testing C stdio runtime (printf & puts with string literals)..." cr
s" void test_io() { puts('  [puts] Hello from C runtime!'); printf('  [printf] Number: %d, Hex: 0x%x, Math: %d * %d = %d\n', 42, 255, 6, 7, 6 * 7); }" c-compile
test_io
cr

." [6] Testing dynamic heap memory allocation (malloc & free)..." cr
s" void test_mem() { int p1 = malloc(64); int p2 = malloc(128); printf('  [malloc] Allocated 64 bytes at: 0x%x\n', p1); printf('  [malloc] Allocated 128 bytes at: 0x%x\n', p2); free(p1); puts('  [free] Freed p1 (64 bytes)'); int p3 = malloc(64); printf('  [malloc] Reallocated 64 bytes (reused chunk): 0x%x\n', p3); free(p2); free(p3); puts('  [free] All dynamic heap blocks freed successfully!'); }" c-compile
test_mem
cr

." [7] Testing pointer dereferencing (*p = val, *p, peek, poke) & Forth sharing..." cr
s" int test_ptr_math() { int p = malloc(32); *p = 100; int a = *p; poke(p, 250); int b = peek(p); free(p); return a + b; }" c-compile
."     test_ptr_math (100 + 250) = " test_ptr_math . cr

s" int create_c_buffer(int size) { return malloc(size); }" c-compile
s" void verify_c_buffer(int ptr) { printf('  [C verify] Pointer 0x%x holds value: %d\n', ptr, *ptr); free(ptr); }" c-compile

."   -> Forth allocates buffer in C: "
64 create_c_buffer constant forth-heap-ptr
." ptr = " forth-heap-ptr . cr
."   -> Forth writes 77777 to ptr with '!':" cr
77777 forth-heap-ptr !
."   -> Forth verifies with '@': " forth-heap-ptr @ . cr
."   -> C verifies and frees the exact same buffer:" cr
forth-heap-ptr verify_c_buffer
cr

." Inline C compilation & execution completed successfully!" cr
