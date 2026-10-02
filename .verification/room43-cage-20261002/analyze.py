"""Read-only comparisons; write only this investigation's new summary."""
from pathlib import Path
import hashlib
import json
import subprocess
import xml.etree.ElementTree as ET

base = Path("Exe/Room43CageSession-20261002")
old = Path("Exe/RuntimePhasePayloadFinalSession2-20261002")
def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))
def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
def git(*args):
    return subprocess.check_output(["rtk", "proxy", "git", *args], text=True).strip()

routes = []
for verb in ["41", "4B"]:
    before = load(base / f"capture-1/verb-{verb}.json")
    after = load(base / f"capture-final/verb-{verb}.json")
    for key in ["recipe", "frames", "rooms", "final", "boundaries", "events"]:
        assert before[key] == after[key], (verb, key)
    assert len(before["changes"]) == len(after["changes"]) == 42
    for a, b in zip(before["changes"], after["changes"]):
        assert a["frame"] == b["frame"]
        for key in a["facts"]:
            if key not in {"shownPaint", "estimates"}:
                assert a["facts"][key] == b["facts"][key], (verb, key)
        assert a["facts"]["shownPaint"] == [645]
        assert b["facts"]["shownPaint"] == b["facts"]["rawPaint"] == []
        assert b["facts"]["estimates"] == []
        assert b["facts"]["states"]["645"] == 0
        assert 645 in b["facts"]["sourceWritten"]
    raw = base / f"capture-final/verb-{verb}-render-0-raw.bmp"
    shown = base / f"capture-final/verb-{verb}-render-0-shown.bmp"
    assert raw.read_bytes() == shown.read_bytes()
    assert raw.read_bytes() == (base / f"capture-1/verb-{verb}-render-0-raw.bmp").read_bytes()
    routes.append(dict(verb=verb, frames=after["frames"], room=after["final"]["RoomId"],
                       roomChanges=after["rooms"], sourceAndExecutionExact=True,
                       beforePaint=[645], afterPaint=[], imageSha256=sha(shown)))

renders = sorted((base / "room-audit").glob("room-*.bmp"))
changed = [p.name for p in renders if p.read_bytes() != (old / "room-audit-2" / p.name).read_bytes()]
assert len(renders) == 76 and changed == ["room-08.bmp", "room-43.bmp"], changed
assert (base / "room-audit/room-audit.txt").read_text().startswith("Rooms with remaining gaps: 0;")
for relative in ["literal/audit.json", "established.json", "sequences.json"]:
    assert load(base / "audits" / relative) == load(old / "audits-1" / relative), relative
assert (base / "audits/blockers.txt").read_bytes() == (old / "audits-1/blockers.txt").read_bytes()

protected = load(Path("Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json"))
assert len(protected) == 57
assert all(sha(Path(p)) == h for p, h in protected.items())
assert not set(git("diff", "--cached", "--name-only").splitlines()).intersection(protected)
prior = load(Path(".verification/room41-route-20261002/route-summary.json"))
assert all(sha(Path(r["path"])) == r["sha256"] for r in prior["reports"])
assert not git("diff", "627b682", "--name-only", "--", "src/SCUMMexplorer.Core/Formats",
               "src/SCUMMexplorer.App/loom-replays.json", ".verification/room41-certainty-20261002",
               ".verification/room41-route-20261002")

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
counts = ET.parse(base / "final-core.trx").find(".//t:Counters", ns).attrib
assert counts["passed"] == counts["total"] == "657" and counts["failed"] == "0"
names = load(base / "names.json")
assert names["renamed"] == 132 and names["entries"] == 330
app = subprocess.run(["rtk", "proxy", "powershell", "-NoProfile", "-Command",
    "Get-Process -Name SCUMMexplorer.App -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id"],
    capture_output=True, text=True)
pids = [int(p) for p in app.stdout.splitlines() if p.strip()]
summary = dict(checkpoint="627b682", routes=routes, diagnosticCycles=1,
    renderingCorrection=True, nativeRoutesImproved=0, raw4Unchanged=True,
    literalSitesValidated=168, literalSitesTotal=235, corePassed=657,
    literalRootsExact=363, membershipsExact=4, sequencesExact=2, blockers=0,
    roomRenders=76, changedRooms=[8, 43], other74RendersByteExact=True, gaps=0,
    namedResponses=132, protected57ExactAndUnstaged=True, historicalRouteReportsExact=True,
    appPids=pids, appLaunched=False, appInteractedWith=False, pushed=False,
    build="Isolated App Release and final normal main-solution Release succeeded, zero warnings/errors. User App disappeared independently before normal build; no agent launch/close or WPF check.",
    userClarification="Play Cutscene works (user names 18 and 9); manual18 fails before Room42. This is not the unchanged raw4 slot4 recipe.")
Path(".verification/room43-cage-20261002/summary.json").write_text(json.dumps(summary, indent=2) + "\n")
print(json.dumps(summary, indent=2))
