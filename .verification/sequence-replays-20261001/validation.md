# Generic replay sequences, 1 October

Resumed from b60f8fe. Shared `LoomReplaySequence` executes explicit verb/click inputs and fixed arrival/scene waits in one session. Scene waits require the audited end boundary, expected room, balanced scope counts, 32 settled frames and no unsupported execution. Each wait is bounded to 3,000 frames. Failure cancels later inputs; pause advances nothing, whole-scene skip cancels the sequence, and room departure drops it. No class/actor/bit/prerequisite writes. The existing one-verb/arrival-click starts retain their semantics.

Fresh `SequenceAudit` uses the production runner and loaded LFL fingerprints. `export_sequences.py` merges only these audited entries and observed rooms with the existing shared catalog. It retains old titles, literal-root coverage and every unresolved owner/site/edge. Full audit/trace stays ignored at `Exe/SequenceSession-20261001/audit-1.json`; compact digest and hash in [audit-summary.json](audit-summary.json).

| Sequence | Frames, including settled waits | Observed membership |
| --- | --- | --- |
| 052 ends +0x017C, then floor click (112,131), 053 ends +0x02AC | 701 | 10/11/12/13/14 |
| 599 use, 32 arrival frames, click (109,102), 086 ends +0x055C, then 610 look, 087 ends +0x09B9 | 2,237 | 34/36/37/38/39/40/58/68 |

**331 starts = 82 scenes / 249 reactions**, including two longer explorer sequences. Literal-root coverage remains **163/235 sites, 55 outstanding owners, seven incomplete roots, 123 unresolved edges**. Working longer sequences do not resolve the failed fresh Room13/Room38 roots or prove original-game prerequisites/input fidelity. No Room36 game-click claim or draft simulation.

Visible `SequenceUiCheck` entire sequences run on the normal Dispatcher clock (no instant preludes): 10/10 completion/pause/same-session/control assertions. `ui-2` initially reported one incorrect departure assertion requiring `_playback == null`, although the new room legitimately had idle arrival playback. Corrected to require no sequence, no scene and disabled Pause; separate `controls-3` validates departure and whole-scene skip (2/2). No production change was needed for that correction. The initial compiler failure was a harness syntax typo, corrected before running. Screenshots of both final rooms and playing/paused stages inspected; screenshots remain ignored.

Core **255/255**, both LOOM and Indy game roots, unrelated untracked LoomObjectLookAuditTests excluded. New tests ensure a failed stage never sends its later input and repeated polling never advances frames. Global picker **21/21**, room picker **20/20**, doorways **16/16** in 2/3/4/8/10/37, room/audio controls **19/19**. Fresh reports under `Exe/SequenceSession-20261001/shared-1` and `regressions-1`. No pixel/rendering/session execution changes: historical room bitmaps are retained; no PNG refresh. Builds zero warnings/errors. Visible WPF with synthetic handlers/layout/media, not physical input or independent listening.

Unrelated work, Games and scummvm preserved. Main checkout Release rebuilt by harness project dependencies; final main solution rebuild also required at session end. Room41/late routes and the requested game status view follow as separate tasks.
