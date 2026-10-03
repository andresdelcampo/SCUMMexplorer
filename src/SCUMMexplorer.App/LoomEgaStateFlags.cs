using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.App;

/// <summary>
/// The LOOM EGA specific part of Game Status' story list: which flags exist (the story bits the game's code sets, D72 and up,
/// found by <see cref="LoomStoryEvents"/>) and what they are called (the catalog scene that sets them). The window itself only
/// shows generic <see cref="StateFlag"/>s, so another edition of the game (for example a demo) supplies its own source.
/// </summary>
internal static class LoomEgaStateFlags
{
    public static GameStatusWindow.FlagSource Create(Func<IEnumerable<ReplayRecipe>> recipes) => new(
        session => session.StoryEvents
            .Select(e => (Flag: new StateFlag(e.Key, Label(e, recipes()), Detail(e)), Named: e.Scenes.Any(s => SceneTitle(s, recipes()) is not null)))
            .OrderBy(t => t.Named ? 0 : 1).ThenBy(t => t.Flag.Label, StringComparer.OrdinalIgnoreCase).Select(t => t.Flag).ToList(),
        (session, key) => session.StoryBit(key), (session, key, value) => session.UpdateStoryBit(key, value),
        "The game remembers what happened in story bits. Check one to mark that scene or conversation as done, as the game would after it ran; clear it to undo. " +
        "A room already on screen shows the change when it is entered again. These are your choices, not played scenes.");

    private static string? SceneTitle(LoomStateSource setter, IEnumerable<ReplayRecipe> recipes)
    {
        if (setter.ObjectId is int obj && setter.VerbId is int verb)
            return recipes.FirstOrDefault(r => r.Steps is null && r.Room == setter.Room && r.ObjectId == obj && r.Verb == verb)?.Title;
        if (setter.Where.StartsWith("Script ", StringComparison.Ordinal) && int.TryParse(setter.Where.AsSpan(7, 3), out var script))
            return recipes.FirstOrDefault(r => r.ScriptIds.Contains(script))?.Title;
        return null;
    }

    private static string Label(LoomStoryEvent e, IEnumerable<ReplayRecipe> recipes)
    {
        var titles = e.Scenes.Select(s => SceneTitle(s, recipes)).Where(t => t is not null).Distinct().Take(2).ToList();
        if (titles.Count > 0) return string.Join(" / ", titles) + (e.Scenes.Select(s => s.Where.Split(" +")[0]).Distinct().Count() > titles.Count ? " ..." : "");
        var first = e.Scenes.FirstOrDefault() ?? e.Setters[0];
        return first.Where.Split(" +")[0] + " ran";
    }

    private static string Detail(LoomStoryEvent e) =>
        e.Key + " is set by: " + string.Join("; ", e.Setters.Select(s => s.Where).Take(6)) + (e.Setters.Count > 6 ? "; " + (e.Setters.Count - 6) + " more" : "");
}
