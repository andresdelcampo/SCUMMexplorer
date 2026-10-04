# Room41/script090 certainty layers — 2 October continuation

Continued from `e285e08`, preserving native level4 recipes `cee1bf9`, implementation `8d12d0f`, completed096/20 recipes `b17c53d`/`f18a18f`, status/costumes/scythe fixes and unrelated untracked work. This is source research and diagnostic instrumentation. Production code, replay catalog and game resources are unchanged. No estimate clearing, flag/note/actor injection, status edits, staff browsing floors or promotions.

## Two different native certainty barriers

Fresh [recipes](room41.json) compare the established source-executed level4 setup with the older direct-response level5/6 setup. Each uses actual37/603 arrival, delayed632 look, active633, decoded walk point/aim, loaded slot4 notes and actual204 Escape. Only the final note-input method differs within each pair.

| Setup/input | Result | Input/scan certainty |
| --- | --- | --- |
| Native level4, mouse notes | Room41,15/14 scopes | Note writes and all023(0..4) calls Conditional |
| Native level4, keyboard notes | Room41,15/14 scopes | Same downstream events, values/estimates, timing and rooms |
| Older raw6, mouse notes | Room41,12/11 scopes | Entered notes known;023(0/1) Reached,023(2/3/4) Conditional |
| Older raw6, keyboard notes | Room41,12/11 scopes | Same downstream events, values/estimates, timing and rooms |
| Native level4 setup, **direct633/41** | Room43,15/15 scopes | Separate exploration control;090 ends +0730 |

All four native routes still refuse Conditional090 +0005 Room42 and settle unsuccessfully after3000 frames. Source213/204 handling is not replaced by an injected override: the probe waits for204's actual override and sends Escape. Mouse/key inputs follow different005 caller sites (+00A4/+041D), then identical downstream events. The prior checkpoint's023(0/1) Reached finding applies to the **older raw6 control**, not the longer native level4 route.

Per-note snapshots now include event cursors, allowing each observation to be associated with its actual source-event range. [Compact evidence, source packet hashes and isolated traces](route-summary.json). Full fresh packets remain ignored under `Exe/Room41CertaintySession-20261002/`.

## Earlier contamination in the native level4 route

The clean egg input retains known staff mode/target/index and all four note writes. Before dye input, mode064=2, target067=207, count062=0 and raw07A=3 are still known. However, practice flagD6C=0 retains an estimate after the source lesson.

1. The first dye note is written as known. During the following8 input-gap frames,020 +0021 starts031 with known note1.031 reads estimatedD6C at +000A and its later steps become Conditional.
2. Native events include031 +0254 palette `actorOps` and +026B animation Conditional. The current generic executor adds an actor estimate at the palette instruction. The second input reports010 +000F `getDist` reading that actor estimate;010 +004F becomes Conditional, count062 and entered note057 become estimated. Snapshots establish the first note remains known while the second becomes estimated.
3. Later reverse-gold input and source level4 execution retain those dependencies. At the final Room41 cast, mode064=2, target067=633, count062=0, raw07A=4 and the note buffer are already estimated. Ego's estimate records the later006 +00E9 object walk.
4. Consequently010's **first** condition +0000 already reads an estimate. The raw4 note-limit branch +0020 is another dependency; it is not the first cause.020 +003C starts022 Conditional. D70=0 is also estimated, so022 +0000 is conditional even before its first candidate023 call.

The [isolated audit](CertaintyAudit/Program.cs) copies captured global values and estimates into a separate runtime. It executes loaded010's first condition,031 only up to its first source event, and022/023 up to chain026. It changes no live session and supplies no replacement notes or cleared estimates. Its raw4 traces show the first estimated branch at010/022 +0000 and031 +000A. Its older raw6 traces show no estimate at010's first condition, then022's first estimated branch at +007D while comparing slot1. The scan's actual event sequence agrees with native playback.

These slices prove dependency localization within the current executor, not additional native input or a completed scene. The010 instruction-limit stop at0007 is intentional;022 ends at its intercepted chain026. An initial missing namespace prevented the audit from compiling; the corrected harness rebuilt and all four slices passed. The first pair comparison also correctly rejected different005 caller offsets; the final comparison preserves them and compares downstream events rather than erasing source locations.

## Source alternatives rule out earlier draft collisions

[Read-only analysis](analyze_source.py) extracts the native211 `setVarRange` tables,0..2 random choices and destination writes directly from the pinned disassembly (SHA256 `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`). No note words are supplied by the analysis.025/034 OR upper learned/used flags; they preserve the lower12 note bits used in this comparison.

For all three boot alternatives of slot4, every earlier slot0..3 alternative differs in forward matching and in every source-capable reverse match: **51 comparisons, zero collisions**. [Derived tables, source offsets and decisive mismatch sites](source-domains.json). This finite source fact explains why earlier candidates reject the entered slot4 sequence, independently of the seed0 choices.

It does not make the selected randomized draft, learned/use flags, entered input, index or inherited path known under the existing executor. A generic future solution would need to retain uncertainty reasons while proving relevant outcomes from source value domains and masks, and distinguish appearance uncertainty from actual placement/movement uncertainty. Masking learned bits alone, changing input method, or treating a converged loop as known would leave other dependencies. No proposed precision change is implemented or native090 success claimed here.

## Preserved recipes and fresh regressions

Both native level4 orders remain source-executed raw4 with estimates retained, D6F0, packed slots0/1/2 `7282/7400/7200`,9/9 scopes in62/06. Their recipe files are byte-identical copies of the established recipes.095 actual override→staff→092→639 still completes096 in46,4/4; full095 control remains43,4/3. All four sleep→369/371→390/391 combinations still return18,7/7.

Fresh Core289 passes with absolute game roots and the unrelated untracked audit excluded. Full363-root outcomes, exact frames/rooms/scope traces, source fingerprints, four established memberships and both sequences match the scope baseline; Blockers0. Main Release builds with zero warnings/errors. [Hashed reports](check-summary.json). No WPF, RoomAudit or pixel rerun was needed or claimed for this diagnostic/docs task; earlier62/16/19/18 UI checks and76 zero-gap renders remain dated. Coverage remains334 starts/168 of235 sites/50 outstanding owners/123 unresolved edges. Native challenge opening remains unresolved.

No SCUMMexplorer App instance was running or launched. Console harnesses finish themselves. No worktree, game/scummvm or unrelated-work changes; no push. Session token-usage tool unavailable; account limits are not session usage.

## Reproduction

Main cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Prefix commands with `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe proxy`; Python below is `C:\Users\adelc\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe -X utf8`. Use fresh output directories and update the summarizer's packet paths before another run.

```powershell
python -X utf8 .verification/archive/room41-certainty-20261002/create_probes.py
python -X utf8 .verification/archive/room41-certainty-20261002/analyze_source.py
dotnet run --project .verification/archive/room41-certainty-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/room41-certainty-20261002/room41.json Exe/Room41CertaintySession-20261002/room41-1
dotnet run --project .verification/archive/room41-certainty-20261002/CertaintyAudit/CertaintyAudit.csproj -c Release -- Games/loom Exe/Room41CertaintySession-20261002/room41-1 Exe/Room41CertaintySession-20261002/certainty-1/traces.json
```

Run ProgressionProbe likewise for `level4-egg-first.json`, `level4-dye-last.json` and `preserved-recipes.json`, with output `egg-first-1`, `dye-last-1`, `preserved-1`. Fresh checks use the existing source-progression `run-checks.ps1 -OutputFolder Exe/Room41CertaintySession-20261002/checks-1` and status-refinements `run-source-audits.ps1 -Output Exe/Room41CertaintySession-20261002/audits-1`; then `summarize.py --checks`. Final main solution command: `dotnet build SCUMMexplorer.slnx -c Release`. Executable: `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`.
