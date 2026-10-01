# Game Status and normal costume refinements

Completed the requested status changes: directly interactive Learned checkboxes, a noninteractive tick/dash for Can reverse, native note tiles cropped from64 to38 pixels, note selection adding the staff, staff removal clearing notes, and an explicit C/D/E minimum for page edits. Changes preview immediately and remain pending until Apply Changes. Close discards them. Busy/paused/stale/session replacement guards remain.

The minimum is **page editing policy**. Core inspection, source writes, replay setup and the inclusive native1..8 APIs remain able to represent zero/one/two notes. A raw lower source count is shown truthfully; selecting C or D explicitly chooses3. This supersedes the earlier page policy allowing explicit C/D-only choices and retaining notes after staff removal.

Normal LOOM mode now offers Rooms/Costumes tabs with graphical cards. Each costume thumbnail uses the decodable stored pose with the most visible palette pixels (stable source-order ties). All resources retain cards, with unavailable pictures clearly labelled. Decode work runs in the background, caches containers, cancels on game replacement/close and discards stale game results. Clicking opens the existing static pose gallery. No animation or gameplay costume change is inferred. Costume selection survives an Advanced round trip without replacing the exploration session. Indy remains rooms/texts only.

Game Status follows Game Texts in Advanced and stays immediately before Open Game Folder in both modes. All sixteen draft labels now use source slot/forward/reverse-response contracts, independent of their randomized note sequences. An unmatched contract keeps Draft N.

## Draft label evidence

Loaded script023 decodes the slot;025 learns it. Verified native code packet: `Exe/VerificationSession-20261001/native-code-1/code.tsv`, SHA256 `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`. Labels are presentation only; no source flags, sequences or response IDs change. Names were cross-checked with the original Lucasfilm [Book of Patterns](https://mocagh.org/lucasfilm/loom-book.pdf). The original [manual](https://mocagh.org/lucasfilm/loom-manual.pdf) explains note variation between games; fixed walkthrough sequences are not identity evidence.

| Slot | Responses | Display name | Native teaching source (023 /025 offsets) |
| --- | --- | --- | --- |
|0|3A/3B|Opening / Closing|Room09 local200 +0008/+003E, egg|
|1|3C/3D|Straw into Gold|Room62 local200 +0041/+0075, spinning wheel|
|2|3E/3F|Dyeing / Bleaching|Room06 local201 +0016/+0045, dye pot|
|3|40/00|Night Vision|Room04 local200 +0191/+01F4, owls|
|4|41/42|Twisting|Room13 object325 look +0008/+0031, waterspout|
|5|43/44|Invisibility|Room24 local204 +0018/+007A, shepherd|
|6|45/46|Emptying|Room06 local215 +0069/+009E, flask|
|7|47/48|Sleep|Room23 local202 +0008/+01A5, sheep|
|8|49/00|Terror|script065 +0145/+0217, orb|
|9|4A/4B|Sharpening|Room20 local202 +0033/+008E, scythe|
|10|4C/00|Healing|Room31 local200 +0046/+0157, Fleece|
|11|4D/00|Reflection|Room25 local204 +0348/+0379, pool|
|12|4E/4F|Silence|script101 +0866/+0895, Chaos|
|13|50/51|Shaping|Room09 local223 +0267/+02AC, Chaos|
|14|52/53|Unmaking|Room09 local224 +037F/+03DA, Chaos|
|15|54/00|Transcend|Room01 local211 +04BC initializes211F/0054;023(15) in029 +000A; prior native evidence retained|

These name associations do not establish that their teaching actions or prerequisites have been reached by a fresh explorer session.

## Verification

Fresh Core **277/277**, both absolute game roots, unrelated untracked audit excluded. Visible WPF **62/62**, normal clock: single-toggle binding, read-only reverse indicator, all16 names/mismatch fallback, cropped native artwork, note/staff coupling/minimum, Apply/Close, preserved flags, busy/stale/pause, native refused/accepted casting, catalog floors, normal costume cards/gallery, Advanced round trip, toolbar and minimum layouts. Screenshots inspected, including the restored scythe in the actual room view. Required DoorwayClickCheck **16/16** across2/3/4/8/10/37; shared RoomControlsCheck **19/19**. Events/layout/session checks are synthetic, not physical input or independent listening.

[Scythe correction](scythe-validation.md): Room20 background retained;76 room renders, zero remaining gaps, only20 changes,75 identical. Only its historical PNG refreshed. Fresh363-root/membership/sequence/Blockers results and packet digests are in [check-summary.json](check-summary.json). Replay metadata and unresolved routes remain unchanged.

Current UI packet `Exe/StatusRefinementsSession-20261001/ui-2/`, Core `tests-final.txt`, navigation `navigation-1/`, source `audits-1/`. Initial ui-1 found a genuine costume/Advanced round-trip bug; returning to normal used to force a room selection. The fix retains both Room and Costume kinds; ui-2 passes62. Earlier compiler errors and failed exploratory renderer packet were corrected before final checks. Full dumps, screenshots and binaries stay ignored under Exe; harnesses/evidence summaries are tracked.

No user app stopped; owned harnesses close themselves. Games/scummvm and unrelated untracked work preserved. No worktree operation or push/publication. Final main Release build has zero warnings/errors; executable `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Release\net10.0-windows\SCUMMexplorer.App.exe`.

## Reproduction

Exact cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Use RTK at `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe` and bundled Python at `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe`.

```powershell
rtk proxy python -X utf8 .verification/status-refinements-20261001/create_ui_check.py
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-ui.ps1 -Output Exe/StatusRefinementsSession-20261001/ui-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-navigation.ps1 -Output Exe/StatusRefinementsSession-20261001/navigation-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-source-audits.ps1 -Output Exe/StatusRefinementsSession-20261001/audits-fresh
rtk proxy dotnet build SCUMMexplorer.slnx -c Release
```

Run Core with absolute `SCUMM_SOUND_TEST_ROOT=$PWD/Games/loom`, `SCUMM_INDY3_EGA_TEST_ROOT=$PWD/Games/IndyCru/EGA`, filter `FullyQualifiedName!~LoomObjectLookAuditTests`. Retain failed packets. The comparison script expects the dated before/after packets and asserts that only20 changed; use fresh output folders for new renderer work.
