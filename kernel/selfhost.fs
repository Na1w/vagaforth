4399104 constant SELF-FN-BUF
4399360 constant SELF-SRC-BUF
4403456 constant SELF-OUT-BUF
variable self-fnlen
variable self-fd
variable self-len
: compile-file
    self-fnlen !
    SELF-FN-BUF self-fnlen @ cmove
    SELF-FN-BUF self-fnlen @ + 0 swap c!
    SELF-FN-BUF 0 0 0 sys-open
    self-fd !
    0 self-len !
    begin
        self-fd @ SELF-SRC-BUF self-len @ + 4096 sys-read
        dup 0 > if
            self-len @ + self-len !
            0
        else
            drop
            1
        then
    until
    self-fd @ sys-close drop
    SELF-SRC-BUF self-len @ + 0 swap c!
    SELF-SRC-BUF self-len @ evaluate
    ;
107 SELF-FN-BUF c!
101 SELF-FN-BUF 1 + c!
114 SELF-FN-BUF 2 + c!
110 SELF-FN-BUF 3 + c!
101 SELF-FN-BUF 4 + c!
108 SELF-FN-BUF 5 + c!
47 SELF-FN-BUF 6 + c!
107 SELF-FN-BUF 7 + c!
101 SELF-FN-BUF 8 + c!
114 SELF-FN-BUF 9 + c!
110 SELF-FN-BUF 10 + c!
101 SELF-FN-BUF 11 + c!
108 SELF-FN-BUF 12 + c!
95 SELF-FN-BUF 13 + c!
115 SELF-FN-BUF 14 + c!
101 SELF-FN-BUF 15 + c!
108 SELF-FN-BUF 16 + c!
102 SELF-FN-BUF 17 + c!
46 SELF-FN-BUF 18 + c!
102 SELF-FN-BUF 19 + c!
115 SELF-FN-BUF 20 + c!
SELF-FN-BUF 21 compile-file
118 SELF-OUT-BUF c!
97 SELF-OUT-BUF 1 + c!
103 SELF-OUT-BUF 2 + c!
97 SELF-OUT-BUF 3 + c!
102 SELF-OUT-BUF 4 + c!
111 SELF-OUT-BUF 5 + c!
114 SELF-OUT-BUF 6 + c!
116 SELF-OUT-BUF 7 + c!
104 SELF-OUT-BUF 8 + c!
95 SELF-OUT-BUF 9 + c!
110 SELF-OUT-BUF 10 + c!
101 SELF-OUT-BUF 11 + c!
119 SELF-OUT-BUF 12 + c!
46 SELF-OUT-BUF 13 + c!
98 SELF-OUT-BUF 14 + c!
105 SELF-OUT-BUF 15 + c!
110 SELF-OUT-BUF 16 + c!
s" START" FIND drop drop HERE 4194304 - 16777216 elf-header
SELF-OUT-BUF 17 save-elf
