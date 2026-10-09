using System.Text;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// The last action as a short story: what was done, which scripts started, who said what, where the game went. Built from the
/// events the bounded run recorded, in the order it recorded them; a step that comes from a branch the run did not take is marked
/// "maybe". The raw trace stays the evidence.
/// </summary>
public static class LoomActionTimeline
{
    public static IReadOnlyList<string> Build(LoomObjectActionResult action, LoomScriptPresenter presenter, string objectName, string? verbName)
    {
        var steps = new List<string>();
        var what = verbName is null ? "" : $" → {verbName}";
        steps.Add(action.IsGameInput
            ? $"You clicked {objectName}{what} (sent through the game's input scripts)"
            : $"{objectName}{what} was run directly (an exploration action: no click, no walk)");

        string? lastSource = null;
        var events = action.Run?.Events ?? [];
        var shown = 0;
        foreach (var scriptEvent in events)
        {
            if (scriptEvent.Kind is ScummScriptEventKind.ScriptsFrozen or ScummScriptEventKind.ObjectStateChanged) continue;
            if (shown++ >= 60) break;
            var phrase = presenter.Describe(scriptEvent);
            var prefix = scriptEvent.Certainty == ScummScriptEventCertainty.Reached ? "" : "maybe: ";
            var source = scriptEvent.SourceLabel == lastSource ? "" : $"  [{scriptEvent.SourceLabel}]";
            lastSource = scriptEvent.SourceLabel;
            steps.Add(prefix + phrase + source);
        }
        if (events.Count > 0 && shown > 60) steps.Add($"… and {events.Count - 60} more events");

        steps.Add(action.DestinationRoomId is int room
            ? $"Result: the game went to Room {room:00}"
            : "Result: no scene change followed");
        return steps;
    }

    public static string Format(IReadOnlyList<string> steps)
    {
        var text = new StringBuilder();
        for (var i = 0; i < steps.Count; i++) text.AppendLine($"{i + 1,2}. {steps[i]}");
        return text.ToString();
    }
}
