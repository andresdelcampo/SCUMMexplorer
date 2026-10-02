"""Keep the separate unchanged raw4 checks in this packet, without rewriting history."""
from pathlib import Path
source = Path('.verification/runtime-object-correspondence-20261002/analyze_loaded_objects.py').read_text(encoding='utf-8')
exec(compile(source, 'preserved-loaded-object-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})

import json
import sys
out = Path(sys.argv[1])
prior = Path('Exe/RuntimeRestorationFinalSession-20261002/loaded-objects-2/route-1')
name = 'room41-raw4-click'
for suffix in ('-calls.jsonl', '-object-selections.jsonl', '-correspondence.jsonl'):
    def rows(path): return [json.loads(line) for line in path.open(encoding='utf-8')]
    assert rows(out/'route-1'/(name+suffix)) == rows(prior/(name+suffix)), suffix
print('Separate raw4 calls/selections/every structural update exact to a3a90ec; IDs and estimates preserved.')
