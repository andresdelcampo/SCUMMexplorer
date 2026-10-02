"""One representative keyboard reproduction with the user's confirmed status edits."""
import json
from pathlib import Path

base = Path('.verification/sharpening-reverse-20261002')
probe = base/'SharpeningProbe'
probe.mkdir(parents=True, exist_ok=False)
source = Path('.verification/room41-certainty-20261002/ProgressionProbe')
code = (source/'Program.cs').read_text(encoding='utf-8')
needle = '            case "frames":'
assert code.count(needle) == 1
code = code.replace(needle, '''            case "user-status":
                // Reproduce the user's confirmed edits through the same supported API.
                // Waiting advances the ordinary scheduler; no busy guard is bypassed.
                int editWait=0;
                while(!session.GameStatus.CanEdit && editWait<3000) {Advance();editWait++;}
                Record("user-status-before",editWait);
                if(!session.GameStatus.CanEdit) throw new InvalidOperationException("User status edit never became available.");
                var sourceDraft=session.GameStatus.Drafts.Single(d=>d.Slot==9);
                if(sourceDraft.Reversible!=true || sourceDraft.ReverseVerb!=0x4B)
                    throw new InvalidDataException("Loaded Sharpening reverse is not established.");
                session.UpdateStaffPossession(true);
                session.UpdateAvailableNotes(sourceDraft.Notes.Max());
                session.UpdateDraftLearned(9,true);
                actions.Add(new {userConfirmedStatusEdits=true,staffHeld=true,availableNotes=sourceDraft.Notes.Max(),learnedSlot=9,
                    noteCountChoice="Minimum sufficient for the unchanged loaded sequence; user exact count unspecified"});
                Record("user-status-after",0);
                break;
''' + needle)
needle = '                Record($"draft-{keySlot}-before",0);'
assert code.count(needle) == 1
code = code.replace(needle, needle+'''
                if(keySlot==9) {
                    var ready=session.GameStatus; var spell=ready.Drafts.Single(d=>d.Slot==9);
                    if(ready.StaffHeld!=true || spell.Learned!=true || spell.Reversible!=true || ready.AvailableNoteCount<spell.Notes.Max())
                        throw new InvalidOperationException("Confirmed staff/notes/learned setup did not survive to the cast.");
                }''')
(probe/'Program.cs').write_text(code, encoding='utf-8')
(probe/'SharpeningProbe.csproj').write_bytes((source/'ProgressionProbe.csproj').read_bytes())
route = json.loads(Path('.verification/estimated-room-loads-20261002/response18.json').read_text())[0]
route['id'] = 'sharpening-reverse-user-status'
route['setupEvidence'] = 'User confirmed Game Status staff, enough notes, learned spell, then note keys. Existing source-input approach retained as a representative setup, not a recording of their exact prior history.'
index = next(i for i,a in enumerate(route['actions']) if a == dict(kind='visit',room=37))
route['actions'].insert(index,dict(kind='user-status'))
assert not any(a['kind']=='verb' and a['objectId']==633 for a in route['actions'])
assert not any(a['kind']=='until-override' and a.get('owner')=='Room 41 local script 204' for a in route['actions'])
(base/'response18.json').write_text(json.dumps([route],indent=2)+'\n',encoding='utf-8')
print('Prepared one recipe: unchanged source notes/reverse flag; confirmed user status edits; keyboard dispatch; full204/090 without Escape.')
