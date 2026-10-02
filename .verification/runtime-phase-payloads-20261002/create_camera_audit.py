"""Separate camera-only copies from an unchanged loaded source recipe."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
runpy.run_path('.verification/runtime-object-correspondence-20261002/create_audit.py', run_name='__main__')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
a = source.index('    using var selectionLog=')
b = source.index('    var session=', a)
source = source[:a]+'''    using var cameraLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-camera.jsonl"));
    var observer=new LoomScriptObserver(observation => {
        if(observation.Site is { Stage:"instruction", Operation:"waitFor", Operands:"camera" })
            cameraLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,observation}));
    }, includeSite: site => site.Stage=="instruction" && site.Operation=="waitFor" && site.Operands=="camera",
       captureSourceAccess:true);
''' + source[b:]
p.write_text(source, encoding='utf-8')
print('Separate selected camera-only chronology; unchanged source actions and full recipe packet.')
