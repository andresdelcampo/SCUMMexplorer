# VGA Advanced Scripts review, 7 October 2026

Every supplied CD global is reviewed below against its decoded instructions and the EGA reference. Names describe static code, not proof that a branch runs. Character/story readings retain the reading evidence level. The runtime accepts reviewed labels only when the decoded instruction fingerprint matches; another build remains unnamed until checked.

CD differences: 4 yields once, 39 and 115 return immediately; 40 plays CD effect cues. The EGA save/music titles are not copied onto CD stubs. CD 51/55/116 are absent; 117-125 are CD additions. Correction from the 8 October reverse review: EGA 90/99 also contain Mandible's introduction after the Room 42 flashes and Goodmold's aftermath before the Room 20 object draws; their old EGA titles described only part of the routines.

| CD script | EGA instructions / CD instructions | Title | Evidence | Source |
| --- | --- | --- | --- | --- |
| 001 | 79 / 146 | boot | decoded | CD Script 01 in DISK01.LEC: +0x02DD print, +0x031C loadRoom, +0x0327 print, +0x033C print; reviewed decoded instruction stream; compared with EGA Script 01 (boot), CD differences retained |
| 002 | 11 / 11 | cutscene start hook | decoded | CD Script 02 in DISK01.LEC: +0x0000 cursorCommand, +0x0002 cursorCommand, +0x0004 freezeScripts; reviewed decoded instruction stream; compared with EGA Script 02 (cutscene start hook), CD differences retained |
| 003 | 9 / 9 | cutscene end hook | decoded | CD Script 03 in DISK01.LEC: +0x0000 isEqual, +0x0007 notEqualZero, +0x000C startScript; reviewed decoded instruction stream; compared with EGA Script 03 (cutscene end hook), CD differences retained |
| 004 | 116 / 2 | yield once and return (CD stub) | decoded | CD Script 04 in DISK01.LEC: +0x0000 breakHere, +0x0001 stopObjectCode; reviewed decoded instruction stream; compared with EGA Script 04 (save screen (F5)), CD differences retained |
| 005 | 151 / 156 | input script | decoded | CD Script 05 in DISK01.LEC: +0x0018 print, +0x0037 chainScript, +0x0053 print, +0x0083 print; reviewed decoded instruction stream; compared with EGA Script 05 (input script), CD differences retained |
| 006 | 86 / 93 | click handler (input loop) | decoded | CD Script 06 in DISK01.LEC: +0x0106 startObject, +0x0182 startObject; reviewed decoded instruction stream; compared with EGA Script 06 (click handler (input loop)), CD differences retained |
| 007 | 12 / 12 | run a sentence (verb on an object) | decoded | CD Script 07 in DISK01.LEC: +0x001E print, +0x002E startObject, +0x004C chainScript; reviewed decoded instruction stream; compared with EGA Script 07 (run a sentence (verb on an object)), CD differences retained |
| 008 | 14 / 14 | show or hide the close-up in the verb area | decoded | CD Script 08 in DISK01.LEC: +0x0025 startObject, +0x0050 print; reviewed decoded instruction stream; compared with EGA Script 08 (show or hide the close-up in the verb area), CD differences retained |
| 009 | 26 / 26 | hint button (practice mode) | reading | CD Script 09 in DISK01.LEC: +0x000C chainScript, +0x001D chainScript; reviewed decoded instruction stream; compared with EGA Script 09 (hint button (practice mode)), CD differences retained |
| 010 | 17 / 17 | a distaff note was played | decoded | CD Script 10 in DISK01.LEC: +0x000C chainScript, +0x001D chainScript, +0x0027 chainScript, +0x004F chainScript; reviewed decoded instruction stream; compared with EGA Script 10 (a distaff note was played), CD differences retained |
| 011 | 13 / 13 | "That thread's too high for me." | decoded | CD Script 11 in DISK01.LEC: +0x0025 print; reviewed decoded instruction stream; compared with EGA Script 11 ("that thread is too high for me"), CD differences retained |
| 012 | 13 / 13 | "Maybe I should stand a bit closer." | decoded | CD Script 12 in DISK01.LEC: +0x001E print; reviewed decoded instruction stream; compared with EGA Script 12 ("I should get closer"), CD differences retained |
| 013 | 12 / 12 | "It might help to point at something first" | decoded | CD Script 13 in DISK01.LEC: +0x001B print; reviewed decoded instruction stream; compared with EGA Script 13 ("I should aim at something first"), CD differences retained |
| 014 | 11 / 9 | after-entry hook | decoded | CD Script 14 in DISK01.LEC: +0x0000 equalZero, +0x0005 notEqualZero, +0x000A isEqual; reviewed decoded instruction stream; compared with EGA Script 14 (after-entry hook), CD differences retained |
| 015 | 8 / 12 | before-exit hook | decoded | CD Script 15 in DISK01.LEC: +0x0024 print; reviewed decoded instruction stream; compared with EGA Script 15 (before-exit hook), CD differences retained |
| 016 | 3 / 3 | set the text line | decoded | CD Script 16 in DISK01.LEC: +0x0005 print; reviewed decoded instruction stream; compared with EGA Script 16 (set the text line), CD differences retained |
| 017 | 43 / 51 | give the ego a costume (by size) | decoded | CD Script 17 in DISK01.LEC: +0x00BE actorOps, +0x00E3 actorOps; reviewed decoded instruction stream; compared with EGA Script 17 (give the ego a costume (by size)), CD differences retained |
| 018 | 95 / 104 | show the distaff | decoded | CD Script 18 in DISK01.LEC: +0x00D1 print, +0x010F print, +0x014D print, +0x018B print; reviewed decoded instruction stream; compared with EGA Script 18 (show the distaff), CD differences retained |
| 019 | 16 / 24 | hide the distaff verbs | decoded | CD Script 19 in DISK01.LEC: +0x0000 move, +0x0005 verbOps, +0x000A increment; reviewed decoded instruction stream; compared with EGA Script 19 (hide the distaff verbs), CD differences retained |
| 020 | 31 / 38 | play the draft back | reading | CD Script 20 in DISK01.LEC: +0x0005 print, +0x004F actorOps, +0x005C actorOps, +0x0069 actorOps; reviewed decoded instruction stream; compared with EGA Script 20 (play the draft back), CD differences retained |
| 021 | 16 / 18 | end the draft playback | decoded | CD Script 21 in DISK01.LEC: +0x001B actorOps, +0x0024 actorOps; reviewed decoded instruction stream; compared with EGA Script 21 (end the draft playback), CD differences retained |
| 022 | 54 / 57 | compare the played draft with the known ones | reading | CD Script 22 in DISK01.LEC: +0x001F print, +0x00BA chainScript, +0x011C chainScript, +0x014E print; reviewed decoded instruction stream; compared with EGA Script 22 (compare the played draft with the known ones), CD differences retained |
| 023 | 33 / 33 | load draft N | decoded | CD Script 23 in DISK01.LEC: +0x0000 multiply, +0x0005 move, +0x000C increment; reviewed decoded instruction stream; compared with EGA Script 23 (load draft N), CD differences retained |
| 024 | 7 / 7 | check whether draft N is learned | decoded | CD Script 24 in DISK01.LEC: +0x0000 move, +0x0005 multiply, +0x000A move; reviewed decoded instruction stream |
| 025 | 7 / 7 | mark draft N as learned | decoded | CD Script 25 in DISK01.LEC: +0x0000 move, +0x0005 move, +0x000A multiply; reviewed decoded instruction stream |
| 026 | 31 / 31 | run the matched draft on the aimed object | reading | CD Script 26 in DISK01.LEC: +0x0019 print, +0x00B1 print; reviewed decoded instruction stream |
| 027 | 63 / 63 | mark a note in the verb area | reading | CD Script 27 in DISK01.LEC: +0x003C print, +0x0052 print, +0x0068 print, +0x007E print; reviewed decoded instruction stream; compared with EGA Script 27 (mark a note in the verb area), CD differences retained |
| 028 | 8 / 8 | clear the draft buffer | decoded | CD Script 28 in DISK01.LEC: +0x0000 isEqual, +0x0007 isNotEqual, +0x000E startScript; reviewed decoded instruction stream; compared with EGA Script 28 (clear the draft buffer), CD differences retained |
| 029 | 52 / 54 | learning glow | reading | CD Script 29 in DISK01.LEC: +0x0031 actorOps, +0x006D print; reviewed decoded instruction stream; compared with EGA Script 29 (learning glow), CD differences retained |
| 030 | 5 / 5 | play one note of a draft on an object | decoded | CD Script 30 in DISK01.LEC: +0x0012 chainScript; reviewed decoded instruction stream; compared with EGA Script 30 (play one note of a draft on an object), CD differences retained |
| 031 | 120 / 156 | animate and sound one note | decoded | CD Script 31 in DISK01.LEC: +0x0054 print, +0x0095 print, +0x00D6 print, +0x0117 print; reviewed decoded instruction stream; compared with EGA Script 31 (animate and sound one note), CD differences retained |
| 032 | 5 / 5 | recolour an actor's palette | decoded | CD Script 32 in DISK01.LEC: +0x0005 actorOps; reviewed decoded instruction stream |
| 033 | 5 / 5 | restore an actor's palette | decoded | CD Script 33 in DISK01.LEC: +0x0005 actorOps; reviewed decoded instruction stream |
| 034 | 7 / 7 | mark draft N's last-note knowledge | reading | CD Script 34 in DISK01.LEC: +0x0000 move, +0x0005 move, +0x000A multiply; reviewed decoded instruction stream |
| 035 | 14 / 13 | reject a draft response | reading | CD Script 35 in DISK01.LEC: +0x0016 print; reviewed decoded instruction stream |
| 036 | 32 / 32 | restore the note letters | decoded | CD Script 36 in DISK01.LEC: +0x0024 print, +0x003B print, +0x0052 print, +0x0069 print; reviewed decoded instruction stream |
| 037 | 11 / 11 | play the queued sound and wait | decoded | CD Script 37 in DISK01.LEC: +0x0000 notEqualZero, +0x0005 breakHere, +0x0006 move; reviewed decoded instruction stream |
| 038 | 10 / 10 | stop queued and current sounds | decoded | CD Script 38 in DISK01.LEC: +0x0000 notEqualZero, +0x0005 stopSound, +0x0008 notEqualZero; reviewed decoded instruction stream |
| 039 | 20 / 1 | return immediately (CD stub) | decoded | CD Script 39 in DISK01.LEC: +0x0000 stopObjectCode; reviewed decoded instruction stream; compared with EGA Script 39 (play a sound (music)), CD differences retained |
| 040 | 19 / 5 | play a CD effect cue | decoded | CD Script 40 in DISK01.LEC: +0x0007 print, +0x0016 print; reviewed decoded instruction stream; compared with EGA Script 40 (play a sound (effect)), CD differences retained |
| 041 | 34 / 34 | spell visual at a point | reading | CD Script 41 in DISK01.LEC: +0x001F actorOps, +0x008C actorOps; reviewed decoded instruction stream; compared with EGA Script 41 (spell visual at a point), CD differences retained |
| 042 | 35 / 35 | Room 03's flying actor | decoded | CD Script 42 in DISK01.LEC: +0x000D actorOps; reviewed decoded instruction stream; compared with EGA Script 42 (Room 03's flying actor), CD differences retained |
| 043 | 2 / 2 | "You cannot save a demonstration game." | decoded | CD Script 43 in DISK01.LEC: +0x0000 print; reviewed decoded instruction stream |
| 044 | 303 / 338 | opening cutscene | decoded | CD Script 44 in DISK01.LEC: +0x008F loadRoom, +0x00B4 actorOps, +0x0107 print, +0x0163 loadRoom; reviewed decoded instruction stream; compared with EGA Script 44 (opening cutscene), CD differences retained |
| 045 | 45 / 47 | Room 02 walk-box watcher | decoded | CD Script 45 in DISK01.LEC: +0x0000 breakHere, +0x0001 getActorWalkBox, +0x0005 breakHere; reviewed decoded instruction stream; compared with EGA Script 45 (Room 02 walk-box watcher), CD differences retained |
| 046 | 833 / 759 | the Elders' trial | decoded | CD Script 46 in DISK01.LEC: +0x00B1 print, +0x0127 actorOps, +0x014C print, +0x0199 print; reviewed decoded instruction stream; compared with EGA Script 46 (the Elders' trial), CD differences retained |
| 047 | 374 / 383 | Hetchel speaks after the trial | reading | CD Script 47 in DISK01.LEC: +0x004B actorOps, +0x0095 actorOps, +0x011A print, +0x013D print; reviewed decoded instruction stream |
| 048 | 50 / 52 | Room 63 to Room 62 transition | reading | CD Script 48 in DISK01.LEC: +0x0040 actorOps, +0x00BB loadRoom, +0x00C0 actorOps; reviewed decoded instruction stream |
| 049 | 163 / 165 | whirlwind scene (Rooms 02 and 11) | reading | CD Script 49 in DISK01.LEC: +0x000A print, +0x00A9 actorOps, +0x00EB print, +0x0106 actorOps; reviewed decoded instruction stream |
| 050 | 21 / 21 | Room 03 to Room 10 transition | decoded | CD Script 50 in DISK01.LEC: +0x0000 loadRoom, +0x0015 actorOps, +0x003C loadRoom; reviewed decoded instruction stream |
| 052 | 81 / 89 | Room 10 to Room 13 cutscene | decoded | CD Script 52 in DISK01.LEC: +0x0013 actorOps, +0x0045 loadRoom, +0x0070 actorOps, +0x00A9 actorOps; reviewed decoded instruction stream; compared with EGA Script 52 (Room 10 to Room 13 cutscene), CD differences retained |
| 053 | 126 / 130 | Room 13 scene with actors 8 and 9 | decoded | CD Script 53 in DISK01.LEC: +0x003A actorOps, +0x0061 actorOps, +0x007D print, +0x0111 loadRoom; reviewed decoded instruction stream |
| 054 | 44 / 44 | Room 15 arrival and learning glow | decoded | CD Script 54 in DISK01.LEC: +0x000A loadRoom, +0x0020 actorOps, +0x0038 actorOps, +0x0056 actorOps; reviewed decoded instruction stream |
| 056 | 15 / 17 | transition into Room 20 | decoded | CD Script 56 in DISK01.LEC: +0x0012 print, +0x0036 loadRoom, +0x003B chainScript; reviewed decoded instruction stream |
| 057 | 13 / 13 | room-local transition through scripts 200 and 207 | decoded | CD Script 57 in DISK01.LEC: +0x002A chainScript; reviewed decoded instruction stream |
| 058 | 8 / 8 | room-local transition through scripts 200 and 59 | decoded | CD Script 58 in DISK01.LEC: +0x0019 chainScript; reviewed decoded instruction stream |
| 059 | 17 / 18 | transition into Room 18 | decoded | CD Script 59 in DISK01.LEC: +0x000A loadRoom, +0x0016 print; reviewed decoded instruction stream |
| 060 | 8 / 8 | room-local transition through scripts 201 and 61 | decoded | CD Script 60 in DISK01.LEC: +0x0019 chainScript; reviewed decoded instruction stream |
| 061 | 13 / 14 | Room 18 arrival through local 203 | decoded | CD Script 61 in DISK01.LEC: +0x000A loadRoom, +0x0016 print; reviewed decoded instruction stream |
| 062 | 11 / 13 | transition into Room 20 through script 63 | decoded | CD Script 62 in DISK01.LEC: +0x0005 print, +0x0029 loadRoom, +0x002E chainScript; reviewed decoded instruction stream |
| 063 | 9 / 9 | room-local transition through scripts 201 and 207 | decoded | CD Script 63 in DISK01.LEC: +0x001D chainScript; reviewed decoded instruction stream |
| 064 | 9 / 11 | "Huh! A glass bell!" | reading | CD Script 64 in DISK01.LEC: +0x0009 print, +0x002D print; reviewed decoded instruction stream |
| 065 | 187 / 189 | Room 19 spell scene | reading | CD Script 65 in DISK01.LEC: +0x0015 loadRoom, +0x002B actorOps, +0x0091 actorOps, +0x00A9 actorOps; reviewed decoded instruction stream |
| 066 | 68 / 67 | Goodmold conversation in Room 17 | reading | CD Script 66 in DISK01.LEC: +0x001F actorOps, +0x003C actorOps, +0x0042 actorOps, +0x0048 actorOps; reviewed decoded instruction stream |
| 067 | 61 / 53 | Goodmold conversation in Room 22 | reading | CD Script 67 in DISK01.LEC: +0x0095 loadRoom, +0x009D print, +0x00B1 chainScript; reviewed decoded instruction stream |
| 068 | 170 / 134 | Master Goodmold's greeting | reading | CD Script 68 in DISK01.LEC: +0x002B print, +0x0064 actorOps, +0x0077 print, +0x00E8 print; reviewed decoded instruction stream |
| 069 | 227 / 266 | Fleece's greeting and the sheep problem | reading | CD Script 69 in DISK01.LEC: +0x003F print, +0x0083 print, +0x00BA actorOps, +0x0100 actorOps; reviewed decoded instruction stream |
| 070 | 82 / 86 | Room 24 sheep scene | decoded | CD Script 70 in DISK01.LEC: +0x0039 actorOps, +0x00C1 actorOps, +0x00E8 actorOps, +0x00EF print; reviewed decoded instruction stream |
| 071 | 7 / 7 | "I'm dizzy!" | reading | CD Script 71 in DISK01.LEC: +0x0014 print; reviewed decoded instruction stream |
| 072 | 28 / 27 | Room 65 to Room 30 transition | decoded | CD Script 72 in DISK01.LEC: +0x0024 loadRoom, +0x004B loadRoom; reviewed decoded instruction stream |
| 073 | 12 / 12 | walk actor 8 across Room 24 | decoded | CD Script 73 in DISK01.LEC: +0x000D actorOps, +0x0030 actorOps; reviewed decoded instruction stream |
| 074 | 17 / 22 | dragon's arrival greeting (Room 28) | reading | CD Script 74 in DISK01.LEC: +0x000C loadRoom, +0x0012 print, +0x0041 actorOps, +0x0047 print; reviewed decoded instruction stream |
| 075 | 84 / 77 | conversation with the hungry dragon | reading | CD Script 75 in DISK01.LEC: +0x0009 actorOps, +0x000F print, +0x0059 actorOps, +0x0069 print; reviewed decoded instruction stream |
| 076 | 33 / 34 | dragon leaves Room 27 | reading | CD Script 76 in DISK01.LEC: +0x0002 loadRoom, +0x0014 actorOps, +0x0034 print, +0x00A2 loadRoom; reviewed decoded instruction stream |
| 077 | 18 / 18 | Room 28 arrival and object classes | decoded | CD Script 77 in DISK01.LEC: +0x0009 loadRoom; reviewed decoded instruction stream |
| 078 | 47 / 48 | Room 65 scene and arrival in Room 30 | decoded | CD Script 78 in DISK01.LEC: +0x0028 actorOps, +0x00AA actorOps, +0x00E2 loadRoom; reviewed decoded instruction stream |
| 079 | 33 / 35 | Room 32 arrival and walk-box changes | decoded | CD Script 79 in DISK01.LEC: +0x001C loadRoom; reviewed decoded instruction stream |
| 080 | 23 / 23 | Room 32 arrival | decoded | CD Script 80 in DISK01.LEC: +0x0026 loadRoom; reviewed decoded instruction stream |
| 081 | 22 / 22 | Room 30 arrival and lighting | decoded | CD Script 81 in DISK01.LEC: +0x0019 loadRoom; reviewed decoded instruction stream |
| 082 | 28 / 33 | arrival in Room 31 and blocked return | reading | CD Script 82 in DISK01.LEC: +0x0005 print, +0x000D actorOps, +0x0031 print, +0x0049 loadRoom; reviewed decoded instruction stream |
| 083 | 17 / 17 | Room 30 arrival | decoded | CD Script 83 in DISK01.LEC: +0x0019 loadRoom; reviewed decoded instruction stream |
| 084 | 18 / 18 | Room 31 arrival | decoded | CD Script 84 in DISK01.LEC: +0x0019 loadRoom; reviewed decoded instruction stream |
| 085 | 191 / 209 | Rusty Nailbender's greeting | reading | CD Script 85 in DISK01.LEC: +0x0032 print, +0x004C print, +0x008B print, +0x009B print; reviewed decoded instruction stream |
| 086 | 182 / 196 | firewood scene | reading | CD Script 86 in DISK01.LEC: +0x0043 print, +0x0097 print, +0x0130 print, +0x019B print; reviewed decoded instruction stream; compared with EGA Script 86 (firewood scene), CD differences retained |
| 087 | 428 / 383 | ghost scene (with CD close-up in Room 96) | reading | CD Script 87 in DISK01.LEC: +0x006A loadRoom, +0x0078 print, +0x00DD actorOps, +0x00F8 print; reviewed decoded instruction stream; compared with EGA Script 87 (ghost scene), CD differences retained |
| 088 | 218 / 215 | Room 34 scene with actors 3, 5 and 6 | decoded | CD Script 88 in DISK01.LEC: +0x0000 loadRoom, +0x0021 actorOps, +0x0049 actorOps, +0x00E9 print; reviewed decoded instruction stream |
| 089 | 43 / 44 | Room 11 scene with actor 4 | decoded | CD Script 89 in DISK01.LEC: +0x0010 loadRoom, +0x0025 actorOps, +0x0041 print, +0x007D loadRoomWithEgo; reviewed decoded instruction stream |
| 090 | 234 / 561 | Room 42 flashes and Mandible's introduction | reading | CD Script 90 in DISK01.LEC: +0x0005 loadRoom, +0x001D actorOps, +0x00D2 loadRoom, +0x00E2 print; reviewed decoded instruction stream; compared with EGA Script 90 (Room 42 flashes), CD differences retained |
| 091 | 478 / 529 | Room 43 cage scene | reading | CD Script 91 in DISK01.LEC: +0x000A print, +0x0045 actorOps, +0x00DC actorOps, +0x00F9 actorOps; reviewed decoded instruction stream; compared with EGA Script 91 (Room 43 cage scene), CD differences retained |
| 092 | 14 / 16 | Room 43 arrival | decoded | CD Script 92 in DISK01.LEC: +0x0005 loadRoom, +0x0025 actorOps, +0x005B actorOps; reviewed decoded instruction stream |
| 093 | 176 / 200 | Room 43 cage scene, second part | reading | CD Script 93 in DISK01.LEC: +0x0045 actorOps, +0x0051 print, +0x0096 print, +0x00D3 print; reviewed decoded instruction stream; compared with EGA Script 93 (Room 43 cage scene, second part), CD differences retained |
| 094 | 71 / 71 | Room 19 scene with actors 2 and 3 | decoded | CD Script 94 in DISK01.LEC: +0x0015 loadRoom, +0x002B actorOps, +0x009F actorOps, +0x00EE actorOps; reviewed decoded instruction stream |
| 095 | 293 / 608 | Mandible opens the Pattern | reading | CD Script 95 in DISK01.LEC: +0x0075 print, +0x00B5 print, +0x0100 print, +0x0122 loadRoom; reviewed decoded instruction stream |
| 096 | 57 / 59 | Room 45 fall | decoded | CD Script 96 in DISK01.LEC: +0x0010 actorOps, +0x0036 loadRoom, +0x005D print, +0x0092 actorOps; reviewed decoded instruction stream |
| 097 | 18 / 18 | Room 34 arrival | decoded | CD Script 97 in DISK01.LEC: +0x0013 loadRoom; reviewed decoded instruction stream |
| 098 | 201 / 200 | Room 34 ghost walk | reading | CD Script 98 in DISK01.LEC: +0x0060 print, +0x007D print, +0x00BF print, +0x0100 print; reviewed decoded instruction stream; compared with EGA Script 98 (Room 34 ghost walk), CD differences retained |
| 099 | 147 / 334 | Goodmold's aftermath and Room 20 objects | reading | CD Script 99 in DISK01.LEC: +0x0073 print, +0x00A8 actorOps, +0x00AF print, +0x00DF actorOps; reviewed decoded instruction stream; compared with EGA Script 99 (draws Room 20's objects), CD differences retained |
| 100 | 249 / 204 | Bobbin speaks with his mother | reading | CD Script 100 in DISK01.LEC: +0x0013 print, +0x0040 print, +0x0072 print, +0x00A5 print; reviewed decoded instruction stream |
| 101 | 300 / 328 | Hetchel confronts the Dead One | reading | CD Script 101 in DISK01.LEC: +0x0034 actorOps, +0x0051 actorOps, +0x008F print, +0x00C8 actorOps; reviewed decoded instruction stream |
| 102 | 45 / 45 | departure to Room 66 | decoded | CD Script 102 in DISK01.LEC: +0x0018 actorOps, +0x0027 print, +0x00A2 loadRoom, +0x00D2 actorOps; reviewed decoded instruction stream |
| 103 | 11 / 11 | "That side of the Pattern is now beyond the reach of our threads." | reading | CD Script 103 in DISK01.LEC: +0x001B print; reviewed decoded instruction stream |
| 104 | 241 / 195 | Room 51 scene with actors 1, 3 and 4 | decoded | CD Script 104 in DISK01.LEC: +0x007B print, +0x0094 actorOps, +0x00C6 actorOps, +0x00D5 actorOps; reviewed decoded instruction stream |
| 105 | 102 / 118 | Hetchel's feather and the Dead One | reading | CD Script 105 in DISK01.LEC: +0x0047 print, +0x00C0 print, +0x0130 print, +0x0186 actorOps; reviewed decoded instruction stream |
| 106 | 244 / 191 | ending in Room 66 | reading | CD Script 106 in DISK01.LEC: +0x0051 actorOps, +0x00B7 actorOps, +0x00DC actorOps, +0x012B print; reviewed decoded instruction stream |
| 107 | 41 / 40 | Room 10 arrival and object classes | decoded | CD Script 107 in DISK01.LEC: +0x0005 loadRoom, +0x0080 actorOps, +0x0098 actorOps, +0x00D0 chainScript; reviewed decoded instruction stream |
| 108 | 98 / 221 | glassmakers scene | reading | CD Script 108 in DISK01.LEC: +0x0053 print, +0x00B2 print, +0x011A print, +0x0189 print; reviewed decoded instruction stream; compared with EGA Script 108 (glassmakers scene), CD differences retained |
| 109 | 68 / 66 | dragon's gold conversation | reading | CD Script 109 in DISK01.LEC: +0x000F print, +0x0069 actorOps, +0x006F print, +0x00B2 actorOps; reviewed decoded instruction stream |
| 110 | 20 / 20 | Room 49 arrival and object classes | decoded | CD Script 110 in DISK01.LEC: +0x0016 loadRoom; reviewed decoded instruction stream |
| 111 | 62 / 63 | Bishop Mandible scene in Room 86 | reading | CD Script 111 in DISK01.LEC: +0x0015 loadRoom, +0x002B actorOps, +0x00DD print, +0x0124 loadRoom; reviewed decoded instruction stream |
| 112 | 25 / 26 | "He's fast asleep." | reading | CD Script 112 in DISK01.LEC: +0x0005 print, +0x003B loadRoom, +0x0040 loadRoom, +0x0047 print; reviewed decoded instruction stream |
| 113 | 20 / 21 | ambient actors during the intro | decoded | CD Script 113 in DISK01.LEC: +0x0010 actorOps; reviewed decoded instruction stream; compared with EGA Script 113 (ambient actors during the intro), CD differences retained |
| 114 | 24 / 25 | ambient actors during the intro | decoded | CD Script 114 in DISK01.LEC: +0x0010 actorOps; reviewed decoded instruction stream; compared with EGA Script 114 (ambient actors during the intro), CD differences retained |
| 115 | 10 / 1 | return immediately (CD stub) | decoded | CD Script 115 in DISK01.LEC: +0x0000 stopObjectCode; reviewed decoded instruction stream |
| 117 | 0 / 6 | debug display of the music flag | decoded | CD Script 117 in DISK01.LEC: +0x0000 print; reviewed decoded instruction stream |
| 118 | 0 / 16 | animate actor 3 in Room 45 | decoded | CD Script 118 in DISK01.LEC: +0x000D actorOps; reviewed decoded instruction stream |
| 119 | 0 / 27 | choose a random failed-draft reply | reading | CD Script 119 in DISK01.LEC: +0x000F print, +0x0037 print, +0x0071 print, +0x00A2 print; reviewed decoded instruction stream |
| 120 | 0 / 429 | set up a game from a passcode | decoded | CD Script 120 in DISK01.LEC: +0x0094 loadRoom, +0x06E4 chainScript, +0x0943 chainScript; reviewed decoded instruction stream |
| 121 | 0 / 59 | assign draft patterns for a passcode | decoded | CD Script 121 in DISK01.LEC: +0x0000 print; reviewed decoded instruction stream |
| 122 | 0 / 63 | the Bishop's sphere order | reading | CD Script 122 in DISK01.LEC: +0x000A loadRoom, +0x0018 print, +0x002B loadRoom, +0x0033 print; reviewed decoded instruction stream |
| 123 | 0 / 12 | walk actor 11 across Room 16 | decoded | CD Script 123 in DISK01.LEC: +0x000D actorOps, +0x0030 actorOps; reviewed decoded instruction stream |
| 124 | 0 / 5 | keep drawing random numbers | decoded | CD Script 124 in DISK01.LEC: +0x0000 getRandomNr, +0x0004 getRandomNr, +0x0009 breakHere; reviewed decoded instruction stream |
| 125 | 0 / 10 | split the passcode into decimal digits | decoded | CD Script 125 in DISK01.LEC: +0x0000 print; reviewed decoded instruction stream |
