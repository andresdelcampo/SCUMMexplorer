using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S4a (14 October 2026): the session's game-independent core is <see cref="ScummSession"/> in the <c>Scumm</c> layer; LOOM's
/// session derives from it. These tests pin the structure: the engine layer never tests which game it runs, and the core runs a
/// game's boot script by itself, with no LOOM code.
/// </summary>
public sealed class ScummSessionArchitectureTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));

    private static string CoreSource() => Path.Combine(SupportMatrixTests.RepositoryRoot(), "src", "SCUMMexplorer.Core");

    [Fact]
    public void TheEngineLayerNeverBranchesOnTheGame()
    {
        // ScummProfile.For is the one place the engine maps a game onto variants (it names IndianaJones3 for the dialect).
        var offenders = Directory.EnumerateFiles(Path.Combine(CoreSource(), "Scumm"), "*.cs", SearchOption.AllDirectories)
            .Where(path => File.ReadAllText(path).Contains("GameId.", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["ScummProfile.cs"], offenders);
    }

    [Fact]
    public void TheEngineLayerDoesNotNameLoomTypes()
    {
        var loomNamespace = "SCUMMexplorer.Core.Loom";
        var offenders = typeof(ScummSession).Assembly.GetTypes()
            .Where(type => type.Namespace == "SCUMMexplorer.Core.Scumm")
            .SelectMany(type => type.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                                                 System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                                                 System.Reflection.BindingFlags.DeclaredOnly)
                .Select(member => (type, member)))
            .Where(item => item.member is System.Reflection.MethodBase or System.Reflection.PropertyInfo or System.Reflection.FieldInfo)
            .Where(item => MemberTypes(item.member).Any(t => t.Namespace == loomNamespace))
            .Select(item => $"{item.type.Name}.{item.member.Name}").ToArray();
        Assert.Empty(offenders);
    }

    private static IEnumerable<Type> MemberTypes(System.Reflection.MemberInfo member) => member switch
    {
        System.Reflection.MethodBase method => method.GetParameters().Select(p => p.ParameterType)
            .Concat(method is System.Reflection.MethodInfo info ? [info.ReturnType] : []),
        System.Reflection.PropertyInfo property => [property.PropertyType],
        System.Reflection.FieldInfo field => [field.FieldType],
        _ => []
    };

    [Fact]
    public void LoomsSessionIsTheGenericSessionPlusLoom()
    {
        Assert.True(typeof(ScummSession).IsAbstract);
        Assert.Equal(typeof(ScummSession), typeof(LoomExplorationSession).BaseType);
        Assert.Equal("SCUMMexplorer.Core.Scumm", typeof(ScummSession).Namespace);
    }

    /// <summary>A session of no game in particular: it adds only what the abstract core asks for.</summary>
    private sealed class BareSession : ScummSession
    {
        public BareSession(GameLibrary game, ScummIndexedObjectSnapshot objects, IScummDataSource data)
            : base(game, objects, 0, null, data, _ => new(ScummSoundLifetimeKind.Unknown, null, false, "bare session")) { }

        protected override ScummSessionSnapshot VisitReachedRoom(GameResource target, ScummObjectActionResult action) =>
            EnterRoom(target, $"Room {target.ResourceId:00} reached by {action.Evidence}", action.ReachedRoomLoad);

        /// <summary>Runs the boot script and the frames until it has ended, as any game's boot does.</summary>
        public int Boot()
        {
            RunBootScript();
            AssumeBootAnswer();
            var frames = 0;
            while (frames < BootFrameLimit && BootGoesOn)
            {
                AdvanceFrame();
                frames++;
            }
            FinishBoot(frames);
            return frames;
        }
    }

    [Fact]
    public void TheGenericSessionBootsALoomEgaGameWithNoLoomCode()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL"));
        var session = new BareSession(game, objects, ScummDataSources.For(game));

        var frames = session.Boot();

        // The same boot LoomExplorationSession's constructor ends with: the opening cutscene plays and script 001 chains on in Room 02.
        Assert.Equal(2, session.Snapshot.RoomId);
        Assert.False(session.BootRunning);
        Assert.Equal(frames, session.BootFrames);
        Assert.Contains("Script 01 chains to script 6", session.SessionTrace);
        Assert.False(session.InCutscene);
    }
}
