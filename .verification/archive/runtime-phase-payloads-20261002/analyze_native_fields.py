"""Keep unchanged native challenge assertions in this packet."""
from pathlib import Path
source = Path('.verification/archive/runtime-object-correspondence-20261002/analyze_native_fields.py').read_text(encoding='utf-8')
exec(compile(source, 'preserved-native-field-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})
