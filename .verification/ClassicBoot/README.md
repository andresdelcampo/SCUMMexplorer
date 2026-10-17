# Classic bounded boot diagnostics — 17 October 2026

Foundation: a568445, f9eee73 and 2797a4d (including Zak's MachineSpeed reset), plus ScummV1DataSource and factory dispatch. This record is groundwork only: neither classic S4b stage is complete. No support-registry cells changed; Maniac's accepted costume status and Zak's partial status remain.

Command from `.worktrees/maniac-classic-s4b`:

`dotnet run --project .verification/ClassicBoot/ClassicBoot.csproj --no-restore -- "D:/Juegos/Lucas/mmansion ega en" "D:/Juegos/Lucas/zak ega en"`

No game variables are injected. Each session runs its bounded constructor boot, followed by 500 frames without player input.

* ManiacClassic: Script 1 +0x0199 loads Room 45; continuation +0x019B. Constructor reaches its 5000-frame cap in the party-selection cutscene. After another 500 frames Script 1 is at +0x01F8 (delay 72), Script 168 +0x000A (delay 410000). No unsupported instruction reported on this route; party selection and later classic semantics remain unvalidated.
* ZakClassic: initial actor palette blocker +0x0015 is fixed by f9eee73 (v1 palette index is the actor number, ScummVM script_v2.cpp o2_actorOps). The next bounded stop is Script 1 +0x0038: `verbOps 0x00, [delete var[0x042]]`. No room loaded, no pending scripts. Shared executor owner was notified; this is not a v1 index failure.

Colour extraction is static evidence, not runtime comparison. Fresh ZakClassic extraction now yields 35 costumes: 24 derived and 11 estimates (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32). The older 25/10 summary predates corrected v1 decoding and object-code bounds and is superseded.

Adapter/factory validation covers both classic fixtures' index owners/states, all discovered room dimensions, verb entries and tiled images, plus enhanced adapter regression and engine-variable reset tests. Command: `dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV1DataSourceTests|FullyQualifiedName~ScummManiacEnhancedSessionTests|FullyQualifiedName~ScummV2SessionVariableTests" -v minimal`: 8 passed, 0 failed. Maniac's fresh colour scan also remains 23 of 23 derived.
