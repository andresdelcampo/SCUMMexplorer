# Maniac/Zak Game Status presentation acceptance

18 October 2026. Scope: the four supplied Maniac/Zak fixtures, the user's character/body-picture, logical-item and compact-layout corrections. The existing accepted native acquisition routes are reused directly from `../admin-inventory-ui-20261018/WindowCheck/`; no game assets are copied.

## Harness

`WindowCheck/StatusPolishWindowCheck.csproj` runs the actual MainWindow and modal InventoryStatusWindow on an isolated profile/cache. Run each process serially, never overlapping WPF windows. Parameters: fixture directory, unique output directory, initial room (44 Maniac; 1 Zak), prior accepted native action JSON path.

Example from `.worktrees/status-polish-window`, with all commands through RTK:

```powershell
rtk proxy dotnet build .verification/status-polish-window-20261018/WindowCheck/StatusPolishWindowCheck.csproj --no-restore -p:NuGetAudit=false --verbosity quiet
rtk proxy Exe/StatusPolishWindowCheck/Debug/net10.0-windows/StatusPolishWindowCheck.exe "D:\Juegos\Lucas\zakE ega en" ".verification/status-polish-window-20261018/window-zak-enhanced" 1 ".verification/admin-inventory-ui-20261018/WindowCheck/zak-enhanced.json"
```

Repeat using `mmansion ega en`/`maniac-classic.json`, `mmansionE ega es`/`maniac-enhanced.json`, and `zak ega en`/`zak-classic.json`; the two Maniac checks use initial room44. Each output directory is unique, containing the private profile/cache, step report, session trace and captures. Native action configurations are unchanged.

The harness checks:

- Maniac Game Status stays hidden during the entire native Room 45 selection, including partially selected parties, and appears after Start; only the three selected characters are recipients.
- Zak offers all four playable characters. Every recipient has a native full-body picture, rendered in both selected choice and expanded dropdown. The appearance API leaves live actor snapshots unchanged. The smaller native Mars-character sprites are accepted at their actual dimensions.
- Maniac offers 48 items, Zak 70 logical items. Remote source aliases 429/431/471/535/536/537 are absent as separate choices; distinct homonyms have unique stable ordinal labels without raw IDs in Normal.
- Catalog and Add controls are 32 pixels high and bottom aligned with Give to, within one pixel, at both 900x720 and 780x560. Search sits above the catalog.
- The original held-item transfer, catalog grant, retained source room, exact Undo, unchanged unrelated state, search, pending capacity, Close discard and modal clock restoration assertions remain.

No ScummVM runtime, original game, saves or normal user profile is opened or modified. Timer advancement is deterministic, so this does not establish perceived audiovisual timing. Captures/output/profile/cache are local ignored evidence under `window-*`.

## Result

Character API `de2ae49e`, App `afaa564`, logical-identity analysis `4b8753e3` and integration `b2c77b5` were integrated into the verification worktree. Harness build passes with zero warnings/errors. All four serial actual-window processes pass:

| Fixture | Source bodies | Items | Result |
|---|---|---|---|
| ManiacClassic | Syd 18x70, Dave 18x72, Bernard 22x73 | 48 | PASS, frame 3309 |
| ManiacEnhanced | Syd 20x74, Dave 22x74, Bernard 22x73 | 48 | PASS, frame 1555 |
| ZakClassic | Zak 16x62, Annie 18x62, Melissa/Leslie 10x27 | 70 | PASS, frame 520 |
| ZakEnhanced | Zak 15x64, Annie 21x64, Melissa/Leslie 9x28 | 70 | PASS, frame 521 |

For all four fixtures, both window sizes report Catalog and Add height 32, and equal bottom coordinates of 247.4133 device-independent pixels. Every source body is checked in the selected recipient choice and dropdown. Apply/Undo, capacity, modal pause/restore and source-state guards pass. Maniac has only the three selected recipients; Zak has all four. The six aliases are absent from both Zak catalogs, and remaining real homonyms have unique stable ordinal labels. Both Zak routes explicitly grant nest 53 to Annie, retain Room 13 code and Undo exact state. Pending additions stop at the remaining available logical catalog: 47 of 79 free slots in Maniac, 67 of 77 in Zak; this does not mean the native 80-slot limit changed.

There are 67 local captures across the four accepted processes. Six key captures were inspected: Classic Maniac minimum layout, Enhanced Maniac native three-body dropdown, Classic Zak four-body dropdown and ordinal-item dropdown, Enhanced Zak four-body dropdown and minimum layout. Compact controls align correctly; all displayed people have full bodies. The item dropdown visibly distinguishes CashCard (1)/(2)/(3) and crystal shard (1)/(2), while keeping true separate objects.

The final read-only identity-proof guard `4294422` arrived just after the Enhanced process started. Both Zak windows therefore use the immediately preceding valid-data behavior; after both exited, the guard was integrated and the own-data identity Probe rebuilt/run against both copies. It reports 70 items in each (`final-catalog.txt`, local ignored), and the final WPF harness rebuild is again zero warnings/errors. The guard only requires explicit known-empty verb tables and clean/complete retained-code disassemblies for identity proof; no fixture/UI behavior change was observed. Root's final full suite covers the final combined source. No extra overlapping WPF run was introduced.

Root owns production changes, support matrix and checkpoint. These window checks establish bounded presentation/edit behavior, not an earned puzzle route or complete game playthrough.

## Retained unsuccessful optional checks

The first build of optional alternate-party support needed a nullable-room `.Value` correction; the next build was clean. Optional Enhanced Razor/Wendy coverage was then attempted without guessing coordinates. Attempt one looked for native menu object names; those object headers did not identify the named portraits. Attempt two followed native party-assignment helper operands, but the verifier still found no matching object before sending any input. Both failed logs are retained locally under `window-maniac-enhanced-alternate` and `window-maniac-enhanced-alternate-source`. Neither is acceptance evidence or a demonstrated product failure. After two approaches, the optional mode was removed. The accepted existing native party selection is unchanged; other playable children's live-window coverage is not claimed here.
