"""Read-only copied-Core observer: stable slots, causal parents, cancellation.

Uses the prior copied-value instruction observer without any new recipe actions.
Only generated files under a fresh ignored Exe folder are instrumented.
"""
from pathlib import Path
import runpy
import sys

repo = Path.cwd()
out = Path(sys.argv[1]).resolve()
runpy.run_path(str(repo / '.verification/archive/certainty-provenance-20261002/create_audit.py'))

def replace(source, old, new):
    assert source.count(old) == 1, old
    return source.replace(old, new)

session_path = repo / 'src/SCUMMexplorer.Core/Formats/LoomExplorationSession.cs'
source = session_path.read_text(encoding='utf-8-sig')
source = replace(source, '        public bool Stopped { get; set; }', '''        public bool Stopped { get; set; }
        public long AuditSlot { get; } = CertaintyInstructionProbe.NewSlot();
        public long? AuditParent { get; init; }''')
source = replace(source,
    'var frame = new ScriptFrame { FreezeResistant = freezeResistant, Recursive = recursive, OriginRoom = parent?.OriginRoom };',
    'var frame = new ScriptFrame { FreezeResistant = freezeResistant, Recursive = recursive, OriginRoom = parent?.OriginRoom, AuditParent = parent?.AuditSlot };')
source = replace(source, '        _executing.Add(frame);', '''        _executing.Add(frame);
        CertaintyInstructionProbe.EnterSlot(frame.AuditSlot, frame.AuditParent, frame.Label,
            frame.Number, startOffset, frame.FreezeCount, frame.Delay, pathEstimated);''')
source = replace(source, '            _executing.RemoveAt(_executing.Count - 1);', '''            CertaintyInstructionProbe.LeaveSlot(frame.AuditSlot);
            _executing.RemoveAt(_executing.Count - 1);''')
source = replace(source, '                _frameScripts.Remove(item);', '''                _frameScripts.Remove(item);
                CertaintyInstructionProbe.SessionListener?.Invoke(new { Stage = "turn", Slot = item.Frame.AuditSlot,
                    Number = item.ScriptNumber, item.Offset, item.Frame.Stopped, item.Frame.FreezeCount, item.Frame.Delay,
                    item.PathEstimated, Locals = new Dictionary<string, ushort>(item.Locals) });''')
source = replace(source, '        foreach (var slot in slots) slot.Stopped = true;', '''        CertaintyInstructionProbe.SessionListener?.Invoke(new { Stage = "cancel", Number = number,
            Caller = CertaintyInstructionProbe.CurrentSlot, Slots = slots.Select(slot => slot.AuditSlot).ToArray() });
        foreach (var slot in slots) slot.Stopped = true;''')
(out / 'LoomExplorationSession.cs').write_text(source, encoding='utf-8')

project_path = out / 'ObservedCore.csproj'
project = project_path.read_text(encoding='utf-8')
project = replace(project, 'LoomBoundedScriptExecutor.cs" />', 'LoomBoundedScriptExecutor.cs;' + session_path.as_posix() + '" />')
project = replace(project, 'LoomBoundedScriptExecutor.cs;CertaintyInstructionProbe.cs', 'LoomBoundedScriptExecutor.cs;LoomExplorationSession.cs;CertaintyInstructionProbe.cs')
project_path.write_text(project, encoding='utf-8')

probe_path = out / 'CertaintyInstructionProbe.cs'
probe = probe_path.read_text(encoding='utf-8')
probe = replace(probe, 'long Run, long? Parent, string Stage,', 'long Run, long? Parent, long Slot, long? ParentSlot, string Stage,')
probe = replace(probe, '    private static long serial;', '''    private static long serial;
    private static long slotSerial;
    private static readonly Stack<(long Slot, long? Parent)> slots = new();
    public static Action<object>? SessionListener;
    public static long CurrentSlot => slots.Count == 0 ? 0 : slots.Peek().Slot;
    public static long NewSlot() => ++slotSerial;
    public static void EnterSlot(long slot, long? parent, string label, int? number, int offset, int freeze, int? delay, bool path)
    {
        slots.Push((slot, parent));
        SessionListener?.Invoke(new { Stage = "enter", Slot = slot, ParentSlot = parent, Label = label, Number = number,
            Offset = offset, Freeze = freeze, Delay = delay, PathEstimated = path });
    }
    public static void LeaveSlot(long slot)
    {
        if (slots.Pop().Slot != slot) throw new InvalidDataException("Slot observer stack mismatch");
        SessionListener?.Invoke(new { Stage = "leave", Slot = slot });
    }''')
probe = replace(probe, 'new(id, stack.Count > 1 ? stack[^2] : null, stage,',
    'new(id, stack.Count > 1 ? stack[^2] : null, CurrentSlot, slots.Count > 0 ? slots.Peek().Parent : null, stage,')
# Include actual table words, caller scope/index and aliases in copied observations.
probe = replace(probe, 'keys.Contains(p.Key) || p.Key.StartsWith("local[") ||',
    'keys.Contains(p.Key) || p.Key.StartsWith("local[") || Enumerable.Range(0x32, 32).Any(i => p.Key == $"var[0x{i:X3}]") ||')
probe_path.write_text(probe, encoding='utf-8')

harness = out / 'Audit/Program.cs'
program = harness.read_text(encoding='utf-8')
program = replace(program, '    CertaintyInstructionProbe.Listener =', '''    using var sessionLog = new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-scheduler.jsonl"));
    CertaintyInstructionProbe.SessionListener = record => sessionLog.WriteLine(JsonSerializer.Serialize(new { frame=elapsed, record }));
    CertaintyInstructionProbe.Listener =''')
harness.write_text(program, encoding='utf-8')
print('Copied session instrumented: stable slot IDs, causal parent, turn/cancel records; no state or certainty writes.')
