"""Build a read-only instruction observer into an isolated copy of Core.

No production files are changed. The observer receives copied values, never a
runtime/session. The existing input recipe harness is reused without new actions.
"""
from pathlib import Path
import hashlib
import json
import sys

repo = Path.cwd()
out = Path(sys.argv[1]).resolve()
assert out.is_relative_to((repo / 'Exe').resolve())
out.mkdir(parents=True, exist_ok=False)
core = repo / 'src/SCUMMexplorer.Core'
executor = core / 'Formats/LoomBoundedScriptExecutor.cs'
source = executor.read_text(encoding='utf-8-sig')
assert hashlib.sha256(executor.read_bytes()).hexdigest() == 'f84df6418e570f1f026ec9c498e160341094287514dec2ef82b11679b1792227'
replacements = [
    ('        while (indexByOffset.TryGetValue(pc, out var instructionIndex))',
     '        var auditRun = CertaintyInstructionProbe.Begin(script, pc, knownVariables, estimated, onEstimatedPath);\n\n        while (indexByOffset.TryGetValue(pc, out var instructionIndex))'),
    ('            var next = instruction.Offset + EncodedLength(instruction.Bytes);',
     '            var next = instruction.Offset + EncodedLength(instruction.Bytes);\n            CertaintyInstructionProbe.Observe(auditRun, script, instruction, knownVariables, estimated, onEstimatedPath);'),
    ('        return new LoomBoundedScriptResult(new Dictionary<int, byte>(states),',
     '        CertaintyInstructionProbe.End(auditRun, script, pc, resumeOffset, reachedStop, unresolved, knownVariables, estimated, onEstimatedPath);\n        return new LoomBoundedScriptResult(new Dictionary<int, byte>(states),'),
]
for old, new in replacements:
    assert source.count(old) == 1
    source = source.replace(old, new)
(out / 'LoomBoundedScriptExecutor.cs').write_text(source, encoding='utf-8')
(out / 'CertaintyInstructionProbe.cs').write_text('''using SCUMMexplorer.Core.Formats;
namespace SCUMMexplorer.Core.Formats;
public sealed record CertaintyInstructionRecord(long Run, long? Parent, string Stage,
    int? Script, string? Context, int Offset, string? Operation, string? Operands,
    bool PathEstimated, IReadOnlyDictionary<string, ushort> Values, string[] Estimated,
    int? Resume = null, bool? Stopped = null, string? Unresolved = null);
public static class CertaintyInstructionProbe
{
    public static Action<CertaintyInstructionRecord>? Listener;
    private static long serial;
    private static readonly List<long> stack = [];
    private static readonly HashSet<string> keys = ["bit[0xD6C]", "bit[0xD67]", "bit[0xD70]", "bit[0xD61]", "bit[0xD66]", "var[0x004]", "var[0x064]", "var[0x067]", "var[0x07A]", "var[0x07B]", "var[0x062]"];
    private static void Emit(long id, LoomScriptDisassembly script, string stage, int offset,
        string? operation, string? operands, IReadOnlyDictionary<string, ushort> values,
        IEnumerable<string> estimated, bool path, int? resume = null, bool? stopped = null, string? unresolved = null)
    {
        if (script.ScriptId is not (10 or 20 or 22 or 23 or 30 or 31) &&
            !(operands?.Contains("bit[0xD6C]") == true || operands?.Contains("bit[0xD67]") == true)) return;
        var copy = values.Where(p => keys.Contains(p.Key) || p.Key.StartsWith("local[") ||
            Enumerable.Range(0x52, 8).Any(i => p.Key == $"var[0x{i:X3}]"))
            .ToDictionary(p => p.Key, p => p.Value);
        Listener?.Invoke(new(id, stack.Count > 1 ? stack[^2] : null, stage,
            script.ScriptId, script.Context, offset, operation, operands, path, copy,
            estimated.Order().ToArray(), resume, stopped, unresolved));
    }
    public static long Begin(LoomScriptDisassembly script, int offset, IReadOnlyDictionary<string, ushort> values, IEnumerable<string> estimated, bool path)
    { var id = ++serial; stack.Add(id); Emit(id, script, "begin", offset, null, null, values, estimated, path); return id; }
    public static void Observe(long id, LoomScriptDisassembly script, LoomScriptInstruction instruction, IReadOnlyDictionary<string, ushort> values, IEnumerable<string> estimated, bool path)
    { Emit(id, script, "instruction", instruction.Offset, instruction.Operation, instruction.Operands, values, estimated, path); }
    public static void End(long id, LoomScriptDisassembly script, int offset, int? resume, bool stopped, string? unresolved, IReadOnlyDictionary<string, ushort> values, IEnumerable<string> estimated, bool path)
    { Emit(id, script, "end", offset, null, null, values, estimated, path, resume, stopped, unresolved); if (stack[^1] != id) throw new InvalidDataException("Observer stack mismatch"); stack.RemoveAt(stack.Count - 1); }
}
''', encoding='utf-8')
project = (core / 'SCUMMexplorer.Core.csproj').read_text(encoding='utf-8-sig')
project = project.replace('<Nullable>enable</Nullable>', '<Nullable>enable</Nullable>\n    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>')
project = project.replace('</Project>', f'''  <ItemGroup>
    <Compile Include="{core.as_posix()}/**/*.cs" Exclude="{core.as_posix()}/obj/**/*.cs;{executor.as_posix()}" />
    <Compile Include="LoomBoundedScriptExecutor.cs;CertaintyInstructionProbe.cs" />
  </ItemGroup>
</Project>''')
(out / 'ObservedCore.csproj').write_text(project, encoding='utf-8')
probe = (repo / '.verification/room41-certainty-20261002/ProgressionProbe/Program.cs').read_text(encoding='utf-8-sig')
needle = '    var session=new LoomExplorationSession'
assert probe.count(needle) == 1
probe = probe.replace(needle, '''    int elapsed=0;
    using var instructionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-instructions.jsonl"));
    CertaintyInstructionProbe.Listener = record => instructionLog.WriteLine(JsonSerializer.Serialize(new { frame=elapsed, record }));
    var session=new LoomExplorationSession''')
assert probe.count('    int elapsed=0; var roomChanges=') == 1
probe = probe.replace('    int elapsed=0; var roomChanges=', '    var roomChanges=')
harness = out / 'Audit'
harness.mkdir()
(harness / 'Program.cs').write_text(probe, encoding='utf-8')
(harness / 'Audit.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="../ObservedCore.csproj" /><Compile Include="{(repo / '.verification/HarnessFailurePolicy.cs').as_posix()}" Link="HarnessFailurePolicy.cs" /></ItemGroup>
</Project>
''', encoding='utf-8')
manifest = {p.relative_to(repo).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
    for p in sorted(core.rglob('*')) if p.is_file() and 'obj' not in p.parts}
(out / 'source-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
print(f'Isolated observer: {harness / "Audit.csproj"}; three copied-executor insertions; production unchanged.')
