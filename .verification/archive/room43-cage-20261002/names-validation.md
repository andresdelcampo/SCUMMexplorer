# Spell names for every numbered draft response

The user requested spell names for **all** cutscene responses, not just the two sword examples. `LoomDraftNames` now shares the existing16 Game Status associations with catalog display titles. All132 numbered responses across the330 generated entries are named; the four established entries retain their titles. Global picker, room picker and Game Texts consume the same loaded catalog.

Named paired labels remain Opening/Closing and Dyeing/Bleaching. Other reverse responses use the existing spell name plus `(reverse)` rather than inventing an unverified opposite name. Thus response8 is `Twisting`,9 is `Twisting (reverse)`,17 is `Sharpening`, and18 is `Sharpening (reverse)`. Object names and room prefixes remain. Generic `Other draft response` entries have no single mapped spell; their honest generic label remains.

The generated JSON and every historical title/evidence packet are untouched. Mapping happens after the existing edition fingerprint check. No verb, recipe, membership, source, frame count or coverage metadata changes. Game Status retains its existing16 labels and its fallback for mismatched/unresolved associations.

The console-only `NameCheck` compiles the actual shared catalog/naming code, validates the loaded game fingerprints, confirms132 title changes and no remaining `Draft response N` labels, and compares every non-title field plus the remainder of the catalog unchanged. Output: `Exe/Room43CageSession-20261002/names.json`. Both isolated App Release and final normal main-solution Release compile successfully with zero warnings/errors; no App launch or visible WPF claim.

```powershell
rtk proxy dotnet run --project .verification/archive/room43-cage-20261002/NameCheck/NameCheck.csproj -c Release -- Games/loom Exe/<fresh-name-report>.json
```
