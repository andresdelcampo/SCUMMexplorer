# Maniac Mansion enhanced S9: reviewed Spanish font and edition boundary

| | |
|---|---|
| Family / stage | ManiacEnhanced / S9 |
| Fixture and edition | `D:\Juegos\Lucas\mmansionE ega es`, Spanish DOS v2 |
| Date and author | 2026-10-18, Codex |
| Commits | Decoder/tests `4acfd5e`; parent wiring `80587b6` |
| Registry change | Unchanged: this corrects the reviewed edition; no alternate enhanced copy is available to establish compatibility. |

## What was done

One bounded edition harness inventories immediate local game folders, detects the fixture from its own files, checks script/preview fingerprints, and compares every text glyph against the executable's decoded font. Four regression tests cover detection/readable labels, exact decoded font hash and low/high glyphs, all 128 text slots, absent/unreviewed executable rejection, and malformed-font rejection with unchanged fallback. No assets are committed.

## Commands run (through RTK)

From this isolated worktree:

```powershell
rtk proxy dotnet run --project .verification/maniacenhanced-editions-20261018/EditionCheck -- "D:\Juegos\Lucas" "D:\Juegos\Lucas\mmansionE ega es"
rtk proxy dotnet test tests/SCUMMexplorer.Tests --filter FullyQualifiedName~ScummManiacEnhancedEditionTests --no-restore
```

The harness was rerun after replacing the rejected raw-slice reference with source-backed decompression and integrating parent font wiring. Logs: `baseline.txt` (failed reference retained), `final.txt`, `tests.txt`. Parent runs shared LOOM regression, real-window check and final full suite.

## Results (numbers)

| Edition | Check | Result |
|---|---|---|
| Available Spanish enhanced | Own detection | ManiacEnhanced, V2, Spanish; 53 rooms, 169 global scripts |
| Available English classic | Alternate enhanced edition | Excluded: separate ManiacClassic/V1 family, 52 rooms |
| Spanish enhanced | Native decoded font | 1024 bytes; all 128 text slots pass (127 drawable glyphs plus suppressed `@` marker) |
| Spanish enhanced | Previous built-in Spanish font | 117/128 text slots match; 11 localized glyphs differ: 60,61,62,91,92,93,95,123,124,125,126 |
| Spanish enhanced | Own preview scan | 59 triggers / 27 groups; verb 15 readable as `Qué es` |
| Spanish enhanced | Focused regressions | 4 passed, 0 failed, 0 skipped |
| Any second enhanced language/demo/patched edition | Cross-edition scripts/text/fingerprint comparison | BLOCKED: no second local enhanced copy |

Executable SHA-256: `AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A` (69084 bytes). Encoded range offset 64128, length 1012; decompressed length 1058; remove 34 leading padding bytes. Result SHA-256: `098B285F43AEC7C97CA2C05D07D31365D2C8371304645AFFC20FEBCE7B12453D`. Room 45 SHA-256 remains `AF22D5CC9774D55BA73F8B092372BB5CE23624F00A93860C55298559D5606E23`. Detection is still based on room files; executable absence does not change family identity.

## Comparison with ScummVM (source files and date; runtime comparison if any)

ScummVM `engines/scumm/charset-fontdata.cpp`, master fetched 2026-10-17 and reviewed 2026-10-18, contains the executable extraction loop: a byte followed by `00 B2 value count 00 B0` expands to `count` copies of `value`; otherwise copy the current byte. Applying this loop gives runs `(encoded offset, decoded offset, value, count)`: (64128,0,0,42), (64167,74,3,11), (64328,239,24,11), (64375,290,0,8), (64482,398,0,9). The decoded boundary is established by matching all 114 unchanged glyphs against the source English table; 14 localized glyphs differ. This differs from the 11 corrections against the existing Spanish replacement table.

Native arrow glyph 1 rows are `0103060C183E0300`; native é at slot 93 is `08103C667E603C00`. These pin both compressed and contiguous regions. Loader accepts only the complete reviewed executable hash and resulting font hash, otherwise uses existing fallback. Native bytes bypass language substitutions. No ScummVM executable was run, and no original-game runtime comparison was performed.

Provenance: the source was reused from the S6 cache at `.worktrees/maniac-classic-s6/obj/charset-fontdata.cpp`, not freshly downloaded for S9. File SHA-256 `D33317844886EEBD2FB8A8950893B91D0581CB36F0036D3DD3AABA6D6514A15E`; extraction loop lines 794–805, localization comment 787–788. Original upstream URL: `https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/charset-fontdata.cpp`.

## Differences and uncertainties kept

The S7 raw slice at offset 64116 was valid for the inspected high accent slots but is **not a contiguous complete font**: lower slots contain compression instructions. The initial S9 `baseline.txt` comparison of 69/128 against that slice is invalid as a full-font reference and is retained as failed evidence. Parent reverted the premature raw-slice implementation before integrating the decoder. No partial font hack ships.

S7 evidence remains 59 triggers / 27 groups / 25 scenes / 7 begun; no full census rerun was needed. S5 original-game visual comparison, native darkness/flashlight fidelity, and prior bounded runtime limitations remain open. Unreviewed/missing executables retain the earlier built-in font, which may not match their localization. Alternate enhanced comparisons remain blocked, not accepted by analogy with classic.

## User checks (exact steps and where; none if none)

No new user action required. Parent inspected the real window's TV preview: Room 17 object 190 verb 4 starts Script 112, reaching Room 40; frame 51 shows native `músicos` / `diseñadores`. The native verb `Qué es` also exercises the corrected é. Parent owns the final screenshot record and full-suite checkpoint.

## Where the facts went (links to the script map / engine notes / tracker entries)

Family owner updates [Maniac script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md) and [tracker](../../docs/maniac/SUPPORT_STATUS.md). Parent owns shared engine font notes, attribution, registry, final checkpoint and handoff. Decoder is `ScummV2NativeFont`; tests are `ScummManiacEnhancedEditionTests`.
