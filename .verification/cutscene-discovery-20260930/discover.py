"""Conservative cutscene candidate inventory from the decoded four-column code.tsv.

Usage: python discover.py <code.tsv> <output.json>
Edges and room operands are possibilities, not validated starts or reachability.
"""
import json
import hashlib
import re
import sys
from collections import defaultdict

owners = defaultdict(list)
incoming = defaultdict(list)
unresolved = []
for line in open(sys.argv[1], encoding="utf-8-sig"):
    columns = line.rstrip("\n").split("\t", 3)
    if len(columns) != 4:
        continue
    owner, offset, operation, operands = columns
    row = dict(owner=owner, offset=offset, operation=operation, operands=operands)
    owners[owner].append(row)
for rows in list(owners.values()):
  for row in rows:
    owner, operation, operands = row["owner"], row["operation"], row["operands"]
    if operation in ("startScript", "chainScript"):
        # Anchor to the full first operand: a variable script's [ is not its argument list.
        target = re.match(r"^(0x[0-9A-Fa-f]+|\d+)(?:\s*,|$)", operands)
        if target:
            number = int(target[1], 0)
            global_owner = f"script {number:03d}"
            room = re.match(r"Room (\d+)", owner)
            local_owner = f"Room {room[1]} local {number}" if room else ""
            if global_owner in owners:
                incoming[global_owner].append(row)
            elif local_owner in owners:
                incoming[local_owner].append(row)
            else:
                unresolved.append(row)
        else:
            unresolved.append(row)

def callers(owner):
    result, seen, queue = [], {owner}, [owner]
    while queue:
        for row in incoming[queue.pop(0)]:
            result.append(row)
            if row["owner"] not in seen:
                seen.add(row["owner"])
                queue.append(row["owner"])
    return result

candidates = []
for owner, rows in owners.items():
    sites = [r for r in rows if r["operation"] == "cutscene"]
    if not sites:
        continue
    reads = [r for r in rows if r["operation"] in (
        "isEqual", "isNotEqual", "isGreater", "isLess", "isGreaterEqual", "isLessEqual",
        "equalZero", "notEqualZero", "getActorWalkBox", "getState", "getOwner")]
    candidates.append(dict(owner=owner, cutscene_sites=sites,
        possible_callers=callers(owner),
        possible_room_commands=[r for r in rows if r["operation"] in ("loadRoom", "loadRoomWithEgo")],
        state_reads_to_review=reads,
        status="Unverified candidate: path, input, required state and scene boundaries need validation"))

output = dict(input_sha256=hashlib.sha256(open(sys.argv[1], "rb").read()).hexdigest(), owner_count=len(owners), candidate_owner_count=len(candidates),
    cutscene_site_count=sum(len(c["cutscene_sites"]) for c in candidates),
    unresolved_script_targets=unresolved, candidates=candidates)
with open(sys.argv[2], "w", encoding="utf-8") as stream:
    json.dump(output, stream, indent=2, ensure_ascii=False)
print(f"{len(candidates)} owners, {output['cutscene_site_count']} cutscene sites; {len(unresolved)} unresolved targets or scopes")
with open(sys.argv[2] + ".md", "w", encoding="utf-8") as stream:
    stream.write("# Decoded cutscene inventory\n\nCandidates, not playable scenes. No path/state validation implied.\n\n")
    stream.write(f"Input SHA256: {output['input_sha256']}\n\n235 sites, 216 candidate owners; unresolved targets/scopes are retained in JSON.\n\n")
    stream.write("| Owner | Cutscene offsets | Direct possible callers | Room commands in owner | State reads |\n|---|---|---|---|---|\n")
    for candidate in candidates:
        stream.write("| " + " | ".join([
            candidate["owner"], ", ".join(r["offset"] for r in candidate["cutscene_sites"]),
            ", ".join(dict.fromkeys(r["owner"] for r in incoming[candidate["owner"]])) or "None resolved",
            "; ".join(r["operation"] + " " + r["operands"] for r in candidate["possible_room_commands"]) or "None",
            str(len(candidate["state_reads_to_review"]))]) + " |\n")
