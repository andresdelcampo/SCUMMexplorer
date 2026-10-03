# Costumes that do not show (3 October 2026)

Question: why do costumes 20, 21, 42, 66, 70, 74, 75, 89, 90, 91, 136 and 171-176 show no picture in the gallery?

Method: a throwaway probe (not committed) scanned all 176 costume directory entries of `Games/loom` through `LoomCostumeReader`/`LoomCostumeRenderer`, then searched every LFL for a valid costume header (length word, format 0x58, offset table inside the resource) at the indexed offset, and asked `LoomRelationshipIndexer` for any script that uses them.

Result: 159 costumes decode and draw. The 17 others fail at the header, before any drawing, because the index entry points at nothing that is a costume:

| Costume | Index entry | Why it cannot be read |
| --- | --- | --- |
| 074, 075 | 21.LFL | the file does not exist in this release |
| 020, 021, 070, 090, 091 | 10.LFL@34497, 03.LFL@15726, 15.LFL@23562, 22.LFL@22088, 16.LFL@55530 | offset lies beyond the end of the file |
| 042, 066, 172, 174, 175 | 09@30998, 15@19325, 53@30255, 19@32116, 19@33119 | the word at the offset is not a plausible length |
| 089, 136, 171, 173, 176 | 20@17805, 27@7972, 18@18619, 53@36514, 47@21463 | format byte is 0x08, 0x00, 0x00, 0x3C, 0x01 (not 0x58) |

No other LFL holds a valid costume header at those offsets. None of the 17 is named by any decoded script (248 literal costume uses, none of them these). The same situation is recorded for scripts 000/051/055/116 in LOOM_SCRIPT_MAP.md.

Conclusion: leftover index entries, not a renderer or reader bug. ScummVM reads the index the same way, so it could not load them either. Reading, not proof: the data of the real costumes at those places was probably removed when the release was built.
