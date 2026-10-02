# Native challenge expressions — 2 October 2026

The bounded executor now evaluates decoded old-v3 postfix `expression` terms: signed word inputs, an integer stack, ordered arithmetic and a final word write. Nested `getRandomNr` executes its destination write and pushes native scratch `var[0]`; its seed provenance remains estimated. Other nested opcodes, unknown operands, stack errors and division by zero remain explicit stops. There are no room-specific expression rules or certainty overrides.

The semantics follow ScummVM's [o5_expression](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v5.cpp) and [integer expression stack](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script.cpp). Native disassembly is `Exe/VerificationSession-20261001/native-code-1/code.tsv`, SHA256 `0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569`. Room69/local201 expressions at0022,0049 and0056 generate the challenge through actual source execution.

An optional Core constructor argument `stopAtBootInput:true` dispatches script001's initial Room69 load and then awaits input. It omits `CompleteBoot`'s assumed answer. Existing `completeBoot:false`, default app boot and stopped-opening behavior retain their contracts. The new mode is a research API; the app's UI is unchanged.

Eleven focused expression cases exercise signed arithmetic, integer intermediates, seeded scratch writes, operand provenance and conservative failures. A native-intro test clicks the source practice choice, generates all four answers, verifies the seed record and confirms001 still awaits a response. Core289/289 passed with absolute game roots and the unrelated untracked object-look audit excluded.

Fresh verification under `Exe/SourceProgressionSession-20261002/`:

* WPF62/62; doorways16/16; controls19/19; input18/18. Normal clocks and visible layout/synthetic handlers, not physical input or independent listening.
* All363 roots match status, frames, first/last activity, ever-open, begun/ended scopes, reached rooms and scene trace. All LFL fingerprints, four established memberships and both sequence packets match `Exe/DraftScopeSession-20261001/final-audits-1/`.
* Blockers: zero blocked random sites. RoomAudit76 has zero remaining gaps; every current historical PNG is unchanged, including the corrected Room20 background.
* Final main Release: zero warnings/errors. Executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`, cwd `D:\Documents\Personal\Code Projects\SCUMMexplorer`.

[Hashed check summary](check-summary.json) and [comparison script](summarize_checks.py) retain the exact report paths. Initial `checks-1` compile failure (missing test namespace/disassembly constructor arguments) is retained; corrected `checks-2` passes. No user app stopped, no push, no game data or replay catalog edits.

Reproduction (prefix every shell invocation with the required absolute RTK `proxy`):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/source-progression-20261001/run-checks.ps1 -OutputFolder Exe/SourceProgressionSession-20261002/checks-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-source-audits.ps1 -Output Exe/SourceProgressionSession-20261002/audits-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-ui.ps1 -Output Exe/SourceProgressionSession-20261002/ui-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/status-refinements-20261001/run-navigation.ps1 -Output Exe/SourceProgressionSession-20261002/navigation-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/source-progression-20261002/run-input.ps1 -Output Exe/SourceProgressionSession-20261002/input-fresh
dotnet run --project .verification/RoomAudit/RoomAudit.csproj -c Release -- Games/loom Exe/SourceProgressionSession-20261002/rooms-fresh
powershell -NoProfile -ExecutionPolicy Bypass -File .verification/draft-scopes-20261001/compare-pixels.ps1 -Audit Exe/SourceProgressionSession-20261002/rooms-fresh -Output Exe/SourceProgressionSession-20261002/pixels-fresh.txt
dotnet build SCUMMexplorer.slnx -c Release
```

The fresh native challenge subsequently dispatches203/205 with four generated glyph clicks; its inherited estimates still prevent a reached opening room. Supporting expressions does not establish a complete native boot. See [progression evidence](validation.md) for the source recipes and remaining certainty dependencies.
