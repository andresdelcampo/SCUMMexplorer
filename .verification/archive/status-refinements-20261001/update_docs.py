from pathlib import Path

record='../.verification/archive/status-refinements-20261001/validation.md'
new='''Requested follow-up completed: Learned toggles directly; Can reverse is a read-only tick/dash; native note tiles are38px high; selecting notes adds the staff, removing staff clears notes, and page edits require at least C/D/E. All16 draft names use source slot/response contracts, independent of randomized notes. Game Status follows Game Texts in Advanced and stays before Open Game Folder in both modes. Normal LOOM now has graphical Rooms/Costumes tabs opening the existing static gallery. The scythe-polishing scene is Room20 in this data (no21); its intact background is preserved instead of guessing later erasing overlays. Core277/277, WPF62/62, doorways16/16, controls19/19;76 rooms with zero gaps, only20 changed. [Evidence and reproduction]('''+record+'''). Source/replay APIs retain raw0/1/2; the C/D/E minimum is page editing policy, never a source prerequisite. Resume090/096 source certainty and existing incomplete routes; no catalog promotion.'''

path=Path('docs/LOOM_EGA_PLAN.md')
text=path.read_text(encoding='utf-8')
marker='## Current follow-up (1 October)\n'
assert marker in text
text=text.replace(marker,marker+'\n'+new+'\n',1)
path.write_text(text,encoding='utf-8')

path=Path('docs/LOOM_SCRIPT_MAP.md')
text=path.read_text(encoding='utf-8')
marker='## Follow-up findings (30 September)\n'
assert marker in text
text=text.replace(marker,marker+'''\nLatest1 October presentation/source-artwork follow-up: all16 draft display labels now guard the loaded slot/forward/reverse contracts; teaching owners/offsets and original manual terminology are tabulated in [status refinements]('''+record+'''). These labels never determine notes or flags. Explicit page selection couples notes/staff with a3-note floor; source APIs and replay setup retain raw values. Native note artwork crops only shared trailing blank rows. Normal costume cards pick a static decoded pose by visible pixel count; no animation order is inferred.\n\nRoom20's scythe is already in the background. Indexed-hidden392..396 are later099 +02BB removal/repair overlays, not first-view missing artwork. Generic fallback now protects intact index-hidden colour-removal images lacking positive entry state or entry/opening draws; actual reached draws still apply. This is an artwork-estimation heuristic, not a source-state transition.75 other room images are identical, all76 have zero gaps. [Rendering evidence](../.verification/archive/status-refinements-20261001/scythe-validation.md).\n''',1)
path.write_text(text,encoding='utf-8')

path=Path('docs/LOOM_CUTSCENE_DISCOVERY.md')
text=path.read_text(encoding='utf-8')
marker='## Current replay coverage (1 October)\n'
text=text.replace(marker,marker+'\nLatest requested UI/artwork follow-up: normal costume cards,16 named drafts and staff/note page coupling are presentation/explorer edits only. The scythe background correction changes Room20 artwork fallback, not script execution. Fresh363-root statuses/frame counts/reached rooms/scope boundaries, four established memberships and both sequence packets match the scope baseline.334 starts/168 of235 sites/50 outstanding owners/123 unresolved edges remain.76 room renders have zero gaps; only20 pixels changed. [Validation]('+record+').\n',1)
path.write_text(text,encoding='utf-8')

path=Path('docs/CHANGELOG.md')
text=path.read_text(encoding='utf-8')
marker='| --- | --- | --- |\n'
text=text.replace(marker,marker+'''| Status controls and normal costumes | Single-click Learned, read-only reverse,38px native notes, staff/note coupling with C/D/E edit minimum,16 source-associated names, stable toolbar position and graphical costume cards; WPF62/62, Core277/277, doorways16/16 and controls19/19 | [validation]('''+record+''') |\n| Scythe background preservation | Generic fallback avoids index-hidden later erasing overlays; polishing scene indexed20 restored; only20 of76 room renders changes, zero gaps; source/replay results unchanged | [source and validation](../.verification/archive/status-refinements-20261001/scythe-validation.md) |\n''',1)
path.write_text(text,encoding='utf-8')

path=Path('docs/NEXT_SESSION_PROMPT.md')
text=path.read_text(encoding='utf-8')
marker='State at checkpoint (1 October 2026):\n'
text=text.replace(marker,marker+'\n* Latest requested UI/artwork task: '+new+'\n* Fresh source/rendering regression:363 literal statuses/frame counts/rooms/scope traces, four memberships and both sequences match scope baseline; Blockers has zero blocked random sites.75 historical PNGs unchanged; only20 refreshed. Current packet Exe/StatusRefinementsSession-20261001/; validation .verification/archive/status-refinements-20261001/validation.md. Older275/47 checkpoints below are historical.\n',1)
text=text.replace('Staff absence disables note choices without destroying stored level. Source zero remains representable internally. Explicit C/D exploration choices remain possible.', 'The page now permits note selection without staff: selecting notes adds it and chooses at least3; removing staff clears notes. Raw source0/1/2 remains representable/inspectable internally; Core source/replay APIs keep1..8. C/D selections in this page now choose3, per the latest user request.')
text=text.replace('Opening/Closing and Dyeing/Bleaching retained;13 other slots neutral.', 'All16 names now guard their slot/response pairs, with neutral Draft N fallback for mismatches; teaching sources are recorded in the latest validation. Notes remain loaded, including randomized sequences.')
text=text.replace('Learned changes only2000; Can reverse read-only.', 'Learned changes only2000 and toggles directly once; Can reverse is a noninteractive tick/dash. Shared trailing blank note rows cropped, preserving native2x scale (38px selectors).')
text=text.replace('Latest requested UI/artwork task: Requested follow-up completed:', 'Latest requested UI/artwork task completed:')
text=text.replace('* Latest completed task: bounded native source progression', '* Previous completed source task: bounded native source progression')
text=text.replace('Eight choices C..C′ write1..8; None removed.', 'Native C..C′ IDs map1..8; the eight page choices now apply the3-note floor; None removed.')
text=text.replace('Label map guards slot/response pair; All16 names now guard', 'All16 names now guard')
text=text.replace('5. Current evidence .verification/staff-correction-', '5. Earlier evidence .verification/staff-correction-')
text=text.replace('Continue SCUMMexplorer from the completed native source-progression research', 'Continue SCUMMexplorer after the completed status/costume/scythe follow-up (.verification/archive/status-refinements-20261001/validation.md; latest git log), retaining the native source-progression research')
path.write_text(text,encoding='utf-8')
print('Updated plan, script map, discovery, changelog and handoff; historical checkpoints retained.')
