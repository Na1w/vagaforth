include core/prelude.fs
include core/host-ext.fs
include core/struct.fs

\ Definiera en struct
struct
    ptr%   field .name
    cell%  field .age
    cell%  field .id
end-struct person%

." Size of person%: " person% . cr

\ Skapa en instans i minnet
create fredrik-obj
person% allot

\ Initiera data
s" Fredrik" drop fredrik-obj .name !
30 fredrik-obj .age !
1337 fredrik-obj .id !

\ Läs data
." Person Name Addr: " fredrik-obj .name @ . cr
." Person Age: " fredrik-obj .age @ . cr
." Person ID: " fredrik-obj .id @ . cr

bye