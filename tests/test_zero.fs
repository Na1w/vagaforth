include core/prelude.fs
include core/host-ext.fs

: cr 10 emit ;

0 0 = if 42 emit cr else 45 emit cr then
1 0 = if 42 emit cr else 45 emit cr then
0 0 <> if 42 emit cr else 45 emit cr then

bye
