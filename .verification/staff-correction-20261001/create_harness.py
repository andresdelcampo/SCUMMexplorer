from pathlib import Path
base=Path('.verification/staff-correction-20261001/StaffCheck')
base.mkdir(parents=True, exist_ok=True)
code=Path('.verification/ui-refinements-20261001/RefinementCheck/Program.cs').read_text(encoding='utf-8')
code=code.replace('S<System.Windows.Controls.Primitives.UniformGrid>("NoteButtons")','S<Canvas>("NoteButtons")')
code=code.replace('Children[count]', 'Children[count - 1]').replace('Children[8]', 'Children[7]')
code=code.replace('grid.Items.Count == 15','grid.Items.Count == 16').replace('fifteen source drafts','sixteen source drafts including Transcend')
code=code.replace('Note(0); Check(S<TextBlock>("NoteIdentities").Text == "No notes available" && S<TextBlock>("StaffLabel").Text == "Held", "graphical zero notes remains independent of staff possession");',
'''Staff(); Check(S<TextBlock>("NoteIdentities").Text == "Staff required" && !S<Canvas>("NoteButtons").IsEnabled, "staff removal disables note choices without changing stored level"); Staff();
                Check(S<Canvas>("NoteButtons").Children.Count == 8 && S<Canvas>("NoteButtons").Children.OfType<Button>().Select(b => (int)b.Tag).SequenceEqual(Enumerable.Range(1,8)), "eight correctly numbered choices with no None");
                Note(1); StatusCall("Apply_Click");
                Check(Session().GameStatus.AvailableNoteCount == 1 && Session().Snapshot.KnownGlobalVariables["var[0x07A]"] == 1, "C selection writes native inclusive level1");
                Note(8);''')
code=code.replace('((GameStatusWindow.DraftRow)grid.Items[14]).Title == "Draft 15"', '((GameStatusWindow.DraftRow)grid.Items[15]).Title == "Transcend" && ((GameStatusWindow.DraftRow)grid.Items[15]).Notes.SequenceEqual(new[] {8,4,5,1})')
code=code.replace('"verified display names and neutral unresolved fallback"','"verified display names include native Transcend C-prime F G C"')
code=code.replace('"clean status tracks live session changes");','"clean status tracks live session changes"); Capture(statusWindow, "status-four-notes");')
code=code.replace('"refinement-check.txt"','"staff-check.txt"')
code=code.replace('Call("PauseScenePlayback"); Capture(w, "status-native-cast");', '''Call("PauseScenePlayback"); Capture(w, "status-native-cast");
                await Pick(26); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.AvailableNoteCount == 8, "catalog departure preserves higher explicit eight-note choice");
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                var transcendRow = (GameStatusWindow.DraftRow)S<DataGrid>("DraftGrid").Items[15];
                S<DataGrid>("DraftGrid").ScrollIntoView(transcendRow); await Task.Delay(200); Capture(statusWindow, "status-transcend");
                Note(1); StatusCall("Apply_Click"); StatusCall("Close_Click"); statusWindow=null;
                await Pick(47); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.StaffHeld == true && Session().GameStatus.AvailableNoteCount >= 3, "native held-staff entry raises low browsing choice to the verified initial three-note level");
                statusWindow = (GameStatusWindow)Call("BuildGameStatusWindow")!; statusWindow.Show(); await Task.Delay(300);
                Note(8); StatusCall("Apply_Click"); StatusCall("Close_Click"); statusWindow=null;
                await Pick(26); await Wait(() => (bool)typeof(MainWindow).GetProperty("CanEditGameStatus", Private)!.GetValue(w)!);
                Check(Session().GameStatus.AvailableNoteCount == 8, "later room selection never reduces higher user level");''')
(base/'Program.cs').write_text(code,encoding='utf-8')
(base/'StaffCheck.csproj').write_text(Path('.verification/ui-refinements-20261001/RefinementCheck/RefinementCheck.csproj').read_text(),encoding='utf-8')
