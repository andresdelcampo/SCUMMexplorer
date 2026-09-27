# Validation: leftover index entries in Game Texts (27 September 2026)

## Change

- New `LflResourceChain` (Core) walks an LFL file as a chain of resources (16-bit length + data, XOR 0xFF), the way ScummVM's `loadResource` reads one. `FindLeftover(game, entry)` returns an `LflLeftoverIndexEntry` when an index entry's offset is not a resource start but lies strictly inside another resource. It names the index entry that starts that resource, or "an unindexed resource at 0x…" when none does. The check is general: it uses no ID list.
- `LoomTextExtractor` checks each indexed script against the chain before decoding it. A leftover entry is collected in `LoomTextExtraction.LeftoverEntries`, is not decoded, and is no longer counted as a skipped range or listed among the decode diagnostics.
- The Game Texts browser (Source and extraction details) lists these entries in their own paragraph: "leftover index entry (points inside <owner>)", with the file, offset and distance into the owning resource. So coverage claims still name them.

## Results

| | Before (game-text-refinement) | After |
| --- | --- | --- |
| Entries | 719 | 719 |
| Ranges visited | 1,419 | 1,419 |
| Ranges skipped | 4 (scripts 0, 51, 55, 116: "declares N bytes … past the end of NN.LFL") | 0 |
| Leftover index entries | not shown | 4, listed separately |
| Unresolved paths | 2 (Room 70 local scripts 203 and 204, truncated) | 2, unchanged |

The four entries and their owners match the [index-chain walk](../global-script-index/index-chain.txt): script 0 → Costume 01, 51 → Script 49, 55 → Room 18, 116 → Script 114.

## Checks

- Tests: **114/114**, 0 warnings. `LflResourceChainTests` has two synthetic tests (chain walking; leftover detection and owner naming) and one real-data test. The real-data test checks that the extractor lists exactly scripts 0, 51, 55 and 116 as leftovers with their owners, that no diagnostic names them, and that no range is skipped. `run-verify.cmd`: build, tests and audit all exit 0; 0 gaps.
- WPF: [TextGalleryCheck](../TextGalleryCheck/Program.cs) opens the real gallery. It checks that the extraction details contain each leftover description and that no diagnostic line names a leftover entry, then captures the expanded details ([capture](extraction-details.png), [text](window-check.txt)). All earlier gallery checks still pass. A synthetic routed event was used; physical input was not tested.

## Limits

- Only indexed scripts are checked in the extractor. The index-chain walk found 17 more off-chain entries (15 costumes and sounds 24 and 79); they are not text sources, and the extractor does not visit them.
- Complete stored-text coverage is still open: text outside decoded control flow is not scanned, and Room 70 local scripts 203 and 204 are truncated.
