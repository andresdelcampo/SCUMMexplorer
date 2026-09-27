# Text refinement verification — 26 September 2026

Working directory for every command: `D:\Documents\Personal\Code Projects\SCUMMexplorer`.
Every shell command used the absolute RTK executable; launch required escalation. The supplied first instruction path `C:\Users\adelc.codex\RTK.md` did not exist; the instruction file was read from `C:\Users\adelc\.codex\RTK.md`.

## Supplied LOOM data

The final extraction contains **719 entries**: 580 spoken lines, 133 object names, and 6 stored lines. Compared with the preceding gallery's 780 entries, 61 low-value entries are removed. Filtering ignores displayed control markers and requires more than one letter/digit; blank, one-character, symbol-only, and control-only entries are excluded.

**345 entries have a decoded object link.** Object-owned sources show the stored name and object ID beside their room; searching the name also finds linked dialogue. Room 03's object 165 displays `cielo`. This describes source ownership, not an inference about what a sentence is discussing. A literal `setObjectName` target is recorded separately from its owning source object; variable targets remain unresolved.

**129 groups contain 379 spoken entries:** 83 explicit cutscene-marker groups (149 entries) and 46 ordinary script-dialogue groups (230 entries). Unmarked runs are grouped only when they contain at least two distinct lines; outer cutscene boundaries separate runs. Shared physical lines are deduplicated in the displayed transcript. Each group stays within a decoded source range, and source order is labelled. Branch alternatives, called scripts, timing, and interleaved execution are not reconstructed into verified full conversations. Speaker labels use decoded literal actor IDs or Ego; variable speakers remain unknown.

The existing extraction gaps are unchanged: four skipped indexed scripts (00, 51, 55, 116) and two truncated Room 70 local paths (203, 204). The extractor visited 1,419 ranges. See [exact diagnostics and assertions](window-check.txt). Complete stored-text recovery remains unresolved.

## Verification

Four semantic extractor regressions cover nested cutscene markers, source order and speakers, ordinary groups before/after cutscene boundaries, ungrouped single lines, explicit/dynamic object targets, and noise filtering. The full solution builds with zero warnings/errors.

The [WPF harness](../TextGalleryCheck/Program.cs) rendered the real windows and passed **19 assertions**, including object-name search, visible object 165 context, Spanish accents, room artwork, empty-result clearing, one row per group, later-line search retaining the complete transcript, speaker/source ordering, distinction between script dialogue and cutscene markers, and room-navigation dispatch. The checks inspect a nine-entry Room 04 local-script group and a 20-entry indexed script 68 dialogue. Physical mouse/keyboard input was not tested; navigation used a synthetic routed button event.

Captures were visually inspected: [object context](gallery.png), [cutscene group](cutscene.png), and [script dialogue](script-dialogue.png).

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter FullyQualifiedName~LoomTextExtractorTests --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/TextGalleryCheck/TextGalleryCheck.csproj --no-restore --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet 'Exe/TextGalleryCheck/Debug/net10.0-windows/TextGalleryCheck.dll' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\Games\loom' 'D:\Documents\Personal\Code Projects\SCUMMexplorer\.verification\game-text-refinement'
```

The first build was blocked by the already running project executable; it was closed gracefully after checking its process path, and subsequent builds passed. The first harness pass used an outdated numeric kind-filter index; the harness was corrected to select the semantic tag, then all assertions passed.

Application artifact: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`. Open **Game texts** and select **Conversations and cutscenes** to browse groups.
