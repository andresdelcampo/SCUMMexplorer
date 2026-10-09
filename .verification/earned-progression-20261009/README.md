# Source-earned learning and level review — 9 October 2026

**Later route checkpoint:** slots 3/7/11 now earn in the reference; seven additional unique learning checks and a Script 106 guard control pass 37 assertions. The complete English Room 46 excursion/Closing chain passes 25 more assertions, with actual source-earned Healing/six-note input and balanced 26/26 scopes. [Actual routes, corrected guard directions, packets and limits](ROUTES.md). Native physical input and audio remain manual.

English Roland EGA is the reference (`Games/loom ega mt32 en`); Spanish Roland EGA (`Games/ALTERNATIVE LOOMS/loom ega mt32 es`) and English CD/VGA (`Games/loom vga en`) use their own scripts, objects and bit banks. The existing actor inventory and its unreviewed outputs were read first and left unmodified/uncommitted. Additional source reads are in the separate actor `Review/` helper and ignored `out-review/`.

## Established evidence reviewed first

- [Level four](../archive/native-prerequisites-20261002/level4-validation.md): two source/input orders reach four notes with nine balanced scopes; learned and used masks are distinct, randomized-draft estimates remain. These routes were not repeated.
- [Later level/source controls](../archive/source-progression-20261002/validation.md): direct crystal-use and reverse-gold responses already reached five/six notes. Direct responses and accepted four-note casts have different coverage.
- [Late Room 09 input](../archive/room09-fix-20261003/validation.md): actual late-story prefix reaches Script 101; its later cast checks supplied learned slots/notes through status. Those supplied prerequisites were not credited as earning.
- [Current Silence source chain](../source-gaps-20261008/README.md): slot 12 and its actual reversal are source-established. No naming change is needed.

## Qualified results

**33 unique lesson checks: eleven slots per edition**, all changing from unlearned to learned through observed Script 25 entry with the matching argument. The packed word changes only mask `0x2000`; all end outside a cutscene with status editable. [Reduced results and packet hashes](summary.json).

| Slots | Executed source request | Coverage |
| --- | --- | --- |
| 1, 2, 4, 5, 6, 8, 9, 10 | Own-source wheel/dye/wind/sheep/cloth/sphere/scythe/straw lesson owners | Eight independent lessons in each edition; 24 checks. Each has balanced scopes. |
| 12, 13, 14 | Own Room 50 Use → Script 89 → Room 07 portal → Room 09 tree Look/Script 101, then own reverse-response callbacks | Nine further learning checks. The actual source prefix earns these flags; no status-supplied learned flag. |
| 0, 15 | Already learned by boot/pickup initialization | Not counted as new lessons. |

The current prefix is derived from each edition: Room 50 objects 722/721/647, Room 07 portals 252/252/177 (English EGA/Spanish EGA/CD respectively). The older Spanish record's late-room IDs must not be copied into the English edition. Earlier tuples serialize as empty objects in the late packets; the source copies and complete session traces retain the actual IDs, and the final helper records named roots.

**Nine later-level source controls** reach Script 29 and the matching final five/six/eight-note limit: three levels in each edition. English/Spanish packets from `out-levels/` were retained rather than repeated; CD controls are in `out-levels-reviewed/`. Crystal Use is an ordinary verb; reverse-gold and ending controls request draft-response callbacks directly. This checks the source helper/state effects, **not an earned four-note cast or a sequential three-to-eight-note playthrough**. The separate level-four input evidence retains its scope and estimates.

All new runtime mutations are catalog visits, actual object callbacks and frame advances. There are no Game Status edits, direct variable/flag/actor writes, learned/used-mask injections, note floors or certainty changes. Seeded/randomized words and estimated values remain in the packets. An observed lesson helper does not prove every original-game prerequisite or physical input.

## Remaining source gates

| Check | Established source guard | Result retained / next meaningful check |
| --- | --- | --- |
| Slot 3 — complete | Room 04 local 200 requires its cumulative local counter to be 3 and staff/input mode 2; local 201 selects note playback against camera x | Actual owlet/wolves local 202 fills the fourth hole; four cumulative Looks now reach Script 25(3). Pointer wording was incorrect. |
| Slot 7 — complete | Room 23 entry requires the own Script 106 completion bit (D71/D72/012) **clear**; local 201 watches Bobbin's x threshold | Actual approach earns Script 25(7). Script 106 is a suppressing completion writer, confirmed by its separate actual-departure control; the earlier prerequisite interpretation was reversed. |
| Slot 11 — complete | Room 25 entry/Look require the pasture-completed bit (DBA/DBB/05B) **clear**; local 208 writes it on departure | Actual Room 24 → 25 arrival, walking/introduction and lamb Look earn Script 25(11). Fresh isolated Looks missed walk history; the earlier set-bit prerequisite interpretation was reversed. |
| Seven notes — complete in English reference | Room 46 local 202 requires observation class 3 for the first three holes; local 203 closes the fourth, and local 204 requires all four closed class 24 | Actual excursions to Rooms 49/26/34, source returns including earned Healing restoration, then four loaded Closing casts reach Script 29(7). No catalog return or direct Closing response substitutes for input. |

No production progression defect is established. The three reference lesson routes and the English seven-note excursion chain are complete within their bounded source scope. Independent learning and catalog-start chains do not claim a full playthrough or physical input. Room 43 and the standalone Room 34 reveal were not reopened. Physical keyboard/mouse/focus and same-state audio/timing remain the [manual original-game comparisons](../../docs/loom/FINAL_GAPS_HANDOFF.md).

## Preserved packets and harness limits

- `out-lessons/` stopped at a null terminal-observation site after useful early packets; the observer now checks for an actual instruction site. Early already-learned slot-0 requests are not credited.
- `out-lessons-final/` preserved the EGA lessons and a CD pickup lookup failure. CD's pickup key is now read from its own Script 17, rather than the EGA key. `out-lessons-cd/` resumed the recorded EGA cases and added CD.
- `out-chains/` preserves unsuccessful cumulative hole looks and the initial unresolved late-prefix lookup. `out-late/` resolves the portal from the second `loadRoomWithEgo` operand and records the nine successful late lessons. Its three preliminary cumulative-labelled entries executed no hole looks and earn no credit; the earlier `out-chains/` contains the actual cumulative control.
- `out-levels-final/` preserves the harness's wrong-room call after the real excursion. The corrected `out-levels-reviewed/` stops dependent actions on a room change and completes the other CD controls.
- Source copies, full game text, builds and runtime traces stay ignored under `out*/`; only helpers and reduced metadata are committed.

## Reproduction

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Every shell and child process uses the mandatory RTK executable; these scripts build into private `out-build/` or `out-level-build/` and set private `DOTNET_CLI_HOME`. No App, OS input, native game or user profile is used.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run.ps1 -OutputName out-reproduction -PriorName none
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run-levels.ps1 -Mode late -OutputName out-late-reproduction -PriorName none
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/run-levels.ps1 -OutputName out-level-reproduction -PriorName none
```

These are reproduction options, not instructions to repeat reviewed routes. For local evidence validation, `rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/earned-progression-20261009/summarize.ps1` rechecks all 33 lessons and hashes their preserved packets. The final helper builds cleanly; no broad replay sweep was rerun. Audio is left for matching manual ScummVM listening because source cue/timer completion is already established and cannot close fidelity by itself.
