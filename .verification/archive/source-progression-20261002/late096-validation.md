# Source095 override establishes096 — 2 October 2026

This recipe finishes script096 through source execution and the game's Escape override. It changes no game variables, actor positions, script slots or certainty. Direct object responses remain explicitly exploratory; this is not a native full-game playthrough or a replay promotion.

From a fresh default source session:

1. Catalog43; direct639/use (0x38). Its source reaction loads44; settle42 frames and require44.
2. Direct653/look (0x36) starts095. Advance2 frames, then press Escape27 at its actual recorded override. `abortCutscene` resumes the source skip jump0720.095 itself writes D7B/DA3/D97/D96 at0720/0725/072A/072F and loads45 at078C. Settle43 more frames; require45.
3. Direct656/look obtains the staff; settle75. Direct655/use runs092; settle49, require43.
4. Direct639/use now runs096. Settle146 frames:096 reaches `endCutscene` +00E7 and its later load46 +00EF. Final46, four begun/four ended scopes.

The ordinary full095 control has estimated D7B/DA3 and stays in43 after3000 frames, four begins/three ends. Waiting longer or revisiting43 does not replace the source override recipe. The successful run has D7B/DA3 equal1 with neither in the estimated-variable set. The skip is real input through `PressKey`, not a jump/known-state injection by the harness.

[Compact source state and hashes](late096-summary.json), recipe [late.json](late.json). Full source packets are ignored in `Exe/SourceProgressionSession-20261002/late-1/`. Shared regression evidence is [check-summary.json](check-summary.json): Core289, WPF62, all363 roots and both sequences unchanged,76 unchanged room renders, main Release clean. Fresh catalog43/639 remains a different44-only start; no fresh-root gap is removed.

Reproduce from the main checkout, each command prefixed with required absolute RTK `proxy`:

```powershell
dotnet run --project .verification/archive/source-progression-20261002/ProgressionProbe/ProgressionProbe.csproj -c Release -- Games/loom .verification/archive/source-progression-20261002/late.json Exe/SourceProgressionSession-20261002/late-fresh
```

The probe reflects only pending/override/event/provenance state, never mutates it. It requires the actual source room before subsequent responses. All inputs, direct verbs and catalog visits are recorded.
