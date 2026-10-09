# Next session

State and evidence, reconciled 9 October 2026. The actual task prompt is supplied to a chat, not stored here. The [prior handoff](archive/NEXT_SESSION_PROMPT_BEFORE_RECONCILIATION_2026-10-08.md) is historical; follow [CURRENT_WORK](CURRENT_WORK.md) and [the live plan](LOOM_EGA_PLAN.md).

## Latest completed correction

The user rebuilt/reopened **English EGA**, selected **Room 46**, navigated to **34**, and saw sleeping Rusty moved by the later ghost conversation. The closed 34 → 46 portal correctly refuses entry after `ebdf3b2`; this second path is now corrected by the approved source preparation.

[Profile-free reproduction](../.verification/catalog-return-20261008/README.md): fresh post-boot session, Room 46 catalog visit, actual hole double-click returning through Script 97 +0042, then actual portal double-click reaching Script 98. Nine diagnostics reproduce late D7A=1 / ghost D9C=0 / costume 112, with inspected source-rendered output. They are **defect reproduction** assertions, not a product fix. Earlier Firewood/dream preparation establishes D9C through Script 87 and yields costume 120. No forced state, native interaction or coverage promotion.

The human explicitly approved real source prerequisites for late-room selection. A fingerprinted English Room 46 recipe now runs missing source stages in the existing session, keeps ghost/restored state, skips completed Awakening/Reflection/Firewood stages, and cancels the target visit if a stage fails. The real WPF route renders ghost costume 120; early closed-portal behavior remains. Final validation: 32 focused Core checks, 18 isolated-profile WPF assertions and zero-warning/error build. Inspectable screenshots are in the checkpoint's ignored output. The raw probe deliberately retains the old missing-history start; its nine passes reproduce the defect and are not the fixed App path.

## Completed and still open

- **Room 43 accepted by the user, 9 October:** the other two arrive a couple of seconds after the scene starts; the swordsman and Bobbin are correctly placed. The user sees no issue and says to move on. [The checked arrival/placement concern is closed](../.verification/room43-accepted-20261009/README.md); do not repeat the scene or retain numerical Room 43 measurements as a blocker. No edition/callback-specific or all-actor fidelity claim is added.
- Documentation is reconciled across live guides and recent packets; seven complete historical snapshots preserve prior research. 31 files / 688 relative links checked with no missing targets; whitespace check passes.
- Local master: `ebdf3b2` portal correction, `0243a5a` [draft/source checkpoint](../.verification/draft-input-20261008/README.md), `9de8193` [catalog return correction](../.verification/catalog-return-20261008/README.md), `e9cdb24` documentation reconciliation, `2a51c06` remaining-source checkpoint, `5486af1` sky checkpoint and `8da4945` whole-draft WPF checkpoint. No push.
- Six prepared English EGA/Spanish EGA/VGA Room 04 note-input casts pass; three profile-free status renders inspected; clean build. [Two VGA sky casts](../.verification/sky-input-20261008/README.md) now pass after playing the source opening to (828,61), with disclosed staff/eight-note preparation, one sky click to (808,59), retained aim 81/distance zero and four real note keys. 24 input assertions; isolated solution build with zero warnings/errors and all 377 links in nine changed Markdown files resolving. The checkpoint records the exact build command and executable. Earlier catalog-arrival failures remain failed. No earned-prerequisite, physical/native or audio claim.
- [Remaining-source checkpoint](../.verification/source-gaps-20261008/README.md): 33 source assertions pass. Script 101's Silence/Silencio → local 212 [12] → Script 23 and the own-edition response word support Silence beyond manual order. Shaping's form-change effect is correlated; canonical label and exact reverse names remain uncertain. The 11/11/8 dynamic first-operand sites are classified; input-dependent targets and incomplete data remain limits. No runtime/display-name change or native interaction.
- [Whole-draft WPF input](../.verification/wpf-casting-20261008/README.md): 168 assertions across 12 independent Room 04 cases—forward/reverse via routed text or scaled staff-strip clicks in English EGA, Spanish EGA and CD/VGA. The real window never activates; private profile/cache, muted audio and isolated build (zero warnings/errors). Failed `out-first` is retained; corrected `out-second` passes. This closes the previously untested Explorer integration item, not original-game physical input, source-earned prerequisites or wall-clock/listening fidelity. No production behavior change.
- User priorities, 9 October: first state-correct cutscene actors—wrong versions/costumes, Bobbin's held/unheld staff or invisibility under changed statuses, as with Rusty's prior non-ghost defect. Next verify source-triggered learning/level progression while playing in Explorer; prepared Game Status input success is not that proof. Audio is less interesting: do only a small straightforward comparison, otherwise leave visual/listening checks, using ScummVM as fidelity reference. [The dossier](loom/FINAL_GAPS_HANDOFF.md) retains exact phases and separate physical-input/Room 09/17 gaps. Room 43 is accepted above. Native control still requires a direct human resume; “moving on” does not override the explicit stop. No broad audit repeat or speculative naming/engine patch is pending.
- Historical full baseline: 833 tests pass/five diagnostic skips, 328 EGA and 222/223 VGA normal/Escape recipe completions. Later VGA walker comparison/rerun is separately scoped. Avoid broad repeats absent a relevant change.

## Parked native investigation

Room 34 local-200 +0035 fortress reveal is unresolved and explicitly parked. Its local-206 ghost pan is a separate scene. Save/query/debugger-close requests are fulfilled; do not repeat them or request a whole-game replay.

The [native packet](../.verification/room34-state-20261008/NATIVE_SESSION.md) preserves matching English Roland EGA/ScummVM 2.2.0 identity, untouched slot 13 hash, early monitor/hidden-box-6 readings, and the eastward route with DCF clear. The user later reported returning to 34 and stopped Computer Use with physical Escape during recovery capture. No refreshed screen/guards followed. **Do not resume Computer Use without a direct human request.** No current open-console or visible-room claim is verified.

## Paths and commands

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. All shell/child commands use RTK, including PowerShell. Preserve profile, native session, saves and ROMs.

- Reference: `Games/loom ega mt32 en`.
- Spanish: `Games/ALTERNATIVE LOOMS/loom ega mt32 es`.
- VGA: `Games/loom vga en`; alternative `Games/ALTERNATIVE LOOMS/loom vga en kixx`.
- Other EGA editions/demos: `Games/ALTERNATIVE LOOMS/`.
- ROMs: `Games/mt32`; bundled `scummvm/` has executables, not source.

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/catalog-return-20261008/Probe/Probe.csproj -- 'Games/loom ega mt32 en' '.verification/catalog-return-20261008/out'
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
```

Built app: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.

Prepared waiting chat: `01a11cdb-3f40-78a2-a3a7-4ab351cefc34`, created before this report; its dispatched prompt predates the finding. It must wait for human work and read current docs when continued. Do not message it without authorization or create another idle continuation chain.
