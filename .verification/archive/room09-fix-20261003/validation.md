# Room09 late-game casts via accepted input — 3 October 2026

After `2892f99`. The corrected probe (select draft by ForwardVerb OR ReverseVerb) completes the full Room09 route in one cycle. Audited union **215→219/235 (93.19%)**, 16 remaining; fresh single-action **169/235 (71.91%)**, 66 gaps/49 owners/335 starts unchanged.

## Route
Catalog arrival Room50 is an explicit exploration assumption (source Room50 entry writes D7B). Actual clicks: 721→089→Room7, 252→Room9, double-click 272 Look → 209 +0000/+0190 chains 101 (+0000/+08B3; visits 59 at frame 832, back in 9 at 1244). Labelled supported setup: staff, 7 notes, learned slots 11/12/13; source notes/reverse flags unchanged. Slot11 forward 4D (6,1,1,6), slot12 reverse 4F (1,1,1,7), slot13 reverse 51 (1,7,1,7), each: approach click, target click, then note keys through public PressKey.

Dispatches: 273/4D, 273/4F, 273/51 through Script 07 +002E. Completed scopes: 4D +000D/end0076 (refusal), local223 +0000/end02C4, local224 +0000/end041B. Both modes (direct ActivateObjectVerb control vs input) pass: direct 3147, input 3300 frames, 6 balanced boundaries, same rooms 11,7,9,59,9.

## Events
252 recorded body events (Script 101, local 223/224, 273/4D/4F/51), all Executed in both modes; 3 differ only in Certainty (retained). Estimates, certainty labels, argument-index uncertainty unchanged.

## Credit
New audited sites: Room 09 local 223 +0000, local 224 +0000, object 273 Verb 0x4D +000D, and script 101 +0000 (completed in the actual conversation prefix of a fully successful input run; earlier failed prefix remains uncredited).

Failure pairs `Exe/Room09Input-20261003` and Room34 chain captures preserved, hashed in preserved-before.json. No production/catalog change; protected 57 exact/unstaged; App untouched, no push. Core662/broad/main Release dated.

```
rtk proxy dotnet run --project .verification/archive/room09-fix-20261003/Room09FixProbe/Room09FixProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python -X utf8 .verification/archive/room09-fix-20261003/check.py
```
