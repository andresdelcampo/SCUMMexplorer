# Next session

State and evidence, reconciled 8 October 2026. The actual task prompt is supplied to a chat, not stored here. The [prior handoff](archive/NEXT_SESSION_PROMPT_BEFORE_RECONCILIATION_2026-10-08.md) is historical; follow [CURRENT_WORK](CURRENT_WORK.md) and [the live plan](LOOM_EGA_PLAN.md).

## Latest completed correction

The user rebuilt/reopened **English EGA**, selected **Room 46**, navigated to **34**, and saw sleeping Rusty moved by the later ghost conversation. The closed 34 → 46 portal correctly refuses entry after `ebdf3b2`; this second path is now corrected by the approved source preparation.

[Profile-free reproduction](../.verification/catalog-return-20261008/README.md): fresh post-boot session, Room 46 catalog visit, actual hole double-click returning through Script 97 +0042, then actual portal double-click reaching Script 98. Nine diagnostics reproduce late D7A=1 / ghost D9C=0 / costume 112, with inspected source-rendered output. They are **defect reproduction** assertions, not a product fix. Earlier Firewood/dream preparation establishes D9C through Script 87 and yields costume 120. No forced state, native interaction or coverage promotion.

The human explicitly approved real source prerequisites for late-room selection. A fingerprinted English Room 46 recipe now runs missing source stages in the existing session, keeps ghost/restored state, skips completed Awakening/Reflection/Firewood stages, and cancels the target visit if a stage fails. The real WPF route renders ghost costume 120; early closed-portal behavior remains. Final validation: 32 focused Core checks, 18 isolated-profile WPF assertions and zero-warning/error build. Inspectable screenshots are in the checkpoint's ignored output. The raw probe deliberately retains the old missing-history start; its nine passes reproduce the defect and are not the fixed App path.

## Completed and still open

- Documentation is reconciled across live guides and recent packets; seven complete historical snapshots preserve prior research. 31 files / 688 relative links checked with no missing targets; whitespace check passes.
- Local master: `ebdf3b2` portal correction, `0243a5a` [draft/source checkpoint](../.verification/draft-input-20261008/README.md), and `9de8193` [catalog return correction](../.verification/catalog-return-20261008/README.md). No push.
- Six prepared English EGA/Spanish EGA/VGA Room 04 note-input casts pass; three profile-free status renders inspected; clean build. VGA Room 02 sky remains unqualified.
- [Remaining-source checkpoint](../.verification/source-gaps-20261008/README.md): 33 source assertions pass. Script 101's Silence/Silencio → local 212 [12] → Script 23 and the own-edition response word support Silence beyond manual order. Shaping's form-change effect is correlated; canonical label and exact reverse names remain uncertain. The 11/11/8 dynamic first-operand sites are classified; input-dependent targets and incomplete data remain limits. No runtime/display-name change or native interaction.
- Next active work is in [the gap dossier](loom/FINAL_GAPS_HANDOFF.md): original-game actor/staff sizes/placement, source-earned casting routes, CD picture/voice/music timing, and specific Roland discrepancies if any.
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
