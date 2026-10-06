namespace SCUMMexplorer.Core.Rendering;

/// <summary>UI-independent, indexed-color room image ready for palette expansion.</summary>
public sealed record RoomImage(int Width, int Height, ReadOnlyMemory<byte> Pixels, IReadOnlyList<uint> Palette)
{
    public RoomDepthMask? DepthMask { get; init; }

    /// <summary>Mask planes 2 and up (the CD game's rooms carry several; a walk box's mask byte picks the plane that hides an actor standing in it).</summary>
    public IReadOnlyList<RoomDepthMask> ExtraDepthMasks { get; init; } = [];

    public bool IsValid => Width > 0 && Height > 0 && Pixels.Length == Width * Height && Palette.Count > 0 &&
                           (DepthMask is null || DepthMask.IsValid && DepthMask.Width == Width && DepthMask.Height == Height);
}

/// <summary>One-bit-per-pixel room occlusion data, expanded to byte values 0 or 1.</summary>
public sealed record RoomDepthMask(int Width, int Height, ReadOnlyMemory<byte> Pixels)
{
    public bool IsValid => Width > 0 && Height > 0 && Pixels.Length == Width * Height;
}

public interface IRoomBackgroundDecoder
{
    bool CanDecode(string filePath, int scummVersion);
    ValueTask<RoomImage> DecodeAsync(string filePath, CancellationToken cancellationToken = default);
}
