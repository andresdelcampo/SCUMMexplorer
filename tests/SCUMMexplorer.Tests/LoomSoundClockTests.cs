using System.Buffers.Binary;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomSoundClockTests
{
    [Fact]
    public void NativeEndsExcludeEnvelopeReleaseAndLoopsNeverExpire()
    {
        // Startup is 40 ticks: ceil(40 * 473 / (192 * 2)) = 50 callbacks.
        var finite = Lifetime([0xFF, 0x2F]);
        Assert.Equal(new(ScummSoundLifetimeKind.Finite, 50, true), finite);
        Assert.Equal(ScummSoundLifetimeKind.Looping, Lifetime([0x90, 60, 64, 4, 0xFF, 0x2F], loop: true).Kind);
        Assert.Equal(ScummSoundLifetimeKind.Unknown, Lifetime([0xFF, 0x2F], loop: true).Kind);
        Assert.Equal(ScummSoundLifetimeKind.Unknown, Lifetime([0x90]).Kind);
        Assert.Equal(ScummSoundLifetimeKind.Unknown, Lifetime([0xFF, 0x2F], loop: true, loopOffset: 50).Kind);
        // A loop target can be inside the resource but point at an invalid command.
        Assert.Equal(ScummSoundLifetimeKind.Unknown, Lifetime([0xFF, 0x2F, 0x01, 0, 0], loop: true, loopOffset: 2).Kind);

        var clock = new ScummSoundClock(id => id == 1 ? finite : new(ScummSoundLifetimeKind.Looping, null, true));
        clock.Start(1, false); clock.Start(2, false);
        Assert.Empty(clock.Advance(6)); // 47.2 callbacks at 0.1 s: still active.
        Assert.True(clock.Status(1).Running);
        Assert.Equal(new[] { 1 }, clock.Advance(1));
        Assert.False(clock.Status(1).Running); Assert.Null(clock.Status(1).Estimate);
        clock.Advance(60 * 1000); Assert.True(clock.Status(2).Running);
    }

    [Fact]
    public void RestartStopOverlapAndUnknownCertaintyAreIndependent()
    {
        var clock = new ScummSoundClock(id => id == 3 ? new(ScummSoundLifetimeKind.Unknown, null, false, "malformed") :
            new(ScummSoundLifetimeKind.Finite, 472, false));
        clock.Start(1, false); clock.Advance(30); clock.Start(2, true); clock.Start(1, false);
        Assert.Empty(clock.Advance(30));
        Assert.Equal(new[] { 1, 2 }, clock.Advance(30));
        Assert.Null(clock.Status(1).Estimate); Assert.NotNull(clock.Status(2).Estimate);
        clock.Start(3, false); clock.Advance(60000);
        Assert.Equal((true, "malformed"), clock.Status(3));
        clock.Stop(3); Assert.Equal((false, (string?)null), clock.Status(3));
        clock.Start(2, true); clock.StopAll(); Assert.Null(clock.Status(2).Estimate);
        Assert.False(clock.Status(0).Running);
    }

    [Fact]
    public void NativeEffectsAndAllSuppliedMusicHaveExplicitLifetimes()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var resources = new GameFolderScanner().Scan(folder).Resources
            .Where(r => r.Kind == ResourceKind.Sound && r.ResourceId.HasValue).ToDictionary(r => r.ResourceId!.Value);
        var decoder = new ScummAdlibAudio();
        foreach (var id in Enumerable.Range(1, 32).Where(id => id != 24))
        {
            var lifetime = decoder.ReadLifetime(resources[id]);
            Assert.NotEqual(ScummSoundLifetimeKind.Unknown, lifetime.Kind);
            if (lifetime.Kind == ScummSoundLifetimeKind.Finite) Assert.True(lifetime.Callbacks > 0);
        }
        Assert.Equal(ScummSoundLifetimeKind.Unknown, decoder.ReadLifetime(resources[24]).Kind);
        Assert.Equal(ScummSoundLifetimeKind.Unknown, decoder.ReadLifetime(resources[33]).Kind);
        Assert.Equal(ScummSoundLifetimeKind.Looping, decoder.ReadLifetime(resources[11]).Kind);
        Assert.True(decoder.ReadLifetime(resources[31]).Callbacks > 472 * 180);
    }

    [Fact]
    public void NativeCompletionLetsTheActualSoundHelperEndWithoutSeedTaint()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 4));
        session.ActivateObjectVerb(177, 0x3A);
        Assert.True(session.InCutscene);
        for (int i = 0; i < 300 && session.InCutscene; i++) session.AdvanceFrame();
        Assert.False(session.InCutscene);
        Assert.Equal((ushort)0, session.Snapshot.KnownGlobalVariables["var[0x0A4]"]);
        Assert.DoesNotContain(23, session.Snapshot.ActiveSounds);
        Assert.Contains(session.SessionTrace, line => line.Contains("Sound 23 completed on its native"));
        Assert.Contains(session.SessionTrace, line => line.StartsWith("endCutscene at "));
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("sound 23", StringComparison.OrdinalIgnoreCase));
    }

    private static ScummSoundLifetime Lifetime(byte[] events, bool loop = false, ushort loopOffset = 0)
    {
        var bytes = new byte[26 + 0x93 + events.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, 22);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 22);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(22), (ushort)(bytes.Length - 22));
        bytes[28] = 0x80; bytes[29] = 192; bytes[30] = loop ? (byte)0 : (byte)1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(31), loopOffset);
        events.CopyTo(bytes, 26 + 0x93);
        for (int i = 0; i < bytes.Length; i++) bytes[i] ^= 0xFF;
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, bytes);
            return new ScummAdlibAudio().ReadLifetime(new("fixture", ResourceKind.Sound, path, ContainerFileNumber: 1, Offset: 0));
        }
        finally { File.Delete(path); }
    }
}
