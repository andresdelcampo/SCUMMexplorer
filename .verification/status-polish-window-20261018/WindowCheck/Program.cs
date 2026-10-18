using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SCUMMexplorer.App;
using SCUMMexplorer.Core.Scumm;

// PlayWindowCheck <fixture> <output> <selected room> <actions.json>
// Genuine click/key inputs in the actual MainWindow session. After checking its
// timer starts, drive that timer's handler deterministically; this is not a timing check.
internal static class Program
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    static void Main(string[] args)
    {
        var output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        Environment.SetEnvironmentVariable("SCUMM_EXPLORER_PROFILE_ROOT", Path.Combine(output, "profile"));
        Environment.SetEnvironmentVariable("SCUMM_ROOM_CACHE_DIR", Path.Combine(output, "room-cache"));
        var actions = JsonSerializer.Deserialize<ActionStep[]>(File.ReadAllText(args[3]), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var app = new App(); app.InitializeComponent();
        var window = new MainWindow();
        object? Field(string name) => typeof(MainWindow).GetField(name, Private)!.GetValue(window);
        object? Call(string name, params object?[] values) => typeof(MainWindow).GetMethod(name, Private)!.Invoke(window, values);
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Save(string name, FrameworkElement? target = null)
        {
            target ??= window; target.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target);
            var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
        }
        window.ContentRendered += async (_, _) =>
        {
            var report = new List<string>();
            ScummGameSession? session = null;
            try
            {
                Call("OpenGameFolder", args[0]);
                await Task.Delay(1000);
                var tree = (TreeView)window.FindName("ResourceTree");
                var item = tree.Items.OfType<TreeViewItem>().SelectMany(g => g.Items.OfType<TreeViewItem>()).First(i =>
                    (int?)i.Tag?.GetType().GetProperty("ResourceId")?.GetValue(i.Tag) == int.Parse(args[2]) &&
                    i.Tag?.GetType().GetProperty("Kind")?.GetValue(i.Tag)?.ToString() == "Room");
                Call("SelectResourceInTree", item.Tag);
                for (var i = 0; i < 1200 && Field("_playTimer") is null; i++) await Task.Delay(100);
                session = (ScummGameSession?)Field("_play") ?? throw new Exception("Play did not start");
                var timer = (DispatcherTimer?)Field("_playTimer") ?? throw new Exception("No play timer");
                await Task.Delay(300);
                Require((int)Field("_playFrame")! > 0, "Window timer did not advance");
                timer.Stop();
                var image = (Image)window.FindName("PlayScreenImage");
                Require(image.IsVisible && image.Cursor == Cursors.None, "Play image/cursor missing");
                Require(image.ToolTip is null && ((TextBlock)window.FindName("ObjectsNote")).Text == "", "Technical note in Normal");
                report.Add($"Initial room={session.CurrentRoomId}; timer frames={Field("_playFrame")}; Normal clean; cursor hidden");
                async Task Frames(int count)
                {
                    for (var i = 0; i < count; i++)
                    {
                        var before = (int)Field("_playFrame")!;
                        for (var wait = 0; wait < 2000 && (int)Field("_playFrame")! == before; wait++)
                        {
                            Call("PlayTimer_Tick", null, EventArgs.Empty);
                            if ((int)Field("_playFrame")! == before) await Task.Delay(10);
                        }
                        Require((int)Field("_playFrame")! > before, "Window frame did not advance (sound preparation?)");
                        if (i % 10 == 0) await Task.Delay(1);
                    }
                }
                var maniac = int.Parse(args[2]) == 44;
                void SelectionHeaderCheck()
                {
                    if (!maniac || session.CurrentRoomId != 45) return;
                    Call("ShowPlayFrame");
                    Require(!((Button)window.FindName("GameStatusButton")).IsVisible, "Game Status offered during character selection");
                    report.Add("Character selection: Game Status hidden");
                }
                SelectionHeaderCheck();
                foreach (var step in actions)
                {
                    switch (step.Op)
                    {
                        case "click":
                            Require(session.ClickScreen(step.X, step.Y, out var why) is not null, "Click refused: " + why);
                            break;
                        case "roomclick":
                            var screenX = step.X - session.ViewLeft;
                            Require(screenX is >= 0 and < 320, "Room point is outside the current view");
                            Require(session.ClickScreen(screenX, step.Y + session.MainScreen.Top, out var roomWhy) is not null, "Room click refused: " + roomWhy);
                            break;
                        case "point": session.PointAtScreen((step.X, step.Y)); break;
                        case "text":
                            var composition = new TextComposition(InputManager.Current, window, step.Text!);
                            Call("PlayTextInput", new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
                            break;
                        case "escape":
                            var source = PresentationSource.FromVisual(window)!;
                            Require((bool)Call("PlayKeyDown", new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent })!, "Escape not handled");
                            break;
                        case "frames": await Frames(step.Count); break;
                        case "room": Require(session.CurrentRoomId == step.Value, $"Expected room {step.Value}; got {session.CurrentRoomId}"); break;
                        case "untilroom":
                            for (var i = 0; i < step.Count && session.CurrentRoomId != step.Value; i++) await Frames(1);
                            Require(session.CurrentRoomId == step.Value, $"Did not reach room {step.Value}");
                            break;
                        case "untilskip":
                            for (var i = 0; i < step.Count && !session.CutsceneSkippable; i++) await Frames(1);
                            Require(session.CutsceneSkippable, "Did not reach cutscene override");
                            break;
                        case "untilcontrol":
                            for (var i = 0; i < step.Count && (!session.UserInputOn || session.InCutscene); i++) await Frames(1);
                            Require(session.UserInputOn && !session.InCutscene, "Control did not return");
                            break;
                        case "owner": Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) == step.Value, $"Object {step.X} not owned by {step.Value}"); break;
                        case "untilowner":
                            for (var i = 0; i < step.Count && session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) != step.Value; i++) await Frames(1);
                            Require(session.Snapshot.ObjectOwners.GetValueOrDefault(step.X) == step.Value, $"Object {step.X} not owned by {step.Value}");
                            break;
                        case "variable": Require(session.Snapshot.KnownGlobalVariables.GetValueOrDefault(step.Text!) == step.Value, "Unexpected variable: " + step.Text); break;
                        case "trace": Require(session.SessionTrace.Any(s => s.Contains(step.Text!, StringComparison.Ordinal)), "Missing trace: " + step.Text); break;
                        case "message": Require(session.Messages.OnScreen.Any(m => m.Text.Contains(step.Text!, StringComparison.Ordinal)), "Missing message: " + step.Text); break;
                        case "control": Require(session.UserInputOn && !session.InCutscene, "No player control"); break;
                        case "save": Call("ShowPlayFrame"); await Task.Delay(30); Save(step.Text!); break;
                        default: throw new Exception("Unknown action: " + step.Op);
                    }
                    SelectionHeaderCheck();
                    report.Add($"{step.Op} {step.Text} ({step.X},{step.Y}) n={step.Count} value={step.Value}: room={session.CurrentRoomId}, inputs={session.DeliveredInput.Count}, frame={Field("_playFrame")}");
                    File.WriteAllLines(Path.Combine(output, "progress.txt"), report);
                }
                for (var n = 0; n < 3000 && !session.CaptureInventoryStatus().CanEdit; n++) await Frames(1);
                Require(session.CaptureInventoryStatus().CanEdit, "No safe edit point: " + session.CaptureInventoryStatus().Refusal);
                var held = actions.Last(a => a.Op == "owner");
                var recipients = session.CaptureInventoryStatus().Characters.Where(c => c.ActorId != held.Value && c.Name is not null).ToArray();
                var partyBindings = ((ValueTuple<string, int>[]?)typeof(ScummSession).GetField("_inventoryPartyBindings", Private)!.GetValue(session)) ?? [];
                var party = partyBindings.Select(binding => (int)session.Snapshot.KnownGlobalVariables.GetValueOrDefault(binding.Item1)).ToHashSet();
                var targetActor = recipients.First().ActorId;
                if (maniac)
                {
                    Require(session.CaptureInventoryStatus().Characters.Count == 3, "The Maniac editor did not restrict recipients to the selected party");
                    Require(session.CaptureInventoryStatus().Characters.All(c => party.Contains(c.ActorId)), "An unselected child is offered");
                }
                else Require(session.CaptureInventoryStatus().Characters.Count == 4, "The Zak editor did not offer all four source characters");
                report.Add($"Admin recipient actor {targetActor}; source party={string.Join(',', party)}; selected-party-only={maniac}");
                var originalInventory = session.Snapshot.Inventory.ToArray();
                var actorsBeforePictures = JsonSerializer.Serialize(session.Actors);
                foreach (var character in session.CaptureInventoryStatus().Characters)
                {
                    var appearance = session.ReadInventoryCharacterAppearance(character.ActorId, out var evidence);
                    Require(appearance is not null && appearance.Number == character.ActorId && appearance.Look.Costume > 0,
                        "Native character body appearance did not resolve: " + character.Name);
                    Require(!string.IsNullOrWhiteSpace(evidence), "Character appearance lacks source evidence");
                    report.Add($"Native display appearance: {character.Name}; costume={appearance!.Look.Costume}; {evidence}");
                }
                Require(JsonSerializer.Serialize(session.Actors) == actorsBeforePictures, "Display appearance API altered live actors");
                var header = (Button)window.FindName("GameStatusButton");
                Require(header.IsVisible && header.IsEnabled, "Inventory header action missing");
                string Unrelated() => JsonSerializer.Serialize(new {
                    session.Snapshot.ObjectStates, session.Snapshot.KnownGlobalVariables, session.Snapshot.ObjectClasses,
                    session.Snapshot.ObjectPositions, session.Actors, session.CurrentRoomId, session.CutscenesBegun,
                    Others = session.Snapshot.ObjectOwners.Where(p => p.Key != held.X).OrderBy(p => p.Key)
                });
                var beforeState = Unrelated();
                void DialogCheck(bool roundTrip, bool paused)
                {
                    if (paused)
                    {
                        Call("PlaybackPause_Click", window, new RoutedEventArgs(Button.ClickEvent));
                        Call("PlayTimer_Tick", null, EventArgs.Empty);
                        Require((bool)Field("_playPaused")!, "Pause action did not pause");
                    }
                    timer.Start();
                    var frame = (int)Field("_playFrame")!;
                    Exception? failure = null;
                    window.Dispatcher.BeginInvoke(new Action(async () =>
                    {
                        InventoryStatusWindow? dialog = null;
                        try
                        {
                            dialog = Application.Current.Windows.OfType<InventoryStatusWindow>().Single();
                            await Task.Delay(250);
                            Require(!timer.IsEnabled && (bool)Field("_playPaused")! && (int)Field("_playFrame")! == frame, "Clock advanced in modal status");
                            Require(((TextBlock)dialog.FindName("AdvancedEvidence")).Visibility == Visibility.Collapsed, "Technical evidence in Normal status");
                            var recipients = (ComboBox)dialog.FindName("GrantRecipient");
                            Require(recipients.Items.Count == session.CaptureInventoryStatus().Characters.Count, "Missing recipient choices");
                            var portraits = recipients.Items.Cast<object>().Count(c => c.GetType().GetProperty("Portrait")!.GetValue(c) is BitmapSource);
                            Require(portraits == recipients.Items.Count, "A native character body picture is missing");
                            foreach (var recipient in recipients.Items)
                            {
                                var picture = (BitmapSource)recipient.GetType().GetProperty("Portrait")!.GetValue(recipient)!;
                                Require(picture.PixelWidth > 0 && picture.PixelHeight >= 20 && (double)picture.PixelHeight / picture.PixelWidth > 1.3,
                                    "Character body is empty or still a head-only portrait");
                                report.Add($"Body picture: {recipient}; native pixels={picture.PixelWidth}x{picture.PixelHeight}");
                            }
                            IEnumerable<DependencyObject> Descendants(DependencyObject visual)
                            {
                                for (var index = 0; index < VisualTreeHelper.GetChildrenCount(visual); index++)
                                {
                                    var child = VisualTreeHelper.GetChild(visual, index);
                                    yield return child;
                                    foreach (var descendant in Descendants(child)) yield return descendant;
                                }
                            }
                            dialog.UpdateLayout();
                            Require(Descendants(recipients).OfType<Image>().Any(image => image.Source is not null && image.IsVisible && image.ActualHeight >= 40), "The selected recipient picture is not visible");
                            foreach (var recipient in recipients.Items)
                            {
                                recipients.SelectedItem = recipient; dialog.UpdateLayout();
                                var source = (BitmapSource)recipient.GetType().GetProperty("Portrait")!.GetValue(recipient)!;
                                Require(Descendants(recipients).OfType<Image>().Any(image => ReferenceEquals(image.Source, source) && image.IsVisible && image.ActualHeight >= 40), "A selected character picture is missing");
                                Save("status-character-" + recipient.GetType().GetProperty("ActorId")!.GetValue(recipient), recipients);
                            }
                            recipients.SelectedValue = held.Value;
                            recipients.IsDropDownOpen = true;
                            await Task.Delay(80);
                            foreach (var recipient in recipients.Items)
                            {
                                var container = (ComboBoxItem)recipients.ItemContainerGenerator.ContainerFromItem(recipient);
                                Require(container is not null, "A dropdown recipient is not rendered");
                                if (recipient.GetType().GetProperty("Portrait")!.GetValue(recipient) is BitmapSource)
                                    Require(Descendants(container!).OfType<Image>().Any(image => image.Source is not null), "A dropdown recipient picture is not rendered");
                            }
                            var popup = (System.Windows.Controls.Primitives.Popup)recipients.Template.FindName("PART_Popup", recipients);
                            Save("status-recipients-dropdown", (FrameworkElement)popup.Child);
                            recipients.IsDropDownOpen = false;
                            report.Add($"Recipient pictures: {portraits}/{recipients.Items.Count}");
                            Save("status-catalog", dialog);
                            var catalogForLayout = (ComboBox)dialog.FindName("CatalogItems");
                            var addForLayout = (Button)dialog.FindName("AddItemButton");
                            var searchForLayout = (TextBox)dialog.FindName("ItemSearch");
                            foreach (var size in new[] { (Width: 900d, Height: 720d), (Width: 780d, Height: 560d) })
                            {
                                dialog.Width = size.Width; dialog.Height = size.Height; dialog.UpdateLayout();
                                double Bottom(FrameworkElement control) => control.TranslatePoint(new Point(0, control.ActualHeight), dialog).Y;
                                Require(Math.Abs(catalogForLayout.ActualHeight - 32) < 1 && Math.Abs(addForLayout.ActualHeight - 32) < 1, "Catalog/Add controls are not compact 32px controls");
                                Require(Math.Abs(Bottom(catalogForLayout) - Bottom(recipients)) < 1 && Math.Abs(Bottom(addForLayout) - Bottom(recipients)) < 1, "Catalog, Give to and Add are not bottom aligned");
                                Require(Bottom(searchForLayout) <= catalogForLayout.TranslatePoint(new Point(), dialog).Y, "Search overlaps catalog");
                                Save($"status-layout-{size.Width}x{size.Height}", dialog);
                                report.Add($"Layout {size.Width}x{size.Height}: catalog={catalogForLayout.ActualHeight}, add={addForLayout.ActualHeight}; bottoms {Bottom(catalogForLayout)}/{Bottom(recipients)}/{Bottom(addForLayout)}");
                            }
                            dialog.Width = 900; dialog.Height = 720; dialog.UpdateLayout();
                            ComboBox Selector() => ((StackPanel)dialog.FindName("InventoryRows")).Children.OfType<Grid>()
                                .SelectMany(g => g.Children.OfType<ComboBox>()).Single(c => (int)c.Tag == held.X);
                            var selector = Selector();
                            Require(selector.IsEnabled && selector.ToolTip is null, "Held item is not editable in Normal");
                            selector.SelectedValue = targetActor;
                            Require(selector.SelectedItem?.ToString() == session.CaptureInventoryStatus().Characters.Single(c => c.ActorId == targetActor).Name,
                                "Recipient label exposes a record or identifier instead of its source name");
                            Require(((Button)dialog.FindName("ApplyButton")).IsEnabled, "Apply not enabled for pending choice");
                            if (roundTrip)
                            {
                                ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(session.Snapshot.ObjectOwners[held.X] == targetActor, "Apply did not change owner");
                                Require(!session.Snapshot.Inventory.Contains(held.X), "Native current-character inventory did not refresh");
                                var afterApplyState = Unrelated();
                                if (afterApplyState != beforeState)
                                {
                                    File.WriteAllText(Path.Combine(output, "unrelated-before.json"), beforeState);
                                    File.WriteAllText(Path.Combine(output, "unrelated-after.json"), afterApplyState);
                                }
                                Require(afterApplyState == beforeState, "Apply changed unrelated game state");
                                Save("status-applied", dialog);
                                ((Button)dialog.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(session.Snapshot.ObjectOwners[held.X] == held.Value, "Undo did not restore owner");
                                Require(session.Snapshot.Inventory.SequenceEqual(originalInventory) && Unrelated() == beforeState, "Undo did not restore inventory or preserve state");
                                Save("status-undone", dialog);
                                var catalog = (ComboBox)dialog.FindName("CatalogItems");
                                var itemCount = catalog.Items.Count;
                                Require(itemCount == (maniac ? 48 : 70), "The logical source inventory catalog has an unexpected count");
                                Require(itemCount == session.CaptureInventoryStatus().Catalog.Count(item => !string.IsNullOrWhiteSpace(item.Name)), "The all-items picker omitted a named source item");
                                if (!maniac)
                                {
                                    var choices = catalog.Items.Cast<object>().ToArray();
                                    var ids = choices.Select(c => (int)c.GetType().GetProperty("ObjectId")!.GetValue(c)!).ToHashSet();
                                    Require(!ids.Overlaps(new[] { 429, 431, 471, 535, 536, 537 }), "A remote-room item alias is offered as a separate item");
                                    var names = choices.Select(c => (string)c.GetType().GetProperty("Name")!.GetValue(c)!).ToArray();
                                    Require(names.Distinct(StringComparer.CurrentCultureIgnoreCase).Count() == names.Length, "Real separate homonyms have indistinguishable labels");
                                    Require(names.Any(n => n.EndsWith(" (1)", StringComparison.Ordinal)) && names.Any(n => n.EndsWith(" (2)", StringComparison.Ordinal)), "Separate homonyms lack stable ordinals");
                                    Require(!names.Any(n => n.Contains("Object ", StringComparison.Ordinal) || System.Text.RegularExpressions.Regex.IsMatch(n, @"\((?:139|429|431|471|535|536|537)\)")), "Normal catalog exposes raw identifiers");
                                    catalog.SelectedItem = choices.First(c => ((string)c.GetType().GetProperty("Name")!.GetValue(c)!).EndsWith(" (1)", StringComparison.Ordinal));
                                    catalog.IsDropDownOpen = true;
                                    await Task.Delay(80);
                                    var catalogPopup = (System.Windows.Controls.Primitives.Popup)catalog.Template.FindName("PART_Popup", catalog);
                                    Save("status-logical-items-dropdown", (FrameworkElement)catalogPopup.Child);
                                    catalog.IsDropDownOpen = false;
                                    report.Add("Zak logical items: aliases collapsed; independent homonyms uniquely named with ordinals");
                                }
                                var search = (TextBox)dialog.FindName("ItemSearch");
                                search.Text = "__no_item_with_this_name__";
                                Require(catalog.Items.Count == 0 && !((Button)dialog.FindName("AddItemButton")).IsEnabled, "Search left an unmatched item actionable");
                                search.Clear();
                                Require(catalog.Items.Count == itemCount, "Clearing search did not restore all items");
                                var choice = catalog.Items.Cast<object>().First();
                                var granted = (int)choice.GetType().GetProperty("ObjectId")!.GetValue(choice)!;
                                var sourceRoom = session.CaptureInventoryStatus().Catalog.Single(c => c.ObjectId == granted).SourceRoomId;
                                catalog.SelectedItem = choice; recipients.SelectedValue = targetActor;
                                ((Button)dialog.FindName("AddItemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(((Button)dialog.FindName("ApplyButton")).IsEnabled, "Pending grant cannot be applied");
                                Save("status-pending-grant", dialog);
                                ((Button)dialog.FindName("ApplyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                var grant = session.CaptureInventoryStatus().Items.Single(c => c.ObjectId == granted);
                                Require(grant.OwnerId == targetActor && grant.RetainedRoomId == sourceRoom, "Grant did not preserve native owner/source room");
                                Save("status-granted", dialog);
                                ((Button)dialog.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                Require(!session.CaptureInventoryStatus().Items.Any(c => c.ObjectId == granted) && Unrelated() == beforeState, "Grant undo did not restore exact state");
                                report.Add($"Catalog grants: {itemCount} available; object {granted} -> actor {targetActor}, source room {sourceRoom}; Apply/Undo pass");
                                var free = session.CaptureInventoryStatus().FreeSlots;
                                var pending = 0;
                                while (((Button)dialog.FindName("AddItemButton")).IsEnabled && pending <= free)
                                {
                                    ((Button)dialog.FindName("AddItemButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                    pending++;
                                }
                                Require(pending <= free, "The UI queued beyond native capacity");
                                Require(!((Button)dialog.FindName("AddItemButton")).IsEnabled, "Catalog/capacity limit was not enforced");
                                Save("status-pending-capacity", dialog);
                                report.Add($"Pending capacity: {pending}/{free}; further Add disabled; Close discards batch");
                                Selector().SelectedValue = targetActor;
                            }
                            ((Button)dialog.FindName("CancelButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        }
                        catch (Exception ex) { failure = ex; if (dialog?.IsVisible == true) Save("status-failed", dialog); }
                        finally { if (dialog?.IsVisible == true) dialog.Close(); }
                    }));
                    Call("GameStatus_Click", header, new RoutedEventArgs(Button.ClickEvent));
                    if (failure is not null) throw failure;
                    Require(timer.IsEnabled && (bool)Field("_playPaused")! == paused, "Previous timer/pause state was not restored");
                    timer.Stop();
                    Require((int)Field("_playFrame")! == frame, "Opening/closing status advanced the session");
                    Require(session.Snapshot.ObjectOwners[held.X] == held.Value && Unrelated() == beforeState && session.Snapshot.Inventory.SequenceEqual(originalInventory), "Close changed game state");
                    report.Add($"Status modal paused={paused}: Apply/Undo={roundTrip}; Cancel retained owner={held.Value}; frame={frame}; timer restored");
                    File.WriteAllLines(Path.Combine(output, "progress.txt"), report);
                }
                DialogCheck(roundTrip: true, paused: false);
                DialogCheck(roundTrip: false, paused: true);
                Save("status-returned");
                report.Add("PASS");
            }
            catch (Exception ex) { report.Add(ex.ToString()); Environment.ExitCode = 1; }
            finally
            {
                File.WriteAllLines(Path.Combine(output, "play-check.txt"), report);
                if (session is not null) File.WriteAllLines(Path.Combine(output, "session-trace.txt"), session.SessionTrace);
                window.Close(); app.Shutdown();
            }
        };
        app.Run(window);
    }
    private sealed record ActionStep(string Op, int X = 0, int Y = 0, int Count = 0, int Value = 0, string? Text = null);
}
