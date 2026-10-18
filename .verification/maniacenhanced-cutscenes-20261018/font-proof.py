"""Read-only native font evidence; prints selected glyphs, ships no game assets."""
import hashlib
import pathlib
import sys

data = pathlib.Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(data).hexdigest().upper() == "AF7A7F667789E4FCA76F4C195BB2996F622D50E796E6CF01E64E4D0A1E0D477A"
font = data[64116:65140]
assert hashlib.sha256(font).hexdigest().upper() == "336B4DFC32EB17CDF6BA78D9116C7F219605807D9573F185A3CDB0340652813A"
print("Verified native font: offset64116,128glyphs,8rows each")
for slot in (60,61,62,91,92,93,94,123,124,125,126):
    glyph = font[slot*8:slot*8+8]
    print(f"slot{slot} {chr(slot)}: {glyph.hex()}")
    for row in glyph:
        print("".join("#" if row & (128 >> column) else "." for column in range(8)))
