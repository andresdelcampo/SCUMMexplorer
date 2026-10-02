"""Add copied numeric access/index lineage logs to the unchanged recipe harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
expanded = '--expanded' in sys.argv[2:]
sys.argv = ['create_audit.py', str(out)] + (['--expanded'] if expanded else [])
runpy.run_path('.verification/runtime-relations-20261002/create_audit.py', run_name='__main__')
p = out / 'Program.cs'
source = p.read_text(encoding='utf-8')
replacements = {
    '    using var instructionLog=': '    using var accessLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-access.jsonl"));\n    using var instructionLog=',
    '        instructionLog.WriteLine': '''        if(observation.SourceAccess is { } access) accessLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,
            observation.Sequence,observation.Slot,Run=observation.Invocation,Site=observation.Site,access}));
        instructionLog.WriteLine''',
    'Origin=r.ObservedOrigin?.Id,r.Observation.PathEstimated': 'Origin=r.ObservedOrigin?.Id,IndexRead=r.IndexRead?.Id,r.Observation.PathEstimated',
    'match.MatchesOwnLiteralChild,match.Rejection,match.Limits': 'match.MatchesOwnLiteralChild,match.Rejection,match.Limits,match.WriterTerminated,match.TerminalRejection'
}
for old, new in replacements.items():
    assert source.count(old) == 1, old
    source = source.replace(old, new)
p.write_text(source, encoding='utf-8')
print('Copied numeric access/index/writer-terminal records enabled; legacy chronology and recipes unchanged.')
