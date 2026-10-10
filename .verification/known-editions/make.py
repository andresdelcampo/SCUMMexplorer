"""Makes src/SCUMMexplorer.Core/Discovery/known-editions.json from the local game copies.
Usage: python .verification/known-editions/make.py   (from the repository root)
Per edition: up to 14 LFL files (taken so that no two editions of a game share the whole sample) with their SHA-256. Raw file bytes are hashed; names are matched case-insensitively."""
import glob, hashlib, json, os, itertools
EDITIONS = [  # (folder, game, label, demo)
 ("Games/loom ega mt32 en", "Loom", "English, Roland upgrade", False),
 ("Games/loom ega en", "Loom", "English", False),
 ("Games/ALTERNATIVE LOOMS/loom ega es", "Loom", "Spanish", False),
 ("Games/ALTERNATIVE LOOMS/loom ega mt32 es", "Loom", "Spanish, Roland upgrade", False),
 ("Games/ALTERNATIVE LOOMS/loom ega fr", "Loom", "French", False),
 ("Games/ALTERNATIVE LOOMS/loom ega de", "Loom", "German", False),
 ("Games/ALTERNATIVE LOOMS/loom ega v1.2 es", "Loom", "Spanish, version 1.2", False),
 ("Games/loom ega it", "Loom", "Italian", False),
 ("Games/ALTERNATIVE LOOMS/loom demo long ega en", "Loom", "English, long demo", True),
 ("Games/ALTERNATIVE LOOMS/loom demo short ega en", "Loom", "English, short demo", True),
 ("Games/FUTURE/indy3 ega en", "IndianaJones3", "English", False),
 ("Games/FUTURE/indy3 ega es", "IndianaJones3", "Spanish", False),
 ("Games/FUTURE/indy3 demo en", "IndianaJones3", "English, demo", True),
]
def hashes(folder):
    return {os.path.basename(f).upper(): hashlib.sha256(open(f, "rb").read()).hexdigest()
            for f in glob.glob(folder + "/*") if f.upper().endswith(".LFL")}
data = [(f, g, l, d, hashes(f)) for f, g, l, d in EDITIONS]
out = []
for game in {e[1] for e in data}:
    group = [e for e in data if e[1] == game]
    for folder, g, label, demo, h in group:
        others = [e for e in group if e[0] != folder]
        # files that tell this edition from each other one, most telling first (a file no other edition shares exactly, then those few share)
        def telling(name):
            return sum(1 for o in others if o[4].get(name) != h[name])
        names = sorted(h, key=lambda n: (-telling(n), n))
        sample = []
        # first make sure every other edition differs from this one in at least two sample files where the data allows, then fill with the most telling
        for need in (1, 2):
            for o in others:
                while sum(1 for n in sample if o[4].get(n) != h[n]) < need:
                    cand = [n for n in names if n not in sample and o[4].get(n) != h[n]]
                    if not cand: break
                    sample.append(cand[0])
        for n in names:
            if len(sample) >= 14: break
            if telling(n) > 0 and n not in sample: sample.append(n)
        # every other edition must differ from this one somewhere in the sample
        for o in others:
            if not any(o[4].get(n) != h[n] for n in sample): print('not told apart by this editions own files (the other one has more files, and ranks higher when it matches all of its own):', label, '<-', o[2])
        out.append({"game": game, "label": label, "demo": demo, "lflFiles": len(h), "files": {n: h[n] for n in sorted(sample)}})
json.dump(out, open("src/SCUMMexplorer.Core/Discovery/known-editions.json", "w"), indent=1)
print(len(out), "editions;", sum(len(e["files"]) for e in out), "hashes")
