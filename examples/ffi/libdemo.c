#include <stdint.h>
#include <stddef.h>

// 1. Basic Arithmetic & Factorial
int64_t demo_add(int64_t a, int64_t b) {
    return a + b;
}

int64_t demo_fact(int64_t n) {
    if (n <= 1) return 1;
    return n * demo_fact(n - 1);
}

// 2. Multi-Argument Calculator (6 arguments)
int64_t demo_sum6(int64_t a, int64_t b, int64_t c, int64_t d, int64_t e, int64_t f) {
    return a + b + c + d + e + f;
}

// 3. String Processing
int64_t demo_strlen(const char *s) {
    int64_t len = 0;
    while (s && s[len]) len++;
    return len;
}

void demo_reverse(char *s) {
    if (!s) return;
    int64_t len = demo_strlen(s);
    for (int64_t i = 0, j = len - 1; i < j; i++, j--) {
        char tmp = s[i];
        s[i] = s[j];
        s[j] = tmp;
    }
}

// 4. Algorithms: DJB2 String Hash
uint64_t demo_hash_djb2(const char *s) {
    uint64_t hash = 5381;
    int c;
    while ((c = (unsigned char)*s++)) {
        hash = ((hash << 5) + hash) + c; /* hash * 33 + c */
    }
    return hash;
}

// 5. Array Processing
int64_t demo_sum_array(const int64_t *arr, int64_t count) {
    int64_t sum = 0;
    for (int64_t i = 0; i < count; i++) {
        sum += arr[i];
    }
    return sum;
}
