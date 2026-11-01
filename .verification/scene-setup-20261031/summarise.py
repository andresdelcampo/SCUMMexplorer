import sys, re, glob
run = sys.argv[1]
base = sys.argv[2] if len(sys.argv) > 2 else None
games = ["indy3-ega-en", "monkey1-ega-en", "monkey1-vga-en", "mi2-mt32-en", "indy4-vga-en"]
def read(g, r):
    try: lines = open(f"{g}-run{r}.txt", encoding="utf-8", errors="replace").read().splitlines()
    except FileNotFoundError: return None
    groups = int(lines[0].split()[0])
    at = set(); nothing = set()
    for i, l in enumerate(lines):
        if l.startswith("ATCUT"):
            key = l[6:].split(" (")[0]
            at.add(key)
            # The result line follows, after the scripts line when MEASURE_SCRIPTS=1.
            result = next((r for r in lines[i + 1:i + 3] if r.strip().startswith("result:")), "")
            if re.search(r"lines 0 rooms \[\]", result): nothing.add(key)
    done = lines[-1].startswith("fresh start")
    return groups, at, nothing, done
tg = ta = tn = 0
for g in games:
    a = read(g, run)
    if not a: continue
    groups, at, nothing, done = a
    tg += groups; ta += len(at); tn += len(nothing)
    line = f"{g:16} {groups:4} own {100*(groups-len(at))/groups:5.1f}%  atcut {100*len(at)/groups:5.1f}% ({len(at)})  nothing {len(nothing)}{'' if done else '  (running)'}"
    if base:
        b = read(g, base)
        if b:
            line += f"  | was atcut {len(b[1])} nothing {len(b[2])}; newly at {sorted(at - b[1])} no longer {sorted(b[1] - at)}"
    print(line)
if tg: print(f"total {tg} own {100*(tg-ta)/tg:.1f}% atcut {100*ta/tg:.1f}% nothing {tn}")
