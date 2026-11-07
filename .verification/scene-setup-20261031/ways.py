"""Usage: python ways.py <run> <previous run>
Each scene's way (Game: the game's own sentence; Direct: its verb run directly; AtCutscene) in two runs, per copy: the counts and the scenes that
changed way (7 November 2026: summarise.py counts only the scenes started at their cutscene)."""
import sys, re

run, base = sys.argv[1], sys.argv[2]
games = ["indy3-ega-en", "indy3-vga-en", "monkey1-ega-en", "monkey1-vga-en", "mi2-mt32-en", "indy4-vga-en"]


def read(game, r):
    try:
        lines = open(f"{game}-run{r}.txt", encoding="utf-8", errors="replace").read().splitlines()
    except FileNotFoundError:
        return None
    ways, key = {}, None
    for line in lines:
        m = re.match(r"^(SETUP|ATCUT|FRESH|OK)?\s*(Room \d+ object \d+ verb 0x[0-9A-F]+)", line)
        if m and m.group(1):
            key = m.group(2)
        w = re.search(r"result: way (\w+) lines (\d+) rooms \[([^\]]*)\]", line)
        if w and key:
            ways[key] = (w.group(1), int(w.group(2)), w.group(3))
            key = None
    return ways


for game in games:
    now, before = read(game, run), read(game, base)
    if now is None or before is None:
        continue
    count = lambda ways, way: sum(1 for v in ways.values() if v[0] == way)
    print(f"{game:16} " + "  ".join(f"{w} {count(before, w)}->{count(now, w)}" for w in ("Game", "GameHeldFirst", "Direct", "AtCutscene")))
    for key in sorted(set(now) & set(before)):
        if now[key][0] != before[key][0]:
            print(f"    {key}: {before[key][0]} (lines {before[key][1]}, rooms [{before[key][2]}]) -> {now[key][0]} (lines {now[key][1]}, rooms [{now[key][2]}])")
