# Global-script cutscene sites via accepted keys/clicks — 3 October 2026

After `e3221c1`. Audited union **223→229/235 (97.45%)**, 6 remaining; fresh single-action **169/235 (71.91%)** unchanged and distinct.

Route (one session, catalog Room34 arrival = explicit exploration assumption; labelled supported status edits: staff held, notes 3 then 7; no flag/class/actor edits). All input through public `PressKey`/`Click`:
- 'q' with staff held and nothing aimed → script 010 chains 013: `013 +0000/end0052` ("apuntar a algo primero").
- Click 561 (aim) then 'u' (note 7) with 3 notes → 010 chains 011: `011 +0000/end0071` (thread too high).
- Walk away, click 561 and press 'q' mid-walk → 010 chains 012: `012 +0000/end004A` (get closer).
- Notes 7, aim 561, keys 1,2,1,2 (not equal to any draft's notes or reverse) → 022 not a spell: `022 +0138/end0151`. (First try, 1,1,1,1, matched a draft sequence and chained 026 instead; not credited.)
- 'q' then Escape two frames later (draft playback, D66 set) → script 005 area 4: `005 +01C0/end01DB`.
- F5 (key 0x13F) → script 005 +0249 starts script 004: `004 +0000`, Room70 save menu opened; click on object 902 (Cancel, 272,100) → `004 end0206` and chain to script 006. **Caveat:** the session stays in Room 70 afterwards; the return to the previous room is not modelled/observed, so this site's end is credited at the source `endCutscene`, with the room return unverified.

Two diagnostic cycles at the route level (a1–a8 retained, a9 final): F5 first settle required balanced scopes/CanEdit, which a menu room cannot satisfy; later fixes were probe-side (label strings `Script 04/05`, wrong-note choice, tail settle). All 6 cutscene/endCutscene pairs Executed; 12 boundaries balanced; 943 frames. Watcher-style input sites have no direct object-verb control; none fabricated.

New audited sites: scripts 004 +0000, 005 +01C0, 011 +0000, 012 +0000, 013 +0000, 022 +0138. Remaining: Room34 local200 +0035 and local207 +0079 (route blocked, see Room34 chain), script 009 +002C (practice-hint verb 0x35 not on after the wrong try — source gate not yet established), script 022 +0005 (needs D70=1 rooms 36/41), script 066 (Room17 local200 +009A), script 096 (Room43 object 639 verb 38).

No production/catalog change; protected 57 exact/unstaged; App untouched; no push. Core662/broad/main Release dated.

```
rtk proxy dotnet run --project .verification/global-scripts-input-20261003/GlobalInputProbe/GlobalInputProbe.csproj -c Release -- Games/loom Exe/<fresh-directory>
rtk proxy python -X utf8 .verification/global-scripts-input-20261003/check.py
```
