# Sharpening reverse with the confirmed user setup — 2 October 2026

Continues `292603a`/`0318792`. **One bounded Core keyboard reproduction completes the reported conversation and capture:**633/4B starts204(0),204 ends+031E and chains090 at+031F,090 executes42/43/60/43 and ends+0730. Final Room43,15 starts/15 ends,6342 total setup-and-playback frames. The final post-cast wait settles at1742 frames, within its3000-frame bound. No additional gameplay fix was needed; the general executed-estimated-load correction already handles this representative case. This is newly verified input behavior, not another production execution change.

## Confirmed setup, not fabricated game prerequisites

The user had already established correct note-key input and conversation dispatch. They explicitly confirm giving themselves the staff, enough notes and the learned spell through Game Status. The earlier four-note/unlearned test did not represent that report. These prerequisites/input method must not be asked again.

The separate recipe retains the previous source-input approach and inserts one named user-status step before the final Room37-to41 approach. The new console probe copies the existing recorder and adds only the supported status-edit calls and a pre-cast setup check. It waits normally for `GameStatus.CanEdit` (bounded3000), then calls `UpdateStaffPossession(true)`, `UpdateAvailableNotes(sourceDraft.Notes.Max())`, and `UpdateDraftLearned(9,true)`. No guard bypass, raw storage mutation, observer expansion, room exception, source-sequence change or reverse-flag edit. The API preserves its normal explicit-user-edit provenance; the count is an explicit known exploration choice, while the existing seeded draft flags/path/arrival/transition estimates remain labelled.

The loaded seed0 sequence remains6,6,6,5. Six available notes are the minimum sufficient representative of the user's “enough notes”; their exact selected count is unspecified. Before casting, the packet independently records staff=true,notes6,learned=true,reversible=true,reverse verb4B. The unchanged keyboard action sends reverse5,6,6,6 (`t y y y`) through `PressKey`, eight ordinary frames apart, while targeting633 through game input. It does not invoke633/4B directly or replace the cast with Play Cutscene. The old setup's earlier047 Escape remains; there is no Escape/skip during204/090.

This is a representative reproduction of the confirmed editing/input method with an existing source-input approach, not a recovered recording of the user's exact prior history or a claim of native puzzle progression.

## Actual execution and retained uncertainty

| Executed instruction | Evidence |
| --- | --- |
|633/4B +0000|`startScript 0xCC,[0x0000]` — the Sharpening-reverse response, not raw4's204(1)|
|Room41 local204 +031E/+031F|Actual end, then chain090|
|090 +0005/+00CF/+01F5/+0379|Loads42/43/60/43 at elapsed5116/5197/5429/5680|
|090 +0730|Actual end, balanced15/15 scopes|

Each listed event is `Executed=true`, `Certainty=Conditional`. Final snapshot remains nonfaithful; Room43 cage645 remains0 with estimated state, outside `StatesSetByCode`. No unsupported/unmodelled execution reported. No estimated value is relabelled as source-established to force continuation.

Full immutable capture: `Exe/SharpeningReverse-20261002/capture-1/sharpening-reverse-user-status.json`. [Hashed summary](summary.json), [assertions](check.py), [separate recipe](response18.json), [probe generation](prepare.py). Exactly one reproduction run; no further diagnostic cycle or general observer work.

Coverage stays **169/235=71.91%** from the separately committed sphere review. A longer keyboard recipe does not validate another literal root. Raw4, earlier response18 and historical packets remain unchanged; all57 protected files hash-exact and unstaged. No App launch/close/interaction or push, no physical-input/WPF claim. Production execution and catalog are unchanged by this task, so Core662/broad checks remain the dated0318792 baseline. The fresh main Release and console catalog checks from the immediately preceding sphere task remain applicable (zero warnings/errors); only the separate Core probe was built for this reproduction.

Reproduce from `D:\Documents\Personal\Code Projects\SCUMMexplorer` with fresh output:

```powershell
rtk proxy dotnet run --project .verification/archive/sharpening-reverse-20261002/SharpeningProbe/SharpeningProbe.csproj -c Release -- Games/loom .verification/archive/sharpening-reverse-20261002/response18.json Exe/<fresh-output>
```

The generated checker currently addresses the preserved capture-1 packet. Keep it and all historical evidence immutable when producing another capture. No next reproduction is needed by default: this bounded task is complete. A remaining UI symptom would need separately authorized visible evidence, with the existing user's App left untouched.
