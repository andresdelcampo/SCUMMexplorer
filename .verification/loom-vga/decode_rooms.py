import struct, sys, zlib

d = bytes(b ^ 0x69 for b in open('en/DISK01.LEC', 'rb').read())


def blocks(d, s, e):
    p = s; r = []
    while p + 6 <= e:
        z = struct.unpack_from('<I', d, p)[0]; t = d[p+4:p+6].decode('latin1')
        if z < 6 or p + z > e: break
        r.append((t, p, z)); p += z
    return r


top = blocks(d, 0, len(d))[0]
lf = [t for t in blocks(d, 6, top[2]) if t[0] == 'LF']


def decode_strip(d, pos, h):
    code = d[pos]; pos += 1
    out = [[0] * 8 for _ in range(h)]
    if code == 1:
        for x in range(8):
            for y in range(h): out[y][x] = d[pos]; pos += 1
        return out
    vertical = 0x0E <= code <= 0x12
    horizontal = 0x18 <= code <= 0x1C
    if not (vertical or horizontal): raise ValueError(hex(code))
    shr = code % 10; mask = 0xFF >> (8 - shr)
    st = {'color': d[pos], 'bits': d[pos+1], 'cl': 8, 'inc': -1, 'pos': pos + 2}

    def fill():
        if st['cl'] <= 8:
            st['bits'] |= d[st['pos']] << st['cl']; st['pos'] += 1; st['cl'] += 8

    def rb():
        st['cl'] -= 1; b = st['bits'] & 1; st['bits'] >>= 1; return b
    outer, inner = (8, h) if vertical else (h, 8)
    for a in range(outer):
        for b in range(inner):
            fill()
            if vertical: out[b][a] = st['color']
            else: out[a][b] = st['color']
            if not rb(): pass
            elif not rb():
                fill(); st['color'] = st['bits'] & mask; st['bits'] >>= shr; st['cl'] -= shr; st['inc'] = -1
            elif not rb(): st['color'] = (st['color'] + st['inc']) & 0xFF
            else:
                st['inc'] = -st['inc']; st['color'] = (st['color'] + st['inc']) & 0xFF
    return out


def png(path, w, h, pal, px):
    def ch(t, b):
        return struct.pack('>I', len(b)) + t + b + struct.pack('>I', zlib.crc32(t + b))
    raw = b''.join(b'\0' + bytes(r) for r in px)
    open(path, 'wb').write(b'\x89PNG\r\n\x1a\n' + ch(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 3, 0, 0, 0))
                           + ch(b'PLTE', bytes(pal)) + ch(b'IDAT', zlib.compress(raw)) + ch(b'IEND', b''))


def room(i):
    t = lf[i]
    ro = [b for b in blocks(d, t[1] + 8, t[1] + t[2]) if b[0] == 'RO'][0]
    ch = {c[0]: c for c in blocks(d, ro[1] + 6, ro[1] + ro[2])}
    w, h = struct.unpack_from('<HH', d, ch['HD'][1] + 6)
    pa = ch['PA'][1] + 6; pal = d[pa+2:pa+2+768]
    bm = ch['BM'][1] + 6
    offs = [struct.unpack_from('<I', d, bm + 4 + 4 * s)[0] for s in range(w // 8)]
    px = [[0] * w for _ in range(h)]
    for s, o in enumerate(offs):
        st = decode_strip(d, bm + o, h)
        for y in range(h):
            for x in range(8): px[y][s*8+x] = st[y][x]
    return struct.unpack_from('<H', d, t[1] + 6)[0], (w, h, pal, px)


if __name__ == '__main__':
    for i in map(int, sys.argv[1:]):
        n, (w, h, pal, px) = room(i)
        png(f'room{n:02d}.png', w, h, pal, px); print(n, w, h)
