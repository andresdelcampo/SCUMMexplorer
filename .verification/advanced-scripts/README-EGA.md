# EGA reverse annotations, 8 October 2026

The VGA review supplied the starting point for an individual review of the English and Spanish EGA globals. English EGA grows from **46 to 116 titled globals**: 70 additional titles, plus fuller existing titles for 90/99. All **113 valid Spanish globals** are titled too. The [complete EGA table](REVIEW-EGA.md) includes instruction counts and sources. This changes Advanced presentation; no interpreter or gameplay behavior is changed.

`loom-ega-reviewed-glossary.json` contains 229 review records (116 English, 113 Spanish), accepted only when the game's decoded operation/operand fingerprint matches. Each valid global has its own EGA source references. Matching the CD's script number alone never imports a label. The common review reader retains the CD manifest and behavior.

Added 13 variable bindings: pointer coordinates, the draft response word, the displayed-note buffer, five ego animation roles, two sound roles and the two five-entry flying-position arrays. The previously vague last-sound label is clarified by the queued sound helper and music player. Five missing draft/note flag meanings now use the actual edition's addresses, including the three pattern masks and the response-refused/consumed flag. This avoids applying the legacy Spanish-number shift a second time. Note-playing and draft rejection are separate flags (English D66/D67; Spanish D67/D68).

Actor identities are scoped to the reviewed scene. The 34 reviewed actor bindings include Fleece, the dragon, Rusty, Goodmold, Stoke, Mandible, Cob, Hetchel, Cygna and the Dead One. Former global slots 2–6 are scoped to the trial or glassmaker scene; slot 12's learning glow is scoped to Script 29. A reused slot no longer acquires a scene identity everywhere. Goodmold occupies actor 3 in Script 66 and actor 6 in 67; Hetchel occupies actor 11 in 88. Character interpretations retain the reading evidence level and references to the identifying dialogue/continuation.

Edition details retained:

- Script 7 uses distance 2 in English EGA and 3 in Spanish EGA (CD also uses 3).
- EGA 4/39/40/115 retain their save/music/effect/input roles; CD stubs and CD audio wording are not imported.
- English 51/55/116 receive descriptions from their own decoded code. Spanish 51/55/116 declare resource lengths beyond their containers and stay unnamed.
- Script 00 stays unresolved: invalid English control-flow targets and an invalid Spanish resource length. There is no established purpose to name.
- The earlier VGA comparison understated EGA 90/99. Both EGA routines already contain Mandible's introduction and Goodmold's aftermath; their old titles described only part of them. The review notes and titles are corrected.
- Script 111 uses a neutral description of its own Room 86 visual and return to Room 31; actor 2 is the glow actor in this helper, so a Bishop identity is not assigned to that slot.

Validation:

- **42 focused Core checks passed**, including all reviewed English/Spanish globals, actual flag addresses, array extent, scoped actors, the changed-script fingerprint rejection, glossary/presenter regressions, usage indexes and CD regressions. The CD formatting test still covers all 1,470 blocks. The three English and three Spanish readable goldens pass. The old Spanish goldens actually contained English instructions/dialogue; they are refreshed from the real Spanish edition, with its own bits and offsets.
- **126 real WPF assertions passed** in `ui-ega-review01/report.txt`. The harness opens English/Spanish Scripts 5/7/16/24/26/37/69/100/111 and VGA 5/7/16. It checks sidebar/page titles, Used by, Explore links, readable names, Raw sources and the VGA 7→8 link navigation. Captures of EGA 26 and 100 were inspected. These are offscreen WPF renders, not physical-input or listening evidence.
- Solution and WPF harness builds: zero warnings/errors. No full suite, long replay or audio harness.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomEgaScriptReviewTests|FullyQualifiedName~LoomCdScriptReviewTests|FullyQualifiedName~LoomCdReadableTests|FullyQualifiedName~LoomGlossaryTests|FullyQualifiedName~LoomScriptPresenterTests|FullyQualifiedName~LoomUsageIndexTests' --verbosity quiet
dotnet build .verification/advanced-scripts/ScriptsCheck --no-restore --verbosity quiet
```

`ScriptReview` takes the absolute Games folder and a fresh output folder, and finds the Spanish game under `ALTERNATIVE LOOMS` when needed. For reproduction, export to `before-ega/review-input`, then run `review-ega.ps1`. Generated dumps, JSON and UI output remain ignored. `ScriptsCheck` uses the same arguments; run its process with a 45-second deadline.

App artifact: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.
