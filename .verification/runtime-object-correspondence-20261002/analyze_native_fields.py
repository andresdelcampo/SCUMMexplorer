"""Retain every original native challenge assertion in this new packet."""
from pathlib import Path
source=Path(".verification/runtime-correspondence-20261002/analyze_native_fields.py").read_text(encoding="utf-8")
exec(compile(source,"preserved-native-fields","exec"),{"__file__":__file__,"__name__":"__main__"})
