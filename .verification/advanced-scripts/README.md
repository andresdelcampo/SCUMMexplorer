# Advanced VGA scripts, 7 October 2026

The user's side-by-side Script 5/7/16 captures showed that the initial VGA readable pass omitted most of the knowledge already present in EGA. This pass compares all **122 CD global scripts** with the EGA reference and adds individually reviewed titles, rather than importing names by number. The full table is [REVIEW.md](REVIEW.md).

The CD review is embedded as `loom-cd-glossary.json`. Each script record carries its title, evidence level, instruction references and a SHA-256 fingerprint of its decoded operations/operands. The CD must match that reviewed stream before getting its labels and scoped actor/flag names. The previous operand-based helpers remain a fallback for recognisable code in another build. EGA data and goldens are unchanged.

The original eight titled CD globals become **122**. Previously established EGA meanings are ported wherever their CD counterparts support them. Straightforward previously unnamed helpers and CD additions receive descriptive titles from their own operations/text. Neutral numeric room/actor titles remain where a story interpretation is not established. Real differences remain: CD 4 yields once; 39 and 115 return immediately; 40 plays CD audio effects; 90 includes Mandible's introduction; 99 includes Goodmold's aftermath. Script 7 keeps its own distance threshold (CD 3, EGA 2). CD dialogue wording stays the CD's wording. CD 51/55/116 are absent.

Readable improvements apply throughout the library: the clicked-object role now matches Script 6's actual assignment and corroborating Scripts 7/10; ambiguity is checked after corroboration. Added input/draft/cutscene flags, arrival-object and flying-coordinate roles, and scene-scoped actors. The flying arrays have five established entries, corroborated by Script 44's own initialization. Bobbin is established by the CD boot. Actor 2 remains unnamed globally because the slot is reused by the flying actor, Elders and Mandible. Script 16 now shows `Bobbin: set text layout center, at 160, text line y`; its layout instruction is labelled as a layout rather than dialogue. CD audio-only print instructions are labelled as cues.

VGA now has **Used by** and **Explore links**, using CD disassemblies and source offsets. Literal local-script references carry the caller's room. A global reference to a local ID with no known room stays unresolved, rather than linking to an arbitrary local 200. Room exit scripts participate in the indexes too. Dynamic targets and decode issues remain unresolved; links are static possibilities. Explore links retains reviewed script titles.

Validation:

- Focused Core checks: 10 passing tests, including all reviewed globals, the three screenshot scripts, scene-scoped actors, CD used-by/local namespaces, CD relationships and three unchanged EGA readable goldens. The existing full-library formatting check still covers all **1,470 CD blocks** (122 globals, 78 entries, 78 exits, 515 locals, 677 object verbs) without raw-operation fallbacks. This formats code; it does not play scenes.
- `ScriptsCheck` opens the real WPF pages for Scripts 5, 7 and 16 on EGA/VGA, checks page/sidebar titles, Used by, Explore links, meaningful operands and Raw source, then opens VGA Script 7's links and navigates to Script 8. All 38 checks in `ui-reviewed/report.txt` pass; its captures are the final UI evidence. Offscreen WPF renders, not physical-input or listening evidence.
- Solution build: zero warnings/errors. No full suite or long replay/audio harness.

Working directory: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

```powershell
dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter 'FullyQualifiedName~LoomCdScriptReviewTests|FullyQualifiedName~LoomCdReadableTests|FullyQualifiedName~RealScriptsMatchTheirGoldenOutput|FullyQualifiedName~LoomUsageIndexTests' --verbosity quiet
dotnet build .verification/advanced-scripts/ScriptsCheck --no-restore --verbosity quiet
# Run Exe/ScriptsCheck/Debug/net10.0-windows/ScriptsCheck.exe with a 45-second process limit:
# arguments: <absolute Games folder> <fresh output folder>.
```

To reproduce the review inputs, run `ScriptReview` with the same two arguments (output `.verification/advanced-scripts/before`), then `review.ps1`. Generated script dumps/JSON contain local game data and are not committed; the reviewed names/fingerprints and summary table contain no game binaries.

App artifact: `Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe`.
