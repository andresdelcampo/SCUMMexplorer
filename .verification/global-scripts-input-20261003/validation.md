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

## Addendum: script 066 via Room17 box 8 (audited 229→230/235, 97.87%, 5 remaining)

Source: Room 17 local 200 polls the ego's box; entering box 8 with bit DB9 clear runs `chainScript 066 [previous box]` (+009A). Script 066 sets DB9=1 and runs its cutscene `+0000/end0152`, then the session enters Room 56 (frame 464). Route (catalog Room17 arrival, no status edits): click box 8 centroid → the walk is first interrupted by the box-4 scene `200 +0022/end0042` (c1, retained, excluded); click box 8 again → `066 +0000/end0152` completes (c2 expected Room17 instead of the resulting Room56, a probe expectation error, retained, excluded); c3 with expected Room56 passes: 908 frames, 4 balanced boundaries, `Exe/GlobalInput-20261003/c3/input.json`. Probe `Room17Box8Probe`, check `check2.py` (summary2/remaining-sites2.json). No direct object-verb control exists for this watcher site. Three cycles, of which the first two were probe-side.

Tried and parked: script 022 +0005 in Room36 (D70=1): Room36's only non-exit object 600 has no verbs, so no object can be aimed (notes give 013 instead; `b1`, retained, excluded). Script 096 needs Room43 object 639/38 with D7B≠0, staff held (D61) and DA3≠0 — prerequisites not yet traced. Remaining 5: Room34 local200 +0035/local207 +0079, script009 +002C, script022 +0005, script096 +0000.

## Remaining 5 sites: decoded gates (no coverage; probes retained)

- **script 009 +002C** (practice-mode hint): proceeds only with staff aimed near an object, `var[0x07B]==0` (practice) and `var[0x063]!=0` after a wrong try; verb 0x35 is turned on by script 027 +0016. Boot sets var07B=1 (script 001 +0145); only Room 69 objects 876/877/878 set 0/1/2. A real click on 876 does set var07B=0 (`d2`), but Room 69's flow then stalls in the session after `local 201 chains to script 204` (4000 frames, `d4`), and a catalog hop to Room34 keeps `VerbScriptVariable`=Room69's local 200, so note keys never reach script 005 (`d3`). Standard-mode wrong try never shows verb 0x35 (verb items 1..18,49,50,54 in a9). Next: understand what Room69 local 204 waits for, or leave to a proper boot-to-Room01 route.
- **script 022 +0005** ("too noisy to weave"): needs D70=1 (Room36 entry, Room41 local206) plus four notes with an aimed object. Room36's only non-exit object 600 has no verbs, so nothing can be aimed (`b1`: 013 instead). Room41 untried.
- **script 096 +0000**: Room43 object 639/38 needs D7B≠0, staff held (D61) and bit DA3 — set only by script 095 +0725, i.e. the Room41 native090/095 chain that is the blocked route-priority task.
- **Room34 local200 +0035 / local207 +0079**: see Room34 chain validation; Healing 586/4D only after scene 087 sets D9D, and the 601 costume gate blocks the way back.

## Addendum: script 096 via actual Room43 clicks (audited 230→231/235, 98.30%, 4 remaining)

Earlier recipe `late096` reached 096 with direct verbs (exploration action, not audited). This task replays it with public `Click`/`PressKey` only (probe `Room43Probe`, check `check3.py`, summary3/remaining-sites3). Route from catalog Room43 (arrival = explicit exploration assumption), no status/flag/class/actor edits:
1. Click object 650 → script 093 (cage scene `+0000/end05FB`, Rooms 87→43→44→43). Source: 093 sets DB6=1/DB7=0, and Room43's class-24 setup (local202 +0031) leaves 639 untouchable until then; the first two cycles (e1, e2: click hit 641, then untouchable 639) are retained and excluded. 640 shadows 650 geometrically but is itself untouchable, so the probe skips untouchable earlier objects when picking a point.
2. Click 639 → Room44; double click 653, two frames, Escape → 095 `+0000/end079D` (skip override) → Room45.
3. Double click 656 (staff, entry resolved as Verb 0x38 +0005, same object code as the inventoried Verb 0x36 entry — not a separate site), click 655 → 092 `+0000/end0039` → Room43.
4. Click 639 → `096 +0000/end00E7`, then Room46.
2228 frames, 5 balanced cutscene scopes, every click `IsGameInput`, none "not sent". Script 096 events Executed (Certainty value 1, as recorded). New audited site: script 096 +0x0000. Fresh single-action coverage 169/235 unchanged.
Four runs (e1–e4): e1/e2 probe-side (click selection, untouchable 639), e3 probe error (650 shadowing), e4 passes; evidence `Exe/GlobalInput-20261003/e4/input.json`. Remaining 4: Room34 local200 +0035/local207 +0079, script009 +002C, script022 +0005. Protected 57 exact/unstaged; no production/catalog change; no App; no push.
