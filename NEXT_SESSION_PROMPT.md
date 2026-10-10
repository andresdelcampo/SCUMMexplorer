# Next session prompt

Copy the block below into a new chat.

```
Decide whether to ship offline-generated cutscene lists ("seeds") for the other LOOM EGA editions. Do not implement anything until I have answered the decision at the end.

Context (read docs/CURRENT_WORK.md, docs/REQUIREMENTS.md R-8.18..R-8.25, .verification/cutscene-scan-ega/README.md and .verification/cd-seed/README.md first):
- The English Roland EGA edition (Games/loom ega mt32 en) keeps its hand-validated catalog (src/SCUMMexplorer.App/loom-replays.json). It stays; do not remove or change it.
- Every other EGA edition (Games/loom ega en, it, ALTERNATIVE LOOMS: es, mt32 es, fr, de, v1.2 es) has an empty catalog (fingerprint mismatch), so since commit 55b625b it uses the live script scan (ScannedCutscenes, R-8.24). Measured on the Spanish edition: about 8 minutes on a first launch (one fresh headless run per scene, in parallel; the planner's trials for state-gated scenes follow), cached per game folder afterwards.
- LOOM CD already ships a fingerprint-checked seed (R-8.25, loom-cd-scenes.json, made by .verification/cd-seed; the live scan is the fallback).
- The 5 October decision (R-8.19, CLAUDE.md) was not to rerun the discovery pipeline for other editions. A shipped seed for them would reverse its practical effect, so it needs my explicit OK.

Check and report (read-only, no commits except notes if I ask):
1. For each available EGA edition, time the live derivation (room lists settled, then planner trials) and note which scenes the planner cannot start (like Room 34's entry). Use .verification/scanned-ega-lists with SCUMM_ROOM_CACHE_DIR set to an empty temp folder, and keep harness windows off-screen as the harness does. Do not run every harness on every edition (CLAUDE.md): only this one.
2. Compare each edition's scan with the English one by derived results, not by ids: same group count, same scene/dialogue split, same rooms per scene (object ids differ from Room 34 onward; match by room + verb + text). List real edition differences.
3. Decide what a seed would be keyed by for EGA (a fingerprint of the LFL files, like ScummV4Archive.Fingerprint for the CD) and whether editions that share data (plain English vs Roland, French/German/v1.2) can share one seed.
4. Estimate the benefit honestly: a seed removes the first-launch wait and the arc/note for the known editions only; the live scan already works for all of them. Say whether that is worth a larger shipped data set and a regeneration step.

Deliver: a short recommendation (ship seeds / keep live scan only / seed only the slowest editions), the measured numbers, and the exact files a seed change would touch. Then ask me for the decision.

Rules: commit only your own files; docs/REQUIREMENTS.md is shared with other sessions, so check git status first. Use rtk for shell commands. Leave %LOCALAPPDATA%\SCUMMexplorer alone.
```
