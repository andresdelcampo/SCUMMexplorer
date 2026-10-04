# Broad replay audit

83 scene starts and 249 dialogue/reaction previews. Coverage remains incomplete; Needs validation lists unresolved scene branches and shared helpers.

Decoded input SHA256: `cc853c6cc56e431903885114c401db9dc7262189be67e68b1751595e8b87c77d`. 362 roots tested; 218 candidate owners, 237 cutscene sites; 169 distinct sites reached by completed previews; 128 unresolved static call edges. These are starts/sites, not a count of unique narrative scenes.

Fresh boot seed 0; catalog visit; direct source verb or entry; no variable, inventory, class or position writes. Probe limit 3000 frames; missing starts excluded after the same ordinary 32-frame arrival/input window used by the app. A successful preview must open a scene, execute matching end boundaries, settle without an open scene/movement/talk and report no unsupported execution. Completion proves this explorer preview, not full original-game prerequisites or ScummVM parity. Full source traces and uncertainties remain in .verification/english-catalog-20261004/audit.

| Status | Recipes |
|---|---|
| No cutscene reached from this start | 19 |
| Validated state preview | 330 |
| Incomplete: prerequisites, waits or executor require review | 3 |
| Brief input-suppression scope: not an independent replay | 10 |

| Trigger | Result | Frames | Reached scene boundaries |
|---|---|---|---|
| Room 02 entry | No cutscene reached from this start | 32 |  |
| Room 02 object 153 Verb 0x38 | Validated state preview | 103 | cutscene at Room 02 Object 153 Verb 0x38 +0x0009 runs script 2; endCutscene at Room 02 Object 153 Verb 0x38 +0x002A runs script 3 |
| Room 02 object 154 Verb 0x36 | Validated state preview | 70 | cutscene at Room 02 Object 154 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 02 Object 154 Verb 0x36 +0x0058 runs script 3 |
| Room 02 object 154 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 02 object 156 Verb 0x3A | Validated state preview | 483 | cutscene at Script 49 +0x0029 runs script 2; endCutscene at Script 49 +0x029A runs script 3 |
| Room 02 object 156 Verb 0x56 | Validated state preview | 96 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 03 object 165 Verb 0x56 | Validated state preview | 73 | cutscene at Room 03 Object 165 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 03 Object 165 Verb 0x56 +0x0042 runs script 3 |
| Room 04 object 169 Verb 0x37 | Validated state preview | 93 | cutscene at Room 04 Object 169 Verb 0x37 +0x0000 runs script 2; endCutscene at Room 04 Object 169 Verb 0x37 +0x003B runs script 3 |
| Room 04 object 170 Verb 0x37 | Validated state preview | 119 | cutscene at Room 04 Object 170 Verb 0x37 +0x0000 runs script 2; endCutscene at Room 04 Object 170 Verb 0x37 +0x003B runs script 3 |
| Room 04 object 171 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01DD runs script 3 |
| Room 04 object 171 Verb 0x3A | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 171 Verb 0x3B | Validated state preview | 107 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 171 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01DD runs script 3 |
| Room 04 object 172 Verb 0x3A | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x3B | Validated state preview | 107 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 172 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01DD runs script 3 |
| Room 04 object 173 Verb 0x3A | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x3B | Validated state preview | 107 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 173 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x36 | Validated state preview | 61 | cutscene at Room 04 local script 200 +0x0000 runs script 2; endCutscene at Room 04 local script 200 +0x01DD runs script 3 |
| Room 04 object 174 Verb 0x3A | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x3B | Validated state preview | 107 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 174 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 175 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x36 | Validated state preview | 201 | cutscene at Room 04 local script 202 +0x0000 runs script 2; endCutscene at Room 04 local script 202 +0x0169 runs script 3 |
| Room 04 object 176 Verb 0x3A | Validated state preview | 102 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x3B | Validated state preview | 103 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 176 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 177 Verb 0x36 | Validated state preview | 114 | cutscene at Room 04 Object 177 Verb 0x36 +0x0017 runs script 2; endCutscene at Room 04 Object 177 Verb 0x36 +0x0085 runs script 3 |
| Room 04 object 177 Verb 0x3A | Validated state preview | 136 | cutscene at Room 04 local script 209 +0x0000 runs script 2; endCutscene at Room 04 local script 209 +0x00E0 runs script 3 |
| Room 04 object 177 Verb 0x3B | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 177 Verb 0x56 | Validated state preview | 96 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 04 object 197 Verb 0x56 | Validated state preview | 73 | cutscene at Room 04 Object 197 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 04 Object 197 Verb 0x56 +0x0042 runs script 3 |
| Room 06 object 205 Verb 0x36 | Validated state preview | 72 | cutscene at Room 06 local script 201 +0x0000 runs script 2; endCutscene at Room 06 local script 201 +0x0053 runs script 3 |
| Room 06 object 205 Verb 0x56 | Validated state preview | 94 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 206 Verb 0x3E | Validated state preview | 71 | cutscene at Room 06 local script 211 +0x0000 runs script 2; endCutscene at Room 06 local script 211 +0x004D runs script 3 |
| Room 06 object 206 Verb 0x3F | Validated state preview | 97 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 206 Verb 0x56 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 207 Verb 0x3E | Validated state preview | 97 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 207 Verb 0x3F | Validated state preview | 80 | cutscene at Room 06 local script 212 +0x0000 runs script 2; endCutscene at Room 06 local script 212 +0x005A runs script 3 |
| Room 06 object 207 Verb 0x56 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 209 Verb 0x36 | Validated state preview | 127 | cutscene at Room 06 Object 209 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 06 Object 209 Verb 0x36 +0x0086 runs script 3 |
| Room 06 object 209 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 210 Verb 0x3E | Validated state preview | 97 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 210 Verb 0x3F | Validated state preview | 80 | cutscene at Room 06 local script 212 +0x0000 runs script 2; endCutscene at Room 06 local script 212 +0x005A runs script 3 |
| Room 06 object 210 Verb 0x56 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 211 Verb 0x3E | Validated state preview | 71 | cutscene at Room 06 local script 211 +0x0000 runs script 2; endCutscene at Room 06 local script 211 +0x004D runs script 3 |
| Room 06 object 211 Verb 0x3F | Validated state preview | 97 | cutscene at Room 06 local script 200 +0x0000 runs script 2; endCutscene at Room 06 local script 200 +0x00CE runs script 3 |
| Room 06 object 211 Verb 0x56 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 06 object 214 Verb 0x36 | Validated state preview | 74 | cutscene at Room 06 local script 215 +0x0000 runs script 2; endCutscene at Room 06 local script 215 +0x00A5 runs script 3 |
| Room 06 object 214 Verb 0x56 | Validated state preview | 96 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 255 Verb 0x38 | Validated state preview | 2787 | cutscene at Script 46 +0x0000 runs script 2; endCutscene at Script 46 +0x13AA runs script 3 |
| Room 08 object 256 Verb 0x36 | Validated state preview | 153 | cutscene at Room 08 Object 256 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 256 Verb 0x36 +0x0085 runs script 3 |
| Room 08 object 256 Verb 0x3E | Validated state preview | 84 | cutscene at Room 08 local script 208 +0x0000 runs script 2; endCutscene at Room 08 local script 208 +0x0054 runs script 3 |
| Room 08 object 256 Verb 0x3F | Validated state preview | 99 | cutscene at Room 08 local script 212 +0x000B runs script 2; endCutscene at Room 08 local script 212 +0x0062 runs script 3 |
| Room 08 object 256 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 257 Verb 0x36 | Validated state preview | 152 | cutscene at Room 08 Object 257 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 257 Verb 0x36 +0x0084 runs script 3 |
| Room 08 object 257 Verb 0x3E | Validated state preview | 99 | cutscene at Room 08 local script 213 +0x0008 runs script 2; endCutscene at Room 08 local script 213 +0x0066 runs script 3 |
| Room 08 object 257 Verb 0x3F | Validated state preview | 82 | cutscene at Room 08 local script 209 +0x0000 runs script 2; endCutscene at Room 08 local script 209 +0x0086 runs script 3 |
| Room 08 object 257 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 08 object 258 Verb 0x36 | Validated state preview | 211 | cutscene at Room 08 Object 258 Verb 0x36 +0x0009 runs script 2; endCutscene at Room 08 Object 258 Verb 0x36 +0x00AA runs script 3 |
| Room 08 object 258 Verb 0x3E | Validated state preview | 99 | cutscene at Room 08 local script 215 +0x0008 runs script 2; endCutscene at Room 08 local script 215 +0x0066 runs script 3 |
| Room 08 object 258 Verb 0x3F | Validated state preview | 102 | cutscene at Room 08 local script 209 +0x0000 runs script 2; endCutscene at Room 08 local script 209 +0x0086 runs script 3; cutscene at Room 08 local script 216 +0x000B runs script 2; endCutscene at Room 08 local script 216 +0x0062 runs script 3 |
| Room 08 object 258 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 268 Verb 0x37 | Validated state preview | 145 | cutscene at Room 09 local script 214 +0x000C runs script 2; endCutscene at Room 09 local script 214 +0x0088 runs script 3 |
| Room 09 object 270 Verb 0x36 | Validated state preview | 102 | cutscene at Room 09 Object 270 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 09 Object 270 Verb 0x36 +0x0015 runs script 3 |
| Room 09 object 270 Verb 0x3A | Validated state preview | 1937 | cutscene at Script 47 +0x0000 runs script 2; endCutscene at Script 47 +0x0D02 runs script 3 |
| Room 09 object 270 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 271 Verb 0x38 | Validated state preview | 73 | cutscene at Room 09 Object 271 Verb 0x38 +0x0005 runs script 2; endCutscene at Room 09 Object 271 Verb 0x38 +0x006F runs script 3 |
| Room 09 object 272 Verb 0x36 | Validated state preview | 86 | cutscene at Room 09 local script 209 +0x0000 runs script 2; endCutscene at Room 09 local script 209 +0x0191 runs script 3 |
| Room 09 object 272 Verb 0x52 | Validated state preview | 969 | cutscene at Script 104 +0x0000 runs script 2; endCutscene at Script 104 +0x06D7 runs script 3; cutscene at Script 29 +0x0000 runs script 2; endCutscene at Script 29 +0x00E2 runs script 3 |
| Room 09 object 272 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 09 object 273 Verb 0x4D | Validated state preview | 97 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BD runs script 3 |
| Room 09 object 273 Verb 0x4F | Validated state preview | 97 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BD runs script 3 |
| Room 09 object 273 Verb 0x51 | Validated state preview | 97 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BD runs script 3 |
| Room 09 object 273 Verb 0x56 | Validated state preview | 97 | cutscene at Room 09 local script 222 +0x0000 runs script 2; endCutscene at Room 09 local script 222 +0x00BD runs script 3 |
| Room 09 object 274 Verb 0x36 | Validated state preview | 178 | cutscene at Script 105 +0x0000 runs script 2; endCutscene at Script 105 +0x020B runs script 3 |
| Room 09 object 275 Verb 0x56 | Validated state preview | 60 | cutscene at Room 09 local script 221 +0x0000 runs script 2; endCutscene at Room 09 local script 221 +0x00F9 runs script 3 |
| Room 10 entry | No cutscene reached from this start | 32 |  |
| Room 10 object 295 Verb 0x38 | Validated state preview | 139 | cutscene at Script 52 +0x0000 runs script 2; endCutscene at Script 52 +0x0167 runs script 3 |
| Room 10 object 296 Verb 0x3A | Validated state preview | 97 | cutscene at Room 10 local script 212 +0x0000 runs script 2; endCutscene at Room 10 local script 212 +0x007C runs script 3 |
| Room 10 object 296 Verb 0x3B | Validated state preview | 88 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 296 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 297 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 297 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 298 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 298 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 299 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 299 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 10 object 300 Verb 0x36 | Validated state preview | 75 | cutscene at Room 10 local script 205 +0x0000 runs script 2; endCutscene at Room 10 local script 205 +0x00B3 runs script 3 |
| Room 10 object 300 Verb 0x56 | Validated state preview | 95 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 12 object 322 Verb 0x38 | Validated state preview | 158 | cutscene at Room 12 Object 322 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 12 Object 322 Verb 0x38 +0x0075 runs script 3 |
| Room 13 entry | No cutscene reached from this start | 32 |  |
| Room 13 object 323 Verb 0x38 | Validated state preview | 105 | cutscene at Script 54 +0x0000 runs script 2; endCutscene at Script 54 +0x00C4 runs script 3; cutscene at Script 29 +0x0000 runs script 2; endCutscene at Script 29 +0x00E2 runs script 3 |
| Room 13 object 324 Verb 0x36 | Validated state preview | 51 | cutscene at Script 107 +0x0000 runs script 2; endCutscene at Script 107 +0x00D6 runs script 3 |
| Room 13 object 325 Verb 0x36 | Validated state preview | 110 | cutscene at Room 13 Object 325 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 13 Object 325 Verb 0x36 +0x005E runs script 3 |
| Room 13 object 325 Verb 0x3A | Validated state preview | 131 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 13 object 325 Verb 0x41 | Validated state preview | 125 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 13 object 325 Verb 0x42 | Validated state preview | 75 | cutscene at Room 13 local script 202 +0x0000 runs script 2; endCutscene at Room 13 local script 202 +0x00F1 runs script 3 |
| Room 13 object 325 Verb 0x56 | Validated state preview | 102 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 15 entry | No cutscene reached from this start | 32 |  |
| Room 15 object 338 Verb 0x38 | Validated state preview | 82 | cutscene at Room 15 Object 338 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 15 Object 338 Verb 0x38 +0x0020 runs script 3 |
| Room 16 object 350 Verb 0x47 | Validated state preview | 111 | cutscene at Room 16 local script 204 +0x0070 runs script 2; endCutscene at Room 16 local script 204 +0x011E runs script 3 |
| Room 16 object 350 Verb 0x48 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 16 object 350 Verb 0x56 | Validated state preview | 99 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 entry | No cutscene reached from this start | 32 |  |
| Room 17 object 357 Verb 0x3A | Validated state preview | 136 | cutscene at Room 17 local script 206 +0x0000 runs script 2; endCutscene at Room 17 local script 206 +0x00CB runs script 3 |
| Room 17 object 357 Verb 0x3B | Validated state preview | 79 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 357 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 358 Verb 0x3A | Validated state preview | 136 | cutscene at Room 17 local script 206 +0x0000 runs script 2; endCutscene at Room 17 local script 206 +0x00CB runs script 3 |
| Room 17 object 358 Verb 0x3B | Validated state preview | 79 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 17 object 358 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 367 Verb 0x36 | Validated state preview | 734 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x02FC runs script 3 |
| Room 18 object 368 Verb 0x38 | Validated state preview | 734 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x02FC runs script 3 |
| Room 18 object 369 Verb 0x36 | Validated state preview | 536 | cutscene at Script 62 +0x0000 runs script 2; endCutscene at Script 62 +0x0018 runs script 3; cutscene at Script 63 +0x0000 runs script 2; endCutscene at Script 63 +0x001C runs script 3; cutscene at Room 20 local script 207 +0x000B runs script 2; endCutscene at Room 20 local script 207 +0x04D6 runs script 3; cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 18 object 369 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 370 Verb 0x36 | Validated state preview | 229 | cutscene at Script 65 +0x0000 runs script 2; endCutscene at Script 65 +0x0302 runs script 3 |
| Room 18 object 370 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 371 Verb 0x36 | Validated state preview | 567 | cutscene at Script 56 +0x0000 runs script 2; endCutscene at Script 56 +0x0025 runs script 3; cutscene at Script 57 +0x0000 runs script 2; endCutscene at Script 57 +0x0029 runs script 3; cutscene at Room 20 local script 207 +0x000B runs script 2; endCutscene at Room 20 local script 207 +0x04D6 runs script 3; cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 18 object 371 Verb 0x3A | Validated state preview | 83 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 371 Verb 0x56 | Validated state preview | 99 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 18 object 372 Verb 0x36 | Validated state preview | 632 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x02FC runs script 3 |
| Room 18 object 372 Verb 0x56 | Validated state preview | 734 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x02FC runs script 3 |
| Room 18 object 374 Verb 0x36 | Validated state preview | 734 | cutscene at Room 18 local script 205 +0x0000 runs script 2; endCutscene at Room 18 local script 205 +0x02FC runs script 3 |
| Room 20 object 386 Verb 0x36 | Validated state preview | 98 | cutscene at Room 20 local script 202 +0x0000 runs script 2; endCutscene at Room 20 local script 202 +0x00AE runs script 3 |
| Room 20 object 386 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 387 Verb 0x36 | Validated state preview | 98 | cutscene at Room 20 local script 202 +0x0000 runs script 2; endCutscene at Room 20 local script 202 +0x00AE runs script 3 |
| Room 20 object 387 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 388 Verb 0x36 | Validated state preview | 116 | cutscene at Script 60 +0x0000 runs script 2; endCutscene at Script 60 +0x0018 runs script 3; cutscene at Script 61 +0x0010 runs script 2; endCutscene at Script 61 +0x002C runs script 3 |
| Room 20 object 388 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 389 Verb 0x36 | Validated state preview | 114 | cutscene at Script 58 +0x0000 runs script 2; endCutscene at Script 58 +0x0018 runs script 3; cutscene at Script 59 +0x0010 runs script 2; endCutscene at Script 59 +0x003F runs script 3 |
| Room 20 object 389 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 20 object 390 Verb 0x47 | Validated state preview | 95 | cutscene at Room 20 local script 212 +0x0000 runs script 2; endCutscene at Room 20 local script 212 +0x0039 runs script 3 |
| Room 20 object 390 Verb 0x48 | Incomplete: prerequisites, waits or executor require review | 78 | cutscene at Room 20 local script 206 +0x0000 runs script 2 |
| Room 20 object 390 Verb 0x49 | Validated state preview | 112 | cutscene at Room 20 local script 211 +0x0000 runs script 2; endCutscene at Room 20 local script 211 +0x004F runs script 3 |
| Room 20 object 390 Verb 0x56 | Validated state preview | 95 | cutscene at Room 20 local script 213 +0x0000 runs script 2; endCutscene at Room 20 local script 213 +0x0039 runs script 3 |
| Room 20 object 391 Verb 0x47 | Validated state preview | 95 | cutscene at Room 20 local script 212 +0x0000 runs script 2; endCutscene at Room 20 local script 212 +0x0039 runs script 3 |
| Room 20 object 391 Verb 0x48 | Incomplete: prerequisites, waits or executor require review | 78 | cutscene at Room 20 local script 206 +0x0000 runs script 2 |
| Room 20 object 391 Verb 0x49 | Validated state preview | 112 | cutscene at Room 20 local script 211 +0x0000 runs script 2; endCutscene at Room 20 local script 211 +0x004F runs script 3 |
| Room 20 object 391 Verb 0x56 | Validated state preview | 95 | cutscene at Room 20 local script 213 +0x0000 runs script 2; endCutscene at Room 20 local script 213 +0x0039 runs script 3 |
| Room 22 entry | No cutscene reached from this start | 32 |  |
| Room 22 object 403 Verb 0x36 | Validated state preview | 168 | cutscene at Room 22 local script 205 +0x0000 runs script 2; endCutscene at Room 22 local script 205 +0x0093 runs script 3 |
| Room 22 object 403 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 404 Verb 0x36 | Validated state preview | 810 | cutscene at Room 22 local script 204 +0x0000 runs script 2; endCutscene at Room 22 local script 204 +0x0093 runs script 3; cutscene at Script 67 +0x0000 runs script 2; endCutscene at Script 67 +0x00FD runs script 3 |
| Room 22 object 404 Verb 0x56 | Validated state preview | 99 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x36 | Validated state preview | 780 | cutscene at Script 108 +0x0000 runs script 2; endCutscene at Script 108 +0x04B4 runs script 3 |
| Room 22 object 405 Verb 0x3A | Validated state preview | 94 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x45 | Validated state preview | 84 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 22 object 405 Verb 0x46 | Validated state preview | 70 | cutscene at Room 22 local script 214 +0x0000 runs script 2; endCutscene at Room 22 local script 214 +0x0064 runs script 3 |
| Room 22 object 405 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 23 entry | No cutscene reached from this start | 32 |  |
| Room 23 object 422 Verb 0x47 | Validated state preview | 62 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x0080 runs script 3 |
| Room 23 object 422 Verb 0x49 | Validated state preview | 155 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x024B runs script 3 |
| Room 23 object 422 Verb 0x56 | Validated state preview | 102 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 423 Verb 0x47 | Validated state preview | 62 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x0080 runs script 3 |
| Room 23 object 423 Verb 0x49 | Validated state preview | 155 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x024B runs script 3 |
| Room 23 object 423 Verb 0x56 | Validated state preview | 102 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 424 Verb 0x47 | Validated state preview | 62 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x0080 runs script 3 |
| Room 23 object 424 Verb 0x49 | Validated state preview | 155 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x024B runs script 3 |
| Room 23 object 424 Verb 0x56 | Validated state preview | 102 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 23 object 425 Verb 0x47 | Validated state preview | 62 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3; cutscene at Room 23 local script 232 +0x0000 runs script 2; endCutscene at Room 23 local script 232 +0x0080 runs script 3 |
| Room 23 object 425 Verb 0x49 | Validated state preview | 155 | cutscene at Room 23 local script 210 +0x0000 runs script 2; endCutscene at Room 23 local script 210 +0x024B runs script 3 |
| Room 23 object 425 Verb 0x56 | Validated state preview | 102 | cutscene at Room 23 local script 229 +0x0000 runs script 2; endCutscene at Room 23 local script 229 +0x002C runs script 3 |
| Room 24 entry | No cutscene reached from this start | 32 |  |
| Room 24 object 431 Verb 0x3A | Validated state preview | 141 | cutscene at Room 24 local script 214 +0x0000 runs script 2; endCutscene at Room 24 local script 214 +0x00D0 runs script 3 |
| Room 24 object 431 Verb 0x3B | Validated state preview | 79 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 431 Verb 0x56 | Validated state preview | 100 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 432 Verb 0x36 | Validated state preview | 403 | cutscene at Room 24 local script 204 +0x0000 runs script 2; cutscene at Room 24 local script 215 +0x0000 runs script 2; endCutscene at Room 24 local script 215 +0x00BC runs script 3; cutscene at Room 24 local script 215 +0x0000 runs script 2; endCutscene at Room 24 local script 215 +0x00BC runs script 3; endCutscene at Room 24 local script 204 +0x0080 runs script 3 |
| Room 24 object 432 Verb 0x3E | Validated state preview | 78 | cutscene at Room 24 local script 213 +0x0000 runs script 2; endCutscene at Room 24 local script 213 +0x0043 runs script 3 |
| Room 24 object 432 Verb 0x3F | Validated state preview | 110 | cutscene at Room 24 local script 212 +0x0018 runs script 2; endCutscene at Room 24 local script 212 +0x0064 runs script 3 |
| Room 24 object 432 Verb 0x47 | Validated state preview | 116 | cutscene at Room 24 Object 432 Verb 0x47 +0x0000 runs script 2; endCutscene at Room 24 Object 432 Verb 0x47 +0x0054 runs script 3 |
| Room 24 object 432 Verb 0x49 | Validated state preview | 103 | cutscene at Room 24 Object 432 Verb 0x49 +0x0000 runs script 2; endCutscene at Room 24 Object 432 Verb 0x49 +0x0042 runs script 3 |
| Room 24 object 432 Verb 0x56 | Validated state preview | 100 | cutscene at Room 24 Object 432 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 24 Object 432 Verb 0x56 +0x003E runs script 3 |
| Room 24 object 433 Verb 0x38 | Validated state preview | 64 | cutscene at Room 24 Object 433 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 24 Object 433 Verb 0x38 +0x000A runs script 3 |
| Room 24 object 433 Verb 0x3E | Validated state preview | 312 | cutscene at Script 70 +0x0000 runs script 2; endCutscene at Script 70 +0x01A2 runs script 3 |
| Room 24 object 433 Verb 0x3F | Validated state preview | 100 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x47 | Validated state preview | 114 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x49 | Validated state preview | 101 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 433 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 24 object 434 Verb 0x47 | Validated state preview | 105 | cutscene at Room 24 local script 215 +0x0000 runs script 2; endCutscene at Room 24 local script 215 +0x00BC runs script 3 |
| Room 24 object 434 Verb 0x56 | Validated state preview | 95 | cutscene at Room 24 local script 215 +0x0000 runs script 2; endCutscene at Room 24 local script 215 +0x00BC runs script 3 |
| Room 25 entry | No cutscene reached from this start | 32 |  |
| Room 25 object 446 Verb 0x37 | No cutscene reached from this start | 32 |  |
| Room 25 object 447 Verb 0x36 | No cutscene reached from this start | 32 |  |
| Room 25 object 447 Verb 0x3C | Validated state preview | 119 | cutscene at Room 25 Object 447 Verb 0x3C +0x0000 runs script 2; endCutscene at Room 25 Object 447 Verb 0x3C +0x0059 runs script 3 |
| Room 25 object 447 Verb 0x56 | Validated state preview | 97 | cutscene at Room 25 Object 447 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 25 Object 447 Verb 0x56 +0x003C runs script 3 |
| Room 26 object 464 Verb 0x36 | Validated state preview | 284 | cutscene at Room 26 local script 211 +0x0000 runs script 2; endCutscene at Room 26 local script 211 +0x0135 runs script 3 |
| Room 26 object 464 Verb 0x3E | Validated state preview | 71 | cutscene at Room 26 Object 464 Verb 0x3E +0x0000 runs script 2; endCutscene at Room 26 Object 464 Verb 0x3E +0x0042 runs script 3 |
| Room 26 object 464 Verb 0x4D | Validated state preview | 348 | cutscene at Room 26 local script 202 +0x0000 runs script 2; endCutscene at Room 26 local script 202 +0x03DE runs script 3 |
| Room 26 object 464 Verb 0x56 | Validated state preview | 101 | cutscene at Room 26 local script 209 +0x0000 runs script 2; endCutscene at Room 26 local script 209 +0x0118 runs script 3 |
| Room 28 entry | No cutscene reached from this start | 32 |  |
| Room 28 object 465 Verb 0x38 | Validated state preview | 65 | cutscene at Script 72 +0x0000 runs script 2; endCutscene at Script 72 +0x0074 runs script 3 |
| Room 28 object 466 Verb 0x36 | Validated state preview | 708 | cutscene at Script 75 +0x0038 runs script 2; endCutscene at Script 75 +0x03F7 runs script 3 |
| Room 28 object 466 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 466 Verb 0x43 | Validated state preview | 94 | cutscene at Room 28 local script 210 +0x0000 runs script 2; endCutscene at Room 28 local script 210 +0x0095 runs script 3 |
| Room 28 object 466 Verb 0x49 | Validated state preview | 221 | cutscene at Room 28 local script 215 +0x0000 runs script 2; endCutscene at Room 28 local script 215 +0x0194 runs script 3 |
| Room 28 object 466 Verb 0x56 | Validated state preview | 98 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 467 Verb 0x36 | Validated state preview | 708 | cutscene at Script 75 +0x0038 runs script 2; endCutscene at Script 75 +0x03F7 runs script 3 |
| Room 28 object 467 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 467 Verb 0x43 | Validated state preview | 94 | cutscene at Room 28 local script 210 +0x0000 runs script 2; endCutscene at Room 28 local script 210 +0x0095 runs script 3 |
| Room 28 object 467 Verb 0x49 | Validated state preview | 221 | cutscene at Room 28 local script 215 +0x0000 runs script 2; endCutscene at Room 28 local script 215 +0x0194 runs script 3 |
| Room 28 object 467 Verb 0x56 | Validated state preview | 98 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 468 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 468 Verb 0x44 | Validated state preview | 118 | cutscene at Room 28 local script 209 +0x0000 runs script 2; endCutscene at Room 28 local script 209 +0x00BD runs script 3 |
| Room 28 object 468 Verb 0x49 | Validated state preview | 96 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 468 Verb 0x56 | Validated state preview | 98 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 469 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 469 Verb 0x44 | Validated state preview | 118 | cutscene at Room 28 local script 209 +0x0000 runs script 2; endCutscene at Room 28 local script 209 +0x00BD runs script 3 |
| Room 28 object 469 Verb 0x49 | Validated state preview | 96 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 469 Verb 0x56 | Validated state preview | 98 | cutscene at Room 28 local script 213 +0x0000 runs script 2; endCutscene at Room 28 local script 213 +0x00A1 runs script 3 |
| Room 28 object 470 Verb 0x36 | Validated state preview | 621 | cutscene at Script 109 +0x0013 runs script 2; endCutscene at Script 109 +0x03AA runs script 3 |
| Room 28 object 470 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 470 Verb 0x3D | Validated state preview | 250 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 200 +0x0010 runs script 2; endCutscene at Room 28 local script 200 +0x0259 runs script 3; cutscene at Script 29 +0x0000 runs script 2; endCutscene at Script 29 +0x00E2 runs script 3 |
| Room 28 object 470 Verb 0x56 | Validated state preview | 99 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 28 object 471 Verb 0x36 | Validated state preview | 621 | cutscene at Script 109 +0x0013 runs script 2; endCutscene at Script 109 +0x03AA runs script 3 |
| Room 28 object 471 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 471 Verb 0x3D | Validated state preview | 250 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 200 +0x0010 runs script 2; endCutscene at Room 28 local script 200 +0x0259 runs script 3; cutscene at Script 29 +0x0000 runs script 2; endCutscene at Script 29 +0x00E2 runs script 3 |
| Room 28 object 471 Verb 0x56 | Validated state preview | 99 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 28 object 472 Verb 0x36 | Validated state preview | 621 | cutscene at Script 109 +0x0013 runs script 2; endCutscene at Script 109 +0x03AA runs script 3 |
| Room 28 object 472 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 472 Verb 0x3D | Validated state preview | 250 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 200 +0x0010 runs script 2; endCutscene at Room 28 local script 200 +0x0259 runs script 3; cutscene at Script 29 +0x0000 runs script 2; endCutscene at Script 29 +0x00E2 runs script 3 |
| Room 28 object 472 Verb 0x56 | Validated state preview | 99 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 28 object 473 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 473 Verb 0x3C | Validated state preview | 199 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 201 +0x0010 runs script 2; endCutscene at Room 28 local script 201 +0x024B runs script 3 |
| Room 28 object 473 Verb 0x56 | Validated state preview | 100 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 28 object 474 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 474 Verb 0x3C | Validated state preview | 199 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 201 +0x0010 runs script 2; endCutscene at Room 28 local script 201 +0x024B runs script 3 |
| Room 28 object 474 Verb 0x56 | Validated state preview | 100 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 28 object 475 Verb 0x38 | Brief input-suppression scope: not an independent replay | 41 | cutscene at Room 28 local script 203 +0x0000 runs script 2; endCutscene at Room 28 local script 203 +0x0007 runs script 3 |
| Room 28 object 475 Verb 0x3C | Validated state preview | 199 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3; cutscene at Room 28 local script 201 +0x0010 runs script 2; endCutscene at Room 28 local script 201 +0x024B runs script 3 |
| Room 28 object 475 Verb 0x56 | Validated state preview | 100 | cutscene at Room 28 local script 218 +0x0000 runs script 2; endCutscene at Room 28 local script 218 +0x00AF runs script 3 |
| Room 30 object 503 Verb 0x38 | Validated state preview | 67 | cutscene at Room 30 Object 503 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 503 Verb 0x38 +0x001E runs script 3 |
| Room 30 object 504 Verb 0x38 | Validated state preview | 53 | cutscene at Room 30 Object 504 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 504 Verb 0x38 +0x001E runs script 3 |
| Room 30 object 505 Verb 0x38 | Validated state preview | 63 | cutscene at Room 30 Object 505 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 30 Object 505 Verb 0x38 +0x001B runs script 3 |
| Room 30 object 507 Verb 0x38 | Validated state preview | 62 | cutscene at Script 77 +0x0000 runs script 2; endCutscene at Script 77 +0x0043 runs script 3 |
| Room 30 object 508 Verb 0x38 | Validated state preview | 106 | cutscene at Script 82 +0x0000 runs script 2; endCutscene at Script 82 +0x0084 runs script 3 |
| Room 30 object 509 Verb 0x38 | Validated state preview | 101 | cutscene at Script 79 +0x0000 runs script 2; endCutscene at Script 79 +0x007C runs script 3 |
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
| Room 31 object 523 Verb 0x36 | Validated state preview | 116 | cutscene at Room 31 local script 200 +0x0000 runs script 2; endCutscene at Room 31 local script 200 +0x015B runs script 3 |
| Room 31 object 523 Verb 0x3A | Validated state preview | 71 | cutscene at Room 31 local script 203 +0x0000 runs script 2; endCutscene at Room 31 local script 203 +0x0113 runs script 3 |
| Room 31 object 523 Verb 0x3B | Validated state preview | 91 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 523 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 524 Verb 0x3A | Validated state preview | 87 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 524 Verb 0x3B | Validated state preview | 71 | cutscene at Room 31 local script 203 +0x0000 runs script 2; endCutscene at Room 31 local script 203 +0x0113 runs script 3 |
| Room 31 object 524 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 31 object 525 Verb 0x36 | Validated state preview | 129 | cutscene at Script 111 +0x0000 runs script 2; endCutscene at Script 111 +0x00FA runs script 3 |
| Room 31 object 525 Verb 0x56 | Validated state preview | 100 | cutscene at Room 31 local script 204 +0x0000 runs script 2; endCutscene at Room 31 local script 204 +0x008E runs script 3 |
| Room 32 object 537 Verb 0x38 | Validated state preview | 108 | cutscene at Room 32 Object 537 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 32 Object 537 Verb 0x38 +0x003B runs script 3 |
| Room 32 object 538 Verb 0x38 | Validated state preview | 107 | cutscene at Script 81 +0x0000 runs script 2; endCutscene at Script 81 +0x004B runs script 3 |
| Room 33 object 543 Verb 0x38 | Validated state preview | 107 | cutscene at Room 33 Object 543 Verb 0x38 +0x0010 runs script 2; endCutscene at Room 33 Object 543 Verb 0x38 +0x0046 runs script 3 |
| Room 33 object 544 Verb 0x41 | Validated state preview | 96 | cutscene at Room 33 local script 205 +0x0000 runs script 2; endCutscene at Room 33 local script 205 +0x00B0 runs script 3 |
| Room 33 object 544 Verb 0x42 | Validated state preview | 73 | cutscene at Room 33 local script 205 +0x0000 runs script 2; endCutscene at Room 33 local script 205 +0x00B0 runs script 3; cutscene at Room 33 local script 200 +0x0006 runs script 2; endCutscene at Room 33 local script 200 +0x005E runs script 3 |
| Room 33 object 544 Verb 0x56 | Validated state preview | 98 | cutscene at Room 33 local script 205 +0x0000 runs script 2; endCutscene at Room 33 local script 205 +0x00B0 runs script 3 |
| Room 34 entry | No cutscene reached from this start | 32 |  |
| Room 34 object 558 Verb 0x37 | No cutscene reached from this start | 32 |  |
| Room 34 object 560 Verb 0x37 | Validated state preview | 1136 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0605 runs script 3 |
| Room 34 object 560 Verb 0x38 | No cutscene reached from this start | 32 |  |
| Room 34 object 561 Verb 0x37 | Validated state preview | 1136 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0605 runs script 3 |
| Room 34 object 562 Verb 0x36 | Validated state preview | 114 | cutscene at Script 112 +0x001A runs script 2; endCutscene at Script 112 +0x007C runs script 3 |
| Room 34 object 562 Verb 0x43 | Validated state preview | 992 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05A8 runs script 3 |
| Room 34 object 562 Verb 0x44 | Validated state preview | 1020 | cutscene at Room 34 local script 201 +0x0000 runs script 2; endCutscene at Room 34 local script 201 +0x0030 runs script 3; cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05A8 runs script 3 |
| Room 34 object 562 Verb 0x4C | Validated state preview | 992 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05A8 runs script 3 |
| Room 34 object 562 Verb 0x56 | Validated state preview | 992 | cutscene at Script 85 +0x0000 runs script 2; endCutscene at Script 85 +0x05A8 runs script 3 |
| Room 34 object 563 Verb 0x3A | Validated state preview | 142 | cutscene at Room 34 local script 218 +0x0000 runs script 2; endCutscene at Room 34 local script 218 +0x00D2 runs script 3 |
| Room 34 object 563 Verb 0x3B | Validated state preview | 79 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 563 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 587 Verb 0x37 | Validated state preview | 1136 | cutscene at Script 98 +0x0035 runs script 2; endCutscene at Script 98 +0x0605 runs script 3 |
| Room 34 object 587 Verb 0x43 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 587 Verb 0x44 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 34 object 587 Verb 0x4C | Validated state preview | 74 | cutscene at Room 34 Object 587 Verb 0x4C +0x0000 runs script 2; endCutscene at Room 34 Object 587 Verb 0x4C +0x0046 runs script 3 |
| Room 34 object 587 Verb 0x4D | Validated state preview | 600 | cutscene at Room 34 local script 209 +0x0000 runs script 2; endCutscene at Room 34 local script 209 +0x03A7 runs script 3 |
| Room 34 object 587 Verb 0x56 | Validated state preview | 93 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 35 entry | No cutscene reached from this start | 32 |  |
| Room 35 object 589 Verb 0x38 | Validated state preview | 65 | cutscene at Room 35 local script 210 +0x001A runs script 2; endCutscene at Room 35 local script 210 +0x0085 runs script 3 |
| Room 35 object 590 Verb 0x3A | Validated state preview | 370 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 207 +0x0006 runs script 2; endCutscene at Room 35 local script 207 +0x002A runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07ED runs script 3 |
| Room 35 object 590 Verb 0x3B | Validated state preview | 353 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07ED runs script 3 |
| Room 35 object 590 Verb 0x56 | Validated state preview | 353 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07ED runs script 3 |
| Room 35 object 597 Verb 0x38 | Validated state preview | 65 | cutscene at Room 35 local script 210 +0x001A runs script 2; endCutscene at Room 35 local script 210 +0x0085 runs script 3 |
| Room 35 object 597 Verb 0x56 | Validated state preview | 353 | cutscene at Room 35 local script 212 +0x0000 runs script 2; endCutscene at Room 35 local script 212 +0x00A9 runs script 3; cutscene at Room 35 local script 204 +0x0096 runs script 2; endCutscene at Room 35 local script 204 +0x07ED runs script 3 |
| Room 36 entry | No cutscene reached from this start | 32 |  |
| Room 37 entry | No cutscene reached from this start | 32 |  |
| Room 37 object 602 Verb 0x37 | Validated state preview | 83 | cutscene at Room 37 Object 602 Verb 0x37 +0x000B runs script 2; endCutscene at Room 37 Object 602 Verb 0x37 +0x004E runs script 3 |
| Room 37 object 605 Verb 0x45 | Validated state preview | 71 | cutscene at Room 37 local script 205 +0x0000 runs script 2; endCutscene at Room 37 local script 205 +0x00BA runs script 3 |
| Room 37 object 605 Verb 0x46 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 37 object 605 Verb 0x56 | Validated state preview | 100 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 611 Verb 0x36 | Incomplete: prerequisites, waits or executor require review | 3000 | cutscene at Script 87 +0x0000 runs script 2 |
| Room 38 object 611 Verb 0x3C | Validated state preview | 70 | cutscene at Room 38 local script 202 +0x0000 runs script 2; endCutscene at Room 38 local script 202 +0x00E2 runs script 3 |
| Room 38 object 611 Verb 0x3D | Validated state preview | 99 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 611 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 612 Verb 0x36 | Validated state preview | 71 | cutscene at Room 38 Object 612 Verb 0x36 +0x0005 runs script 2; endCutscene at Room 38 Object 612 Verb 0x36 +0x0070 runs script 3 |
| Room 38 object 617 Verb 0x3A | Validated state preview | 88 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 617 Verb 0x3B | Validated state preview | 73 | cutscene at Room 38 local script 203 +0x0000 runs script 2; endCutscene at Room 38 local script 203 +0x00B2 runs script 3 |
| Room 38 object 617 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 618 Verb 0x3A | Validated state preview | 73 | cutscene at Room 38 local script 203 +0x0000 runs script 2; endCutscene at Room 38 local script 203 +0x00B2 runs script 3 |
| Room 38 object 618 Verb 0x3B | Validated state preview | 85 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 38 object 618 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 41 object 633 Verb 0x36 | Validated state preview | 1367 | cutscene at Room 41 Object 633 Verb 0x36 +0x0000 runs script 2; endCutscene at Room 41 Object 633 Verb 0x36 +0x008D runs script 3 |
| Room 41 object 634 Verb 0x41 | Validated state preview | 1697 | cutscene at Room 41 local script 213 +0x0000 runs script 2; endCutscene at Room 41 local script 213 +0x00A9 runs script 3; cutscene at Room 41 local script 204 +0x0006 runs script 2; endCutscene at Room 41 local script 204 +0x0319 runs script 3; cutscene at Script 90 +0x0000 runs script 2; endCutscene at Script 90 +0x0716 runs script 3 |
| Room 41 object 634 Verb 0x42 | Validated state preview | 72 | cutscene at Room 41 local script 205 +0x0000 runs script 2; endCutscene at Room 41 local script 205 +0x0043 runs script 3 |
| Room 41 object 634 Verb 0x4B | Validated state preview | 1697 | cutscene at Room 41 local script 213 +0x0000 runs script 2; endCutscene at Room 41 local script 213 +0x00A9 runs script 3; cutscene at Room 41 local script 204 +0x0006 runs script 2; endCutscene at Room 41 local script 204 +0x0319 runs script 3; cutscene at Script 90 +0x0000 runs script 2; endCutscene at Script 90 +0x0716 runs script 3 |
| Room 41 object 634 Verb 0x56 | Validated state preview | 98 | cutscene at Room 41 local script 213 +0x0000 runs script 2; endCutscene at Room 41 local script 213 +0x00A9 runs script 3 |
| Room 43 object 640 Verb 0x38 | No cutscene reached from this start | 32 |  |
| Room 43 object 641 Verb 0x36 | Validated state preview | 148 | cutscene at Script 94 +0x0000 runs script 2; endCutscene at Script 94 +0x0132 runs script 3 |
| Room 43 object 644 Verb 0x3A | Validated state preview | 71 | cutscene at Script 91 +0x0025 runs script 2; endCutscene at Script 91 +0x0E57 runs script 3 |
| Room 43 object 644 Verb 0x3B | Validated state preview | 90 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 43 object 644 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 43 object 651 Verb 0x38 | Validated state preview | 895 | cutscene at Script 93 +0x0000 runs script 2; endCutscene at Script 93 +0x05C8 runs script 3 |
| Room 44 object 653 Verb 0x38 | Validated state preview | 48 | cutscene at Script 92 +0x0000 runs script 2; endCutscene at Script 92 +0x0039 runs script 3 |
| Room 44 object 654 Verb 0x36 | Validated state preview | 855 | cutscene at Script 95 +0x0000 runs script 2; endCutscene at Script 95 +0x0780 runs script 3 |
| Room 45 object 656 Verb 0x38 | Validated state preview | 49 | cutscene at Script 92 +0x0000 runs script 2; endCutscene at Script 92 +0x0039 runs script 3 |
| Room 45 object 657 Verb 0x36 | Validated state preview | 80 | cutscene at Room 45 Object 657 Verb 0x36 +0x0005 runs script 2; endCutscene at Room 45 Object 657 Verb 0x36 +0x007B runs script 3 |
| Room 46 object 683 Verb 0x36 | Validated state preview | 44 | cutscene at Script 110 +0x0000 runs script 2; endCutscene at Script 110 +0x005B runs script 3 |
| Room 46 object 683 Verb 0x3A | Validated state preview | 69 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003D runs script 3 |
| Room 46 object 683 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 683 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 684 Verb 0x3A | Validated state preview | 69 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003D runs script 3 |
| Room 46 object 684 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 684 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 685 Verb 0x36 | Validated state preview | 42 | cutscene at Script 97 +0x0000 runs script 2; endCutscene at Script 97 +0x0042 runs script 3 |
| Room 46 object 685 Verb 0x3A | Validated state preview | 69 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003D runs script 3 |
| Room 46 object 685 Verb 0x3B | Validated state preview | 79 | cutscene at Room 46 local script 202 +0x0000 runs script 2; endCutscene at Room 46 local script 202 +0x00F6 runs script 3 |
| Room 46 object 685 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 46 object 686 Verb 0x3A | Validated state preview | 69 | cutscene at Room 46 local script 201 +0x0000 runs script 2; endCutscene at Room 46 local script 201 +0x003D runs script 3 |
| Room 46 object 686 Verb 0x3B | Validated state preview | 71 | cutscene at Room 46 local script 203 +0x0000 runs script 2; endCutscene at Room 46 local script 203 +0x00A4 runs script 3 |
| Room 46 object 686 Verb 0x56 | Validated state preview | 98 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 47 entry | Validated state preview | 325 | cutscene at Room 47 local script 205 +0x000E runs script 2; endCutscene at Room 47 local script 205 +0x0182 runs script 3 |
| Room 47 object 698 Verb 0x36 | Validated state preview | 1763 | cutscene at Script 100 +0x0000 runs script 2; endCutscene at Script 100 +0x0A45 runs script 3 |
| Room 47 object 698 Verb 0x56 | Validated state preview | 352 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3; cutscene at Room 47 local script 205 +0x000E runs script 2; endCutscene at Room 47 local script 205 +0x0182 runs script 3 |
| Room 49 object 705 Verb 0x38 | Validated state preview | 57 | cutscene at Room 49 Object 705 Verb 0x38 +0x0000 runs script 2; endCutscene at Room 49 Object 705 Verb 0x38 +0x0027 runs script 3 |
| Room 49 object 708 Verb 0x36 | Validated state preview | 793 | cutscene at Script 99 +0x0000 runs script 2; endCutscene at Script 99 +0x0504 runs script 3 |
| Room 49 object 708 Verb 0x56 | Validated state preview | 786 | cutscene at Script 99 +0x0000 runs script 2; endCutscene at Script 99 +0x0504 runs script 3 |
| Room 50 object 722 Verb 0x38 | Validated state preview | 130 | cutscene at Script 89 +0x0000 runs script 2; endCutscene at Script 89 +0x0074 runs script 3 |
| Room 51 object 742 Verb 0x38 | Validated state preview | 157 | cutscene at Script 102 +0x0000 runs script 2; endCutscene at Script 102 +0x00D8 runs script 3 |
| Room 51 object 743 Verb 0x56 | Validated state preview | 41 | cutscene at Script 103 +0x0000 runs script 2; endCutscene at Script 103 +0x0072 runs script 3 |
| Room 51 object 744 Verb 0x36 | Validated state preview | 283 | cutscene at Script 105 +0x0000 runs script 2; endCutscene at Script 105 +0x020B runs script 3 |
| Room 62 object 809 Verb 0x3C | Validated state preview | 78 | cutscene at Room 62 local script 207 +0x0000 runs script 2; endCutscene at Room 62 local script 207 +0x0058 runs script 3 |
| Room 62 object 809 Verb 0x3D | Validated state preview | 58 | cutscene at Room 62 local script 201 +0x0000 runs script 2; endCutscene at Room 62 local script 201 +0x007D runs script 3 |
| Room 62 object 809 Verb 0x56 | Validated state preview | 102 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 810 Verb 0x3C | Validated state preview | 58 | cutscene at Room 62 local script 203 +0x0000 runs script 2; endCutscene at Room 62 local script 203 +0x007D runs script 3 |
| Room 62 object 810 Verb 0x3D | Validated state preview | 78 | cutscene at Room 62 local script 208 +0x0000 runs script 2; endCutscene at Room 62 local script 208 +0x0057 runs script 3 |
| Room 62 object 810 Verb 0x56 | Validated state preview | 102 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 811 Verb 0x36 | Validated state preview | 73 | cutscene at Room 62 local script 200 +0x0000 runs script 2; endCutscene at Room 62 local script 200 +0x006E runs script 3 |
| Room 62 object 811 Verb 0x56 | Validated state preview | 103 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 62 object 812 Verb 0x36 | Validated state preview | 73 | cutscene at Room 62 local script 200 +0x0000 runs script 2; endCutscene at Room 62 local script 200 +0x006E runs script 3 |
| Room 63 object 821 Verb 0x56 | Validated state preview | 97 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 63 object 822 Verb 0x40 | Validated state preview | 82 | cutscene at Script 48 +0x0000 runs script 2; endCutscene at Script 48 +0x00C8 runs script 3 |
| Room 63 object 823 Verb 0x40 | Validated state preview | 82 | cutscene at Script 48 +0x0000 runs script 2; endCutscene at Script 48 +0x00C8 runs script 3 |
| Room 65 object 828 Verb 0x38 | Validated state preview | 62 | cutscene at Script 77 +0x0000 runs script 2; endCutscene at Script 77 +0x0043 runs script 3 |
| Room 65 object 829 Verb 0x40 | Validated state preview | 94 | cutscene at Script 78 +0x0000 runs script 2; endCutscene at Script 78 +0x00BD runs script 3 |
| Room 65 object 829 Verb 0x56 | Validated state preview | 100 | cutscene at Script 41 +0x0000 runs script 2; endCutscene at Script 41 +0x009A runs script 3 |
| Room 66 object 834 Verb 0x56 | Validated state preview | 95 | cutscene at Room 66 Object 834 Verb 0x56 +0x0000 runs script 2; endCutscene at Room 66 Object 834 Verb 0x56 +0x004D runs script 3 |
