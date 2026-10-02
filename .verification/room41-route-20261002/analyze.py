"""Check the bounded raw4 investigation without modifying historical evidence."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(*args):
    return subprocess.run(["rtk", "proxy", "git", *args], check=True,
                          capture_output=True, encoding="utf-8").stdout.strip()


base = Path(__file__).parent
output = Path(sys.argv[1])
allow_user_app = "--allow-user-app" in sys.argv[2:]
historical = Path("Exe/RuntimePhasePayloadFinalSession2-20261002/final-evidence/room41-1")
recipe_path = Path(".verification/room41-certainty-20261002/room41.json")
before_path = historical / "room41-raw4-click.json"
after_path = output / "raw4-1/room41-raw4-click.json"
control_path = historical / "direct41-source-level4-skip204.json"
before, after, control = map(load, [before_path, after_path, control_path])
recipe = next(r for r in load(recipe_path) if r["id"] == "room41-raw4-click")
assert before["route"] == after["route"] == recipe
assert load(output / "raw4-only.json") == [recipe]
assert before_path.read_bytes() == after_path.read_bytes()


def outcome(packet):
    return dict(room=packet["snapshots"][-1]["snapshot"]["RoomId"],
                begun=sum(s.startswith("cutscene at ") for s in packet["boundaries"]),
                ended=sum(s.startswith("endCutscene at ") for s in packet["boundaries"]),
                elapsed=packet["elapsed"],
                script090End=any(e["SourceScriptId"] == 90 and e["Operation"] == "endCutscene"
                                 for e in packet["events"]))


assert outcome(after) == dict(room=41, begun=15, ended=14, elapsed=7659, script090End=False)
assert outcome(control)["room"] == 43 and outcome(control)["script090End"]
assert outcome(control)["begun"] == outcome(control)["ended"] == 15
tail = after["snapshots"][-1]
assert not any(p["number"] == 90 for p in tail["pending"])
assert any(p["freeze"] > 0 for p in tail["pending"])
assert any("possible Room 42 load" in u for u in after["uncertainties"])
native090 = [e for e in after["events"] if e["SourceScriptId"] == 90]
assert [(e["Offset"], e["Operation"], e["Certainty"]) for e in native090] == [
    (0, "cutscene", 1), (5, "loadRoom", 1)]
assert native090[1]["TargetId"] == 42 and native090[1]["Operands"] == "0x2A"

selected_sites = [
    ("Script 22", 0xC3, "chainScript"),
    ("Script 26", 0x7F, "startScript"),
    ("Room 41 Object 633 Verb 0x41", 0, "startScript"),
    ("Room 41 local script 204", 0x31E, "endCutscene"),
    ("Room 41 local script 204", 0x31F, "chainScript"),
    ("Script 90", 0, "cutscene"),
    ("Script 90", 5, "loadRoom"),
]
cast = next(s for s in after["snapshots"] if s["stage"] == "draft-4-before")


def select_events(packet, start):
    return [dict(eventIndex=i, source=e["SourceLabel"], offset=f"{e['Offset']:04X}",
                 operation=e["Operation"], certainty=e["Certainty"],
                 estimatedArgumentIndexes=e["EstimatedArgumentIndexes"])
            for i, e in enumerate(packet["events"]) if i >= start and
            (e["SourceLabel"], e["Offset"], e["Operation"]) in selected_sites]


native_chain = select_events(after, cast["eventCount"])
assert len(native_chain) == 7 and all(e["certainty"] == 1 for e in native_chain)
control_chain = select_events(control, 0)
assert all(e["certainty"] == 0 for e in control_chain if e["source"] in
           {"Room 41 Object 633 Verb 0x41", "Room 41 local script 204", "Script 90"})
keys = ["bit[0xD6C]", "bit[0xD67]", "bit[0xD70]", "var[0x064]", "var[0x067]",
        "var[0x062]", "var[0x07A]", "var[0x03A]", "var[0x03B]"]
precast = {k: dict(value=cast["snapshot"]["KnownGlobalVariables"][k],
                  estimated=k in cast["estimatedVariables"]) for k in keys}
assert all(precast[k]["estimated"] for k in keys[:-1])
assert precast["var[0x03B]"] == dict(value=16961, estimated=False)

code_path = Path("Exe/VerificationSession-20261001/native-code-1/code.tsv")
assert sha(code_path) == "0d0f8af036d1dc8ce0ebc80fbf1bfd3090f160e926803890b06ec34e9625c569"
code = code_path.read_text(encoding="utf-8").splitlines()
source_sites = [("Room 41 local 204", "+0x031E", "endCutscene"),
                ("Room 41 local 204", "+0x031F", "chainScript"),
                ("script 090", "+0x0000", "cutscene"),
                ("script 090", "+0x0005", "loadRoom"),
                ("script 090", "+0x0702", "loadRoom"),
                ("script 090", "+0x0730", "endCutscene")]
for site in source_sites:
    assert sum(tuple(line.split("\t")[:3]) == site for line in code) == 1, site

protected = load(Path("Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json"))
assert protected == load(output / "protected-before.json") and len(protected) == 57
assert all(sha(Path(name)) == digest for name, digest in protected.items())
assert not set(git("diff", "--cached", "--name-only").splitlines()).intersection(protected)
assert not git("diff", "55cb5ec", "--name-only", "--", "src", "tests")
changed_verification = git("diff", "55cb5ec", "--name-only", "--", ".verification").splitlines()
assert all(p.startswith(".verification/room41-route-20261002/") or
           p == ".verification/verb-area/VerbAreaCheck/Program.cs" for p in changed_verification)
git("merge-base", "--is-ancestor", "55cb5ec", "HEAD")
app = subprocess.run(["rtk", "proxy", "powershell", "-NoProfile", "-Command",
                      "Get-Process -Name SCUMMexplorer.App -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id"],
                     capture_output=True, encoding="utf-8")
app_ids = [int(line) for line in app.stdout.splitlines() if line.strip()]
assert allow_user_app or not app_ids, app.stdout

summary = dict(
    checkpoint="55cb5ec", freshOutput=output.as_posix(), diagnosticCycles=2,
    productionChanged=False, newWorkingRoutes=0, recipeUnchanged=True,
    fullPacketByteExact=True, before=outcome(before), after=outcome(after),
    historicalDirectControl=outcome(control), historicalControlRerun=False,
    nativeSelectedEvents=native_chain, historicalControlSelectedEvents=control_chain,
    finalCastState=precast,
    finalPending=[dict(label=p["label"], number=p["number"], offset=p["offset"],
                       freeze=p["freeze"], pathEstimated=p["pathEstimated"]) for p in tail["pending"]],
    observationLimit="Event path and packet equality; no new call/return/completion or lifetime proof.",
    all57ProtectedFilesExactAndUnstaged=True, previousVerificationPacketsUnchanged=True,
    userRequestedAppLeftOpen=allow_user_app, appPidsAtCheck=app_ids,
    appLaunched=False, pushPerformed=False,
    runtimeBaseline="Prior Core654/focused328 and broad regressions are dated; not rerun.",
    literalCoverage=dict(validated=168, total=235, unvalidated=67),
    reports=[dict(path=p.as_posix(), sha256=sha(p)) for p in
             [recipe_path, before_path, after_path, control_path, code_path]],
)
(base / "route-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
print("Room41 raw4: before=after Room41 15/14, 7659 frames; entire packet byte-exact; "
      f"2 diagnostic cycles; 0 new working routes; 57 protected files exact; App count={len(app_ids)}.")
