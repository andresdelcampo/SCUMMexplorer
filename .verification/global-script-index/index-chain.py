# Walks each LFL file's resource chain (16-bit length + tag, XOR 0xFF) and
# reports 00.LFL directory entries whose offset is not a resource start,
# with the resource that contains the offset and the entry that owns it.
import os, struct, sys
root = sys.argv[1] if len(sys.argv) > 1 else "Games/loom"
def lfl(n): return bytes(b ^ 0xFF for b in open(os.path.join(root, "%02d.LFL" % n), "rb").read())
d = lfl(0); p = 0
magic, nobj = struct.unpack_from("<HH", d, p); p += 4 + nobj * 4
entries = {}
for kind in ["room", "costume", "script", "sound"]:
    n = d[p]; p += 1
    rooms = list(d[p:p + n]); p += n
    offs = list(struct.unpack_from("<%dH" % n, d, p)); p += 2 * n
    for i in range(n):
        if offs[i] != 0xFFFF:
            entries.setdefault((i if kind == "room" else rooms[i], offs[i]), []).append("%s %d" % (kind, i))
chains = {}
for (r, o), names in sorted(entries.items()):
    if not os.path.exists(os.path.join(root, "%02d.LFL" % r)):
        print("%02d.LFL missing: %s" % (r, ", ".join(names))); continue
    if r not in chains:
        f = lfl(r); q = 0; chain = []
        while q + 2 <= len(f):
            ln = struct.unpack_from("<H", f, q)[0]
            if ln < 4: break
            chain.append((q, ln)); q += ln
        chains[r] = (chain, q == len(f))
    chain, exact = chains[r]
    if any(s == o for s, _ in chain): continue
    inside = [(s, l) for s, l in chain if s < o < s + l]
    owner = entries.get((r, inside[0][0]), ["?"]) if inside else ["?"]
    print("%s: %02d.LFL 0x%04X is not a resource start; inside 0x%04X (+%d bytes), owned by %s; chain covers file exactly: %s"
          % (", ".join(names), r, o, inside[0][0] if inside else -1, o - inside[0][0] if inside else -1, ", ".join(owner), exact))
