# Broad replay audit

75 scene starts and 216 dialogue/reaction previews. Coverage remains incomplete; Needs validation lists unresolved scene branches and shared helpers.

Decoded input SHA256: `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`. 363 roots tested; 216 candidate owners, 235 cutscene sites; 139 distinct sites reached by completed previews; 123 unresolved static call edges. These are starts/sites, not a count of unique narrative scenes.

Fresh boot seed 0; catalog visit; direct source verb or entry; no variable, inventory, class or position writes. Probe limit 3000 frames; missing starts excluded after the same ordinary 32-frame arrival/input window used by the app. A successful preview must open a scene, execute matching end boundaries, settle without an open scene/movement/talk and report no unsupported execution. Completion proves this explorer preview, not full original-game prerequisites or ScummVM parity. Full source traces and uncertainties remain in ignored Exe/VerificationSession-20261001/replay-audit.

| Status | Recipes |
|---|---|
| No cutscene reached from this start | 19 |
| Validated state preview | 289 |
| Incomplete: prerequisites, waits or executor require review | 45 |
| Brief input-suppression scope: not an independent replay | 10 |

| Trigger | Result | Frames | Reached scene boundaries |
|---|---|---|---|
| Room 02 entry | No cutscene reached from this start | 32 |  |
| Room 02 object 153 Verb 0x38 | Validated state preview | 103 | cutscene at Room 02 Object 153 Verb 0x38 +0x0009 runs script 2; endCutscene at Room 02 Object 153 Verb 0x38 +0x002A runs script 3 |
| Room 02 object 154 Verb 0x36 | Validated state preview | 68 | cutscene at Room 02 Object 154 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 02 Object 154 Verb 0x36 +0x0056 runs script 3 |
| Room 02 object 154 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 02 object 156 Verb 0x3A | Validated state preview | 228 | cutscene at Script 49 +0x002C runs script 2; endCutscene at Script 49 +0x029F runs script 3 |
| Room 02 object 156 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 03 object 165 Verb 0x56 | Validated state preview | 72 | cutscene at Room 03 Object 165 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 03 Object 165 Verb 0x56 +0x0040 runs script 3 |
| Room 04 object 169 Verb 0x37 | Validated state preview | 93 | cutscene at Room 04 Object 169 Verb 0x37 +0x0000 runs script 2; endCutscene at Room 04 Object 169 Verb 0x37 +0x003B runs script 3 |
| Room 04 object 170 Verb 0x37 | Validated state preview | 119 | cutscene at Room 04 Object 170 Verb 0x37 +0x0000 runs script 2; endCutscene at Room 04 Object 170 Verb 0x37 +0x003B runs script 3 |
| Room 04 object 171 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01E5 runs script 3 |
| Room 04 object 171 Verb 0x3A | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 171 Verb 0x3B | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 171 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01E5 runs script 3 |
| Room 04 object 172 Verb 0x3A | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x3B | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01E5 runs script 3 |
| Room 04 object 173 Verb 0x3A | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x3B | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01E5 runs script 3 |
| Room 04 object 174 Verb 0x3A | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x3B | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 175 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x36 | Validated state preview | 200 | cutscene at Room 04 local script 202 +0x0000 runs script 2; endCutscene at Room 04 local script 202 +0x0167 runs script 3 |
| Room 04 object 176 Verb 0x3A | Validated state preview | 72 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x3B | Validated state preview | 72 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 177 Verb 0x36 | Validated state preview | 112 | cutscene at Room 04 Object 177 Verb 0x36 +0x0017 runs script 2; endCutscene at Room 04 Object 177 Verb 0x36 +0x0083 runs script 3 |
| Room 04 object 177 Verb 0x3A | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 04 local script 209 +0x0000 runs script 2 |
| Room 04 object 177 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 177 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 197 Verb 0x56 | Validated state preview | 73 | cutscene at Room 04 Object 197 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 04 Object 197 Verb 0x56 +0x0041 runs script 3 |
| Room 06 object 205 Verb 0x36 | Validated state preview | 72 | cutscene at Room 06 local script 201 +0x0000 runs script 2; endCutscene at Room 06 local script 201 +0x004E runs script 3 |
| Room 06 object 205 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 206 Verb 0x3E | Validated state preview | 67 | cutscene at Room 06 local script 211 +0x0000 runs script 2; endCutscene at Room 06 local script 211 +0x0048 runs script 3 |
| Room 06 object 206 Verb 0x3F | Validated state preview | 95 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 206 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 207 Verb 0x3E | Validated state preview | 95 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 207 Verb 0x3F | Validated state preview | 78 | cutscene at Room 06 local script 212 +0x0000 runs script 2; endCutscene at Room 06 local script 212 +0x0058 runs script 3 |
| Room 06 object 207 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 209 Verb 0x36 | Validated state preview | 118 | cutscene at Room 06 Object 209 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 06 Object 209 Verb 0x36 +0x007D runs script 3 |
| Room 06 object 209 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 210 Verb 0x3E | Validated state preview | 95 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 210 Verb 0x3F | Validated state preview | 78 | cutscene at Room 06 local script 212 +0x0000 runs script 2; endCutscene at Room 06 local script 212 +0x0058 runs script 3 |
| Room 06 object 210 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 211 Verb 0x3E | Validated state preview | 67 | cutscene at Room 06 local script 211 +0x0000 runs script 2; endCutscene at Room 06 local script 211 +0x0048 runs script 3 |
| Room 06 object 211 Verb 0x3F | Validated state preview | 95 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 211 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 214 Verb 0x36 | Validated state preview | 74 | cutscene at Room 06 local script 215 +0x0000 runs script 2; endCutscene at Room 06 local script 215 +0x00A7 runs script 3 |
| Room 06 object 214 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 255 Verb 0x38 | Validated state preview | 2864 | cutscene at Script 46 +0x0000 runs script 2; endCutscene at Script 46 +0x1421 runs script 3 |
| Room 08 object 256 Verb 0x36 | Validated state preview | 139 | cutscene at Room 08 Object 256 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 256 Verb 0x36 +0x007A runs script 3 |
| Room 08 object 256 Verb 0x3E | Validated state preview | 80 | cutscene at Room 08 local script 208 +0x0000 runs script 2; endCutscene at Room 08 local script 208 +0x004F runs script 3 |
| Room 08 object 256 Verb 0x3F | Validated state preview | 101 | cutscene at Room 08 local script 212 +0x000B runs script 2; endCutscene at Room 08 local script 212 +0x0062 runs script 3 |
| Room 08 object 256 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 257 Verb 0x36 | Validated state preview | 150 | cutscene at Room 08 Object 257 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 257 Verb 0x36 +0x0088 runs script 3 |
| Room 08 object 257 Verb 0x3E | Validated state preview | 101 | cutscene at Room 08 local script 213 +0x0008 runs script 2; endCutscene at Room 08 local script 213 +0x0066 runs script 3 |
| Room 08 object 257 Verb 0x3F | Validated state preview | 80 | cutscene at Room 08 local script 209 +0x0000 runs script 2; endCutscene at Room 08 local script 209 +0x007B runs script 3 |
| Room 08 object 257 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 258 Verb 0x36 | Validated state preview | 208 | cutscene at Room 08 Object 258 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 258 Verb 0x36 +0x009E runs script 3 |
| Room 08 object 258 Verb 0x3E | Validated state preview | 101 | cutscene at Room 08 local script 215 +0x0008 runs script 2; endCutscene at Room 08 local script 215 +0x0066 runs script 3 |
| Room 08 object 258 Verb 0x3F | Validated state preview | 121 | cutscene at Room 08 local script 209 +0x0000 runs script 2; cutscene at Room 08 local script 216 +0x000B runs script 2; endCutscene at Room 08 local script 216 +0x0062 runs script 3; endCutscene at Room 08 local script 209 +0x007B runs script 3 |
| Room 08 object 258 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 268 Verb 0x37 | Validated state preview | 146 | cutscene at Room 09 local script 214 +0x000C runs script 2; endCutscene at Room 09 local script 214 +0x0094 runs script 3 |
| Room 09 object 270 Verb 0x36 | Validated state preview | 100 | cutscene at Room 09 Object 270 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 09 Object 270 Verb 0x36 +0x0015 runs script 3 |
| Room 09 object 270 Verb 0x3A | Validated state preview | 1936 | cutscene at Script 47 +0x0000 runs script 2; endCutscene at Script 47 +0x0D19 runs script 3 |
| Room 09 object 270 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 271 Verb 0x38 | Validated state preview | 73 | cutscene at Room 09 Object 271 Verb 0x38 +0x0005 runs script 2; endCutscene at Room 09 Object 271 Verb 0x38 +0x006F runs script 3 |
| Room 09 object 272 Verb 0x36 | Validated state preview | 86 | cutscene at Room 09 local script 209 +0x0000 runs script 2; endCutscene at Room 09 local script 209 +0x0190 runs script 3 |
| Room 09 object 272 Verb 0x52 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 104 +0x0000 runs script 2; endCutscene at Script 104 +0x06FC runs script 3; cutscene at Script 29 +0x0000 runs script 2 |
| Room 09 object 272 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 273 Verb 0x4D | Validated state preview | 96 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BC runs script 3 |
| Room 09 object 273 Verb 0x4F | Validated state preview | 96 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BC runs script 3 |
| Room 09 object 273 Verb 0x51 | Validated state preview | 96 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BC runs script 3 |
| Room 09 object 273 Verb 0x56 | Validated state preview | 96 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BC runs script 3 |
| Room 09 object 274 Verb 0x36 | Validated state preview | 171 | cutscene at Script 105 +0x0000 runs script 2; endCutscene at Script 105 +0x0209 runs script 3 |
| Room 09 object 275 Verb 0x56 | Validated state preview | 60 | cutscene at Room 09 local script 221 +0x0000 runs script 2; endCutscene at Room 09 local script 221 +0x00F5 runs script 3 |
| Room 10 entry | No cutscene reached from this start | 32 |  |
| Room 10 object 295 Verb 0x38 | Validated state preview | 334 | cutscene at Script 52 +0x0000 runs script 2; endCutscene at Script 52 +0x017C runs script 3 |
| Room 10 object 296 Verb 0x3A | Validated state preview | 97 | cutscene at Room 10 local script 212 +0x0000 runs script 2; endCutscene at Room 10 local script 212 +0x007C runs script 3 |
| Room 10 object 296 Verb 0x3B | Validated state preview | 66 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 296 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 297 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 297 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 298 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 298 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 299 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 299 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 300 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 300 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 12 object 322 Verb 0x38 | Validated state preview | 158 | cutscene at Room 12 Object 322 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 12 Object 322 Verb 0x38 +0x0077 runs script 3 |
| Room 13 entry | No cutscene reached from this start | 32 |  |
| Room 13 object 323 Verb 0x38 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 54 +0x0000 runs script 2; endCutscene at Script 54 +0x00C4 runs script 3; cutscene at Script 29 +0x0000 runs script 2 |
| Room 13 object 324 Verb 0x36 | Validated state preview | 51 | cutscene at Script 107 +0x0000 runs script 2; endCutscene at Script 107 +0x00D6 runs script 3 |
| Room 13 object 325 Verb 0x36 | Validated state preview | 107 | cutscene at Room 13 Object 325 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 13 Object 325 Verb 0x36 +0x005B runs script 3 |
| Room 13 object 325 Verb 0x3A | Validated state preview | 111 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 13 object 325 Verb 0x41 | Validated state preview | 106 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 13 object 325 Verb 0x42 | Validated state preview | 75 | cutscene at Room 13 local script 202 +0x0000 runs script 2; endCutscene at Room 13 local script 202 +0x00F1 runs script 3 |
| Room 13 object 325 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 15 entry | No cutscene reached from this start | 32 |  |
| Room 15 object 338 Verb 0x38 | Validated state preview | 82 | cutscene at Room 15 Object 338 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 15 Object 338 Verb 0x38 +0x0020 runs script 3 |
| Room 16 object 350 Verb 0x47 | Validated state preview | 117 | cutscene at Room 16 local script 204 +0x0072 runs script 2; endCutscene at Room 16 local script 204 +0x0128 runs script 3 |
| Room 16 object 350 Verb 0x48 | Validated state preview | 71 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 16 object 350 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 entry | No cutscene reached from this start | 32 |  |
| Room 17 object 357 Verb 0x3A | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 17 local script 206 +0x0000 runs script 2 |
| Room 17 object 357 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 357 Verb 0x56 | Validated state preview | 72 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 358 Verb 0x3A | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 17 local script 206 +0x0000 runs script 2 |
| Room 17 object 358 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 358 Verb 0x56 | Validated state preview | 72 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 367 Verb 0x36 | Validated state preview | 757 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x0316 runs script 3 |
| Room 18 object 368 Verb 0x38 | Validated state preview | 757 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x0316 runs script 3 |
| Room 18 object 369 Verb 0x36 | Validated state preview | 570 | cutscene at Script 62 +0x0000 runs script 2; endCutscene at Script 62 +0x0018 runs script 3; cutscene at Script 63 +0x0000 runs script 2; endCutscene at Script 63 +0x001C runs script 3; cutscene at Room 20 local script 207 +0x000B runs script 2; endCutscene at Room 20 local script 207 +0x0548 runs script 3; cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 18 object 369 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 370 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 218 | cutscene at Script 65 +0x0000 runs script 2 |
| Room 18 object 370 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 371 Verb 0x36 | Validated state preview | 607 | cutscene at Script 56 +0x0000 runs script 2; endCutscene at Script 56 +0x0025 runs script 3; cutscene at Script 57 +0x0000 runs script 2; endCutscene at Script 57 +0x0029 runs script 3; cutscene at Room 20 local script 207 +0x000B runs script 2; endCutscene at Room 20 local script 207 +0x0548 runs script 3; cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 18 object 371 Verb 0x3A | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 371 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 372 Verb 0x36 | Validated state preview | 655 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x0316 runs script 3 |
| Room 18 object 372 Verb 0x56 | Validated state preview | 757 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x0316 runs script 3 |
| Room 18 object 374 Verb 0x36 | Validated state preview | 757 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x0316 runs script 3 |
| Room 20 object 386 Verb 0x36 | Validated state preview | 99 | cutscene at Room 20 local script 202 +0x0000 runs script 2; endCutscene at Room 20 local script 202 +0x00AF runs script 3 |
| Room 20 object 386 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 387 Verb 0x36 | Validated state preview | 99 | cutscene at Room 20 local script 202 +0x0000 runs script 2; endCutscene at Room 20 local script 202 +0x00AF runs script 3 |
| Room 20 object 387 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 388 Verb 0x36 | Validated state preview | 116 | cutscene at Script 60 +0x0000 runs script 2; endCutscene at Script 60 +0x0018 runs script 3; cutscene at Script 61 +0x0010 runs script 2; endCutscene at Script 61 +0x002C runs script 3 |
| Room 20 object 388 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 389 Verb 0x36 | Validated state preview | 114 | cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 20 object 389 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 390 Verb 0x47 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 212 +0x0000 runs script 2 |
| Room 20 object 390 Verb 0x48 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 206 +0x0000 runs script 2 |
| Room 20 object 390 Verb 0x49 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 211 +0x0000 runs script 2 |
| Room 20 object 390 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 213 +0x0000 runs script 2 |
| Room 20 object 391 Verb 0x47 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 212 +0x0000 runs script 2 |
| Room 20 object 391 Verb 0x48 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 206 +0x0000 runs script 2 |
| Room 20 object 391 Verb 0x49 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 211 +0x0000 runs script 2 |
| Room 20 object 391 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 20 local script 213 +0x0000 runs script 2 |
| Room 22 entry | No cutscene reached from this start | 32 |  |
| Room 22 object 403 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 22 local script 205 +0x0000 runs script 2 |
| Room 22 object 403 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 404 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 22 local script 204 +0x0000 runs script 2 |
| Room 22 object 404 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x36 | Validated state preview | 816 | cutscene at Script 108 +0x0000 runs script 2; endCutscene at Script 108 +0x04E6 runs script 3 |
| Room 22 object 405 Verb 0x3A | Validated state preview | 61 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x45 | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x46 | Validated state preview | 70 | cutscene at Room 22 local script 214 +0x0000 runs script 2; endCutscene at Room 22 local script 214 +0x0064 runs script 3 |
| Room 22 object 405 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 23 entry | No cutscene reached from this start | 32 |  |
| Room 23 object 422 Verb 0x47 | Validated state preview | 72 | cutscene at Room 23 local script 229 +0x0000 runs script 2; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x007B runs script 3; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 422 Verb 0x49 | Validated state preview | 160 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x0252 runs script 3 |
| Room 23 object 422 Verb 0x56 | Validated state preview | 76 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 423 Verb 0x47 | Validated state preview | 72 | cutscene at Room 23 local script 229 +0x0000 runs script 2; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x007B runs script 3; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 423 Verb 0x49 | Validated state preview | 160 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x0252 runs script 3 |
| Room 23 object 423 Verb 0x56 | Validated state preview | 76 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 424 Verb 0x47 | Validated state preview | 72 | cutscene at Room 23 local script 229 +0x0000 runs script 2; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x007B runs script 3; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 424 Verb 0x49 | Validated state preview | 160 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x0252 runs script 3 |
| Room 23 object 424 Verb 0x56 | Validated state preview | 76 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 425 Verb 0x47 | Validated state preview | 72 | cutscene at Room 23 local script 229 +0x0000 runs script 2; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x007B runs script 3; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 425 Verb 0x49 | Validated state preview | 160 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x0252 runs script 3 |
| Room 23 object 425 Verb 0x56 | Validated state preview | 76 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 24 entry | No cutscene reached from this start | 32 |  |
| Room 24 object 431 Verb 0x3A | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 214 +0x0000 runs script 2 |
| Room 24 object 431 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 431 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 432 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 204 +0x0000 runs script 2; cutscene at Room 24 local script 215 +0x0000 runs script 2 |
| Room 24 object 432 Verb 0x3E | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 213 +0x0000 runs script 2 |
| Room 24 object 432 Verb 0x3F | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 212 +0x0018 runs script 2 |
| Room 24 object 432 Verb 0x47 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 Object 432 Verb 0x47 +0x0000 runs script 2 |
| Room 24 object 432 Verb 0x49 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 Object 432 Verb 0x49 +0x0000 runs script 2 |
| Room 24 object 432 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 Object 432 Verb 0x56 +0x0000 runs script 2 |
| Room 24 object 433 Verb 0x38 | Validated state preview | 64 | cutscene at Room 24 Object 433 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 24 Object 433 Verb 0x38 +0x000A runs script 3 |
| Room 24 object 433 Verb 0x3E | Validated state preview | 310 | cutscene at Script 70 +0x0000 runs script 2; endCutscene at Script 70 +0x01A2 runs script 3 |
| Room 24 object 433 Verb 0x3F | Validated state preview | 71 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x47 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x49 | Validated state preview | 84 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 434 Verb 0x47 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 215 +0x0000 runs script 2 |
| Room 24 object 434 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 24 local script 215 +0x0000 runs script 2 |
| Room 25 entry | No cutscene reached from this start | 32 |  |
| Room 25 object 446 Verb 0x37 | No cutscene reached from this start | 32 |  |
| Room 25 object 447 Verb 0x36 | No cutscene reached from this start | 32 |  |
| Room 25 object 447 Verb 0x3C | Validated state preview | 124 | cutscene at Room 25 Object 447 Verb 0x3C +0x0000 runs script 2; endCutscene at Room 25 Object 447 Verb 0x3C +0x005F runs script 3 |
| Room 25 object 447 Verb 0x56 | Validated state preview | 101 | cutscene at Room 25 Object 447 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 25 Object 447 Verb 0x56 +0x0041 runs script 3 |
| Room 26 object 464 Verb 0x36 | Validated state preview | 311 | cutscene at Room 26 local script 211 +0x0000 runs script 2; endCutscene at Room 26 local script 211 +0x0149 runs script 3 |
| Room 26 object 464 Verb 0x3E | Validated state preview | 66 | cutscene at Room 26 Object 464 Verb 0x3E +0x0000 runs script 2; endCutscene at Room 26 Object 464 Verb 0x3E +0x003C runs script 3 |
| Room 26 object 464 Verb 0x4D | Validated state preview | 398 | cutscene at Room 26 local script 202 +0x0000 runs script 2; endCutscene at Room 26 local script 202 +0x041D runs script 3 |
| Room 26 object 464 Verb 0x56 | Validated state preview | 77 | cutscene at Room 26 local script 209 +0x0000 runs script 2; endCutscene at Room 26 local script 209 +0x0118 runs script 3 |
| Room 28 entry | No cutscene reached from this start | 32 |  |
| Room 28 object 465 Verb 0x38 | Validated state preview | 65 | cutscene at Script 72 +0x0000 runs script 2; endCutscene at Script 72 +0x0074 runs script 3 |
| Room 28 object 466 Verb 0x36 | Validated state preview | 739 | cutscene at Script 75 +0x0038 runs script 2; endCutscene at Script 75 +0x0419 runs script 3 |
| Room 28 object 466 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 466 Verb 0x43 | Validated state preview | 94 | cutscene at Room 28 local script 210 +0x0000 runs script 2; endCutscene at Room 28 local script 210 +0x0099 runs script 3 |
| Room 28 object 466 Verb 0x49 | Validated state preview | 234 | cutscene at Room 28 local script 215 +0x0000 runs script 2; endCutscene at Room 28 local script 215 +0x01A2 runs script 3 |
| Room 28 object 466 Verb 0x56 | Validated state preview | 75 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 467 Verb 0x36 | Validated state preview | 739 | cutscene at Script 75 +0x0038 runs script 2; endCutscene at Script 75 +0x0419 runs script 3 |
| Room 28 object 467 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 467 Verb 0x43 | Validated state preview | 94 | cutscene at Room 28 local script 210 +0x0000 runs script 2; endCutscene at Room 28 local script 210 +0x0099 runs script 3 |
| Room 28 object 467 Verb 0x49 | Validated state preview | 234 | cutscene at Room 28 local script 215 +0x0000 runs script 2; endCutscene at Room 28 local script 215 +0x01A2 runs script 3 |
| Room 28 object 467 Verb 0x56 | Validated state preview | 75 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 468 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 468 Verb 0x44 | Validated state preview | 126 | cutscene at Room 28 local script 209 +0x0000 runs script 2; endCutscene at Room 28 local script 209 +0x00C6 runs script 3 |
| Room 28 object 468 Verb 0x49 | Validated state preview | 77 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 468 Verb 0x56 | Validated state preview | 75 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 469 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 469 Verb 0x44 | Validated state preview | 126 | cutscene at Room 28 local script 209 +0x0000 runs script 2; endCutscene at Room 28 local script 209 +0x00C6 runs script 3 |
| Room 28 object 469 Verb 0x49 | Validated state preview | 77 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 469 Verb 0x56 | Validated state preview | 75 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 470 Verb 0x36 | Validated state preview | 642 | cutscene at Script 109 +0x0016 runs script 2; endCutscene at Script 109 +0x03C4 runs script 3 |
| Room 28 object 470 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 470 Verb 0x3D | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 470 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 471 Verb 0x36 | Validated state preview | 642 | cutscene at Script 109 +0x0016 runs script 2; endCutscene at Script 109 +0x03C4 runs script 3 |
| Room 28 object 471 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 471 Verb 0x3D | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 471 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 472 Verb 0x36 | Validated state preview | 642 | cutscene at Script 109 +0x0016 runs script 2; endCutscene at Script 109 +0x03C4 runs script 3 |
| Room 28 object 472 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 472 Verb 0x3D | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 472 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 473 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 473 Verb 0x3C | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 473 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 474 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 474 Verb 0x3C | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 474 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 475 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 475 Verb 0x3C | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 28 object 475 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 28 local script 218 +0x0000 runs script 2 |
| Room 30 object 503 Verb 0x38 | Validated state preview | 67 | cutscene at Room 30 Object 503 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 503 Verb 0x38 +0x001E runs script 3 |
| Room 30 object 504 Verb 0x38 | Validated state preview | 53 | cutscene at Room 30 Object 504 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 504 Verb 0x38 +0x001E runs script 3 |
| Room 30 object 505 Verb 0x38 | Validated state preview | 63 | cutscene at Room 30 Object 505 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 505 Verb 0x38 +0x001B runs script 3 |
| Room 30 object 507 Verb 0x38 | Validated state preview | 62 | cutscene at Script 77 +0x0000 runs script 2; endCutscene at Script 77 +0x0043 runs script 3 |
| Room 30 object 508 Verb 0x38 | Validated state preview | 105 | cutscene at Script 82 +0x0000 runs script 2; endCutscene at Script 82 +0x0082 runs script 3 |
| Room 30 object 509 Verb 0x38 | Validated state preview | 101 | cutscene at Script 79 +0x0000 runs script 2; endCutscene at Script 79 +0x0084 runs script 3 |
| Room 30 object 510 Verb 0x38 | Validated state preview | 110 | cutscene at Script 80 +0x0000 runs script 2; endCutscene at Script 80 +0x0046 runs script 3 |
| Room 30 object 511 Verb 0x38 | Validated state preview | 65 | cutscene at Room 30 Object 511 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 511 Verb 0x38 +0x0040 runs script 3 |
| Room 30 object 512 Verb 0x38 | Validated state preview | 51 | cutscene at Room 30 Object 512 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 512 Verb 0x38 +0x0039 runs script 3 |
| Room 30 object 513 Verb 0x38 | Validated state preview | 51 | cutscene at Room 30 Object 513 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 513 Verb 0x38 +0x0039 runs script 3 |
| Room 30 object 514 Verb 0x38 | Validated state preview | 54 | cutscene at Room 30 Object 514 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 514 Verb 0x38 +0x001B runs script 3 |
| Room 30 object 515 Verb 0x38 | Validated state preview | 57 | cutscene at Room 30 Object 515 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 515 Verb 0x38 +0x001B runs script 3 |
| Room 30 object 516 Verb 0x38 | Validated state preview | 57 | cutscene at Room 30 Object 516 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 516 Verb 0x38 +0x001B runs script 3 |
| Room 30 object 517 Verb 0x38 | Validated state preview | 49 | cutscene at Script 84 +0x0000 runs script 2; endCutscene at Script 84 +0x003A runs script 3 |
| Room 30 object 518 Verb 0x38 | Validated state preview | 49 | cutscene at Script 84 +0x0000 runs script 2; endCutscene at Script 84 +0x003A runs script 3 |
| Room 31 object 521 Verb 0x38 | Validated state preview | 45 | cutscene at Script 83 +0x0000 runs script 2; endCutscene at Script 83 +0x0037 runs script 3 |
| Room 31 object 523 Verb 0x36 | Validated state preview | 118 | cutscene at Room 31 local script 200 +0x0000 runs script 2; endCutscene at Room 31 local script 200 +0x015D runs script 3 |
| Room 31 object 523 Verb 0x45 | Validated state preview | 71 | cutscene at Room 31 local script 203 +0x0000 runs script 2; endCutscene at Room 31 local script 203 +0x0113 runs script 3 |
| Room 31 object 523 Verb 0x46 | Validated state preview | 69 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 523 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 524 Verb 0x45 | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 524 Verb 0x46 | Validated state preview | 71 | cutscene at Room 31 local script 203 +0x0000 runs script 2; endCutscene at Room 31 local script 203 +0x0113 runs script 3 |
| Room 31 object 524 Verb 0x56 | Validated state preview | 73 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 525 Verb 0x36 | Validated state preview | 129 | cutscene at Script 111 +0x0000 runs script 2; endCutscene at Script 111 +0x00FA runs script 3 |
| Room 31 object 525 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 31 local script 204 +0x0000 runs script 2 |
| Room 32 object 537 Verb 0x38 | Validated state preview | 108 | cutscene at Room 32 Object 537 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 32 Object 537 Verb 0x38 +0x003F runs script 3 |
| Room 32 object 538 Verb 0x38 | Validated state preview | 107 | cutscene at Script 81 +0x0000 runs script 2; endCutscene at Script 81 +0x004B runs script 3 |
| Room 33 object 543 Verb 0x38 | Validated state preview | 107 | cutscene at Room 33 Object 543 Verb 0x38 +0x0010 runs script 2; endCutscene at Room 33 Object 543 Verb 0x38 +0x0046 runs script 3 |
| Room 33 object 544 Verb 0x41 | Validated state preview | 72 | cutscene at Room 33 local script 205 +0x0000 runs script 2; endCutscene at Room 33 local script 205 +0x00B0 runs script 3 |
| Room 33 object 544 Verb 0x42 | Validated state preview | 73 | cutscene at Room 33 local script 205 +0x0000 runs script 2; cutscene at Room 33 local script 200 +0x0006 runs script 2; endCutscene at Room 33 local script 200 +0x005E runs script 3; endCutscene at Room 33 local script 205 +0x00B0 runs script 3 |
| Room 33 object 544 Verb 0x56 | Validated state preview | 78 | cutscene at Room 33 local script 205 +0x0000 runs script 2; endCutscene at Room 33 local script 205 +0x00B0 runs script 3 |
| Room 34 entry | No cutscene reached from this start | 32 |  |
| Room 34 object 557 Verb 0x37 | No cutscene reached from this start | 32 |  |
| Room 34 object 559 Verb 0x37 | Validated state preview | 1158 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0618 runs script 3 |
| Room 34 object 559 Verb 0x38 | No cutscene reached from this start | 32 |  |
| Room 34 object 560 Verb 0x37 | Validated state preview | 1158 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0618 runs script 3 |
| Room 34 object 561 Verb 0x36 | Validated state preview | 126 | cutscene at Script 112 +0x0026 runs script 2; endCutscene at Script 112 +0x008F runs script 3 |
| Room 34 object 561 Verb 0x43 | Validated state preview | 1019 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05CE runs script 3 |
| Room 34 object 561 Verb 0x44 | Validated state preview | 1044 | cutscene at Room 34 local script 201 +0x0000 runs script 2; endCutscene at Room 34 local script 201 +0x0030 runs script 3; cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05CE runs script 3 |
| Room 34 object 561 Verb 0x4C | Validated state preview | 1019 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05CE runs script 3 |
| Room 34 object 561 Verb 0x56 | Validated state preview | 1019 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05CE runs script 3 |
| Room 34 object 562 Verb 0x3A | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 34 local script 218 +0x0000 runs script 2 |
| Room 34 object 562 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 562 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 586 Verb 0x37 | Validated state preview | 1158 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0618 runs script 3 |
| Room 34 object 586 Verb 0x43 | Validated state preview | 66 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 586 Verb 0x44 | Validated state preview | 66 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 586 Verb 0x4C | Validated state preview | 74 | cutscene at Room 34 Object 586 Verb 0x4C +0x0000 runs script 2; endCutscene at Room 34 Object 586 Verb 0x4C +0x0046 runs script 3 |
| Room 34 object 586 Verb 0x4D | Validated state preview | 619 | cutscene at Room 34 local script 209 +0x0000 runs script 2; endCutscene at Room 34 local script 209 +0x03C0 runs script 3 |
| Room 34 object 586 Verb 0x56 | Validated state preview | 69 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 35 entry | No cutscene reached from this start | 32 |  |
| Room 35 object 588 Verb 0x38 | Validated state preview | 66 | cutscene at Room 35 local script 210 +0x001A runs script 2; endCutscene at Room 35 local script 210 +0x0085 runs script 3 |
| Room 35 object 589 Verb 0x3A | Validated state preview | 363 | cutscene at Room 35 local script 212 +0x0000 runs script 2; cutscene at Room 35 local script 207 +0x0006 runs script 2; endCutscene at Room 35 local script 207 +0x002A runs script 3; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07FC runs script 3 |
| Room 35 object 589 Verb 0x3B | Validated state preview | 58 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3 |
| Room 35 object 589 Verb 0x56 | Validated state preview | 346 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07FC runs script 3 |
| Room 35 object 596 Verb 0x38 | Validated state preview | 66 | cutscene at Room 35 local script 210 +0x001A runs script 2; endCutscene at Room 35 local script 210 +0x0085 runs script 3 |
| Room 35 object 596 Verb 0x56 | Validated state preview | 346 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07FC runs script 3 |
| Room 36 entry | No cutscene reached from this start | 32 |  |
| Room 37 entry | No cutscene reached from this start | 32 |  |
| Room 37 object 601 Verb 0x37 | Validated state preview | 80 | cutscene at Room 37 Object 601 Verb 0x37 +0x000B runs script 2; endCutscene at Room 37 Object 601 Verb 0x37 +0x004B runs script 3 |
| Room 37 object 604 Verb 0x45 | Validated state preview | 71 | cutscene at Room 37 local script 205 +0x0000 runs script 2; endCutscene at Room 37 local script 205 +0x00BA runs script 3 |
| Room 37 object 604 Verb 0x46 | Validated state preview | 59 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 37 object 604 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 610 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 87 +0x0000 runs script 2 |
| Room 38 object 610 Verb 0x3C | Validated state preview | 70 | cutscene at Room 38 local script 202 +0x0000 runs script 2; endCutscene at Room 38 local script 202 +0x00E0 runs script 3 |
| Room 38 object 610 Verb 0x3D | Validated state preview | 65 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 610 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 611 Verb 0x36 | Validated state preview | 69 | cutscene at Room 38 Object 611 Verb 0x36 +0x0005 runs script 2; endCutscene at Room 38 Object 611 Verb 0x36 +0x006D runs script 3 |
| Room 38 object 616 Verb 0x3A | Validated state preview | 62 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 616 Verb 0x3B | Validated state preview | 73 | cutscene at Room 38 local script 203 +0x0000 runs script 2; endCutscene at Room 38 local script 203 +0x00B2 runs script 3 |
| Room 38 object 616 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 617 Verb 0x3A | Validated state preview | 73 | cutscene at Room 38 local script 203 +0x0000 runs script 2; endCutscene at Room 38 local script 203 +0x00B2 runs script 3 |
| Room 38 object 617 Verb 0x3B | Validated state preview | 58 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 617 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 41 object 632 Verb 0x36 | Validated state preview | 1191 | cutscene at Room 41 Object 632 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 41 Object 632 Verb 0x36 +0x009B runs script 3 |
| Room 41 object 633 Verb 0x41 | Incomplete: prerequisites, waits or executor require review | 1686 | cutscene at Room 41 local script 213 +0x0000 runs script 2; cutscene at Room 41 local script 204 +0x0006 runs script 2; endCutscene at Room 41 local script 204 +0x031E runs script 3; cutscene at Script 90 +0x0000 runs script 2; endCutscene at Script 90 +0x0730 runs script 3 |
| Room 41 object 633 Verb 0x42 | Validated state preview | 68 | cutscene at Room 41 local script 205 +0x0000 runs script 2; endCutscene at Room 41 local script 205 +0x003E runs script 3 |
| Room 41 object 633 Verb 0x4B | Incomplete: prerequisites, waits or executor require review | 1680 | cutscene at Room 41 local script 213 +0x0000 runs script 2; cutscene at Room 41 local script 204 +0x0006 runs script 2; endCutscene at Room 41 local script 204 +0x031E runs script 3; cutscene at Script 90 +0x0000 runs script 2; endCutscene at Script 90 +0x0730 runs script 3 |
| Room 41 object 633 Verb 0x56 | Validated state preview | 75 | cutscene at Room 41 local script 213 +0x0000 runs script 2; endCutscene at Room 41 local script 213 +0x00A9 runs script 3 |
| Room 43 object 639 Verb 0x38 | No cutscene reached from this start | 32 |  |
| Room 43 object 640 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 94 +0x0000 runs script 2 |
| Room 43 object 640 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 43 object 643 Verb 0x3A | Validated state preview | 71 | cutscene at Script 91 +0x0022 runs script 2; endCutscene at Script 91 +0x0E83 runs script 3 |
| Room 43 object 643 Verb 0x3B | Validated state preview | 66 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 43 object 643 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 43 object 650 Verb 0x38 | Validated state preview | 923 | cutscene at Script 93 +0x0000 runs script 2; endCutscene at Script 93 +0x05FB runs script 3 |
| Room 44 object 652 Verb 0x38 | Validated state preview | 48 | cutscene at Script 92 +0x0000 runs script 2; endCutscene at Script 92 +0x0039 runs script 3 |
| Room 44 object 653 Verb 0x36 | Validated state preview | 858 | cutscene at Script 95 +0x0000 runs script 2; endCutscene at Script 95 +0x079D runs script 3 |
| Room 45 object 655 Verb 0x38 | Validated state preview | 49 | cutscene at Script 92 +0x0000 runs script 2; endCutscene at Script 92 +0x0039 runs script 3 |
| Room 45 object 656 Verb 0x36 | Validated state preview | 75 | cutscene at Room 45 Object 656 Verb 0x36 +0x0005 runs script 2; endCutscene at Room 45 Object 656 Verb 0x36 +0x0074 runs script 3 |
| Room 46 object 682 Verb 0x36 | Validated state preview | 44 | cutscene at Script 110 +0x0000 runs script 2; endCutscene at Script 110 +0x005B runs script 3 |
| Room 46 object 682 Verb 0x3A | Validated state preview | 71 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003F runs script 3 |
| Room 46 object 682 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 682 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 683 Verb 0x3A | Validated state preview | 71 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003F runs script 3 |
| Room 46 object 683 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 683 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 684 Verb 0x36 | Validated state preview | 42 | cutscene at Script 97 +0x0000 runs script 2; endCutscene at Script 97 +0x0042 runs script 3 |
| Room 46 object 684 Verb 0x3A | Validated state preview | 71 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003F runs script 3 |
| Room 46 object 684 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 684 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 685 Verb 0x3A | Validated state preview | 71 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003F runs script 3 |
| Room 46 object 685 Verb 0x3B | Validated state preview | 71 | cutscene at Room 46 local script 203 +0x0000 runs script 2; endCutscene at Room 46 local script 203 +0x00A4 runs script 3 |
| Room 46 object 685 Verb 0x56 | Validated state preview | 74 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 47 entry | Validated state preview | 348 | cutscene at Room 47 local script 205 +0x000E runs script 2; endCutscene at Room 47 local script 205 +0x019E runs script 3 |
| Room 47 object 697 Verb 0x36 | Validated state preview | 1801 | cutscene at Script 100 +0x0000 runs script 2; endCutscene at Script 100 +0x0A7B runs script 3 |
| Room 47 object 697 Verb 0x56 | Validated state preview | 375 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3; cutscene at Room 47 local script 205 +0x000E runs script 2; endCutscene at Room 47 local script 205 +0x019E runs script 3 |
| Room 49 object 704 Verb 0x38 | Validated state preview | 57 | cutscene at Room 49 Object 704 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 49 Object 704 Verb 0x38 +0x0027 runs script 3 |
| Room 49 object 707 Verb 0x36 | Validated state preview | 805 | cutscene at Script 99 +0x0000 runs script 2; endCutscene at Script 99 +0x050F runs script 3 |
| Room 49 object 707 Verb 0x56 | Validated state preview | 795 | cutscene at Script 99 +0x0000 runs script 2; endCutscene at Script 99 +0x050F runs script 3 |
| Room 50 object 721 Verb 0x38 | Validated state preview | 130 | cutscene at Script 89 +0x0000 runs script 2; endCutscene at Script 89 +0x0074 runs script 3 |
| Room 51 object 741 Verb 0x38 | Validated state preview | 157 | cutscene at Script 102 +0x0000 runs script 2; endCutscene at Script 102 +0x00E2 runs script 3 |
| Room 51 object 742 Verb 0x56 | Validated state preview | 41 | cutscene at Script 103 +0x0000 runs script 2; endCutscene at Script 103 +0x0077 runs script 3 |
| Room 51 object 743 Verb 0x36 | Validated state preview | 281 | cutscene at Script 105 +0x0000 runs script 2; endCutscene at Script 105 +0x0209 runs script 3 |
| Room 62 object 808 Verb 0x3C | Validated state preview | 78 | cutscene at Room 62 local script 207 +0x0000 runs script 2; endCutscene at Room 62 local script 207 +0x0058 runs script 3 |
| Room 62 object 808 Verb 0x3D | Validated state preview | 58 | cutscene at Room 62 local script 201 +0x0000 runs script 2; endCutscene at Room 62 local script 201 +0x007B runs script 3 |
| Room 62 object 808 Verb 0x56 | Validated state preview | 81 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 809 Verb 0x3C | Validated state preview | 58 | cutscene at Room 62 local script 203 +0x0000 runs script 2; endCutscene at Room 62 local script 203 +0x007B runs script 3 |
| Room 62 object 809 Verb 0x3D | Validated state preview | 78 | cutscene at Room 62 local script 208 +0x0000 runs script 2; endCutscene at Room 62 local script 208 +0x0057 runs script 3 |
| Room 62 object 809 Verb 0x56 | Validated state preview | 81 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 810 Verb 0x36 | Validated state preview | 73 | cutscene at Room 62 local script 200 +0x0000 runs script 2; endCutscene at Room 62 local script 200 +0x006D runs script 3 |
| Room 62 object 810 Verb 0x56 | Validated state preview | 75 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 811 Verb 0x36 | Validated state preview | 73 | cutscene at Room 62 local script 200 +0x0000 runs script 2; endCutscene at Room 62 local script 200 +0x006D runs script 3 |
| Room 63 object 820 Verb 0x56 | Validated state preview | 72 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 63 object 821 Verb 0x40 | Validated state preview | 82 | cutscene at Script 48 +0x0000 runs script 2; endCutscene at Script 48 +0x00C8 runs script 3 |
| Room 63 object 822 Verb 0x40 | Validated state preview | 82 | cutscene at Script 48 +0x0000 runs script 2; endCutscene at Script 48 +0x00C8 runs script 3 |
| Room 65 object 827 Verb 0x38 | Validated state preview | 62 | cutscene at Script 77 +0x0000 runs script 2; endCutscene at Script 77 +0x0043 runs script 3 |
| Room 65 object 828 Verb 0x40 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 78 +0x0000 runs script 2 |
| Room 65 object 828 Verb 0x56 | Validated state preview | 76 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 66 object 833 Verb 0x56 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Room 66 Object 833 Verb 0x56 +0x0000 runs script 2 |
