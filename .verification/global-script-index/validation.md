# Validation: undecodable global scripts 0, 51, 55 and 116 (27 September 2026)

**Verdict: the index entries are bogus leftovers. Our reader is correct.** No code change was needed. None of these entries can be the source of Room 58's first view, which keeps its labelled random pick.

## Comparison with ScummVM (`engines/scumm`, master)

- `resource_v3.cpp`, `ScummEngine_v3old::readResTypeList`: for every non-room type, the loader reads `num`, then `num` room-number bytes, then `num` 16-bit offsets (`0xFFFF` = invalid). `LflIndexReader.ReadDirectory` does the same, in the same order (rooms, costumes, scripts, sounds).
- `resource.cpp`, `ScummEngine::loadResource`: it opens the entry's room file, seeks to `_roomoffs`, and for `GF_OLD_BUNDLE` reads the size as a 16-bit value at that offset (sounds take a separate path). `LoomScriptReader.Decode` does the same and skips the 4-byte header. ScummVM has no workaround or remapping for these entries. It would read the same garbage size if a script ever started them.

## Evidence from the data

[`index-chain.py`](index-chain.py) → [`index-chain.txt`](index-chain.txt) walks each LFL file as a chain of `length(2) + tag(2)` resources. For every file involved, the chain covers the file exactly. All four entries point *inside* a resource that another index entry owns:

| Script | File, offset | Lies inside | Owner |
| --- | --- | --- | --- |
| 0 | 01.LFL 0x9E07 | 0x718C, +11387 bytes | costume 1 |
| 51 | 11.LFL 0x4775 | 0x449D, +728 bytes (5 bytes before script 52 at 0x477A) | script 49 |
| 55 | 18.LFL 0x41FB | 0x0000, +16891 bytes (the room block, which ends at 0x4236) | Room 18 |
| 116 | 02.LFL 0x6F67 | 0x6F4D, +26 bytes | script 114 |

These are not isolated cases. 21 directory entries point off-chain: 15 costumes, these 4 scripts, and sounds 24 and 79. Another 13 entries (rooms 21, 48, 71–78 and 80, and costumes 74 and 75) name LFL files that this release does not contain. This fits stale index entries left in the shipped `00.LFL`.

[`ScriptRefs`](ScriptRefs/Program.cs) → [`script-references.txt`](script-references.txt) decodes 1,370 sources: every decodable global script, room entry and exit script, room local script and object verb. It finds:

- **No literal `startScript`/`chainScript`/`isScriptRunning` of 0, 51, 55 or 116.** The 22 literal-0 hits are `stopScript 0x00` (stop the current script, `o5_stopScript`) and `freezeScripts 0x00` (the unfreeze flag). Neither is a script ID.
- Three starts go through a variable. Room 02 local 203 starts `local[0]` for 0xCD…0xDD (its credits loop, local scripts 205–221). Room 10 local 205 starts `local[0]`, which its callers pass as 0xCE–0xD1 (object verbs 297–300). Neither reaches the four IDs.

So no reached or possible code path loads these scripts, and none of them can show Room 58 (objects 790–795). Room 58 still has no decoded batch and keeps its labelled seeded pick (`Room58WithoutScriptEvidenceUsesALabelledRandomPickAndLeavesNoGap`).

## Checks

- New real-data test `LoomScriptIndexTests.ScriptsThatFailToDecodePointInsideAnotherIndexedResource`. It checks that exactly scripts 0, 51, 55 and 116 fail to decode, that every other script entry starts a resource in its file, and that each failing entry lies strictly inside a resource another entry owns.
- Build: 0 warnings. Tests: **101/101**.

## Remaining

- The Game Texts browser and the batch scan still list these four as decode failures. They are now explained, but the UI does not yet mark them as leftover entries.
- The 17 costume and sound entries that point off-chain were not examined beyond this listing.
