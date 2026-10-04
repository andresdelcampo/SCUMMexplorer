# Decoded cutscene inventory

Candidates, not playable scenes. No path/state validation implied.

Input SHA256: e1885c3896dcccb0c84fd2301d491a6a5e15feafbf48c8353602935cf938f82d

235 sites, 216 candidate owners; unresolved targets/scopes are retained in JSON.

| Owner | Cutscene offsets | Direct possible callers | Room commands in owner | State reads |
|---|---|---|---|---|
| script 004 | +0x0000 | script 005, script 115 | loadRoom 0x46 | 16 |
| script 005 | +0x01C0 | None resolved | None | 50 |
| script 009 | +0x002C | script 005 | None | 10 |
| script 011 | +0x0000 | script 010, script 031 | None | 2 |
| script 012 | +0x0000 | script 009, script 010 | None | 2 |
| script 013 | +0x0000 | script 009, script 010 | None | 2 |
| script 022 | +0x0005, +0x0138 | script 020 | None | 15 |
| script 029 | +0x0000 | script 049, script 054, script 104, Room 06 local 200, Room 08 local 217, Room 09 object 270 Verb 0x3A, Room 10 local 212, Room 28 local 200, Room 46 local 204, Room 62 local 205 | None | 11 |
| script 041 | +0x0000 | Room 02 object 154 Verb 0x56, Room 02 object 156 Verb 0x56, Room 04 object 171 Verb 0x3A, Room 04 object 171 Verb 0x3B, Room 04 object 171 Verb 0x56, Room 04 object 172 Verb 0x3A, Room 04 object 172 Verb 0x3B, Room 04 object 172 Verb 0x56, Room 04 object 173 Verb 0x3A, Room 04 object 173 Verb 0x3B, Room 04 object 173 Verb 0x56, Room 04 object 174 Verb 0x3A, Room 04 object 174 Verb 0x3B, Room 04 object 174 Verb 0x56, Room 04 object 175 Verb 0x56, Room 04 object 176 Verb 0x3A, Room 04 object 176 Verb 0x3B, Room 04 object 176 Verb 0x56, Room 04 object 177 Verb 0x3B, Room 04 object 177 Verb 0x56, Room 06 object 205 Verb 0x56, Room 06 object 206 Verb 0x56, Room 06 object 207 Verb 0x56, Room 06 object 209 Verb 0x56, Room 06 object 210 Verb 0x56, Room 06 object 211 Verb 0x56, Room 06 object 214 Verb 0x56, Room 08 object 256 Verb 0x56, Room 08 object 257 Verb 0x56, Room 08 object 258 Verb 0x56, Room 09 object 270 Verb 0x56, Room 09 object 272 Verb 0x56, Room 10 object 296 Verb 0x3A, Room 10 object 296 Verb 0x3B, Room 10 object 296 Verb 0x56, Room 10 object 297 Verb 0x56, Room 10 object 298 Verb 0x56, Room 10 object 299 Verb 0x56, Room 10 object 300 Verb 0x56, Room 13 object 325 Verb 0x3A, Room 13 object 325 Verb 0x41, Room 13 object 325 Verb 0x56, Room 16 local 204, Room 16 object 350 Verb 0x56, Room 17 object 357 Verb 0x3B, Room 17 object 357 Verb 0x56, Room 17 object 358 Verb 0x3B, Room 17 object 358 Verb 0x56, Room 18 object 369 Verb 0x56, Room 18 object 370 Verb 0x56, Room 18 object 371 Verb 0x3A, Room 18 object 371 Verb 0x56, Room 20 local 215, Room 20 object 388 Verb 0x56, Room 20 object 389 Verb 0x56, Room 22 object 403 Verb 0x56, Room 22 object 404 Verb 0x56, Room 22 object 405 Verb 0x3A, Room 22 object 405 Verb 0x45, Room 22 object 405 Verb 0x46, Room 22 object 405 Verb 0x56, Room 24 object 431 Verb 0x3B, Room 24 object 431 Verb 0x56, Room 24 object 433 Verb 0x3F, Room 24 object 433 Verb 0x47, Room 24 object 433 Verb 0x49, Room 24 object 433 Verb 0x56, Room 31 object 523 Verb 0x46, Room 31 object 523 Verb 0x56, Room 31 object 524 Verb 0x45, Room 31 object 524 Verb 0x56, Room 34 object 561 Verb 0x43, Room 34 object 561 Verb 0x56, Room 34 object 562 Verb 0x3B, Room 34 object 562 Verb 0x56, Room 34 object 586 Verb 0x43, Room 34 object 586 Verb 0x44, Room 34 object 586 Verb 0x56, Room 37 object 604 Verb 0x45, Room 37 object 604 Verb 0x46, Room 37 object 604 Verb 0x56, Room 38 object 610 Verb 0x3C, Room 38 object 610 Verb 0x3D, Room 38 object 610 Verb 0x56, Room 38 object 616 Verb 0x3A, Room 38 object 616 Verb 0x56, Room 38 object 617 Verb 0x3B, Room 38 object 617 Verb 0x56, Room 43 object 640 Verb 0x56, Room 43 object 643 Verb 0x3B, Room 43 object 643 Verb 0x56, Room 46 object 682 Verb 0x56, Room 46 object 683 Verb 0x56, Room 46 object 684 Verb 0x56, Room 46 object 685 Verb 0x56, Room 47 object 697 Verb 0x56, Room 62 object 808 Verb 0x56, Room 62 object 809 Verb 0x56, Room 62 object 810 Verb 0x56, Room 63 object 820 Verb 0x56, Room 65 object 828 Verb 0x56 | None | 5 |
| script 045 | +0x001D, +0x0049, +0x0075, +0x009D | script 044, script 049, Room 02 entry | None | 7 |
| script 046 | +0x0000 | Room 08 object 255 Verb 0x38 | loadRoom 0x09; loadRoom 0x43; loadRoom 0x09; loadRoom 0x36; loadRoom 0x09; loadRoom 0x03; loadRoom 0x08; loadRoom 0x09; loadRoom 0x0B; loadRoom 0x08; loadRoom 0x09; loadRoom 0x09 | 35 |
| script 047 | +0x0000 | Room 09 local 214, Room 09 object 270 Verb 0x3A | loadRoom 0x51; loadRoom 0x09; loadRoom 0x08; loadRoom 0x51; loadRoom 0x09; loadRoom 0x51; loadRoom 0x09; loadRoom 0x09 | 23 |
| script 048 | +0x0000 | Room 63 object 821 Verb 0x40, Room 63 object 822 Verb 0x40 | loadRoom 0x3E | 5 |
| script 049 | +0x002C | Room 02 object 156 Verb 0x3A | loadRoom 0x0B; loadRoom 0x02; loadRoom 0x02 | 18 |
| script 052 | +0x0000 | Room 10 object 295 Verb 0x38 | loadRoom 0x0B; loadRoomWithEgo 0x0144, 0x0D, walkTo=(280, 120) | 15 |
| script 053 | +0x0000 | Room 13 local 201 | loadRoom 0x0E; loadRoom 0x0C; loadRoom 0x0C | 5 |
| script 054 | +0x0000 | Room 13 object 323 Verb 0x38 | loadRoom 0x0F; loadRoom 0x0F | 4 |
| script 056 | +0x0000 | Room 18 object 371 Verb 0x36 | loadRoom 0x14 | 2 |
| script 057 | +0x0000 | script 056 | None | 2 |
| script 058 | +0x0000 | Room 20 local 207, Room 20 object 389 Verb 0x36 | None | 1 |
| script 059 | +0x0010 | script 058 | loadRoom 0x12 | 3 |
| script 060 | +0x0000 | Room 20 object 388 Verb 0x36 | None | 1 |
| script 061 | +0x0010 | script 060 | loadRoom 0x12 | 1 |
| script 062 | +0x0000 | Room 18 object 369 Verb 0x36 | loadRoom 0x14 | 1 |
| script 063 | +0x0000 | script 062 | None | 1 |
| script 065 | +0x0000 | Room 18 object 370 Verb 0x36 | loadRoom 0x13; loadRoom 0x12 | 15 |
| script 066 | +0x0000 | Room 17 local 200 | loadRoom 0x11 | 8 |
| script 067 | +0x0000 | Room 22 local 200, Room 22 local 204 | loadRoom 0x16 | 7 |
| script 069 | +0x0000 | Room 25 local 200, Room 25 object 447 Verb 0x36 | loadRoom 0x37; loadRoom 0x19; loadRoom 0x18; loadRoom 0x19; loadRoom 0x19 | 11 |
| script 070 | +0x0000 | Room 24 object 433 Verb 0x3E | loadRoom 0x1B | 9 |
| script 072 | +0x0000 | Room 28 object 465 Verb 0x38 | loadRoom 0x41; loadRoom 0x1E | 0 |
| script 075 | +0x0038 | Room 28 object 466 Verb 0x36, Room 28 object 467 Verb 0x36 | loadRoom 0x4F; loadRoom 0x1C; loadRoom 0x1C | 2 |
| script 076 | +0x0000 | Room 28 local 212, Room 28 local 215 | loadRoom 0x1B; loadRoom 0x1C | 1 |
| script 077 | +0x0000 | Room 30 object 507 Verb 0x38, Room 65 object 827 Verb 0x38 | loadRoom 0x1C | 0 |
| script 078 | +0x0000 | Room 65 object 828 Verb 0x40 | loadRoom 0x1E | 6 |
| script 079 | +0x0000 | Room 30 object 509 Verb 0x38 | loadRoom 0x20 | 0 |
| script 080 | +0x0000 | Room 30 object 510 Verb 0x38 | loadRoom 0x20 | 0 |
| script 081 | +0x0000 | Room 32 object 538 Verb 0x38 | loadRoom 0x1E | 0 |
| script 082 | +0x0000 | Room 30 object 508 Verb 0x38 | loadRoom 0x1F | 1 |
| script 083 | +0x0000 | Room 31 object 521 Verb 0x38 | loadRoom 0x1E | 0 |
| script 084 | +0x0000 | Room 30 object 517 Verb 0x38, Room 30 object 518 Verb 0x38 | loadRoom 0x1F | 0 |
| script 085 | +0x0000 | Room 34 local 201, Room 34 object 561 Verb 0x43, Room 34 object 561 Verb 0x4C, Room 34 object 561 Verb 0x56 | loadRoom 0x34; loadRoom 0x22; loadRoom 0x22 | 16 |
| script 086 | +0x0000 | Room 37 local 200 | loadRoom 0x3A; loadRoom 0x25; loadRoom 0x26; loadRoom 0x26 | 6 |
| script 087 | +0x0000 | Room 38 object 610 Verb 0x36 | loadRoom 0x22; loadRoom 0x44; loadRoom 0x26; loadRoom 0x25; loadRoom 0x28; loadRoom 0x27; loadRoom 0x28; loadRoom 0x26; loadRoom 0x26 | 28 |
| script 089 | +0x0000 | Room 50 object 721 Verb 0x38 | loadRoom 0x0B; loadRoomWithEgo 0x00FB, 0x07, walkTo=(47, 109) | 0 |
| script 090 | +0x0000 | Room 41 local 204 | loadRoom 0x2A; loadRoom 0x2B; loadRoom 0x3C; loadRoom 0x2B; loadRoom 0x2B | 15 |
| script 091 | +0x0022 | Room 43 object 643 Verb 0x3A | loadRoom 0x2C; loadRoom 0x2A; loadRoom 0x2C; loadRoom 0x3C; loadRoom 0x2A; loadRoom 0x2B; loadRoom 0x2C; loadRoom 0x2B; loadRoom 0x2B | 32 |
| script 092 | +0x0000 | Room 44 object 652 Verb 0x38, Room 45 object 655 Verb 0x38 | loadRoom 0x2B | 0 |
| script 093 | +0x0000 | Room 43 object 650 Verb 0x38 | loadRoom 0x57; loadRoom 0x2B; loadRoom 0x2C; loadRoom 0x2B; loadRoom 0x2B | 8 |
| script 094 | +0x0000 | Room 43 object 640 Verb 0x36 | loadRoom 0x13; loadRoom 0x2B | 8 |
| script 095 | +0x0000 | Room 44 object 653 Verb 0x36 | loadRoom 0x2A; loadRoom 0x2D; loadRoom 0x2D | 30 |
| script 096 | +0x0000 | Room 43 object 639 Verb 0x38 | loadRoom 0x2D; loadRoomWithEgo 0x02AD, 0x2E, walkTo=(832, 108) | 2 |
| script 097 | +0x0000 | Room 46 object 684 Verb 0x36 | loadRoom 0x22 | 0 |
| script 098 | +0x0035 | Room 34 object 557 Verb 0x37, Room 34 object 559 Verb 0x37, Room 34 object 560 Verb 0x37, Room 34 object 586 Verb 0x37 | loadRoom 0x39; loadRoom 0x22; loadRoom 0x22 | 8 |
| script 099 | +0x0000 | Room 49 object 707 Verb 0x36, Room 49 object 707 Verb 0x56 | loadRoom 0x14; loadRoom 0x31; loadRoom 0x31 | 9 |
| script 100 | +0x0000 | Room 47 object 697 Verb 0x36 | loadRoom 0x3D; loadRoom 0x2F; loadRoom 0x3D; loadRoom 0x2F; loadRoom 0x2F | 19 |
| script 101 | +0x0000 | Room 09 local 209, Room 09 local 214 | loadRoom 0x3B; loadRoom 0x09; loadRoom 0x09 | 20 |
| script 102 | +0x0000 | Room 51 object 741 Verb 0x38 | loadRoom 0x42 | 2 |
| script 103 | +0x0000 | Room 51 object 742 Verb 0x56 | None | 2 |
| script 104 | +0x0000 | Room 09 object 272 Verb 0x52 | loadRoom 0x0E; loadRoom 0x33; loadRoom 0x3B; loadRoom 0x3D; loadRoom 0x33; loadRoom 0x33 | 16 |
| script 105 | +0x0000 | Room 09 object 274 Verb 0x36, Room 51 object 743 Verb 0x36 | None | 6 |
| script 107 | +0x0000 | Room 13 object 324 Verb 0x36 | loadRoom 0x0A | 3 |
| script 108 | +0x0000 | Room 22 object 405 Verb 0x36 | loadRoom 0x38; loadRoom 0x16; loadRoom 0x16 | 5 |
| script 109 | +0x0016 | Room 28 object 470 Verb 0x36, Room 28 object 471 Verb 0x36, Room 28 object 472 Verb 0x36 | loadRoom 0x4F; loadRoom 0x1C; loadRoom 0x1C | 4 |
| script 110 | +0x0000 | Room 46 object 682 Verb 0x36 | loadRoom 0x31 | 0 |
| script 111 | +0x0000 | Room 31 object 525 Verb 0x36 | loadRoom 0x56; loadRoom 0x1F | 8 |
| script 112 | +0x0026 | Room 34 object 561 Verb 0x36 | loadRoom 0x1D; loadRoom 0x22 | 2 |
| Room 02 object 153 Verb 0x38 | +0x0009 | None resolved | loadRoomWithEgo 0x00A0, 0x03, walkTo=(250, 68) | 1 |
| Room 02 object 154 Verb 0x36 | +0x0000 | None resolved | None | 1 |
| Room 03 object 165 Verb 0x56 | +0x0000 | None resolved | None | 2 |
| Room 04 local 200 | +0x0000 | Room 04 object 171 Verb 0x36, Room 04 object 172 Verb 0x36, Room 04 object 173 Verb 0x36, Room 04 object 174 Verb 0x36 | None | 11 |
| Room 04 local 202 | +0x0000 | Room 04 object 176 Verb 0x36 | None | 3 |
| Room 04 local 209 | +0x0000 | Room 04 object 177 Verb 0x3A | None | 6 |
| Room 04 object 169 Verb 0x37 | +0x0000 | None resolved | None | 2 |
| Room 04 object 170 Verb 0x37 | +0x0000 | None resolved | None | 2 |
| Room 04 object 177 Verb 0x36 | +0x0017 | None resolved | loadRoomWithEgo 0x0339, 0x40, walkTo=(-1, -1) | 1 |
| Room 04 object 197 Verb 0x56 | +0x0000 | None resolved | None | 2 |
| Room 06 local 200 | +0x0000 | Room 06 local 203, Room 06 local 204, Room 06 local 205, Room 06 local 206, Room 06 local 207, Room 06 local 208, Room 06 local 209, Room 06 local 210 | None | 14 |
| Room 06 local 201 | +0x0000 | Room 06 object 205 Verb 0x36 | None | 3 |
| Room 06 local 211 | +0x0000 | Room 06 object 206 Verb 0x3E, Room 06 object 207 Verb 0x3E, Room 06 object 210 Verb 0x3E, Room 06 object 211 Verb 0x3E | None | 3 |
| Room 06 local 212 | +0x0000 | Room 06 object 206 Verb 0x3F, Room 06 object 207 Verb 0x3F, Room 06 object 210 Verb 0x3F, Room 06 object 211 Verb 0x3F | None | 3 |
| Room 06 local 215 | +0x0000 | Room 06 object 214 Verb 0x36 | None | 4 |
| Room 06 object 209 Verb 0x36 | +0x0000 | None resolved | None | 0 |
| Room 08 local 208 | +0x0000 | Room 08 local 211, Room 08 local 213, Room 08 local 215 | None | 2 |
| Room 08 local 209 | +0x0000 | Room 08 local 212, Room 08 local 214, Room 08 local 216 | None | 2 |
| Room 08 local 211 | +0x0008 | Room 08 object 256 Verb 0x3E | None | 5 |
| Room 08 local 212 | +0x000B | Room 08 object 256 Verb 0x3F | None | 5 |
| Room 08 local 213 | +0x0008 | Room 08 object 257 Verb 0x3E | None | 5 |
| Room 08 local 214 | +0x000B | Room 08 object 257 Verb 0x3F | None | 5 |
| Room 08 local 215 | +0x0008 | Room 08 object 258 Verb 0x3E | None | 5 |
| Room 08 local 216 | +0x000B | Room 08 object 258 Verb 0x3F | None | 5 |
| Room 08 object 256 Verb 0x36 | +0x0009 | None resolved | None | 0 |
| Room 08 object 257 Verb 0x36 | +0x0009 | None resolved | None | 0 |
| Room 08 object 258 Verb 0x36 | +0x0009 | None resolved | None | 0 |
| Room 09 local 209 | +0x0000 | Room 09 object 272 Verb 0x36 | None | 11 |
| Room 09 local 214 | +0x000C | Room 09 object 268 Verb 0x37 | None | 8 |
| Room 09 local 221 | +0x0000 | Room 09 object 275 Verb 0x56 | None | 5 |
| Room 09 local 222 | +0x0000 | Room 09 object 273 Verb 0x4D, Room 09 object 273 Verb 0x4F, Room 09 object 273 Verb 0x51, Room 09 object 273 Verb 0x56 | None | 5 |
| Room 09 local 223 | +0x0000 | Room 09 object 273 Verb 0x4F | None | 16 |
| Room 09 local 224 | +0x0000 | Room 09 object 273 Verb 0x51 | None | 16 |
| Room 09 object 270 Verb 0x36 | +0x0000 | None resolved | None | 1 |
| Room 09 object 271 Verb 0x38 | +0x0005 | None resolved | None | 1 |
| Room 09 object 273 Verb 0x4D | +0x000D | None resolved | None | 5 |
| Room 10 local 200 | +0x0039, +0x0110 | Room 10 entry | None | 16 |
| Room 10 local 205 | +0x0000 | Room 10 object 297 Verb 0x36, Room 10 object 298 Verb 0x36, Room 10 object 299 Verb 0x36, Room 10 object 300 Verb 0x36 | None | 9 |
| Room 10 local 212 | +0x0000 | Room 10 object 296 Verb 0x3A | None | 8 |
| Room 10 local 213 | +0x0000 | Room 10 object 296 Verb 0x3B | None | 2 |
| Room 10 object 296 Verb 0x3B | +0x000C | None resolved | None | 4 |
| Room 12 object 322 Verb 0x38 | +0x0000 | None resolved | loadRoomWithEgo 0x0144, 0x0D, walkTo=(280, 120) | 1 |
| Room 13 local 202 | +0x0000 | Room 13 object 325 Verb 0x42 | None | 7 |
| Room 13 object 325 Verb 0x36 | +0x0000 | None resolved | None | 2 |
| Room 15 local 200 | +0x001D, +0x006D | Room 15 entry | None | 5 |
| Room 15 object 338 Verb 0x38 | +0x0000 | None resolved | loadRoomWithEgo 0x01A2, 0x17, walkTo=(396, 134) | 0 |
| Room 16 local 204 | +0x0072 | Room 16 object 350 Verb 0x47, Room 16 object 350 Verb 0x48 | None | 7 |
| Room 17 local 200 | +0x0022, +0x004D | Room 17 entry | None | 9 |
| Room 17 local 206 | +0x0000 | Room 17 object 357 Verb 0x3A, Room 17 object 358 Verb 0x3A | None | 6 |
| Room 18 local 205 | +0x0000 | Room 18 object 367 Verb 0x36, Room 18 object 368 Verb 0x38, Room 18 object 372 Verb 0x36, Room 18 object 372 Verb 0x56, Room 18 object 374 Verb 0x36 | None | 2 |
| Room 20 local 202 | +0x0000 | Room 20 object 386 Verb 0x36, Room 20 object 387 Verb 0x36 | None | 3 |
| Room 20 local 206 | +0x0000 | Room 20 object 390 Verb 0x48, Room 20 object 391 Verb 0x48 | None | 5 |
| Room 20 local 207 | +0x000B | Room 20 local 206 | None | 14 |
| Room 20 local 211 | +0x0000 | Room 20 object 390 Verb 0x49, Room 20 object 391 Verb 0x49 | None | 1 |
| Room 20 local 212 | +0x0000 | Room 20 object 390 Verb 0x47, Room 20 object 391 Verb 0x47 | None | 1 |
| Room 20 local 213 | +0x0000 | Room 20 object 390 Verb 0x56, Room 20 object 391 Verb 0x56 | None | 1 |
| Room 22 local 200 | +0x002F, +0x0083 | Room 22 entry, Room 22 local 204, Room 22 local 205 | None | 7 |
| Room 22 local 204 | +0x0000 | Room 22 object 404 Verb 0x36 | None | 8 |
| Room 22 local 205 | +0x0000 | Room 22 object 403 Verb 0x36 | None | 7 |
| Room 22 local 214 | +0x0000 | Room 22 object 405 Verb 0x45, Room 22 object 405 Verb 0x46 | None | 5 |
| Room 23 local 200 | +0x0020, +0x0058, +0x0084, +0x00B3 | Room 23 entry | None | 7 |
| Room 23 local 202 | +0x0000 | Room 23 local 201 | None | 12 |
| Room 23 local 210 | +0x0000 | Room 23 object 422 Verb 0x49, Room 23 object 423 Verb 0x49, Room 23 object 424 Verb 0x49, Room 23 object 425 Verb 0x49 | None | 19 |
| Room 23 local 214 | +0x0000 | Room 23 local 213 | None | 7 |
| Room 23 local 229 | +0x0000 | Room 23 object 422 Verb 0x47, Room 23 object 422 Verb 0x56, Room 23 object 423 Verb 0x47, Room 23 object 423 Verb 0x56, Room 23 object 424 Verb 0x47, Room 23 object 424 Verb 0x56, Room 23 object 425 Verb 0x47, Room 23 object 425 Verb 0x56 | None | 3 |
| Room 23 local 232 | +0x0000 | Room 23 object 422 Verb 0x47, Room 23 object 423 Verb 0x47, Room 23 object 424 Verb 0x47, Room 23 object 425 Verb 0x47 | None | 0 |
| Room 24 local 200 | +0x001D, +0x0089 | Room 24 entry | None | 7 |
| Room 24 local 204 | +0x0000 | Room 24 object 432 Verb 0x36 | None | 2 |
| Room 24 local 211 | +0x0018 | Room 24 object 432 Verb 0x3E | None | 2 |
| Room 24 local 212 | +0x0018 | Room 24 object 432 Verb 0x3F | None | 2 |
| Room 24 local 213 | +0x0000 | Room 24 local 211, Room 24 local 212 | None | 3 |
| Room 24 local 214 | +0x0000 | Room 24 object 431 Verb 0x3A | None | 6 |
| Room 24 local 215 | +0x0000 | Room 24 local 207, Room 24 local 208, Room 24 object 434 Verb 0x47, Room 24 object 434 Verb 0x56 | None | 8 |
| Room 24 object 432 Verb 0x47 | +0x0000 | None resolved | None | 1 |
| Room 24 object 432 Verb 0x49 | +0x0000 | None resolved | None | 1 |
| Room 24 object 432 Verb 0x56 | +0x0000 | None resolved | None | 1 |
| Room 24 object 433 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 25 local 200 | +0x0058 | Room 25 entry | None | 12 |
| Room 25 local 204 | +0x0000 | Room 25 local 200, Room 25 object 447 Verb 0x36 | None | 4 |
| Room 25 local 205 | +0x0000 | Room 25 object 447 Verb 0x36 | None | 3 |
| Room 25 local 206 | +0x0000 | Room 25 object 447 Verb 0x36 | None | 5 |
| Room 25 local 208 | +0x0000 | Room 25 object 446 Verb 0x37 | loadRoomWithEgo 0x01AE, 0x18, walkTo=(131, 125) | 1 |
| Room 25 object 447 Verb 0x3C | +0x0000 | None resolved | None | 1 |
| Room 25 object 447 Verb 0x56 | +0x0000 | None resolved | None | 1 |
| Room 26 local 202 | +0x0000 | Room 26 object 464 Verb 0x4D | None | 19 |
| Room 26 local 209 | +0x0000 | Room 26 object 464 Verb 0x56 | None | 10 |
| Room 26 local 211 | +0x0000 | Room 26 object 464 Verb 0x36 | None | 0 |
| Room 26 object 464 Verb 0x3E | +0x0000 | None resolved | None | 2 |
| Room 28 local 200 | +0x0010 | Room 28 object 470 Verb 0x3D, Room 28 object 471 Verb 0x3D, Room 28 object 472 Verb 0x3D | None | 17 |
| Room 28 local 201 | +0x0010 | Room 28 object 473 Verb 0x3C, Room 28 object 474 Verb 0x3C, Room 28 object 475 Verb 0x3C | None | 16 |
| Room 28 local 203 | +0x0000 | Room 28 object 466 Verb 0x38, Room 28 object 467 Verb 0x38, Room 28 object 468 Verb 0x38, Room 28 object 469 Verb 0x38, Room 28 object 470 Verb 0x38, Room 28 object 471 Verb 0x38, Room 28 object 472 Verb 0x38, Room 28 object 473 Verb 0x38, Room 28 object 474 Verb 0x38, Room 28 object 475 Verb 0x38 | None | 0 |
| Room 28 local 209 | +0x0000 | Room 28 object 468 Verb 0x44, Room 28 object 469 Verb 0x44 | None | 6 |
| Room 28 local 210 | +0x0000 | Room 28 object 466 Verb 0x43, Room 28 object 467 Verb 0x43 | None | 5 |
| Room 28 local 212 | +0x0064 | Room 28 entry, Room 28 local 210 | None | 7 |
| Room 28 local 213 | +0x0000 | Room 28 object 466 Verb 0x56, Room 28 object 467 Verb 0x56, Room 28 object 468 Verb 0x49, Room 28 object 468 Verb 0x56, Room 28 object 469 Verb 0x49, Room 28 object 469 Verb 0x56 | None | 6 |
| Room 28 local 215 | +0x0000 | Room 28 object 466 Verb 0x49, Room 28 object 467 Verb 0x49 | None | 13 |
| Room 28 local 218 | +0x0000 | Room 28 local 200, Room 28 local 201, Room 28 local 216, Room 28 local 217 | None | 8 |
| Room 30 object 503 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 30 object 504 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 30 object 505 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 30 object 511 Verb 0x38 | +0x0000 | None resolved | None | 1 |
| Room 30 object 512 Verb 0x38 | +0x0000 | None resolved | None | 1 |
| Room 30 object 513 Verb 0x38 | +0x0000 | None resolved | None | 1 |
| Room 30 object 514 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 30 object 515 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 30 object 516 Verb 0x38 | +0x0000 | None resolved | None | 0 |
| Room 31 local 200 | +0x0000 | Room 31 object 523 Verb 0x36 | None | 5 |
| Room 31 local 203 | +0x0000 | Room 31 object 523 Verb 0x45, Room 31 object 524 Verb 0x46 | None | 9 |
| Room 31 local 204 | +0x0000 | Room 31 object 525 Verb 0x56 | None | 6 |
| Room 32 object 537 Verb 0x38 | +0x0000 | None resolved | loadRoomWithEgo 0x01FD, 0x1E, walkTo=(954, 68) | 0 |
| Room 33 local 200 | +0x0006 | Room 33 object 544 Verb 0x42 | None | 0 |
| Room 33 local 202 | +0x0006 | Room 33 object 544 Verb 0x41 | None | 0 |
| Room 33 local 205 | +0x0000 | Room 33 local 200, Room 33 local 202, Room 33 object 544 Verb 0x41, Room 33 object 544 Verb 0x42, Room 33 object 544 Verb 0x56 | None | 8 |
| Room 33 object 543 Verb 0x38 | +0x0010 | None resolved | loadRoomWithEgo 0x022D, 0x22, walkTo=(64, 73) | 0 |
| Room 34 local 200 | +0x0035 | Room 34 entry | None | 6 |
| Room 34 local 201 | +0x0000, +0x0046, +0x0134, +0x0197 | Room 34 object 561 Verb 0x44 | None | 9 |
| Room 34 local 202 | +0x0000 | Room 34 object 559 Verb 0x38, Room 34 object 561 Verb 0x4C | None | 11 |
| Room 34 local 207 | +0x0079 | Room 34 object 559 Verb 0x37 | None | 3 |
| Room 34 local 209 | +0x0000 | Room 34 object 586 Verb 0x4D | None | 11 |
| Room 34 local 218 | +0x0000 | Room 34 object 562 Verb 0x3A | None | 6 |
| Room 34 object 586 Verb 0x4C | +0x0000 | None resolved | None | 2 |
| Room 35 local 204 | +0x0096 | Room 35 entry | None | 50 |
| Room 35 local 207 | +0x0006 | Room 35 object 589 Verb 0x3A | None | 1 |
| Room 35 local 210 | +0x001A | Room 35 object 588 Verb 0x38, Room 35 object 596 Verb 0x38 | loadRoomWithEgo 0x0256, 0x24, walkTo=(32, 128) | 3 |
| Room 35 local 212 | +0x0000 | Room 35 local 207, Room 35 object 589 Verb 0x3B, Room 35 object 589 Verb 0x56, Room 35 object 596 Verb 0x56 | None | 7 |
| Room 36 local 200 | +0x001D, +0x0048, +0x0077, +0x00A6 | Room 36 entry | None | 7 |
| Room 37 local 205 | +0x0000 | Room 37 object 604 Verb 0x45, Room 37 object 604 Verb 0x46 | None | 7 |
| Room 37 object 601 Verb 0x37 | +0x000B | None resolved | None | 1 |
| Room 38 local 202 | +0x0000 | Room 38 object 610 Verb 0x3C, Room 38 object 610 Verb 0x3D | None | 9 |
| Room 38 local 203 | +0x0000 | Room 38 object 616 Verb 0x3B, Room 38 object 617 Verb 0x3A | None | 8 |
| Room 38 object 611 Verb 0x36 | +0x0005 | None resolved | None | 1 |
| Room 41 local 204 | +0x0006 | Room 41 object 633 Verb 0x41, Room 41 object 633 Verb 0x4B | None | 12 |
| Room 41 local 205 | +0x0000 | Room 41 object 633 Verb 0x42 | None | 2 |
| Room 41 local 213 | +0x0000 | Room 41 local 204, Room 41 object 633 Verb 0x56 | None | 7 |
| Room 41 object 632 Verb 0x36 | +0x0000 | None resolved | None | 0 |
| Room 45 object 656 Verb 0x36 | +0x0005 | None resolved | None | 1 |
| Room 46 local 201 | +0x0000 | Room 46 object 682 Verb 0x3A, Room 46 object 683 Verb 0x3A, Room 46 object 684 Verb 0x3A, Room 46 object 685 Verb 0x3A | None | 2 |
| Room 46 local 202 | +0x0000 | Room 46 object 682 Verb 0x3B, Room 46 object 683 Verb 0x3B, Room 46 object 684 Verb 0x3B | None | 6 |
| Room 46 local 203 | +0x0000 | Room 46 object 685 Verb 0x3B | None | 5 |
| Room 47 local 205 | +0x000E, +0x01B7 | Room 47 entry | None | 6 |
| Room 49 object 704 Verb 0x38 | +0x0000 | None resolved | loadRoomWithEgo 0x02AA, 0x2E, walkTo=(392, 110) | 0 |
| Room 62 local 200 | +0x0000 | Room 62 object 810 Verb 0x36, Room 62 object 811 Verb 0x36 | None | 3 |
| Room 62 local 201 | +0x0000 | Room 62 object 808 Verb 0x3D | None | 4 |
| Room 62 local 202 | +0x0000 | Room 62 object 808 Verb 0x3C | None | 4 |
| Room 62 local 203 | +0x0000 | Room 62 object 809 Verb 0x3C | None | 4 |
| Room 62 local 204 | +0x0000 | Room 62 object 809 Verb 0x3D | None | 4 |
| Room 62 local 207 | +0x0000 | Room 62 object 808 Verb 0x3C, Room 62 object 809 Verb 0x3C | None | 3 |
| Room 62 local 208 | +0x0000 | Room 62 object 808 Verb 0x3D, Room 62 object 809 Verb 0x3D | None | 3 |
| Room 66 object 833 Verb 0x56 | +0x0000 | None resolved | None | 3 |
