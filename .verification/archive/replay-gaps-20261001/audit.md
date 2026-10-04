# Incomplete replay diagnostics, 1 October

45 incomplete roots rerun with fresh seed-0 catalog/direct-verb setup and 3000 frames each. No variable, class, inventory, position or global-script injection. Decoded input SHA256 `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`.

Checkpoint evidence at 0/100/1000/2500/3000 frames; classifications describe reached stalls, not original-game prerequisites. The bound was not extended blindly and no root was promoted. Full snapshots/traces remain ignored in Exe/VerificationSession-20261001/replay-gap-probe-1.

| Observed family | Roots |
|---|---|
| Sound completion flag var0A4 remains nonzero | 38 |
| Following helper waits for script040 sound completion | 4 |
| Walk-box/script polling; source route or executor review required | 1 |
| Scope ended/stopped without all matching end boundaries; review required | 2 |

| Root | Observed family | Live pending sources at frame 3000 |
|---|---|---|
| Room 04 object 177 Verb 0x3A | Sound completion flag var0A4 remains nonzero | Room 04 local 203 +0x0017; script 040 +0x003D; Room 04 local 209 +0x0096 |
| Room 09 object 272 Verb 0x52 | Following helper waits for script040 sound completion | script 040 +0x003D; script 029 +0x00BE |
| Room 13 object 323 Verb 0x38 | Following helper waits for script040 sound completion | script 040 +0x003D; script 029 +0x00BE; Room 15 local 200 +0x0006 |
| Room 17 object 357 Verb 0x3A | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 17 local 206 +0x0080 |
| Room 17 object 358 Verb 0x3A | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 17 local 206 +0x0080 |
| Room 18 object 370 Verb 0x36 | Following helper waits for script040 sound completion | script 040 +0x003D |
| Room 20 object 390 Verb 0x47 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 212 +0x000C |
| Room 20 object 390 Verb 0x48 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 206 +0x000C |
| Room 20 object 390 Verb 0x49 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 211 +0x000C |
| Room 20 object 390 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 213 +0x000C |
| Room 20 object 391 Verb 0x47 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 212 +0x000C |
| Room 20 object 391 Verb 0x48 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 206 +0x000C |
| Room 20 object 391 Verb 0x49 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 211 +0x000C |
| Room 20 object 391 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 20 local 204 +0x00FD; Room 20 local 213 +0x000C |
| Room 22 object 403 Verb 0x36 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 22 local 205 +0x0038 |
| Room 22 object 404 Verb 0x36 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 22 local 204 +0x0038 |
| Room 24 object 431 Verb 0x3A | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 214 +0x0080 |
| Room 24 object 432 Verb 0x36 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 215 +0x00B7; Room 24 local 204 +0x0075 |
| Room 24 object 432 Verb 0x3E | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 216 +0x0146; Room 24 local 213 +0x0034 |
| Room 24 object 432 Verb 0x3F | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 216 +0x0146; Room 24 local 212 +0x0024 |
| Room 24 object 432 Verb 0x47 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 216 +0x0146; Room 24 object 432 Verb 0x47 +0x000C |
| Room 24 object 432 Verb 0x49 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 216 +0x0146; Room 24 object 432 Verb 0x49 +0x000C |
| Room 24 object 432 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 216 +0x0146; Room 24 object 432 Verb 0x56 +0x000C |
| Room 24 object 434 Verb 0x47 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 215 +0x00B7 |
| Room 24 object 434 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 24 local 215 +0x00B7 |
| Room 28 object 470 Verb 0x3D | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 470 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 471 Verb 0x3D | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 471 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 472 Verb 0x3D | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 472 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 473 Verb 0x3C | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 473 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 474 Verb 0x3C | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 474 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 475 Verb 0x3C | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 28 object 475 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 28 local 218 +0x00AA |
| Room 31 object 525 Verb 0x56 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 31 local 204 +0x0089 |
| Room 34 object 562 Verb 0x3A | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; Room 34 local 218 +0x0080 |
| Room 38 object 610 Verb 0x36 | Walk-box/script polling; source route or executor review required | script 039 +0x003D; Room 37 local 203 +0x002B; Room 37 local 200 +0x0006; script 087 +0x047C |
| Room 41 object 633 Verb 0x41 | Scope ended/stopped without all matching end boundaries; review required | script 039 +0x003D; Room 43 local 205 +0x003C |
| Room 41 object 633 Verb 0x4B | Scope ended/stopped without all matching end boundaries; review required | script 039 +0x003D; Room 43 local 205 +0x0053 |
| Room 43 object 640 Verb 0x36 | Following helper waits for script040 sound completion | script 040 +0x003D; Room 19 local 201 +0x001A; script 094 +0x007C |
| Room 65 object 828 Verb 0x40 | Sound completion flag var0A4 remains nonzero | script 040 +0x003D; script 078 +0x0092 |
| Room 66 object 833 Verb 0x56 | Sound completion flag var0A4 remains nonzero | Room 66 local 203 +0x0022; Room 66 local 204 +0x0022; Room 66 local 205 +0x0022; script 040 +0x003D; Room 66 local 208 +0x009B; Room 66 object 833 Verb 0x56 +0x0022 |
