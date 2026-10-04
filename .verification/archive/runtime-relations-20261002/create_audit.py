"""Attach optional production relation/completion observers to the unchanged recipe harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
expanded = '--expanded' in sys.argv[2:]
sys.argv = ['create_audit.py', str(out)] + (['--expanded'] if expanded else [])
runpy.run_path('.verification/archive/runtime-bindings-20261002/create_audit.py', run_name='__main__')
p = out / 'Program.cs'
source = p.read_text(encoding='utf-8')
needle = '    var observer=new LoomScriptObserver'
assert source.count(needle) == 1
source = source.replace(needle, '''    using var relationLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-relations.jsonl"));
    using var completionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-completion.jsonl"));
    var completion=new LoomCompletionObserver(match => {
        var r=match.Read; var w=match.Writer;
        completionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,
            Sequence=r.Observation.Sequence,Slot=r.Observation.Slot,Run=r.Observation.Invocation,
            Site=r.Observation.Site,Binding=r.Binding,r.Observation.PathEstimated,
            match.MatchesOwnLiteralChild,match.Rejection,match.Limits,
            Writer=w is null ? null : new{w.Id,Sequence=w.Observation.Sequence,Slot=w.Observation.Slot,
                w.Observation.ParentSlot,Run=w.Observation.Invocation,Site=w.Observation.Site,
                w.Value,w.ValueEstimated,w.Observation.PathEstimated,w.ConfirmationSequence},
            WriterEnd=match.WriterEnd is null ? null : new{match.WriterEnd.Sequence,match.WriterEnd.ReachedStop,
                match.WriterEnd.ResumeOffset,match.WriterEnd.UnresolvedReason,match.WriterEnd.Site}}));
    });
    var relations=new LoomRuntimeRelationTracker(update => {
        completion.Observe(update);
        foreach(var r in update.Reads) relationLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,
            update.Event,Kind="read",r.Id,Sequence=r.Observation.Sequence,Slot=r.Observation.Slot,
            Run=r.Observation.Invocation,Site=r.Observation.Site,r.Binding,r.Dependencies,
            Origin=r.ObservedOrigin?.Id,r.Observation.PathEstimated}));
        foreach(var w in update.Writes) relationLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,
            update.Event,Kind="write",w.Id,Sequence=w.Observation.Sequence,Slot=w.Observation.Slot,
            w.Observation.ParentSlot,Run=w.Observation.Invocation,Site=w.Observation.Site,
            w.Destination,w.SourceReads,w.Value,w.ValueEstimated,w.Observation.PathEstimated,w.ConfirmationSequence}));
        if(update.Invalidated.Count>0 || update.Barrier is not null)
            relationLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,update.Event,Kind="invalidate",
                Sequence=update.Observation?.Sequence,update.Invalidated,update.Barrier}));
    });
    var observer=new LoomScriptObserver''')
needle = 'captureOperandBindings:true);'
assert source.count(needle) == 1
source = source.replace(needle, 'captureOperandBindings:true, runtimeRelations:relations);')
p.write_text(source, encoding='utf-8')
print('Optional copied relation/completion sinks enabled; no recipe or Core copy/patch.')
