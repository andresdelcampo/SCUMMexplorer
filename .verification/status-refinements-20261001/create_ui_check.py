from pathlib import Path

base=Path('.verification/status-refinements-20261001')
target=base/'StatusRefinementCheck'
target.mkdir(parents=True,exist_ok=True)
original=Path('.verification/staff-correction-20261001/StaffCheck')
code=(original/'Program.cs').read_text(encoding='utf-8')
code=code.replace('using System.Windows.Media.Imaging;', 'using System.Windows.Media.Imaging;\nusing System.Windows.Automation.Peers;\nusing System.Windows.Automation.Provider;')
code=code.replace('top.Children.IndexOf(global) == top.Children.IndexOf(status) + 1', 'top.Children.IndexOf(status) == top.Children.IndexOf(Find<Button>("GameTextsButton")) + 1')
code=code.replace('Advanced: Game Status immediately beside global Play Cutscene', 'Advanced: Game Status immediately after Game Texts')
code=code.replace('Staff(); Check(S<TextBlock>("NoteIdentities").Text == "Staff required" && !S<Canvas>("NoteButtons").IsEnabled, "staff removal disables note choices without changing stored level"); Staff();', '''Staff(); Check(S<TextBlock>("NoteIdentities").Text == "Staff required" && S<Canvas>("NoteButtons").IsEnabled, "note choices remain available to add the staff");
                StatusCall("Apply_Click");
                Check(Session().GameStatus.StaffHeld == false && Session().GameStatus.AvailableNoteCount == 0, "removing staff and applying clears notes");
                Note(2); StatusCall("Apply_Click");
                Check(Session().GameStatus.StaffHeld == true && Session().GameStatus.AvailableNoteCount == 3, "note selection adds staff and enforces C D E minimum");
                Staff(); StatusCall("Apply_Click"); Staff(); StatusCall("Apply_Click");
                Check(Session().GameStatus.StaffHeld == true && Session().GameStatus.AvailableNoteCount == 3, "adding staff alone restores C D E minimum");''')
code=code.replace('AvailableNoteCount == 1 && Session().Snapshot.KnownGlobalVariables["var[0x07A]"] == 1, "C selection writes native inclusive level1"', 'AvailableNoteCount == 3 && Session().Snapshot.KnownGlobalVariables["var[0x07A]"] == 3, "C selection retains minimum native level3"')
needle='Capture(statusWindow, "status-normal");'
assert code.count(needle)==1
code=code.replace(needle,needle+'''
                Check(S<Canvas>("NoteButtons").Height < 64 && S<Canvas>("NoteButtons").Children.OfType<Button>().All(b=>b.ActualHeight==S<Canvas>("NoteButtons").Height), "all note choices crop trailing empty artwork rows at native scale");
                report.Add($"Note choice height: {S<Canvas>("NoteButtons").Height}");
                T? Child<T>(DependencyObject parent) where T:DependencyObject { for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var c=VisualTreeHelper.GetChild(parent,i);if(c is T t)return t;if(Child<T>(c) is {} nested)return nested;}return null; }
                var learned=(DataGridTemplateColumn)grid.Columns.Single(c=>(string)c.Header=="Learned");
                var check=Child<CheckBox>(learned.GetCellContent(row))!;
                Check(check.IsEnabled && check.IsHitTestVisible && !grid.IsReadOnly, "Learned checkbox is directly interactive before entering cell edit mode");
                var beforeLearned=row.Learned;
                ((IToggleProvider)new CheckBoxAutomationPeer(check).GetPattern(PatternInterface.Toggle)).Toggle();
                Check(row.Learned==!beforeLearned && S<Button>("ApplyButton").IsEnabled, "one checkbox toggle changes Learned and enables Apply without a focus click");
                ((IToggleProvider)new CheckBoxAutomationPeer(check).GetPattern(PatternInterface.Toggle)).Toggle();
                var reverse=(DataGridTemplateColumn)grid.Columns.Single(c=>(string)c.Header=="Can reverse");
                var indicator=Child<TextBlock>(reverse.GetCellContent(row))!;
                Check(reverse.IsReadOnly && Child<CheckBox>(reverse.GetCellContent(row)) is null && !indicator.Focusable && indicator.ToolTip.ToString()!.StartsWith("Read only"), "reverse capability has a noninteractive read-only indicator instead of an editable checkbox");
                var expectedNames=new[]{"Opening / Closing","Straw into Gold","Dyeing / Bleaching","Night Vision","Twisting","Invisibility","Emptying","Sleep","Terror","Sharpening","Healing","Reflection","Silence","Shaping","Unmaking","Transcend"};
                Check(grid.Items.Cast<GameStatusWindow.DraftRow>().Select(r=>r.Title).SequenceEqual(expectedNames), "all sixteen draft slots have source-associated names independent of their randomized notes");
                var names=typeof(MainWindow).Assembly.GetType("SCUMMexplorer.App.LoomDraftNames")!;
                var mismatched=Session().GameStatus.Drafts[3] with {ForwardVerb=0x3A};
                Check(names.GetMethod("Title")!.Invoke(null,[mismatched])!.Equals("Draft 4"), "unmatched response contract retains neutral name fallback");
''')
needle='var session = Session(); Advanced(true);'
assert code.count(needle)==1
code=code.replace(needle,'''var session = Session();
                Find<RadioButton>("CostumesTab").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Check(Find<ScrollViewer>("CostumeCardScroll").IsVisible && !Find<ScrollViewer>("RoomCardScroll").IsVisible, "normal mode offers graphical costume selection beside Rooms");
                await Wait(()=>Find<WrapPanel>("CostumeCardList").Children.OfType<Button>().All(b=>!b.ToolTip.ToString()!.Contains("loading")));
                var cards=Find<WrapPanel>("CostumeCardList").Children.OfType<Button>().ToArray();
                Check(cards.Length==((GameLibrary)Field("_game")!).Resources.Count(r=>r.Kind==ResourceKind.Costume), "every costume has a card, including clearly unavailable resources");
                Check(cards.Count(c=>c.Content is Panel p && p.Children.OfType<Image>().Any(i=>i.Source is not null))>100, "costume cards show decoded representative poses");
                Capture(w,"normal-costume-cards");
                var card=cards.First(c=>c.Content is Panel);
                card.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                await Wait(()=>Find<FrameworkElement>("CostumePanel").IsVisible);
                Check(Find<WrapPanel>("CostumePoseGallery").Children.Count>0, "normal costume card opens existing decoded pose gallery");
                Advanced(true); Advanced(false);
                Check(Find<ScrollViewer>("CostumeCardScroll").IsVisible && Find<FrameworkElement>("CostumePanel").IsVisible && ReferenceEquals(session,Session()), "costume selection and exploration session survive Advanced round trip");
                Capture(w,"normal-costume-gallery");
                await Pick(20);
                Check(!((SCUMMexplorer.Core.Rendering.RoomObjectSelection)Field("_objectSelection")!).PaintOrder().Any(o=>o.Id is >=392 and <=396), "scythe background remains visible in the actual polishing room view");
                Capture(w,"scythe-polishing-room");
                await Pick(2); Advanced(true);''')
(target/'Program.cs').write_text(code,encoding='utf-8')
(target/'StatusRefinementCheck.csproj').write_text((original/'StaffCheck.csproj').read_text(encoding='utf-8'),encoding='utf-8')
print('Created WPF status/costume regression harness with current UI expectations.')
