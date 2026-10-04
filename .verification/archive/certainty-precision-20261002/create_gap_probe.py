"""Same native level4 recipe, with optional read-only gap snapshots."""
from pathlib import Path
import json
base = Path(__file__).parent
recipes = json.loads(Path('.verification/archive/room41-certainty-20261002/room41.json').read_text())
recipe = next(r for r in recipes if r['id'] == 'room41-raw4-click')
recipe['recordDraftGaps'] = True
(base / 'room41-gaps.json').write_text(json.dumps([recipe], indent=2) + '\n', encoding='utf-8')
