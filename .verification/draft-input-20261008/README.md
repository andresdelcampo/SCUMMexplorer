# Source-backed draft labels and actual note input — 8 October 2026

The user asked for the remaining work that could be done autonomously. This checkpoint verifies source-driven casting in isolated Explorer sessions and improves one draft label. The standalone Room 34 fortress reveal remains parked. The portal correction in `ebdf3b2` remains the baseline. No native ScummVM interaction was resumed.

## Sharpening and Blunting

Room 41's sword has separate entries for `4A` and `4B` in each edition. English EGA object 634, Spanish EGA object 633 and VGA object 559 all send `4A` to refusal local 205 and `4B` to local 204 with argument zero.

The English EGA zero-argument branch in local 204 selects the print at `+00F6`: the blade's edge has been blunted. Spanish independently agrees at `+00F9`: the edge is no longer sharp. The nonzero argument selects the distinct twisted-edge response; it must not be mistaken for the blunting direction. Conditional branch targets are false branches in this bytecode dialect.

VGA retains those two sword callbacks and the zero argument, but shortens the dialogue. Its corresponding meaning is inferred from that matching response structure and the independently explicit EGA texts; VGA does not itself print the EGA blunting sentence. This is source comparison, not independent original-game visual confirmation.

`LoomDraftNames` now displays **Sharpening / Blunting** for the pair, and **Blunting** for verb `4B`, replacing the less useful “Sharpening (reversed)” label. It changes no response verb, draft sequence, story prerequisite or execution.

## Actual casting checks

`Probe` starts independent sessions from each edition's own data. It uses a catalog Room 04 visit and the public Game Status staff/eight-note settings as disclosed exploration setup. The existing draft words are retained; no learned-draft edits, story bits, actor placements or direct draft callbacks are injected. These are prepared Explorer input checks, not source-earned whole-game routes.

The target is selected from the room's own verb table and shared native hit test. A room click aims at it; four ordinary note-letter keys then pass through Scripts 005/010/020/022/026/007 to the object's response. Every case checks the aimed object, expected forward/reverse verb, actual draft playback, response text, balanced scene boundaries and settled playback.

| Edition | Target | Forward / reverse frames | Draft-playing frames | Observed playback poses |
| --- | --- | --- | --- | --- |
| English Roland EGA | Room 04 hole, object 171 | 267 / 281 | 51 / 51 | 3, 6, 7, 11, 13 |
| Spanish Roland EGA | Room 04 hole, object 171 | 274 / 288 | 51 / 51 | 3, 6, 7, 11, 13 |
| English VGA | Room 04 thorns, object 101 | 364 / 364 | 76 / 76 | 3, 6, 7, 11, 13, 26 |

All **six cases pass**. EGA responds that the hole is already open or that its inhabitant might dislike closing it. VGA responds that thorns cannot be woven. The result verifies dispatch and ordinary source responses, including refusals; it does not claim a visible world transformation from every cast. EGA active note-sound IDs 1/2/3 were sampled. VGA's active-sound set was empty in this probe; this set does not verify the application's separate CD voice/music output. No listening or original-game timing claim follows from the frame counts.

Earlier Room 02 attempts are retained in ignored `out-first/` and `out-second/`. EGA forward/reverse sky casts completed. VGA's source input cleared the target before the notes; Script 010 therefore sent each attempt to Script 013's aim-first response. That route remains unqualified. The later Room 04 success must not hide or reclassify it as a successful sky route or an established executor defect.

## Other source limits

The scan visits globals, entry/exit, locals and explicit object verbs independently in all three editions. It finds no decoded literal `stopObjectScript` or `pickupObject` instruction in these reference releases. Spanish's four existing invalid indexed entries are retained in the report. Incomplete/interior bytecode and dynamic targets remain limitations; this does not prove every possible inventory/lifetime path absent, and does not justify implementing those historical engine gaps speculatively.

Room 09's `4F` response restores speech through local 223; `51` changes Hetchel's costume through local 224. Their later-state guards and edition-specific text are retained in the source dumps. These effects narrow the interpretation but do not supply stronger source names for every unhandled forward verb. The existing Silence/Shaping associations and uncertainty remain unchanged.

## Build and visual check

The solution builds with **zero warnings and errors**. A profile-free capture program renders the real `GameStatusWindow` directly on isolated sessions, without constructing `MainWindow`, loading saved Explorer state or applying edits. English EGA, Spanish EGA and VGA renders were inspected: the combined label fits and displays correctly. No broad suite was rerun for a display-only change.

Generated source dumps, traces, frame records and PNGs remain ignored under `out*/`. The supplied native session, game data, saves, ROMs and Explorer profile were not modified. Concurrent documentation edits are preserved and excluded from blanket staging. No push.

Run from `D:\Documents\Personal\Code Projects\SCUMMexplorer`, through the required RTK wrapper:

```powershell
rtk proxy dotnet run --project .verification/draft-input-20261008/Probe/Probe.csproj -- Games .verification/draft-input-20261008/out-final 4
rtk proxy dotnet run --project .verification/draft-input-20261008/Ui/Ui.csproj -- Games .verification/draft-input-20261008/out-ui
rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Built app: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`. Original-game actor/staff bounds, physical input and audiovisual comparisons remain open; the fortress reveal remains parked.
