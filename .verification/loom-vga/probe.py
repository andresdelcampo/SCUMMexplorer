# Read-only probe of the extracted LOOM CD files (run iso_extract.py <Loom.bin> <outdir> first).
# Usage: python probe.py <outdir>
import struct, sys, collections
out = sys.argv[1]
def blocks(d, start, end):
    p = start; r = []
    while p + 6 <= end:
        sz = struct.unpack_from('<I', d, p)[0]; tag = d[p+4:p+6].decode('latin1')
        if sz < 6 or p + sz > end: r.append(('??', p, sz, tag)); break
        r.append((tag, p, sz)); p += sz
    return r
d = bytes(b ^ 0x69 for b in open(out + '/DISK01.LEC', 'rb').read())      # the .LEC is XOR 0x69
top = blocks(d, 0, len(d))[0]; inner = blocks(d, 6, top[2])
lf = [t for t in inner if t[0] == 'LF']
print('LE', top, '| FO + %d LF room blocks' % len(lf))
tags = collections.Counter(); sizes = {}
for t in lf:
    room = struct.unpack_from('<H', d, t[1] + 6)[0]
    for b in blocks(d, t[1] + 8, t[1] + t[2]):
        tags[b[0]] += 1
        if b[0] == 'RO':
            for c in blocks(d, b[1] + 6, b[1] + b[2]): sizes.setdefault(c[0], 0); sizes[c[0]] += c[2]
print('room-level blocks:', dict(tags)); print('RO children total bytes:', sizes)
raw = open(out + '/000.LFL', 'rb').read()                                 # index: plain tags, plain data
print('index blocks:', blocks(raw, 0, len(raw)))
