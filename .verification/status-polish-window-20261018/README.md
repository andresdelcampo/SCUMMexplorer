# Maniac/Zak Game Status presentation acceptance

18 October 2026. Scope: the four supplied Maniac/Zak fixtures, the user's character/body-picture, logical-item and compact-layout corrections. The existing accepted native acquisition routes are reused directly from `../admin-inventory-ui-20261018/WindowCheck/`; no game assets are copied.

## Harness

`WindowCheck/StatusPolishWindowCheck.csproj` runs the actual MainWindow and modal InventoryStatusWindow on an isolated profile/cache. Run each process serially, never overlapping WPF windows. Parameters: fixture directory, unique output directory, initial room (44 Maniac; 1 Zak), prior accepted native action JSON path.

The harness checks:

- Maniac Game Status stays hidden during the entire native Room 45 selection, including partially selected parties, and appears after Start; only the three selected characters are recipients.
- Zak offers all four playable characters. Every recipient has a native full-body picture, rendered in both selected choice and expanded dropdown. The appearance API leaves live actor snapshots unchanged. The smaller native Mars-character sprites are accepted at their actual dimensions.
- Maniac offers 48 items, Zak 70 logical items. Remote source aliases 429/431/471/535/536/537 are absent as separate choices; distinct homonyms have unique stable ordinal labels without raw IDs in Normal.
- Catalog and Add controls are 32 pixels high and bottom aligned with Give to, within one pixel, at both 900x720 and 780x560. Search sits above the catalog.
- The original held-item transfer, catalog grant, retained source room, exact Undo, unchanged unrelated state, search, pending capacity, Close discard and modal clock restoration assertions remain.

No ScummVM runtime, original game, saves or normal user profile is opened or modified. Timer advancement is deterministic, so this does not establish perceived audiovisual timing. Captures/output/profile/cache are local ignored evidence under `window-*`.

## Result

Character API `de2ae49e` and App `afaa564` integrated into the verification worktree. Harness build passes with zero warnings/errors. Two serial Maniac actual-window processes pass:

| Fixture | Source bodies | Items | Result |
|---|---|---|---|
| ManiacClassic | Syd 18x70, Dave 18x72, Bernard 22x73 | 48 | PASS, frame 3309 |
| ManiacEnhanced | Syd 20x74, Dave 22x74, Bernard 22x73 | 48 | PASS, frame 1555 |

For both fixtures, both window sizes report Catalog and Add height 32, and equal bottom coordinates of 247.4133 device-independent pixels. Every source body is checked in the selected recipient choice and dropdown. Apply/Undo, capacity, modal pause/restore and source-state guards pass. Minimum-size Classic layout and Enhanced native three-character dropdown captures were inspected: compact controls align correctly and all displayed people have full bodies.

Zak final logical-catalog integration and its two windows are pending. Root owns production changes, support matrix and checkpoint.

## Retained unsuccessful optional checks

The first build of optional alternate-party support needed a nullable-room `.Value` correction; the next build was clean. Optional Enhanced Razor/Wendy coverage was then attempted without guessing coordinates. Attempt one looked for native menu object names; those object headers did not identify the named portraits. Attempt two followed native party-assignment helper operands, but the verifier still found no matching object before sending any input. Both failed logs are retained locally under `window-maniac-enhanced-alternate` and `window-maniac-enhanced-alternate-source`. Neither is acceptance evidence or a demonstrated product failure. After two approaches, the optional mode was removed. The accepted existing native party selection is unchanged; other playable children's live-window coverage is not claimed here.
