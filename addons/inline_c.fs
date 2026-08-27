create c-src-buf 65536 allot drop
create c-str-pool 65536 allot drop
variable c-str-pool-ptr

create c-tok-str 64 allot drop
create c-fn-name-buf 64 allot drop
create c-var-name-buf 64 allot
create c-call-name-buf 64 allot drop
variable c-call-name-len drop
create c-vars-name 1024 allot drop
create c-vars-len 128 allot drop
create c-vars-off 128 allot drop
create c-funcs-name 1024 allot drop
create c-funcs-len 64 allot drop
create c-funcs-xt 64 allot drop
create c-funcs-nargs 64 allot drop

variable c-src-ptr
variable c-src-end
variable c-tok-type
variable c-tok-val
variable c-tok-len
variable c-fn-name-len
variable c-var-name-len
variable c-num-vars
variable c-frame-size
variable c-num-funcs
variable c-cur-fn-xt
variable c-cur-fn-nargs

variable parse-expr-xt
variable parse-stmt-xt
variable fn-xt

: 4comma
    dup c,
    dup 8 rshift c,
    dup 16 rshift c,
    24 rshift c,
    ;

: 8comma
    dup 4comma
    32 rshift 4comma
    ;

variable c-putchar-xt
HERE c-putchar-xt !
85 c, 72 c, 137 c, 229 c, 72 c, 131 c, 236 c, 16 c, 72 c, 137 c, 125 c, 248 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 72 c, 141 c, 117 c, 248 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 137 c, 236 c, 93 c, 195 c,

variable c-getchar-xt
HERE c-getchar-xt !
85 c, 72 c, 137 c, 229 c, 72 c, 131 c, 236 c, 16 c, 72 c, 199 c, 69 c, 248 c, 0 c, 0 c, 0 c, 0 c, 72 c, 199 c, 192 c, 0 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 0 c, 0 c, 0 c, 0 c, 72 c, 141 c, 117 c, 248 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 133 c, 192 c, 126 c, 7 c, 72 c, 15 c, 182 c, 69 c, 248 c, 235 c, 7 c, 72 c, 199 c, 192 c, 255 c, 255 c, 255 c, 255 c, 72 c, 137 c, 236 c, 93 c, 195 c,

variable c-puts-xt
HERE c-puts-xt !
85 c, 72 c, 137 c, 229 c, 65 c, 84 c, 73 c, 137 c, 252 c, 72 c, 49 c, 201 c, 65 c, 128 c, 60 c, 12 c, 0 c, 116 c, 5 c, 72 c, 255 c, 193 c, 235 c, 244 c, 72 c, 137 c, 202 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 76 c, 137 c, 230 c, 15 c, 5 c, 72 c, 131 c, 236 c, 16 c, 198 c, 4 c, 36 c, 10 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 72 c, 137 c, 230 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 131 c, 196 c, 16 c, 72 c, 49 c, 192 c, 65 c, 92 c, 72 c, 137 c, 236 c, 93 c, 195 c,

variable c-gets-xt
HERE c-gets-xt !
85 c, 72 c, 137 c, 229 c, 65 c, 84 c, 65 c, 85 c, 73 c, 137 c, 252 c, 77 c, 49 c, 237 c, 72 c, 131 c, 236 c, 16 c, 72 c, 199 c, 192 c, 0 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 0 c, 0 c, 0 c, 0 c, 72 c, 137 c, 230 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 133 c, 192 c, 126 c, 24 c, 138 c, 4 c, 36 c, 72 c, 131 c, 196 c, 16 c, 60 c, 10 c, 116 c, 34 c, 60 c, 13 c, 116 c, 206 c, 67 c, 136 c, 4 c, 44 c, 73 c, 255 c, 197 c, 235 c, 197 c, 72 c, 131 c, 196 c, 16 c, 77 c, 133 c, 237 c, 117 c, 12 c, 72 c, 49 c, 192 c, 65 c, 93 c, 65 c, 92 c, 72 c, 137 c, 236 c, 93 c, 195 c, 67 c, 198 c, 4 c, 44 c, 0 c, 76 c, 137 c, 224 c, 65 c, 93 c, 65 c, 92 c, 72 c, 137 c, 236 c, 93 c, 195 c,

variable c-printf-xt
HERE c-printf-xt !
85 c, 72 c, 137 c, 229 c, 83 c, 65 c, 84 c, 65 c, 85 c, 65 c, 86 c, 65 c, 87 c, 72 c, 129 c, 236 c, 128 c, 0 c, 0 c, 0 c, 73 c, 137 c, 252 c, 72 c, 137 c, 117 c, 176 c, 72 c, 137 c, 85 c, 184 c, 72 c, 137 c, 77 c, 192 c, 76 c, 137 c, 69 c, 200 c, 76 c, 137 c, 77 c, 208 c, 77 c, 49 c, 237 c, 73 c, 15 c, 182 c, 4 c, 36 c, 132 c, 192 c, 15 c, 132 c, 190 c, 1 c, 0 c, 0 c, 60 c, 37 c, 117 c, 54 c, 73 c, 255 c, 196 c, 73 c, 15 c, 182 c, 4 c, 36 c, 132 c, 192 c, 15 c, 132 c, 170 c, 1 c, 0 c, 0 c, 60 c, 37 c, 116 c, 34 c, 60 c, 99 c, 116 c, 72 c, 60 c, 115 c, 116 c, 121 c, 60 c, 100 c, 15 c, 132 c, 192 c, 0 c, 0 c, 0 c, 60 c, 105 c, 15 c, 132 c, 184 c, 0 c, 0 c, 0 c, 60 c, 120 c, 15 c, 132 c, 39 c, 1 c, 0 c, 0 c, 235 c, 0 c, 72 c, 131 c, 236 c, 16 c, 136 c, 4 c, 36 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 72 c, 137 c, 230 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 131 c, 196 c, 16 c, 73 c, 255 c, 196 c, 235 c, 143 c, 74 c, 139 c, 68 c, 237 c, 176 c, 73 c, 255 c, 197 c, 72 c, 131 c, 236 c, 16 c, 136 c, 4 c, 36 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 72 c, 137 c, 230 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 131 c, 196 c, 16 c, 73 c, 255 c, 196 c, 233 c, 90 c, 255 c, 255 c, 255 c, 78 c, 139 c, 116 c, 237 c, 176 c, 73 c, 255 c, 197 c, 77 c, 133 c, 246 c, 117 c, 8 c, 73 c, 255 c, 196 c, 233 c, 69 c, 255 c, 255 c, 255 c, 73 c, 15 c, 182 c, 6 c, 132 c, 192 c, 116 c, 42 c, 72 c, 131 c, 236 c, 16 c, 136 c, 4 c, 36 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 72 c, 137 c, 230 c, 72 c, 199 c, 194 c, 1 c, 0 c, 0 c, 0 c, 15 c, 5 c, 72 c, 131 c, 196 c, 16 c, 73 c, 255 c, 198 c, 235 c, 206 c, 73 c, 255 c, 196 c, 233 c, 11 c, 255 c, 255 c, 255 c, 74 c, 139 c, 68 c, 237 c, 176 c, 73 c, 255 c, 197 c, 72 c, 131 c, 236 c, 32 c, 76 c, 141 c, 116 c, 36 c, 30 c, 65 c, 198 c, 6 c, 0 c, 72 c, 133 c, 192 c, 121 c, 12 c, 72 c, 247 c, 216 c, 73 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 235 c, 3 c, 77 c, 49 c, 255 c, 72 c, 49 c, 210 c, 72 c, 199 c, 195 c, 10 c, 0 c, 0 c, 0 c, 72 c, 247 c, 243 c, 128 c, 194 c, 48 c, 73 c, 255 c, 206 c, 65 c, 136 c, 22 c, 72 c, 133 c, 192 c, 117 c, 229 c, 77 c, 133 c, 255 c, 116 c, 7 c, 73 c, 255 c, 206 c, 65 c, 198 c, 6 c, 45 c, 72 c, 141 c, 84 c, 36 c, 30 c, 76 c, 41 c, 242 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 76 c, 137 c, 246 c, 15 c, 5 c, 72 c, 131 c, 196 c, 32 c, 73 c, 255 c, 196 c, 233 c, 148 c, 254 c, 255 c, 255 c, 74 c, 139 c, 68 c, 237 c, 176 c, 73 c, 255 c, 197 c, 72 c, 131 c, 236 c, 32 c, 76 c, 141 c, 116 c, 36 c, 30 c, 65 c, 198 c, 6 c, 0 c, 72 c, 137 c, 194 c, 72 c, 131 c, 226 c, 15 c, 128 c, 250 c, 10 c, 124 c, 5 c, 128 c, 194 c, 87 c, 235 c, 3 c, 128 c, 194 c, 48 c, 73 c, 255 c, 206 c, 65 c, 136 c, 22 c, 72 c, 193 c, 232 c, 4 c, 72 c, 133 c, 192 c, 117 c, 221 c, 72 c, 141 c, 84 c, 36 c, 30 c, 76 c, 41 c, 242 c, 72 c, 199 c, 192 c, 1 c, 0 c, 0 c, 0 c, 72 c, 199 c, 199 c, 1 c, 0 c, 0 c, 0 c, 76 c, 137 c, 246 c, 15 c, 5 c, 72 c, 131 c, 196 c, 32 c, 73 c, 255 c, 196 c, 233 c, 53 c, 254 c, 255 c, 255 c, 72 c, 49 c, 192 c, 72 c, 129 c, 196 c, 128 c, 0 c, 0 c, 0 c, 65 c, 95 c, 65 c, 94 c, 65 c, 93 c, 65 c, 92 c, 91 c, 72 c, 137 c, 236 c, 93 c, 195 c,

: c-peek-ch
    c-src-ptr @ c-src-end @ < if
        c-src-ptr @ c@
    else
        0
    then
    ;

: c-get-ch
    c-peek-ch
    c-src-ptr @ 1+ c-src-ptr !
    ;

: c-skip-ws
    begin
        c-peek-ch dup 32 = swap dup 9 = swap dup 10 = swap 13 = + + + if
            c-get-ch drop
            0
        else
            1
        then
    until
    ;

: is-alpha
    dup 64 > over 91 < and
    swap dup 96 > over 123 < and
    swap 95 = + + if 1 else 0 then
    ;

: is-digit
    dup 47 > swap 58 < and if 1 else 0 then
    ;

: is-alnum
    dup is-alpha swap is-digit + if 1 else 0 then
    ;

variable eq-len2
variable eq-addr2
variable eq-len1
variable eq-addr1
variable str-i
variable str-match

: str-eq
    eq-len2 !
    eq-addr2 !
    eq-len1 !
    eq-addr1 !
    eq-len1 @ eq-len2 @ = 0= if
        0
    else
        1 str-match !
        0 str-i !
        begin
            str-i @ eq-len1 @ < if
                eq-addr1 @ str-i @ + c@
                eq-addr2 @ str-i @ + c@
                = 0= if
                    0 str-match !
                then
                str-i @ 1+ str-i !
                0
            else
                1
            then
        until
        str-match @
    then
    ;

create kw-int-str 105 c, 110 c, 116 c,
create kw-char-str 99 c, 104 c, 97 c, 114 c,
create kw-void-str 118 c, 111 c, 105 c, 100 c,
create kw-if-str 105 c, 102 c,
create kw-else-str 101 c, 108 c, 115 c, 101 c,
create kw-while-str 119 c, 104 c, 105 c, 108 c, 101 c,
create kw-return-str 114 c, 101 c, 116 c, 117 c, 114 c, 110 c,

: check-keyword
    c-tok-str c-tok-len @ kw-int-str 3 str-eq if
        10 c-tok-type !
    else c-tok-str c-tok-len @ kw-char-str 4 str-eq if
        10 c-tok-type !
    else c-tok-str c-tok-len @ kw-void-str 4 str-eq if
        10 c-tok-type !
    else c-tok-str c-tok-len @ kw-if-str 2 str-eq if
        11 c-tok-type !
    else c-tok-str c-tok-len @ kw-else-str 4 str-eq if
        12 c-tok-type !
    else c-tok-str c-tok-len @ kw-while-str 5 str-eq if
        13 c-tok-type !
    else c-tok-str c-tok-len @ kw-return-str 6 str-eq if
        14 c-tok-type !
    else
        2 c-tok-type !
    then then then then then then then
    ;

variable str-ch
variable str-delim

: next-tok
    c-skip-ws
    c-peek-ch 0= if
        0 c-tok-type !
    else
        c-peek-ch 34 = c-peek-ch 39 = + c-peek-ch 96 = + if
            c-get-ch str-delim !
            c-str-pool-ptr @ c-tok-val !
            begin
                c-peek-ch str-delim @ = 0= c-peek-ch 0= 0= and if
                    c-peek-ch 92 = if
                        c-get-ch drop
                        c-peek-ch 110 = if
                            c-get-ch drop 10 str-ch !
                        else c-peek-ch 116 = if
                            c-get-ch drop 9 str-ch !
                        else c-peek-ch 114 = if
                            c-get-ch drop 13 str-ch !
                        else c-peek-ch 48 = if
                            c-get-ch drop 0 str-ch !
                        else
                            c-get-ch str-ch !
                        then then then then
                    else
                        c-get-ch str-ch !
                    then
                    str-ch @ c-str-pool-ptr @ c!
                    c-str-pool-ptr @ 1+ c-str-pool-ptr !
                    0
                else
                    1
                then
            until
            c-peek-ch str-delim @ = if c-get-ch drop then
            0 c-str-pool-ptr @ c!
            c-str-pool-ptr @ 1+ c-str-pool-ptr !
            3 c-tok-type !
        else c-peek-ch is-digit if
            0 c-tok-val !
            begin
                c-peek-ch is-digit if
                    c-tok-val @ 10 * c-get-ch 48 - + c-tok-val !
                    0
                else 1 then
            until
            1 c-tok-type !
        else c-peek-ch is-alpha if
            0 c-tok-len !
            begin
                c-peek-ch is-alnum if
                    c-get-ch c-tok-str c-tok-len @ + c!
                    c-tok-len @ 1+ c-tok-len !
                    0
                else 1 then
            until
            check-keyword
        else
            c-get-ch c-tok-val !
            c-tok-val @ 61 = if
                c-peek-ch 61 = if c-get-ch drop 200 c-tok-type ! else 61 c-tok-type ! then
            else c-tok-val @ 33 = if
                c-peek-ch 61 = if c-get-ch drop 201 c-tok-type ! else 33 c-tok-type ! then
            else c-tok-val @ 60 = if
                c-peek-ch 61 = if c-get-ch drop 202 c-tok-type ! else 60 c-tok-type ! then
            else c-tok-val @ 62 = if
                c-peek-ch 61 = if c-get-ch drop 203 c-tok-type ! else 62 c-tok-type ! then
            else
                c-tok-val @ c-tok-type !
            then then then then
        then then then
    then
    ;

: add-local-var
    c-num-vars @ 8 * c-vars-len + c-tok-len @ swap !
    c-frame-size @ 8 + c-frame-size !
    c-num-vars @ 8 * c-vars-off + c-frame-size @ swap !
    c-tok-str c-vars-name c-num-vars @ 16 * + c-tok-len @ cmove
    c-num-vars @ 1+ c-num-vars !
    ;

variable find-idx
variable find-res
: find-local-var
    0 find-idx !
    0 find-res !
    begin
        find-idx @ c-num-vars @ < if
            c-tok-str c-tok-len @
            c-vars-name find-idx @ 16 * +
            find-idx @ 8 * c-vars-len + @
            str-eq if
                find-idx @ 8 * c-vars-off + @ find-res !
                1
            else
                find-idx @ 1+ find-idx !
                0
            then
        else
            1
        then
    until
    find-res @
    ;

variable fn-find-idx
variable fn-find-res
: find-function
    0 fn-find-idx !
    0 fn-find-res !
    begin
        fn-find-idx @ c-num-funcs @ < if
            c-tok-str c-tok-len @
            c-funcs-name fn-find-idx @ 16 * +
            fn-find-idx @ 8 * c-funcs-len + @
            str-eq if
                fn-find-idx @ 8 * c-funcs-xt + @ fn-find-res !
                1
            else
                fn-find-idx @ 1+ fn-find-idx !
                0
            then
        else
            1
        then
    until
    fn-find-res @
    ;

: add-function
    c-num-funcs @ 8 * c-funcs-len + c-fn-name-len @ swap !
    c-num-funcs @ 8 * c-funcs-xt + c-cur-fn-xt @ swap !
    c-num-funcs @ 8 * c-funcs-nargs + c-cur-fn-nargs @ swap !
    c-fn-name-buf c-funcs-name c-num-funcs @ 16 * + c-fn-name-len @ cmove
    c-num-funcs @ 1+ c-num-funcs !
    ;

variable rb-nargs
variable rb-xt
variable rb-len
variable rb-name

: register-builtin
    rb-nargs !
    rb-xt !
    rb-len !
    rb-name !
    c-num-funcs @ 8 * c-funcs-len + rb-len @ swap !
    c-num-funcs @ 8 * c-funcs-xt + rb-xt @ swap !
    c-num-funcs @ 8 * c-funcs-nargs + rb-nargs @ swap !
    rb-name @ c-funcs-name c-num-funcs @ 16 * + rb-len @ cmove
    c-num-funcs @ 1+ c-num-funcs !
    ;

create name-putchar 112 c, 117 c, 116 c, 99 c, 104 c, 97 c, 114 c,
create name-getchar 103 c, 101 c, 116 c, 99 c, 104 c, 97 c, 114 c,
create name-puts 112 c, 117 c, 116 c, 115 c,
create name-gets 103 c, 101 c, 116 c, 115 c,
create name-printf 112 c, 114 c, 105 c, 110 c, 116 c, 102 c,

: init-builtins
    0 c-num-funcs !
    name-putchar 7 c-putchar-xt @ 1 register-builtin
    name-getchar 7 c-getchar-xt @ 0 register-builtin
    name-puts 4 c-puts-xt @ 1 register-builtin
    name-gets 4 c-gets-xt @ 1 register-builtin
    name-printf 6 c-printf-xt @ 1 register-builtin
    ;

: parse-primary
    c-tok-type @ 1 = if
        72 c, 184 c, c-tok-val @ 8comma
        next-tok
    else c-tok-type @ 3 = if
        72 c, 184 c, c-tok-val @ 8comma
        next-tok
    else c-tok-type @ 2 = if
        find-function
        dup 0 > if
            fn-xt @ swap fn-xt !
            next-tok
            next-tok
            0
            begin
                c-tok-type @ 41 = 0= if
                    parse-expr-xt @ EXECUTE
                    80 c,
                    1+
                    c-tok-type @ 44 = if next-tok then
                    0
                else 1 then
            until
            next-tok
            dup 1 = if 95 c, then
            dup 2 = if 94 c, 95 c, then
            dup 3 = if 90 c, 94 c, 95 c, then
            dup 4 = if 89 c, 90 c, 94 c, 95 c, then
            dup 5 = if 65 c, 88 c, 89 c, 90 c, 94 c, 95 c, then
            dup 6 = if 65 c, 89 c, 65 c, 88 c, 89 c, 90 c, 94 c, 95 c, then
            drop
            49 c, 192 c,
            232 c, fn-xt @ HERE 4 + - 4comma
            fn-xt !
        else
            drop
            find-local-var
            dup 0 > if
                72 c, 139 c, 133 c, 0 swap - 4comma
                next-tok
            else
                drop
                next-tok
                next-tok
                0
                begin
                    c-tok-type @ 41 = 0= if
                        parse-expr-xt @ EXECUTE
                        80 c,
                        1+
                        c-tok-type @ 44 = if next-tok then
                        0
                    else 1 then
                until
                next-tok
                dup 1 = if 95 c, then
                dup 2 = if 94 c, 95 c, then
                dup 3 = if 90 c, 94 c, 95 c, then
                dup 4 = if 89 c, 90 c, 94 c, 95 c, then
                dup 5 = if 65 c, 88 c, 89 c, 90 c, 94 c, 95 c, then
                dup 6 = if 65 c, 89 c, 65 c, 88 c, 89 c, 90 c, 94 c, 95 c, then
                drop
                49 c, 192 c,
                232 c, c-cur-fn-xt @ HERE 4 + - 4comma
            then
        then
    else c-tok-type @ 40 = if
        next-tok
        parse-expr-xt @ EXECUTE
        next-tok
    then then then then
    ;

: parse-multiplicative
    parse-primary
    begin
        c-tok-type @ 42 = if
            80 c,
            next-tok
            parse-primary
            72 c, 137 c, 195 c,
            88 c,
            72 c, 15 c, 175 c, 195 c,
            0
        else c-tok-type @ 47 = if
            80 c,
            next-tok
            parse-primary
            72 c, 137 c, 195 c,
            88 c,
            72 c, 153 c,
            72 c, 247 c, 251 c,
            0
        else c-tok-type @ 37 = if
            80 c,
            next-tok
            parse-primary
            72 c, 137 c, 195 c,
            88 c,
            72 c, 153 c,
            72 c, 247 c, 251 c,
            72 c, 137 c, 208 c,
            0
        else 1 then then then
    until
    ;

: parse-additive
    parse-multiplicative
    begin
        c-tok-type @ 43 = if
            80 c, next-tok parse-multiplicative
            72 c, 137 c, 195 c, 88 c,
            72 c, 1 c, 216 c,
            0
        else c-tok-type @ 45 = if
            80 c, next-tok parse-multiplicative
            72 c, 137 c, 195 c, 88 c,
            72 c, 41 c, 216 c,
            0
        else 1 then then
    until
    ;

: parse-relational
    parse-additive
    begin
        c-tok-type @ 60 = if
            80 c, next-tok parse-additive
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 156 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else c-tok-type @ 202 = if
            80 c, next-tok parse-additive
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 158 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else c-tok-type @ 62 = if
            80 c, next-tok parse-additive
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 159 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else c-tok-type @ 203 = if
            80 c, next-tok parse-additive
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 157 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else 1 then then then then
    until
    ;

: parse-equality
    parse-relational
    begin
        c-tok-type @ 200 = if
            80 c, next-tok parse-relational
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 148 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else c-tok-type @ 201 = if
            80 c, next-tok parse-relational
            72 c, 137 c, 195 c, 88 c,
            72 c, 57 c, 216 c,
            15 c, 149 c, 192 c,
            72 c, 15 c, 182 c, 192 c,
            0
        else 1 then then
    until
    ;

variable assign-done
variable assign-off
: parse-expr
    0 assign-done !
    c-tok-type @ 2 = if
        c-tok-str c-var-name-buf c-tok-len @ cmove
        c-tok-len @ c-var-name-len !
        find-local-var
        dup 0 > if
            assign-off !
            c-skip-ws
            c-peek-ch 61 = if
                c-get-ch drop
                next-tok
                parse-equality
                72 c, 137 c, 133 c, 0 assign-off @ - 4comma
                1 assign-done !
            then
        else
            drop
        then
    then
    assign-done @ 0= if
        parse-equality
    then
    ;

variable f-true
variable f-flags
variable f-xt

: get-xt
    FIND
    f-true !
    f-flags !
    f-xt !
    f-xt @
    ;

s" parse-expr" get-xt parse-expr-xt !

variable jmp-else-addr
variable jmp-end-addr
variable loop-start-addr
variable loop-end-addr

: parse-stmt
    c-tok-type @ 14 = if
        next-tok
        c-tok-type @ 59 = if
            72 c, 49 c, 192 c,
        else
            parse-expr
        then
        72 c, 137 c, 236 c, 93 c, 195 c,
        c-tok-type @ 59 = if next-tok then
    else c-tok-type @ 10 = if
        next-tok
        begin c-tok-type @ 42 = if next-tok 0 else 1 then until
        add-local-var
        next-tok
        c-tok-type @ 61 = if
            next-tok
            parse-expr
            c-num-vars @ 1- 8 * c-vars-off + @
            72 c, 137 c, 133 c, 0 swap - 4comma
        then
        c-tok-type @ 59 = if next-tok then
    else c-tok-type @ 11 = if
        next-tok
        next-tok
        parse-expr
        next-tok
        72 c, 133 c, 192 c,
        15 c, 132 c,
        HERE jmp-else-addr !
        0 4comma
        c-tok-type @ 123 = if
            next-tok
            begin
                c-tok-type @ 125 = 0= if
                    parse-stmt-xt @ EXECUTE
                    0
                else 1 then
            until
            next-tok
        else
            parse-stmt-xt @ EXECUTE
        then
        233 c,
        HERE jmp-end-addr !
        0 4comma
        HERE jmp-else-addr @ 4 + -
        dup jmp-else-addr @ c!
        dup 8 rshift jmp-else-addr @ 1+ c!
        dup 16 rshift jmp-else-addr @ 2 + c!
        24 rshift jmp-else-addr @ 3 + c!

        c-tok-type @ 12 = if
            next-tok
            c-tok-type @ 123 = if
                next-tok
                begin
                    c-tok-type @ 125 = 0= if
                        parse-stmt-xt @ EXECUTE
                        0
                    else 1 then
                until
                next-tok
            else
                parse-stmt-xt @ EXECUTE
            then
        then
        HERE jmp-end-addr @ 4 + -
        dup jmp-end-addr @ c!
        dup 8 rshift jmp-end-addr @ 1+ c!
        dup 16 rshift jmp-end-addr @ 2 + c!
        24 rshift jmp-end-addr @ 3 + c!
    else c-tok-type @ 13 = if
        next-tok
        next-tok
        HERE loop-start-addr !
        parse-expr
        next-tok
        72 c, 133 c, 192 c,
        15 c, 132 c,
        HERE loop-end-addr !
        0 4comma
        c-tok-type @ 123 = if
            next-tok
            begin
                c-tok-type @ 125 = 0= if
                    parse-stmt-xt @ EXECUTE
                    0
                else 1 then
            until
            next-tok
        else
            parse-stmt-xt @ EXECUTE
        then
        233 c,
        loop-start-addr @ HERE 4 + - 4comma
        HERE loop-end-addr @ 4 + -
        dup loop-end-addr @ c!
        dup 8 rshift loop-end-addr @ 1+ c!
        dup 16 rshift loop-end-addr @ 2 + c!
        24 rshift loop-end-addr @ 3 + c!
    else
        parse-expr
        c-tok-type @ 59 = if next-tok then
    then then then then
    ;

s" parse-stmt" get-xt parse-stmt-xt !

: parse-c-function
    next-tok
    begin c-tok-type @ 42 = if next-tok 0 else 1 then until
    c-tok-str c-fn-name-buf c-tok-len @ cmove
    c-tok-len @ c-fn-name-len !
    next-tok
    next-tok

    0 c-num-vars !
    0 c-frame-size !
    0 c-cur-fn-nargs !

    begin
        c-tok-type @ 41 = 0= if
            c-tok-type @ 10 = if next-tok then
            begin c-tok-type @ 42 = if next-tok 0 else 1 then until
            add-local-var
            c-cur-fn-nargs @ 1+ c-cur-fn-nargs !
            next-tok
            c-tok-type @ 44 = if next-tok then
            0
        else 1 then
    until
    next-tok

    HERE c-cur-fn-xt !
    85 c,
    72 c, 137 c, 229 c,
    72 c, 129 c, 236 c, 128 c, 0 c, 0 c, 0 c,
    c-cur-fn-nargs @ 0 > if 72 c, 137 c, 125 c, 248 c, then
    c-cur-fn-nargs @ 1 > if 72 c, 137 c, 117 c, 240 c, then
    c-cur-fn-nargs @ 2 > if 72 c, 137 c, 85 c, 232 c, then

    c-tok-type @ 123 = if
        next-tok
        begin
            c-tok-type @ 125 = 0= if
                parse-stmt
                0
            else 1 then
        until
        next-tok
    then

    72 c, 137 c, 236 c, 93 c, 195 c,

    add-function

    c-fn-name-buf c-fn-name-len @ create-header align8

    c-cur-fn-nargs @ 0= if
        87 c,
        232 c, c-cur-fn-xt @ HERE 4 + - 4comma
        95 c,
        72 c, 131 c, 199 c, 8 c,
        72 c, 137 c, 71 c, 248 c,
        72 c, 137 c, 248 c,
        195 c,
    then

    c-cur-fn-nargs @ 1 = if
        72 c, 139 c, 71 c, 248 c,
        72 c, 131 c, 239 c, 8 c,
        87 c,
        72 c, 137 c, 199 c,
        232 c, c-cur-fn-xt @ HERE 4 + - 4comma
        95 c,
        72 c, 131 c, 199 c, 8 c,
        72 c, 137 c, 71 c, 248 c,
        72 c, 137 c, 248 c,
        195 c,
    then

    c-cur-fn-nargs @ 2 = if
        72 c, 139 c, 119 c, 248 c,
        72 c, 139 c, 71 c, 240 c,
        72 c, 131 c, 239 c, 16 c,
        87 c,
        72 c, 137 c, 199 c,
        232 c, c-cur-fn-xt @ HERE 4 + - 4comma
        95 c,
        72 c, 131 c, 199 c, 8 c,
        72 c, 137 c, 71 c, 248 c,
        72 c, 137 c, 248 c,
        195 c,
    then
    ;

variable c-src-addr
variable c-src-len

: c-compile
    c-src-len !
    c-src-addr !
    c-src-addr @ c-src-buf c-src-len @ cmove
    c-src-buf c-src-ptr !
    c-src-buf c-src-len @ + c-src-end !
    init-builtins
    c-str-pool c-str-pool-ptr !
    next-tok

    begin
        c-tok-type @ 0 > if
            parse-c-function
            0
        else
            1
        then
    until
    ;
