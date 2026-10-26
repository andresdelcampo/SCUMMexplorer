using System.Windows;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.App;

/// <summary>
/// R-6.43: LOOM's Advanced panel (`Room entry`, `Last action`, `Assumptions`) reaches the screen the way every other game's does: this builds its
/// <see cref="RoomPanelData"/> from the exploration session and <c>FillRoomPanel</c> draws it. LOOM's richer account (the session's interpretation of the entry, the
/// events it caused, the action timeline, the grouped assumptions) travels in the optional fields of the data.
/// </summary>
public partial class MainWindow
{
    private sealed record LoomPanelEntry(ScummScriptDisassembly? Script, IReadOnlyList<ScummScriptEvent> Events, string? Summary, string Text);

    private LoomPanelEntry? _loomPanelEntry;

    private RoomPanelData? BuildLoomPanel()
    {
        if (_currentRoomResource?.ResourceId is not int roomId) return null;
        var interactive = SessionModeToggle.IsChecked == true && _session is not null;
        var entry = _loomPanelEntry;
        RoomPanelAction? last = null;
        if (IsAdvancedMode && _session?.LastAction is { } action && (action.SourceRoomId == roomId || action.DestinationRoomId == roomId))
        {
            var actionName = _currentRoomObjectRecords.FirstOrDefault(record => record.Id == action.ObjectId) is { } actionObject &&
                             _roomObjectNames.TryGetValue(actionObject.Id, out var storedName)
                ? storedName : "Something in the scene";
            var summary = action.DestinationRoomId is int
                ? action.IsGameInput
                    ? $"{actionName}: a new scene opened. The game walk remains a preview."
                    : $"{actionName}: a new scene opened in this state preview."
                : action.VerbId == 0 ? $"{actionName}: ran; no scene change followed."
                : $"{actionName} · {LoomVerbNames.Label(action.VerbId)}: ran; no scene change followed.";
            var timeline = LoomActionTimeline.Format(LoomActionTimeline.Build(action, new LoomScriptPresenter(_names.Glossary, _names.VerbName), actionName,
                action.VerbId == 0 ? null : LoomVerbNames.Label(action.VerbId)));
            last = new RoomPanelAction(actionName, summary, action.Evidence, action.Uncertainties, action.Script)
            {
                Timeline = timeline.TrimEnd(),
                RawTrace = action.Evidence + Environment.NewLine +
                           string.Join(Environment.NewLine, action.Uncertainties.Select(item => "Unresolved: " + item)) +
                           Environment.NewLine + (action.Script is null ? "Object bytecode unavailable." : FormatScript(action.Script)) +
                           Environment.NewLine + (action.Run?.FormatSummary() ?? "No object bytecode was run.")
            };
        }

        var groups = new List<RoomAssumptionGroup>();
        if (interactive)
        {
            var issues = new List<string>();
            if (_estimatedObjectIds.Count > 0) issues.Add($"The artwork of {_estimatedObjectIds.Count} object(s) is estimated.");
            // The session logs everything it assumed since it began; this view lists what came after the last room entry.
            var log = _session!.Snapshot.Uncertainties;
            issues.AddRange(log.Skip(Math.Min(_issueBaseline, log.Count)).Distinct().TakeLast(30));
            foreach (var group in LoomAssumptionGroups.Group(issues))
                groups.Add(new(group.Title, group.Meaning, [.. group.Items.Select(issue =>
                    (_names.NameBits(_names.NameScripts(issue)), issue))]));
        }
        return new RoomPanelData(roomId, entry?.Script, null, [], last, [])
        {
            EntrySummary = entry?.Summary,
            EntryEvents = entry?.Events,
            EntryText = entry?.Text,
            AssumptionGroups = groups,
            HasAssumptions = interactive
        };
    }

    /// <summary>Draws LOOM's panel from the session's present state.</summary>
    private void RefreshLoomPanel() => FillRoomPanel(BuildLoomPanel());
}
