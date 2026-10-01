using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SCUMMexplorer.App;

public partial class CutscenePickerWindow : Window
{
    private IReadOnlyList<CutscenePickerItem> _items = [];
    internal CutscenePickerWindow(IReadOnlyList<CutscenePickerItem> items, string summary)
    {
        InitializeComponent(); _items = items; CoverageSummary.Text = summary; Refresh();
    }
    internal Action? SelectedPlay { get; private set; }
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => Refresh();
    private void Search_Changed(object sender, TextChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        if (SceneList is null || SearchBox is null || CategoryFilter is null) return;
        var text = SearchBox.Text.Trim();
        SceneList.ItemsSource = _items.Where(i => CategoryFilter.SelectedIndex switch
        {
            0 => i.Play is not null && i.Category == "Scenes",
            1 => i.Play is not null && i.Category == "Dialogue and reactions",
            2 => i.Play is not null,
            _ => i.Play is null
        }).Where(i => text.Length == 0 || (i.Title + " " + i.Note).Contains(text, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (SceneList.Items.Count > 0) SceneList.SelectedIndex = 0;
        else { PlayButton.IsEnabled = false; SelectionNote.Text = "No matching entries."; SourceDetails.Text = ""; }
    }
    private void SceneList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = SceneList.SelectedItem as CutscenePickerItem;
        PlayButton.IsEnabled = item?.Play is not null;
        SelectionNote.Text = item?.Play is null ? item?.Status ?? "Choose a scene." :
            "Validated explorer preview from an assumed arrival. Direct draft responses do not imply the draft was played. Prior game choices are not reconstructed.";
        SourceDetails.Text = item?.Note ?? "";
    }
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (SceneList.SelectedItem is not CutscenePickerItem { Play: not null } item) return;
        SelectedPlay = item.Play; DialogResult = true;
    }
    private void SceneList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(SceneList, source) is ListBoxItem)
            Play_Click(sender, e);
    }
}
