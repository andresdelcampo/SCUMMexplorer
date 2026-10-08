using System.Collections;
using System.Reflection;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

internal static class StateTransitions
{
    public static void Run(string output)
    {
        foreach (var (name, folder, boy, gate, seen, input) in new[] {
            ("ega-en", "Games/loom ega mt32 en", 562, "bit[0xD7A]", "bit[0xDCF]", "bit[0xD6D]"),
            ("ega-es", "Games/ALTERNATIVE LOOMS/loom ega mt32 es", 561, "bit[0xD7B]", "bit[0xDD0]", "bit[0xD6E]"),
            ("vga-en", "Games/loom vga en", 487, "bit[0x01B]", "bit[0x070]", "bit[0x00E]") })
        {
            var game = new GameFolderScanner().Scan(folder);
            var data = ScummDataSources.For(game);
            var frame = 0;
            var phase = "construction";
            LoomExplorationSession? session = null;
            var samples = new List<object>();
            var frames = new List<object>();
            var checkpoints = new List<object>();
            var results = new List<object>();
            var checks = new List<string>();
            var reached = new List<(string Phase, LoomScriptObservation Observation)>();
            object? Field(object value, string key) => value.GetType().GetField(key, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(value);
            object? Prop(object value, string key) => value.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(value);
            string Backing(string key) => $"var[0x{Convert.ToInt32(key[6..^1], 16) >> 4:X3}]";
            ushort? BitValue(string key)
            {
                var values = session!.Snapshot.KnownGlobalVariables;
                if (values.TryGetValue(key, out var value)) return value;
                if (values.ContainsKey(LoomBoundedScriptExecutor.SeparateBitStorage)) return 0;
                var bit = Convert.ToInt32(key[6..^1], 16);
                return values.TryGetValue(Backing(key), out var word) ? (ushort)((word >> (bit & 15)) & 1) : null;
            }
            object State()
            {
                var s = session!;
                var runtime = Field(s, "_runtime")!;
                var pending = ((IEnumerable)Field(s, "_pending")!).Cast<object>().Where(p =>
                    ((LoomScriptDisassembly)Prop(p, "Script")!).Context == "Room 34 local script 200").Select(p => new {
                    script = ((LoomScriptDisassembly)Prop(p, "Script")!).Context ?? $"global {((LoomScriptDisassembly)Prop(p, "Script")!).ScriptId}",
                    offset = Prop(p, "Offset"), room = Prop(p, "RoomId"), number = Prop(p, "ScriptNumber")
                }).ToArray();
                var a = s.Actors.Single(a => a.Number == 1);
                return new { frame, phase, room = s.CurrentRoomId, a.X, a.Y, a.WalkBox, a.Moving,
                    s.CameraX, s.CameraEstimated, cameraRange = new { Min = s.CameraRange?.Min, Max = s.CameraRange?.Max }, s.InCutscene, s.Messages.HaveMsg,
                    userPut = Prop(runtime, "UserPut"), pending,
                    bits = new[] { gate, seen, input }.ToDictionary(k => k, BitValue),
                    boxes = s.CurrentWalkBoxes?.Boxes.Select(b => new { b.Index, flags = s.CurrentWalkBoxes.GetFlags(b.Index) }).ToArray() };
            }
            var observer = new LoomScriptObserver(o => {
                if (session is not null)
                {
                    reached.Add((phase, o));
                    samples.Add(new { frame, phase, observation = o, state = State() });
                }
            }, includeSite: site => (site.Script is 85 or 97 or 98 || site.Context?.StartsWith("Room 34 ") == true ||
                    site.Context == "Room 46 entry script") &&
                (site.Stage != "instruction" || site.Operation is "stopScript" or "startScript" or "chainScript" or "cutscene" or "endCutscene" or
                    "roomOps" or "matrixOps" or "setBoxFlags" or "putActor" or "walkActorTo" or "getActorWalkBox" ||
                    site.Operands?.Contains("bit[") == true || site.Context == "Room 34 local script 200"),
                includeKey: k => new[] { gate, seen, input, Backing(gate), Backing(seen), Backing(input),
                    LoomBoundedScriptExecutor.SeparateBitStorage, "var[0x002]", "var[0x011]", "var[0x012]", "var[0x102]" }.Contains(k) || k.StartsWith("local["),
                captureSourceAccess: true);
            session = new LoomExplorationSession(game, game.Vga?.ReadIndexedObjects() ??
                new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")),
                completeBoot: game.Id == GameId.Loom, observer: observer);
            bool MonitorActive() => ((IEnumerable)Field(session, "_pending")!).Cast<object>()
                .Any(p => ((LoomScriptDisassembly)Prop(p, "Script")!).Context == "Room 34 local script 200");
            bool BitIs(string key, ushort expected) => BitValue(key) == expected;
            void Require(bool condition, string label)
            {
                if (!condition) throw new InvalidDataException(name + ": " + label);
                checks.Add(label);
            }
            void Tick() { frames.Add(State()); session.AdvanceFrame(); frame++; }
            bool Settle(string label, int marker, int bound = 6000)
            {
                phase = label;
                var quiet = 0;
                var start = frame;
                for (; frame - start < bound && quiet < 16;)
                {
                    Tick();
                    quiet = session.GameStatus.CanEdit && !session.ActorsMoving ? quiet + 1 : 0;
                }
                var trace = session.SessionTrace.Skip(marker).ToArray();
                var boundaries = trace.Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
                var starts = boundaries.Count(t => t.StartsWith("cutscene at "));
                var ends = boundaries.Count(t => t.StartsWith("endCutscene at "));
                var settled = quiet == 16;
                results.Add(new { label, frames = frame - start, settled, starts, ends, boundaries, trace, state = State() });
                checkpoints.Add(State());
                Console.WriteLine($"{name} {label}: {frame-start} frames, settled {settled}, {starts}/{ends} boundaries, camera {session.CameraX} range {session.CameraRange}");
                return settled && starts == ends;
            }
            phase = "fresh-room34";
            session.VisitRoomById(34);
            Require(Settle("fresh-room34", session.SessionTrace.Count), "fresh Room 34 settles");
            Require(MonitorActive() && BitIs(gate, 0) && BitIs(seen, 0) && BitIs(input, 0), "fresh monitor has the three source guards clear");
            Require(session.CurrentWalkBoxes?.GetFlags(6) == 0x80, "fresh eligible monitor has box 6 hidden");
            phase = "wake-input";
            var marker = session.SessionTrace.Count;
            session.ActivateObjectVerb(boy, 0x44);
            var wake = Settle("wake", marker);
            Require(wake && !MonitorActive() && BitIs(seen, 1), "wake settles with monitor absent and completion flag set");
            var stop = reached.FirstOrDefault(r => r.Observation.Site is { Stage: "instruction", Script: 85, Offset: 10, Operation: "stopScript" });
            var mark = reached.FirstOrDefault(r => r.Observation.Site is { Stage: "instruction", Script: 85, Offset: 12, Operation: "move" });
            Require(stop.Observation is not null && mark.Observation is not null && stop.Observation.Sequence < mark.Observation.Sequence,
                "wake executes Script 85 stop before its completion-flag write");
            if (wake && name == "ega-en")
            {
                // Existing Firewood/ghost sequence, retained as a prepared preview. The
                // visit/staff steps are disclosed estimates; these are direct callbacks,
                // not a reproduction of native casting or an original-game route.
                marker = session.SessionTrace.Count;
                phase = "swap-input";
                session.ActivateObjectVerb(boy, 0x4C);
                if (Settle("swap", marker))
                {
                    phase = "existing-firewood-preview";
                    LoomReplayStep[] steps = [new("visit", 36), new("staff", 1), new("frames", Frames: 8),
                        new("verb", 600, 0x38), new("frames", Frames: 32), new("click", X: 109, Y: 102),
                        new("scene", EndBoundary: "endCutscene at Script 86 +0x0536", ExpectedRoom: 38),
                        new("verb", 611, 0x36), new("scene", EndBoundary: "endCutscene at Script 87 +0x0984", ExpectedRoom: 38),
                        new("visit", 35), new("frames", Frames: 8), new("visit", 34), new("frames", Frames: 40)];
                    var sequence = new LoomReplaySequence(session, steps);
                    var routeStart = frame;
                    while (sequence.Continue() && frame - routeStart < 12000) { Tick(); sequence.FrameAdvanced(); }
                    results.Add(new { label = phase, sequence.Complete, sequence.Failure, state = State() });
                    checkpoints.Add(State());
                    if (sequence.Complete && session.GameStatus.CanEdit)
                    {
                        phase = "prepared-room46-entry";
                        marker = session.SessionTrace.Count;
                        session.VisitRoomById(46);
                        Require(Settle("prepared-room46-entry", marker), "prepared Room 46 entry settles");
                        phase = "return-input";
                        marker = session.SessionTrace.Count;
                        session.ActivateObjectVerb(685, 0x36);
                        Require(Settle("source-room46-return", marker), "source Room 46 return settles with balanced boundaries");
                        Require(BitIs(gate, 1) && !MonitorActive() && session.CurrentWalkBoxes?.GetFlags(6) == 0x80,
                            "source return has late flag set, monitor absent and box 6 hidden after replacement");
                        Require(session.CameraRange is { Min: 160, Max: 160 }, "late return camera is locked at 160 before ghost interaction");
                        marker = session.SessionTrace.Count;
                        phase = "ghost-input";
                        session.ActivateObjectVerb(560, 0x37);
                        if (Settle("ghost", marker))
                        {
                            Require(session.CameraRange is { Min: 160, Max: 464 } &&
                                session.CurrentWalkBoxes?.GetFlags(10) == 0 && session.CurrentWalkBoxes.GetFlags(11) == 0,
                                "ghost interaction opens camera range and boxes 10/11");
                            Require(!MonitorActive() && BitIs(gate, 1) && BitIs(seen, 1), "ghost interaction does not restart the early monitor");
                            marker = session.SessionTrace.Count;
                            phase = "public-east-walk";
                            session.WalkEgoTo(378, 134);
                            Require(Settle("public-east-walk", marker) && session.CameraX > 160, "public eastward walk scrolls after ghost interaction");
                            marker = session.SessionTrace.Count;
                            phase = "restore-input";
                            session.ActivateObjectVerb(587, 0x4D);
                            Require(Settle("restore", marker) && session.CurrentWalkBoxes?.GetFlags(6) == 0 && !MonitorActive(),
                                "restoration leaves box 6 enabled with monitor absent");
                            Require(reached.Any(r => r.Phase == "restore" && r.Observation.Site is
                                { Stage: "instruction", Context: "Room 34 local script 209", Operation: "setBoxFlags", Offset: 0x399 }),
                                "restoration reaches local 209 source box-6 write");
                        }
                        else throw new InvalidDataException("Prepared ghost interaction did not settle.");
                    }
                    else throw new InvalidDataException("Existing Firewood/ghost prerequisite failed: " + sequence.Failure);
                }
                else throw new InvalidDataException("Prepared swap did not settle.");
            }
            File.WriteAllText(Path.Combine(output, name + "-transitions.json"), JsonSerializer.Serialize(new {
                limits = "Bounded Explorer prepared preview. Catalog arrival, staff and route estimates remain visible; direct verbs are not native draft casting. No private state, box injection, placement or forced scene boundary was applied.",
                checkpoints, results, samples, frames,
                checks,
                actorEstimates = session.Actors.Select(a => new { a.Number, a.Estimate }).ToArray(),
                uncertainties = session.Snapshot.Uncertainties.ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"{name}: {checks.Count} source/state assertions passed.");
        }
    }
}
