"""Create an isolated generic route probe with native note input, plus source excerpts."""
import pathlib
base=pathlib.Path('.verification/draft-scopes-20261001')
(base/'DraftProbe').mkdir(parents=True,exist_ok=True)
code=pathlib.Path('.verification/coverage-routes-20261001/RouteProbe/Program.cs').read_text(encoding='utf-8')
code='using SCUMMexplorer.Core.Rendering;\n'+code
code=code.replace('case "frames": Frames(', '''case "target":
                var record = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r=>r.ResourceId==session.Snapshot.RoomId).FilePath)).Single(o=>o.Id==step.GetProperty("objectId").GetInt32());
                actions.Add(session.Click(record.X+record.Width/2,record.Y+record.Height/2));break;
            case "draft":
                int slot=step.GetProperty("slot").GetInt32();
                ushort packed=session.Snapshot.KnownGlobalVariables[$"var[0x{0x32+2*slot:X3}]"];
                for(int i=0;i<4;i++) {
                    int id=((packed>>(3*i))&7)+1;
                    var note=session.VerbArea().Items.Single(v=>v.VerbId==id);
                    actions.Add(session.ClickVerbArea(note.X+1,note.Y+1));
                    for(int f=0;f<step.GetProperty("gap").GetInt32();f++)session.AdvanceFrame();
                }
                break;
            case "frames": Frames(''')
(base/'DraftProbe/Program.cs').write_text(code,encoding='utf-8')
(base/'DraftProbe/DraftProbe.csproj').write_text(pathlib.Path('.verification/coverage-routes-20261001/RouteProbe/RouteProbe.csproj').read_text(),encoding='utf-8')
lines=pathlib.Path('Exe/VerificationSession-20261001/native-code-1/code.tsv').read_text(encoding='utf-8').splitlines()
prefixes=('script 010\t','script 017\t','script 018\t','script 020\t','script 021\t','script 023\t','script 024\t','script 025\t','script 026\t','script 034\t','Room 41 local 213\t','Room 41 local 204\t','Room 01 local 211\t')
source=pathlib.Path('Exe/DraftScopeSession-20261001/source.tsv')
source.parent.mkdir(parents=True,exist_ok=True)
source.write_text('\n'.join(l for l in lines if l.startswith(prefixes))+'\n',encoding='utf-8')
