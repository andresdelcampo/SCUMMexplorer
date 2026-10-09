using System.Buffers.Binary;
using System.Net;
using System.Text;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

if (args.Length is < 1 or > 2)
{
    Console.Error.WriteLine("Usage: Indy3ConversationExport <EGA game folder> [output.html]");
    return 2;
}

var game = new GameFolderScanner().Scan(args[0]);
if (game.Id != GameId.IndianaJones3 || game.Version != ScummVersion.V3)
    throw new InvalidDataException("The supplied folder is not a supported Indiana Jones and the Last Crusade EGA v3 folder.");

var extraction = new LoomTextExtractor().Extract(game);
var roomResolver = new ConversationRoomResolver(game);
var classifiedGroups = extraction.Entries
    .Where(entry => entry.ConversationKind == LoomTextExtractor.ChoiceConversationKind && entry.ConversationId is not null)
    .GroupBy(entry => entry.ConversationId!, StringComparer.Ordinal)
    .Select(group => BuildConversation(group, roomResolver, game)).ToArray();
var groups = classifiedGroups
    .Where(item => item.ExclusionReason is null)
    .OrderBy(item => item.RoomIds.FirstOrDefault(int.MaxValue))
    .ThenBy(item => item.Source, StringComparer.Ordinal)
    .ThenBy(item => item.StartOffset)
    .ToArray();
groups = ExpandConditionalOptions(groups, extraction, game);
groups = LinkConversationMenus(groups, game);
groups = TraceBranchReplies(groups, extraction, game);
groups = TraceOpeningLines(groups, extraction, game);

if (groups.Length == 0) throw new InvalidDataException("No multiple-choice dialogue sets were found.");

var outputPath = Path.GetFullPath(args.Length == 2 ? args[1] : Path.Combine(game.FolderPath, "..", "indy3-conversations.html"));
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
var roomImages = new Dictionary<int, string>();
var roomById = game.Rooms.Where(room => room.ResourceId.HasValue).ToDictionary(room => room.ResourceId!.Value);
var decoder = new ScummV3EgaRoomDecoder();
foreach (var roomId in groups.SelectMany(item => item.RoomIds).Distinct())
{
    // Room 32's supplied EGA bitmap is an all-black placeholder, not usable scene art.
    if (roomId == 32) continue;
    if (!roomById.TryGetValue(roomId, out var room)) continue;
    try { roomImages[roomId] = ToBmpDataUri(decoder.Decode(File.ReadAllBytes(room.FilePath))); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
    {
        Console.Error.WriteLine($"Room {roomId:00} artwork omitted: {ex.Message}");
    }
}

File.WriteAllText(outputPath, RenderHtml(groups, roomImages, extraction), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
var graphPath = Path.ChangeExtension(outputPath, ".graph.json");
var graph = new
{
    format = "SCUMMexplorer Indy3 EGA conversation graph v1",
    source = Path.GetFullPath(args[0]),
    included = groups,
    excluded = classifiedGroups.Where(group => group.ExclusionReason is not null)
        .Select(group => new { group.Id, group.ScriptId, group.Source, group.StartOffset, reason = group.ExclusionReason }),
    diagnostics = extraction.Diagnostics
};
File.WriteAllText(graphPath, System.Text.Json.JsonSerializer.Serialize(graph,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
Console.WriteLine($"Wrote {outputPath}");
Console.WriteLine($"Wrote graph audit {graphPath}");
Console.WriteLine($"Choice sets: {groups.Length}; choices: {groups.Sum(group => group.Options.Count)}; room images: {roomImages.Count}; excluded sets: {classifiedGroups.Count(group => group.ExclusionReason is not null)}; extractor diagnostics: {extraction.Diagnostics.Count}");
return 0;

static Conversation BuildConversation(IGrouping<string, LoomTextEntry> group, ConversationRoomResolver roomResolver, GameLibrary game)
{
    var rows = group.ToArray();
    var choices = rows.Where(entry => entry.IsPlayerChoice && entry.ChoiceVerbId.HasValue)
        .GroupBy(entry => entry.ChoiceVerbId!.Value)
        .Select(items =>
        {
            var label = items.OrderBy(entry => entry.ConversationLine).First();
            var replies = rows.Where(entry => entry.Kind == "Spoken line" && entry.ChoiceVerbId == label.ChoiceVerbId)
                .OrderBy(entry => entry.ConversationLine).ToArray();
            return new Option(label.Text, label.ChoiceVerbId!.Value, label.ConversationLine.GetValueOrDefault(),
                label.CodeOffset, label.Source, replies.Select(reply => new Reply(reply.Text, reply.Speaker,
                    reply.CodeOffset, reply.Source)).ToArray(), [], []);
        })
        .OrderBy(option => option.Order)
        .ToArray();
    var scriptId = rows.Select(entry => entry.ScriptId).FirstOrDefault(value => value.HasValue);
    var roomContexts = scriptId is int indexedScript
        ? rows.Where(entry => entry.IsPlayerChoice).SelectMany(entry => roomResolver.At(indexedScript, entry.CodeOffset))
        : [];
    var roomEvidence = roomContexts.GroupBy(context => context.RoomId).ToDictionary(items => items.Key,
        items => items.Select(item => item.Evidence).Distinct(StringComparer.Ordinal).ToArray());
    foreach (var directRoom in rows.Where(entry => entry.RoomId.HasValue).Select(entry => entry.RoomId!.Value).Distinct())
        roomEvidence.TryAdd(directRoom, [$"Choice is stored in Room {directRoom:00} script data."]);
    if (roomEvidence.Count == 0 && scriptId == 86 &&
        game.Resources.FirstOrDefault(resource => resource.Kind == ResourceKind.Script && resource.ResourceId == scriptId)?.ContainerFileNumber is int storedWithRoom)
        roomEvidence[storedWithRoom] = [$"Stored alongside Room {storedWithRoom:00}; actual play room and activation are unverified."];
    var source = rows.Select(entry => entry.Source).FirstOrDefault() ?? "Unknown source";
    var offset = rows.Min(entry => entry.CodeOffset);
    var roomIds = roomEvidence.Keys.ToArray(); // resolver order keeps the normal path first when several rooms are possible
    var exclusionReason = DialogueMenuExclusionReason(choices, rows, game, scriptId);
    if (exclusionReason is null && choices.Length < 2)
        exclusionReason = "The decoded menu has fewer than two literal option labels.";
    return new Conversation(group.Key, roomIds, roomEvidence, scriptId, source, offset, choices, exclusionReason,
        NoVerifiedScene: scriptId == 86 && roomEvidence.Keys.All(roomId => roomId == 32), Prelude: []);
}

static string? DialogueMenuExclusionReason(IReadOnlyList<Option> options, IReadOnlyList<LoomTextEntry> rows,
    GameLibrary game, int? scriptId)
{
    var speaks = rows.Any(entry => entry.Kind == "Spoken line");
    if (options.Count > 0 && options.All(option => IsGenericActionLabel(option.Text)) && !speaks)
        return "Only generic object/action selectors are present; no dialogue is spoken.";
    var disassembly = ReadSourceDisassembly(rows, scriptId, game);
    if (!speaks && disassembly?.Instructions.Any(item => item.Operation is "loadRoom" or "loadRoomWithEgo") == true)
        return "The decoded menu flow selects room transitions; this is a travel destination picker.";
    if (!speaks && IsIndirectTravelPicker(disassembly))
        return "The room entry assigns a destination code to the shared travel variable and starts the travel UI dispatcher; this is a travel destination picker.";
    if (!speaks && options.Count >= 10 && options.All(option => option.Text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 2))
        return "This is a global command verb list, with no spoken dialogue or sentence-like choices.";
    if (!speaks && disassembly?.Instructions.Count(item => item.Operation == "verbOps" && item.Operands.Contains("new", StringComparison.OrdinalIgnoreCase)) >= 5)
        return "This script builds a multi-slot command verb bar; its labels are interface actions, not dialogue choices.";
    if (!speaks && disassembly?.Instructions.Any(item => item.Operation is "doSentence" or "pickupObject") == true)
        return "The decoded branches dispatch object actions without a spoken conversation.";
    return null;
}

static bool IsIndirectTravelPicker(ScummScriptDisassembly? disassembly)
{
    if (disassembly is null) return false;
    var setsDestination = disassembly.Instructions.Any(item => item.Operation == "move" &&
        System.Text.RegularExpressions.Regex.IsMatch(item.Operands, @"^var\[0x033\]\s*=\s*0x00(?:C8|CC)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    var startsTravelUi = disassembly.Instructions.Any(item => item.Operation == "startScript" &&
        System.Text.RegularExpressions.Regex.IsMatch(item.Operands, @"^0x0*15,", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    return setsDestination && startsTravelUi;
}

static ScummScriptDisassembly? ReadSourceDisassembly(IReadOnlyList<LoomTextEntry> rows, int? scriptId, GameLibrary game)
{
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    try
    {
        if (scriptId is int indexed)
        {
            var resource = game.Resources.FirstOrDefault(item => item.Kind == ResourceKind.Script && item.ResourceId == indexed);
            return resource is null ? null : reader.Decode(resource);
        }
        var roomId = rows.Select(entry => entry.RoomId).FirstOrDefault(value => value.HasValue);
        var room = roomId is int value ? game.Rooms.FirstOrDefault(item => item.ResourceId == value) : null;
        if (room is null) return null;
        var source = rows.Select(entry => entry.Source).FirstOrDefault() ?? "";
        if (source.Contains("entry script", StringComparison.OrdinalIgnoreCase)) return reader.DecodeRoomEntry(room);
        if (source.Contains("exit script", StringComparison.OrdinalIgnoreCase)) return reader.DecodeRoomExit(room);
        var local = System.Text.RegularExpressions.Regex.Match(source, @"Local script\s+(?<id>\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (local.Success)
        {
            var localId = int.Parse(local.Groups["id"].Value, System.Globalization.CultureInfo.InvariantCulture);
            var globalCount = game.Resources.Count(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue);
            return reader.DecodeRoomLocalScript(room, localId, globalCount);
        }
        return null;
    }
    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return null; }
}

static bool IsGenericActionLabel(string label)
{
    var normalized = System.Text.RegularExpressions.Regex.Replace(label.Trim().ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ").Trim();
    return normalized is "ofrece un objeto" or "da un punetazo" or "da un puñetazo" or
        "offer object" or "offer an object" or "give an object" or "punch" or "hit" or
        "open" or "close" or "look at" or "pick up" or "give" or "use" or "push" or "pull" or "walk to" or "talk to" or
        "abrir" or "coger" or "dar" or "usar" or "empujar" or "tirar" or "hablar con" or "mirar" or "recoger";
}

static Conversation[] LinkConversationMenus(Conversation[] groups, GameLibrary game)
{
    var scripts = new Dictionary<int, ScummScriptDisassembly>();
    var resources = game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
        .ToDictionary(item => item.ResourceId!.Value);
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
    {
        try { scripts[resource.ResourceId!.Value] = reader.Decode(resource); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
    }
    var groupsByScript = groups.Where(group => group.ScriptId.HasValue).GroupBy(group => group.ScriptId!.Value)
        .ToDictionary(group => group.Key, group => group.OrderBy(item => item.StartOffset).ToArray());
    var links = groups.ToDictionary(group => group.Id, _ => new List<ConversationLink>(), StringComparer.Ordinal);
    foreach (var (scriptId, scriptGroups) in groupsByScript)
    {
        if (!scripts.TryGetValue(scriptId, out var script)) continue;
        foreach (var group in scriptGroups)
        {
            var startIndex = IndexOf(script.Instructions, group.StartOffset);
            if (startIndex < 0) continue;
            var runEnd = startIndex;
            while (runEnd + 1 < script.Instructions.Count && script.Instructions[runEnd + 1].Operation == "verbOps") runEnd++;
            var nextMenuOffset = scriptGroups.Where(item => item.StartOffset > group.StartOffset)
                .Select(item => item.StartOffset).DefaultIfEmpty(int.MaxValue).Min();
            var ids = group.Options.Select(option => option.VerbId).ToHashSet();
            string? selectionVariable = null;
            var ranges = new List<(int VerbId, int From, int To)>();
            foreach (var test in script.Instructions.Skip(runEnd + 1))
            {
                if (test.Operation != "isEqual") continue;
                var match = System.Text.RegularExpressions.Regex.Match(test.Operands,
                    @"^(?<var>[a-z]+\[[^\]]+\]),\s*0x(?<value>[0-9A-Fa-f]+),.*->\s*0x(?<target>[0-9A-Fa-f]+)");
                if (!match.Success) continue;
                var verb = int.Parse(match.Groups["value"].Value, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture);
                if (!ids.Contains(verb)) continue;
                selectionVariable ??= match.Groups["var"].Value;
                if (selectionVariable != match.Groups["var"].Value) continue;
                // Every choice enters the same dispatcher. A matching handler
                // may be stored beyond later menus, while their handlers must
                // never be seeded as if they belonged to this menu.
                foreach (var option in group.Options) ranges.Add((option.VerbId, test.Offset, 0));
                break;
            }
            foreach (var range in ranges)
            {
                if (selectionVariable is null) continue;
                var offsetMap = script.Instructions.Select((instruction, index) => (instruction.Offset, index))
                    .ToDictionary(item => item.Offset, item => item.index);
                var menuAt = scriptGroups.ToDictionary(item => item.StartOffset, item => item);
                var pending = new Stack<(int Index, bool ConditionalPath, bool SelectionKnown)>();
                var visited = new HashSet<(int Index, bool ConditionalPath, bool SelectionKnown)>();
                if (offsetMap.TryGetValue(range.From, out var branchStart)) pending.Push((branchStart, false, true));
                while (pending.Count > 0)
                {
                    var state = pending.Pop();
                    var index = state.Index;
                    if (index < 0 || index >= script.Instructions.Count || !visited.Add(state)) continue;
                    var instruction = script.Instructions[index];
                    if (menuAt.TryGetValue(instruction.Offset, out var nested))
                    {
                        links[group.Id].Add(new ConversationLink(range.VerbId, nested.Id,
                            !state.SelectionKnown
                                ? "The selection slot was overwritten; this is a possible branch from a later item/action choice."
                                : state.ConditionalPath
                                ? "Reachable through decoded jumps and one or more unevaluated conditions from this selection."
                                : "Reachable through decoded jumps from this selection.", "branch"));
                        continue;
                    }
                    if (instruction.Operation == "stopObjectCode") continue;
                    if (instruction.Operation is "startScript" or "chainScript")
                    {
                        var call = System.Text.RegularExpressions.Regex.Match(instruction.Operands, @"^0x(?<id>[0-9A-Fa-f]+),");
                        if (call.Success && int.TryParse(call.Groups["id"].Value, System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out var targetScript) &&
                            groupsByScript.TryGetValue(targetScript, out var calledMenus) && calledMenus.Length > 0)
                            foreach (var called in FirstMenus(targetScript))
                                links[group.Id].Add(new ConversationLink(range.VerbId, called.Id,
                                    $"Literal {instruction.Operation} call to Script {targetScript:00} at +0x{instruction.Offset:X4}; " +
                                    (state.ConditionalPath ? "call path includes an unevaluated condition." : "the call's runtime arguments are not evaluated."), "call"));
                        if (instruction.Operation == "chainScript") continue;
                    }
                    var nextIndex = index + 1;
                    if (instruction.Operation == "jumpRelative")
                    {
                        if (TryBranchTarget(instruction, out var target) && offsetMap.TryGetValue(target, out var jumpIndex)) pending.Push((jumpIndex, state.ConditionalPath, state.SelectionKnown));
                        continue;
                    }
                    if (instruction.Operation == "beginOverride")
                    {
                        var normalIndex = nextIndex < script.Instructions.Count && script.Instructions[nextIndex].Operation == "jumpRelative"
                            ? nextIndex + 1 : nextIndex;
                        if (normalIndex < script.Instructions.Count) pending.Push((normalIndex, state.ConditionalPath, state.SelectionKnown));
                        if (resources.TryGetValue(scriptId, out var overrideResource) &&
                            TryOverrideTarget(overrideResource, instruction.Offset, out var overrideTarget) &&
                            offsetMap.TryGetValue(overrideTarget, out var overrideIndex)) pending.Push((overrideIndex, true, state.SelectionKnown));
                        continue;
                    }
                    if (instruction.Operation == "isEqual" && TryEquality(instruction, out var variable, out var value, out var equalTarget) &&
                        variable == selectionVariable)
                    {
                        if (!state.SelectionKnown)
                        {
                            if (nextIndex < script.Instructions.Count) pending.Push((nextIndex, true, false));
                            if (offsetMap.TryGetValue(equalTarget, out var uncertainIndex)) pending.Push((uncertainIndex, true, false));
                        }
                        else if (value == range.VerbId)
                        {
                            if (nextIndex < script.Instructions.Count) pending.Push((nextIndex, state.ConditionalPath, true));
                        }
                        else if (offsetMap.TryGetValue(equalTarget, out var notSelectedIndex)) pending.Push((notSelectedIndex, state.ConditionalPath, true));
                        continue;
                    }
                    var selectionKnown = state.SelectionKnown && !WritesVariable(instruction, selectionVariable);
                    if (IsConditional(instruction.Operation) && TryBranchTarget(instruction, out var branchTarget) &&
                        offsetMap.TryGetValue(branchTarget, out var branchIndex)) pending.Push((branchIndex, true, selectionKnown));
                    if (nextIndex < script.Instructions.Count) pending.Push((nextIndex, state.ConditionalPath || IsConditional(instruction.Operation) || instruction.Operation == "beginOverride", selectionKnown));
                }
            }
        }
    }
    return groups.Select(group => group with
    {
        Options = group.Options.Select(option => option with
        {
            NextMenus = links[group.Id].Where(link => link.VerbId == option.VerbId)
                .DistinctBy(link => (link.TargetId, link.Kind)).ToArray()
        }).ToArray()
    }).ToArray();

    static int IndexOf(IReadOnlyList<ScummScriptInstruction> instructions, int offset)
    {
        for (var index = 0; index < instructions.Count; index++)
            if (instructions[index].Offset == offset) return index;
        return -1;
    }

    IEnumerable<Conversation> FirstMenus(int targetScript)
    {
        if (!groupsByScript.TryGetValue(targetScript, out var targetGroups) || !scripts.TryGetValue(targetScript, out var targetData))
            return [];
        var byOffset = targetData.Instructions.Select((instruction, index) => (instruction.Offset, index))
            .ToDictionary(item => item.Offset, item => item.index);
        var menuOffsets = targetGroups.ToDictionary(item => item.StartOffset, item => item);
        var result = new Dictionary<string, Conversation>(StringComparer.Ordinal);
        var pending = new Stack<int>();
        var visited = new HashSet<int>();
        if (targetData.Instructions.Count > 0) pending.Push(0);
        while (pending.Count > 0)
        {
            var index = pending.Pop();
            if (index < 0 || index >= targetData.Instructions.Count || !visited.Add(index)) continue;
            var instruction = targetData.Instructions[index];
            if (menuOffsets.TryGetValue(instruction.Offset, out var menu)) { result[menu.Id] = menu; continue; }
            if (instruction.Operation == "stopObjectCode") continue;
            var next = index + 1;
            if (instruction.Operation == "jumpRelative")
            {
                if (TryBranchTarget(instruction, out var target) && byOffset.TryGetValue(target, out var jump)) pending.Push(jump);
                continue;
            }
            if (instruction.Operation == "beginOverride")
            {
                var normalIndex = next < targetData.Instructions.Count && targetData.Instructions[next].Operation == "jumpRelative"
                    ? next + 1 : next;
                if (normalIndex < targetData.Instructions.Count) pending.Push(normalIndex);
                if (resources.TryGetValue(targetScript, out var overrideResource) &&
                    TryOverrideTarget(overrideResource, instruction.Offset, out var overrideTarget) &&
                    byOffset.TryGetValue(overrideTarget, out var overrideIndex)) pending.Push(overrideIndex);
                continue;
            }
            if (IsConditional(instruction.Operation) && TryBranchTarget(instruction, out var branch) && byOffset.TryGetValue(branch, out var taken)) pending.Push(taken);
            if (next < targetData.Instructions.Count) pending.Push(next);
        }
        return result.Values.OrderBy(item => item.StartOffset).ToArray();
    }

    static bool TryOverrideTarget(GameResource resource, int offset, out int target)
    {
        target = 0;
        if (resource.Offset is not int resourceOffset) return false;
        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
        var jumpOffset = resourceOffset + 4 + offset + 2;
        if (jumpOffset < 0 || jumpOffset > data.Length - 3 || data[jumpOffset] != 0x18) return false;
        var displacement = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(jumpOffset + 1, 2));
        target = offset + 5 + displacement;
        return true;
    }

    static bool IsConditional(string operation) => operation is "isGreaterEqual" or "isNotEqual" or "isLessEqual" or
        "isLess" or "isEqual" or "isGreater" or "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState";
    static bool TryBranchTarget(ScummScriptInstruction instruction, out int target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(instruction.Operands, @"->\s*0x(?<target>[0-9A-Fa-f]+)");
        return match.Success && int.TryParse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out target) || (target = 0) != 0;
    }
    static bool TryEquality(ScummScriptInstruction instruction, out string variable, out int value, out int target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(instruction.Operands,
            @"^(?<var>[a-z]+\[[^\]]+\]),\s*0x(?<value>[0-9A-Fa-f]+),.*->\s*0x(?<target>[0-9A-Fa-f]+)");
        variable = match.Groups["var"].Value; value = target = 0;
        return match.Success && int.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out value) &&
               int.TryParse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out target);
    }
    static bool WritesVariable(ScummScriptInstruction instruction, string variable) =>
        System.Text.RegularExpressions.Regex.IsMatch(instruction.Operands,
            "^" + System.Text.RegularExpressions.Regex.Escape(variable) + @"\s*(?:=|\+=|-=|\+\+|--)" );
}

static Conversation[] TraceBranchReplies(Conversation[] groups, LoomTextExtraction extraction, GameLibrary game)
{
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    var resources = game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
        .ToDictionary(item => item.ResourceId!.Value);
    var updated = new List<Conversation>(groups.Length);
    foreach (var group in groups)
    {
        if (group.ScriptId is not int scriptId || !resources.TryGetValue(scriptId, out var resource)) { updated.Add(group); continue; }
        ScummScriptDisassembly script;
        try { script = reader.Decode(resource); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
        { updated.Add(group); continue; }
        var instructions = script.Instructions;
        var offsets = instructions.Select((instruction, index) => (instruction.Offset, index)).ToDictionary(item => item.Offset, item => item.index);
        var menuGroups = groups.Where(item => item.ScriptId == scriptId).ToArray();
        var nextMenuOffset = menuGroups.Where(item => item.StartOffset > group.StartOffset).Select(item => item.StartOffset).DefaultIfEmpty(int.MaxValue).Min();
        var startIndex = Array.FindIndex(instructions.ToArray(), instruction => instruction.Offset == group.StartOffset);
        if (startIndex < 0) { updated.Add(group); continue; }
        var runEnd = startIndex;
        while (runEnd + 1 < instructions.Count && instructions[runEnd + 1].Operation == "verbOps") runEnd++;
        var tests = new List<(string Variable, int Verb, int From, int To)>();
        string? selection = null;
        foreach (var test in instructions.Skip(runEnd + 1).Where(item => item.Operation == "isEqual"))
        {
            if (!TryEquality(test, out var variable, out var verb, out var target) || !group.Options.Any(option => option.VerbId == verb)) continue;
            selection ??= variable;
            if (selection != variable) continue;
            foreach (var option in group.Options) tests.Add((variable, option.VerbId, test.Offset, target));
            break;
        }
        if (selection is null || tests.Count == 0) { updated.Add(group); continue; }
        var entriesByOffset = extraction.Entries.Where(entry => entry.ScriptId == scriptId && entry.Kind == "Spoken line")
            .GroupBy(entry => entry.CodeOffset).ToDictionary(items => items.Key, items => items.ToArray());
        var menuAt = menuGroups.ToDictionary(item => item.StartOffset, item => item);
        var tracedOptions = new List<Option>();
        foreach (var option in group.Options)
        {
            var replies = new Dictionary<int, Reply>();
            foreach (var branch in tests.Where(test => test.Verb == option.VerbId))
            {
                var pending = new Stack<(int Index, string Path, bool SelectionKnown)>();
                var visited = new HashSet<(int Index, string Path, bool SelectionKnown)>();
                if (offsets.TryGetValue(branch.From, out var fromIndex)) pending.Push((fromIndex, "", true));
                var work = 0;
                while (pending.Count > 0 && work++ < 12000)
                {
                    var state = pending.Pop();
                    if (state.Index < 0 || state.Index >= instructions.Count || !visited.Add(state)) continue;
                    var instruction = instructions[state.Index];
                    if (menuAt.ContainsKey(instruction.Offset)) continue;
                    if (entriesByOffset.TryGetValue(instruction.Offset, out var spoken))
                        foreach (var entry in spoken)
                        {
                            var path = state.Path.Length == 0 ? "Direct decoded path" : state.Path;
                            replies.TryAdd(entry.CodeOffset, new Reply(entry.Text, entry.Speaker, entry.CodeOffset, entry.Source, path));
                        }
                    if (instruction.Operation == "stopObjectCode" || instruction.Operation == "chainScript") continue;
                    var next = state.Index + 1;
                    if (instruction.Operation == "jumpRelative")
                    {
                        if (TryTarget(instruction, out var target) && offsets.TryGetValue(target, out var jumpIndex)) pending.Push((jumpIndex, state.Path, state.SelectionKnown));
                        continue;
                    }
                    if (instruction.Operation == "beginOverride")
                    {
                        var normalIndex = next < instructions.Count && instructions[next].Operation == "jumpRelative" ? next + 1 : next;
                        if (normalIndex < instructions.Count) pending.Push((normalIndex, state.Path, state.SelectionKnown));
                        if (TryOverrideTarget(resource, instruction.Offset, out var overrideTarget) && offsets.TryGetValue(overrideTarget, out var overrideIndex))
                            pending.Push((overrideIndex, AddUncertainPath(state.Path, instruction.Offset, "cutscene skip"), state.SelectionKnown));
                        continue;
                    }
                    if (instruction.Operation == "isEqual" && TryEquality(instruction, out var compared, out var value, out var equalTarget) && compared == selection)
                    {
                        if (!state.SelectionKnown)
                        {
                            var unknownSelectionPath = MarkUnknownSelection(state.Path, instruction.Offset);
                            if (next < instructions.Count) pending.Push((next, unknownSelectionPath, false));
                            if (offsets.TryGetValue(equalTarget, out var uncertainTarget)) pending.Push((uncertainTarget, unknownSelectionPath, false));
                        }
                        else if (value == option.VerbId)
                        {
                            if (next < instructions.Count) pending.Push((next, state.Path, true));
                        }
                        else if (offsets.TryGetValue(equalTarget, out var otherChoice)) pending.Push((otherChoice, state.Path, true));
                        continue;
                    }
                    var selectionKnown = state.SelectionKnown && !WritesVariable(instruction, selection);
                    if (IsConditional(instruction.Operation) && TryTarget(instruction, out var conditionTarget) && offsets.TryGetValue(conditionTarget, out var branchIndex))
                        pending.Push((branchIndex, AddUncertainPath(state.Path, instruction.Offset, "conditional jump"), selectionKnown));
                    if (next < instructions.Count)
                        pending.Push((next, IsConditional(instruction.Operation)
                            ? AddUncertainPath(state.Path, instruction.Offset, "conditional fall-through")
                            : state.Path, selectionKnown));
                }
            }
            // Insertion order follows execution edges. Sorting by byte offset
            // would put a shared backwards-jump reply before its preceding line.
            var traced = replies.Values.ToArray();
            tracedOptions.Add(option with { Replies = traced });
        }
        updated.Add(group with { Options = tracedOptions });
    }
    return updated.ToArray();

    static string AddUncertainPath(string path, int offset, string kind) =>
        path.Length > 0 ? path : $"Possible {kind} at +0x{offset:X4}; condition not evaluated";
    static string MarkUnknownSelection(string path, int offset) =>
        path.Contains("selection slot was overwritten", StringComparison.Ordinal)
            ? path
            : (path.Length == 0 ? "" : path + "; ") + $"selection slot was overwritten before +0x{offset:X4}; later handler conditions not evaluated";

    static bool WritesVariable(ScummScriptInstruction instruction, string variable) =>
        System.Text.RegularExpressions.Regex.IsMatch(instruction.Operands,
            "^" + System.Text.RegularExpressions.Regex.Escape(variable) + @"\s*(?:=|\+=|-=|\+\+|--)" );

    static bool TryEquality(ScummScriptInstruction instruction, out string variable, out int verb, out int target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(instruction.Operands,
            @"^(?<var>[a-z]+\[[^\]]+\]),\s*0x(?<value>[0-9A-Fa-f]+),.*->\s*0x(?<target>[0-9A-Fa-f]+)");
        variable = match.Groups["var"].Value; verb = target = 0;
        return match.Success && int.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out verb) &&
               int.TryParse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber,
                   System.Globalization.CultureInfo.InvariantCulture, out target);
    }
    static bool TryTarget(ScummScriptInstruction instruction, out int target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(instruction.Operands, @"->\s*0x(?<target>[0-9A-Fa-f]+)");
        target = 0;
        return match.Success && int.TryParse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out target);
    }
    static bool IsConditional(string operation) => operation is "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState";
    static bool TryOverrideTarget(GameResource resource, int offset, out int target)
    {
        target = 0;
        if (resource.Offset is not int resourceOffset) return false;
        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
        var jumpOffset = resourceOffset + 4 + offset + 2;
        if (jumpOffset < 0 || jumpOffset > data.Length - 3 || data[jumpOffset] != 0x18) return false;
        var displacement = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(jumpOffset + 1, 2));
        target = offset + 5 + displacement;
        return true;
    }
}

static Conversation[] TraceOpeningLines(Conversation[] groups, LoomTextExtraction extraction, GameLibrary game)
{
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    var resources = game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
        .ToDictionary(item => item.ResourceId!.Value);
    var updated = new List<Conversation>(groups.Length);
    foreach (var group in groups)
    {
        if (group.ScriptId is not int scriptId || !resources.TryGetValue(scriptId, out var resource))
        { updated.Add(group); continue; }
        ScummScriptDisassembly script;
        try { script = reader.Decode(resource); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException)
        { updated.Add(group); continue; }
        var offsets = script.Instructions.Select((instruction, index) => (instruction.Offset, index))
            .ToDictionary(item => item.Offset, item => item.index);
        var menus = groups.Where(item => item.ScriptId == scriptId).Select(item => item.StartOffset).ToHashSet();
        var spoken = extraction.Entries.Where(entry => entry.ScriptId == scriptId && entry.Kind == "Spoken line")
            .GroupBy(entry => entry.CodeOffset).ToDictionary(items => items.Key, items => items.ToArray());
        var pending = new Stack<(int Index, bool Conditional)>();
        var visited = new HashSet<(int Index, bool Conditional)>();
        var replies = new Dictionary<int, Reply>();
        var reachesThisMenu = false;
        if (script.Instructions.Count > 0) pending.Push((0, false));
        while (pending.Count > 0 && visited.Count < 15000)
        {
            var state = pending.Pop();
            if (state.Index < 0 || state.Index >= script.Instructions.Count || !visited.Add(state)) continue;
            var instruction = script.Instructions[state.Index];
            if (menus.Contains(instruction.Offset))
            {
                reachesThisMenu |= instruction.Offset == group.StartOffset;
                continue;
            }
            if (spoken.TryGetValue(instruction.Offset, out var entries))
                foreach (var entry in entries)
                    replies.TryAdd(entry.CodeOffset, new Reply(entry.Text, entry.Speaker, entry.CodeOffset, entry.Source,
                        state.Conditional ? $"Possible conditional path before menu; condition not evaluated" : "Direct decoded path"));
            if (instruction.Operation is "stopObjectCode" or "chainScript") continue;
            var next = state.Index + 1;
            if (instruction.Operation == "jumpRelative")
            {
                if (TryTarget(instruction, out var target) && offsets.TryGetValue(target, out var jump)) pending.Push((jump, state.Conditional));
                continue;
            }
            if (instruction.Operation == "beginOverride")
            {
                var normal = next < script.Instructions.Count && script.Instructions[next].Operation == "jumpRelative" ? next + 1 : next;
                if (normal < script.Instructions.Count) pending.Push((normal, state.Conditional));
                if (TryOverrideTarget(resource, instruction.Offset, out var target) && offsets.TryGetValue(target, out var skip))
                    pending.Push((skip, true));
                continue;
            }
            if (IsConditional(instruction.Operation) && TryTarget(instruction, out var branch) && offsets.TryGetValue(branch, out var taken))
                pending.Push((taken, true));
            if (next < script.Instructions.Count) pending.Push((next, state.Conditional || IsConditional(instruction.Operation)));
        }
        updated.Add(group with { Prelude = reachesThisMenu ? replies.Values.ToArray() : [] });
    }
    return updated.ToArray();

    static bool TryTarget(ScummScriptInstruction instruction, out int target)
    {
        var match = System.Text.RegularExpressions.Regex.Match(instruction.Operands, @"->\s*0x(?<target>[0-9A-Fa-f]+)");
        return match.Success && int.TryParse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out target) || (target = 0) != 0;
    }
    static bool IsConditional(string operation) => operation is "isGreaterEqual" or "isNotEqual" or "isLessEqual" or
        "isLess" or "isEqual" or "isGreater" or "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState";
    static bool TryOverrideTarget(GameResource resource, int offset, out int target)
    {
        target = 0;
        if (resource.Offset is not int resourceOffset) return false;
        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath));
        var jumpOffset = resourceOffset + 4 + offset + 2;
        if (jumpOffset < 0 || jumpOffset > data.Length - 3 || data[jumpOffset] != 0x18) return false;
        target = offset + 5 + BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(jumpOffset + 1, 2));
        return true;
    }
}

static Conversation[] ExpandConditionalOptions(Conversation[] groups, LoomTextExtraction extraction, GameLibrary game)
{
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    var scripts = new Dictionary<int, ScummScriptDisassembly>();
    foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
    {
        try { scripts[resource.ResourceId!.Value] = reader.Decode(resource); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
    }
    var updated = new List<Conversation>(groups.Length);
    foreach (var group in groups)
    {
        if (group.ScriptId is not int scriptId || !scripts.TryGetValue(scriptId, out var script)) { updated.Add(group); continue; }
        var menuEnd = groups.Where(item => item.ScriptId == scriptId && item.StartOffset > group.StartOffset)
            .Select(item => item.StartOffset).DefaultIfEmpty(int.MaxValue).Min();
        var currentIds = group.Options.Select(option => option.VerbId).ToHashSet();
        var tests = script.Instructions.Where(item => item.Offset > group.StartOffset && item.Offset < menuEnd && item.Operation == "isEqual")
            .Select(item =>
            {
                var match = System.Text.RegularExpressions.Regex.Match(item.Operands,
                    @"^(?<var>[a-z]+\[[^\]]+\]),\s*0x(?<value>[0-9A-Fa-f]+),.*->\s*0x(?<target>[0-9A-Fa-f]+)");
                return match.Success ? (Instruction: item, Variable: match.Groups["var"].Value,
                    Value: int.Parse(match.Groups["value"].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                    Target: int.Parse(match.Groups["target"].Value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)) : default;
            }).Where(item => item.Instruction is not null).ToArray();
        var firstTest = tests.FirstOrDefault(item => currentIds.Contains(item.Value));
        if (firstTest.Instruction is null) { updated.Add(group); continue; }
        var selection = firstTest.Variable;
        var dispatch = tests.Where(test => test.Variable == selection).ToArray();
        var values = dispatch.Select(test => test.Value).ToHashSet();
        var labels = script.Instructions.Where(item => item.Operation == "verbOps" && item.Offset >= group.StartOffset && item.Offset < menuEnd &&
                TryVerbLabel(item, out _, out _))
            .Select(item => { TryVerbLabel(item, out var verb, out var text); return (item.Offset, Verb: verb, Text: text); })
            .Where(item => values.Contains(item.Verb)).ToArray();
        var options = group.Options.ToDictionary(option => option.VerbId);
        foreach (var label in labels)
        {
            if (options.TryGetValue(label.Verb, out var existing))
            {
                if (!string.Equals(existing.Text, label.Text, StringComparison.Ordinal) && !existing.AlternativeLabels.Contains(label.Text, StringComparer.Ordinal))
                    options[label.Verb] = existing with { AlternativeLabels = existing.AlternativeLabels.Append(label.Text).ToArray() };
                continue;
            }
            var related = dispatch.Where(test => test.Value == label.Verb).Select(test =>
            {
                var from = script.Instructions.FirstOrDefault(item => item.Offset > test.Instruction.Offset)?.Offset ?? test.Instruction.Offset;
                return (From: from, To: test.Target);
            }).Where(range => range.To > range.From).ToArray();
            var replies = extraction.Entries.Where(entry => entry.ScriptId == scriptId && entry.Kind == "Spoken line" &&
                    related.Any(range => entry.CodeOffset >= range.From && entry.CodeOffset < range.To))
                .OrderBy(entry => entry.CodeOffset)
                .Select(entry => new Reply(entry.Text, entry.Speaker, entry.CodeOffset, entry.Source)).ToArray();
            options[label.Verb] = new Option(label.Text, label.Verb, (label.Verb - 120) * 1000 + label.Offset,
                label.Offset, $"Indexed script {scriptId:00} conditional label", replies, [], []);
        }
        updated.Add(group with { Options = options.Values.OrderBy(option => option.Order).ToArray() });
    }
    return updated.ToArray();

    static bool TryVerbLabel(ScummScriptInstruction instruction, out int verb, out string text)
    {
        verb = 0; text = "";
        if (!int.TryParse(instruction.Operands.Split(',', 2)[0].Trim().Replace("0x", "", StringComparison.Ordinal),
                System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out verb)) return false;
        var literals = ScummTextDecoder.DecodeLiterals(instruction.Operands);
        if (literals.Count == 0) return false;
        text = literals[0];
        return true;
    }
}

static string RenderHtml(IReadOnlyList<Conversation> groups, IReadOnlyDictionary<int, string> rooms, LoomTextExtraction extraction)
{
    var html = new StringBuilder(256_000);
    html.Append("""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Indiana Jones and the Last Crusade EGA — Dialogue choices</title>
<style>
:root{color-scheme:dark;--paper:#171a1c;--panel:#202628;--line:#41484a;--ink:#e8e1d3;--muted:#a9aaa3;--gold:#e1b85f;--red:#b85645}*{box-sizing:border-box}body{margin:0;background:radial-gradient(ellipse at 45% -20%,#404136 0,#1b1e1e 56%,#131516 100%);color:var(--ink);font:16px/1.5 Georgia,"Times New Roman",serif}header{max-width:1260px;margin:auto;padding:44px 30px 24px}h1{font-weight:500;letter-spacing:.02em;font-size:clamp(2rem,5vw,3.6rem);line-height:1.05;margin:0 0 13px;color:#f1dfb9}.eyebrow,.tag,.source,.small{font:600 .72rem/1.3 system-ui,sans-serif;letter-spacing:.09em;text-transform:uppercase;color:var(--muted)}.eyebrow{color:var(--gold)}.intro{max-width:850px;color:#d2cbbc;font-size:1.08rem}.notice{border-left:3px solid var(--gold);padding:12px 17px;background:#272924;max-width:950px;color:#d7d1c3}.toolbar{position:sticky;top:0;z-index:5;background:#171a1cee;border-block:1px solid var(--line);backdrop-filter:blur(8px);padding:12px max(24px,calc((100vw - 1200px)/2));display:flex;gap:10px;align-items:center}.toolbar input{font:inherit;background:#252b2c;color:var(--ink);border:1px solid #59615e;border-radius:4px;padding:9px 12px;min-width:220px;flex:1}.toolbar button{font:600 .82rem system-ui,sans-serif;background:#343b3a;color:var(--ink);border:1px solid #656a61;border-radius:4px;padding:9px 12px;cursor:pointer}.toolbar button:hover,.toolbar input:focus{border-color:var(--gold);outline-color:var(--gold)}#count{white-space:nowrap;color:var(--muted);font: .8rem system-ui,sans-serif}.layout{max-width:1260px;margin:auto;padding:24px 30px 70px;display:grid;grid-template-columns:minmax(210px,260px) minmax(0,1fr);gap:28px}.index{align-self:start;position:sticky;top:72px;max-height:calc(100vh - 92px);overflow:auto;border-right:1px solid var(--line);padding-right:16px}.index h2{font-size:.8rem;font-family:system-ui,sans-serif;letter-spacing:.1em;text-transform:uppercase;color:var(--gold)}.index a{display:block;color:var(--ink);text-decoration:none;padding:6px 5px;border-bottom:1px solid #ffffff0b;font-size:.88rem}.index a:hover{color:var(--gold)}main{min-width:0}.conversation{background:linear-gradient(145deg,#242a29,#1b2021);border:1px solid #4b504c;border-radius:7px;margin:0 0 18px;overflow:hidden;box-shadow:0 10px 24px #0002}.conversation>summary{cursor:pointer;list-style:none;display:grid;grid-template-columns:180px 1fr auto;gap:17px;align-items:center;padding:12px}.conversation>summary::-webkit-details-marker{display:none}.conversation>summary:hover{background:#ffffff06}.thumb{width:180px;aspect-ratio:16/7;object-fit:cover;background:#090a0a;border:1px solid #515651;image-rendering:pixelated}.summary-title{font-size:1.14rem}.summary-sub{font: .78rem system-ui,sans-serif;color:var(--muted);margin-top:5px}.arrow{color:var(--gold);font: .8rem system-ui,sans-serif}.conversation[open] .arrow{transform:rotate(90deg)}.content{padding:0 22px 23px}.scene{margin:0 -22px 22px;background:#111;padding:12px;text-align:center}.scene img{max-width:min(100%,800px);height:auto;max-height:360px;object-fit:contain;image-rendering:pixelated;box-shadow:0 6px 30px #0008}.scene-caption{margin-top:6px;color:var(--muted);font:.72rem system-ui,sans-serif}.options{display:grid;gap:10px}.option{border:1px solid #54584f;border-radius:5px;background:#1b2020}.option>summary{list-style:none;cursor:pointer;padding:12px 14px;display:flex;gap:12px;align-items:baseline}.option>summary::-webkit-details-marker{display:none}.option>summary:before{content:"▸";color:var(--gold);font: .85rem system-ui,sans-serif}.option[open]>summary:before{content:"▾"}.choice{color:#f0d390;font-size:1.1rem}.replies{padding:0 18px 13px 39px}.reply{border-left:2px solid #747260;padding:5px 0 5px 12px;margin:5px 0;color:#d8d5cc}.speaker{color:#bdb69f;font:600 .68rem system-ui,sans-serif;text-transform:uppercase;letter-spacing:.08em;margin-right:8px}.empty{color:var(--muted);font-style:italic}.sourcebox{margin-top:20px;border-top:1px solid var(--line);padding-top:12px}.sourcebox summary{cursor:pointer;color:#c7c2b5;font: .77rem system-ui,sans-serif}.sourcebox pre{white-space:pre-wrap;color:#9da39d;font:.74rem/1.5 ui-monospace,Consolas,monospace}.pending{color:#e0a48d}.foot{margin:26px 0;color:var(--muted);font: .8rem/1.5 system-ui,sans-serif}.no-room{color:#dfb5a4}.hidden{display:none!important}@media(max-width:800px){.layout{grid-template-columns:1fr;padding:14px}.index{position:static;max-height:180px;border-right:0;border-bottom:1px solid var(--line);padding:0 0 12px}.conversation>summary{grid-template-columns:110px 1fr auto}.thumb{width:110px}.toolbar{padding:10px;flex-wrap:wrap}.toolbar input{min-width:140px}}@media(max-width:520px){header{padding:30px 18px 18px}.conversation>summary{grid-template-columns:1fr auto}.thumb{width:100%;grid-column:1/-1;aspect-ratio:16/7}.content{padding-inline:14px}.scene{margin-inline:-14px}.toolbar #count{order:5;width:100%}}
.next-menus{display:grid;gap:6px;margin:12px 0 0}.next-link{display:block;border-left:2px solid var(--gold);padding:7px 10px;color:#e8d39d;text-decoration:none;background:#ffffff08}.next-link:hover{background:#ffffff12}.next-link small{display:block;color:var(--muted);font:.7rem/1.4 system-ui,sans-serif;margin-top:3px}
</style>
</head>
<body>
<header><div class="eyebrow">DOS EGA · SOURCE-DERIVED REFERENCE</div><h1>Talk your way through it</h1>
<p class="intro">Multiple-choice dialogue recovered from the supplied <em>Indiana Jones and the Last Crusade</em> EGA game data. Open a scene, then expand an answer to read the spoken lines stored in that selection branch.</p>
<p class="notice">This is a static script map. It follows decoded jumps and same-script choice dispatch, and links literal calls to scripts containing decoded menus. Runtime conditions and call arguments are not evaluated, so those branches are shown as possible paths; unresolved activation and room links are marked. No outcome or “best path” is assigned.</p></header>
<div class="toolbar"><input id="search" type="search" placeholder="Find a person, place or line…" aria-label="Search conversations"><button id="openAll" type="button">Expand all</button><button id="closeAll" type="button">Collapse all</button><span id="count"></span></div>
<div class="layout"><nav class="index" aria-label="Conversation index"><h2>Scenes</h2><div id="sceneIndex"></div></nav><main id="conversations">
""");

    var indexes = groups.Select((group, index) => (group.Id, Index: index + 1)).ToDictionary(item => item.Id, item => item.Index, StringComparer.Ordinal);
    var incoming = groups.SelectMany(group => group.Options).SelectMany(option => option.NextMenus)
        .Select(link => link.TargetId).ToHashSet(StringComparer.Ordinal);
    var renderedNodes = new HashSet<string>(StringComparer.Ordinal);
    var roots = groups.Where(group => !incoming.Contains(group.Id)).ToList();
    var nodeById = groups.ToDictionary(group => group.Id, StringComparer.Ordinal);

    void RenderMenu(Conversation menu)
    {
        var nodeId = $"node-{indexes[menu.Id]:000}";
        if (!renderedNodes.Add(menu.Id))
        {
            html.Append("<a class=\"shared-node\" href=\"#").Append(nodeId).Append("\">↪ Shared menu or loop: ")
                .Append(E(menu.Options.FirstOrDefault()?.Text ?? "choice set")).Append("</a>");
            return;
        }
        html.Append("<details class=\"menu-node\" id=\"").Append(nodeId).Append("\"><summary>")
            .Append(E(menu.Options.FirstOrDefault()?.Text ?? "Choice set"))
            .Append(" <small>Script ").Append(menu.ScriptId?.ToString("00") ?? "room script")
            .Append(" · +0x").Append(menu.StartOffset.ToString("X4")).Append("</small></summary><div class=\"menu-body\">");
        if (menu.NoVerifiedScene)
            html.Append("<p class=\"no-room\"><strong>No scene established.</strong> This dialogue script is stored alongside Room 32; its stored background is blank.</p>");
        else if (menu.RoomIds.Count == 0)
            html.Append("<p class=\"no-room\"><strong>Room picture unavailable:</strong> this script's room cannot be established from the current source links.</p>");
        else
            foreach (var room in menu.RoomIds)
                if (rooms.TryGetValue(room, out var image))
                    html.Append("<figure class=\"scene\"><img alt=\"Original EGA background for Room ").Append(room.ToString("00")).Append("\" src=\"").Append(image)
                        .Append("\"><figcaption class=\"scene-caption\">Room ").Append(room.ToString("00")).Append(" · original EGA background on a source-linked caller path</figcaption></figure>");
        if (menu.Prelude.Count > 0)
        {
            html.Append("<section class=\"prelude\"><h3>Opening lines before the first choice</h3>");
            foreach (var line in menu.Prelude)
            {
                if (line.Path != "Direct decoded path") html.Append("<p class=\"conditional-note\">").Append(E(line.Path)).Append("</p>");
                html.Append("<div class=\"reply\">");
                if (!string.IsNullOrWhiteSpace(line.Speaker)) html.Append("<span class=\"speaker\">").Append(E(line.Speaker)).Append("</span>");
                html.Append(E(line.Text)).Append("</div>");
            }
            html.Append("</section>");
        }
        html.Append("<div class=\"options\">");
        foreach (var option in menu.Options)
        {
            html.Append("<details class=\"option\"><summary><span class=\"choice\">").Append(E(option.Text)).Append("</span></summary><div class=\"replies\">");
            if (option.Replies.Count == 0) html.Append("<p class=\"empty\">No literal spoken reply was decoded in this selection branch.</p>");
            foreach (var replyPath in option.Replies.GroupBy(reply => reply.Path, StringComparer.Ordinal))
            {
                if (replyPath.Key != "Direct decoded path")
                    html.Append("<p class=\"conditional-note\">").Append(E(replyPath.Key)).Append("</p>");
                foreach (var reply in replyPath)
                {
                    html.Append("<div class=\"reply\">");
                    if (!string.IsNullOrWhiteSpace(reply.Speaker)) html.Append("<span class=\"speaker\">").Append(E(reply.Speaker)).Append("</span>");
                    html.Append(E(reply.Text)).Append("</div>");
                }
            }
            if (option.AlternativeLabels.Count > 0)
            {
                html.Append("<details class=\"alternatives\"><summary>Conditional alternate wording · ").Append(option.AlternativeLabels.Count)
                    .Append(" variants</summary><div class=\"replies\"><p class=\"empty\">The game changes the label under a condition that this reference does not evaluate.</p>");
                foreach (var label in option.AlternativeLabels) html.Append("<div class=\"reply\">").Append(E(label)).Append("</div>");
                html.Append("</div></details>");
            }
            if (option.NextMenus.Count > 0)
            {
                html.Append("<div class=\"next-menus\"><span class=\"tag\">Possible continuation</span>");
                foreach (var edge in option.NextMenus)
                {
                    html.Append("<div class=\"edge-note\">").Append(E(edge.Kind)).Append(" · ").Append(E(edge.Note)).Append("</div>");
                    if (nodeById.TryGetValue(edge.TargetId, out var target)) RenderMenu(target);
                }
                html.Append("</div>");
            }
            else html.Append("<p class=\"empty\">No later choice menu was linked from the decoded branch. The branch may end, change scene, or continue through unresolved code.</p>");
            html.Append("</div></details>");
        }
        html.Append("</div><details class=\"sourcebox\"><summary>Source evidence</summary><pre>")
            .Append(E($"Set: {menu.Id}\nSource: {menu.Source}\nScript: {(menu.ScriptId is int scriptId ? $"{scriptId:00}" : "room-local / embedded")}\nChoice offset: +0x{menu.StartOffset:X4}\nEdges follow decoded jumps and same-variable literal choice tests. Other conditional state remains unevaluated."))
            .Append("</pre></details></div></details>");
    }

    foreach (var root in roots.Concat(groups.Where(group => !roots.Contains(group))))
    {
        if (renderedNodes.Contains(root.Id)) continue;
        var index = indexes[root.Id];
        var id = $"scene-{index:000}";
        var conversation = root;
        var title = conversation.Options.FirstOrDefault()?.Text ?? $"Choice set {index}";
        var location = conversation.NoVerifiedScene ? "Scene not established" : conversation.RoomIds.Count > 0 ? string.Join(", ", conversation.RoomIds.Select(roomId => $"Room {roomId:00}")) : "Room association unresolved";
        html.Append("<details class=\"conversation\" id=\"").Append(id).Append("\" data-search=\"")
            .Append(Attr(string.Join(' ', conversation.Options.Select(option => option.Text).Concat(conversation.Options.SelectMany(option => option.Replies.Select(reply => reply.Text))))))
            .Append("\"><summary>");
        if (!conversation.NoVerifiedScene && conversation.RoomIds.FirstOrDefault(-1) is int thumbRoom && thumbRoom >= 0 && rooms.TryGetValue(thumbRoom, out var thumb))
            html.Append("<img class=\"thumb\" alt=\"Room ").Append(thumbRoom.ToString("00")).Append(" background\" src=\"").Append(thumb).Append("\">");
        else html.Append("<div class=\"thumb no-room\" aria-hidden=\"true\">Scene not established</div>");
        html.Append("<div><div class=\"summary-title\">").Append(E(title)).Append("</div><div class=\"summary-sub\">")
            .Append(E(location)).Append(" · source-linked conversation</div></div><span class=\"arrow\" aria-hidden=\"true\">▶</span></summary><div class=\"content\">");
        RenderMenu(conversation);
        html.Append("<details class=\"sourcebox\"><summary>Room evidence</summary><pre>")
            .Append(E($"Set: {conversation.Id}\nSource: {conversation.Source}\nScript: {(conversation.ScriptId is int scriptId ? $"{scriptId:00}" : "room-local / embedded")}\nFirst choice code offset: +0x{conversation.StartOffset:X4}\nRoom evidence: {string.Join(" | ", conversation.RoomEvidence.SelectMany(pair => pair.Value))}\nChoice labels and spoken lines remain source-derived. Outgoing links are possible CFG/call edges, not evaluation of runtime variables or game state."))
            .Append("</pre></details></div></details>");
    }

    html.Append("</main></div><footer class=\"foot\" style=\"max-width:1260px;margin:0 auto;padding:0 30px 40px\">")
        .Append(E($"{groups.Count:N0} multiple-choice sets · {groups.Sum(group => group.Options.Count):N0} options · {extraction.DecodedBytes:N0} decoded instruction bytes from {extraction.ResourcesVisited:N0} decode ranges · {extraction.Diagnostics.Count:N0} decoder diagnostics"))
        .Append("<br>Original room art and script text are embedded; this file works offline. The source HTML can be regenerated with the repository tool. Stored strings are evidence of possible dialogue, not proof that every path is reachable in a normal playthrough.</footer>")
        .Append("<script>(function(){const cards=[...document.querySelectorAll('.conversation')],search=document.querySelector('#search'),index=document.querySelector('#sceneIndex'),count=document.querySelector('#count');function title(card){return card.querySelector('.summary-title').textContent}function openTarget(id){const target=document.getElementById(id);if(!target)return;let node=target;while(node&&node!==document.body){node.classList?.remove('hidden');if(node.tagName==='DETAILS')node.open=true;node=node.parentElement}target.scrollIntoView({behavior:'smooth',block:'start'})}function refresh(){const q=search.value.trim().toLocaleLowerCase();let n=0;index.replaceChildren();cards.forEach(card=>{const ok=!q||card.textContent.toLocaleLowerCase().includes(q)||card.dataset.search.toLocaleLowerCase().includes(q);card.classList.toggle('hidden',!ok);if(ok){n++;const a=document.createElement('a');a.href='#'+card.id;a.textContent=title(card);a.addEventListener('click',()=>openTarget(card.id));index.append(a)}});count.textContent=n+' of '+cards.length+' scenes'}document.querySelectorAll('.shared-node').forEach(a=>a.addEventListener('click',e=>{e.preventDefault();openTarget(a.hash.slice(1))}));search.addEventListener('input',refresh);document.querySelector('#openAll').onclick=()=>cards.filter(c=>!c.classList.contains('hidden')).forEach(c=>{c.open=true;c.querySelectorAll('details.menu-node,details.option,details.alternatives').forEach(d=>d.open=true)});document.querySelector('#closeAll').onclick=()=>cards.forEach(c=>{c.open=false;c.querySelectorAll('details.menu-node,details.option,details.alternatives').forEach(d=>d.open=false)});refresh()})();</script></body></html>");
    return html.ToString();
}

static string ToBmpDataUri(RoomImage image)
{
    var stride = (image.Width + 3) & ~3;
    const int paletteBytes = 16 * 4;
    const int pixelOffset = 14 + 40 + paletteBytes;
    var pixelBytes = stride * image.Height;
    var bmp = new byte[pixelOffset + pixelBytes];
    bmp[0] = (byte)'B'; bmp[1] = (byte)'M';
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(2), bmp.Length);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(10), pixelOffset);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(14), 40);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(18), image.Width);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(22), image.Height);
    BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(26), 1);
    BinaryPrimitives.WriteUInt16LittleEndian(bmp.AsSpan(28), 8);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(34), pixelBytes);
    BinaryPrimitives.WriteInt32LittleEndian(bmp.AsSpan(46), 16);
    for (var index = 0; index < 16; index++)
    {
        var argb = image.Palette[index];
        var offset = 54 + index * 4;
        bmp[offset] = (byte)argb;
        bmp[offset + 1] = (byte)(argb >> 8);
        bmp[offset + 2] = (byte)(argb >> 16);
    }
    var pixels = image.Pixels.Span;
    for (var y = 0; y < image.Height; y++)
    {
        var sourceRow = y * image.Width;
        var targetRow = pixelOffset + (image.Height - y - 1) * stride;
        pixels.Slice(sourceRow, image.Width).CopyTo(bmp.AsSpan(targetRow, image.Width));
    }
    return "data:image/bmp;base64," + Convert.ToBase64String(bmp);
}

static string E(string value) => WebUtility.HtmlEncode(value);
static string Attr(string value) => WebUtility.HtmlEncode(value).Replace("\"", "&quot;", StringComparison.Ordinal);

sealed record Conversation(string Id, IReadOnlyList<int> RoomIds, IReadOnlyDictionary<int, string[]> RoomEvidence,
    int? ScriptId, string Source, int StartOffset, IReadOnlyList<Option> Options, string? ExclusionReason, bool NoVerifiedScene,
    IReadOnlyList<Reply> Prelude);
sealed record Option(string Text, int VerbId, int Order, int CodeOffset, string Source, IReadOnlyList<Reply> Replies,
    IReadOnlyList<string> AlternativeLabels, IReadOnlyList<ConversationLink> NextMenus);
sealed record Reply(string Text, string? Speaker, int CodeOffset, string Source, string Path = "Direct decoded path");
sealed record ConversationLink(int VerbId, string TargetId, string Note, string Kind);
