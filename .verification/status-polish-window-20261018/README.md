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

Pending integration commits and exclusive WPF lease. Root owns production changes, support matrix and checkpoint.
