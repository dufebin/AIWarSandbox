using Godot;

namespace AIWarSandbox.World;

[GlobalClass]
public partial class MapConfig : Resource
{
    [Export] public int Seed { get; set; } = 1337;
    [Export] public int Size { get; set; } = 128;
    [Export] public float HeightScale { get; set; } = 12.0f;
    [Export] public float NoiseFrequency { get; set; } = 0.02f;
    [Export] public float ObstacleDensity { get; set; } = 0.08f;
    [Export] public int ResourcePointCount { get; set; } = 4;
    [Export] public int EnemySpawnCount { get; set; } = 3;
    [Export] public int FriendlySpawnCount { get; set; } = 3;

    /// <summary>Height of the central tactical plateau (meters).</summary>
    [Export] public float HillHeight { get; set; } = 8.0f;
    /// <summary>Number of cover corridors (lower trenches) crossing the map.</summary>
    [Export] public int CorridorCount { get; set; } = 2;
    /// <summary>Half-extent of the central plateau as fraction of map half-size.</summary>
    [Export] public float PlateauRadius { get; set; } = 0.18f;
    /// <summary>Depth of cover corridors below baseline (meters).</summary>
    [Export] public float CorridorDepth { get; set; } = 3.0f;
    /// <summary>Half-width of cover corridors (meters).</summary>
    [Export] public float CorridorWidth { get; set; } = 4.0f;

    public static MapConfig CreateDefault() => new();

    /// <summary>FirstMap preset: 128m square, seed 1337, 4v4 spawns, 4 resource
    /// points, central plateau, two cover corridors.</summary>
    public static MapConfig CreateFirstMap() => new()
    {
        Size = 128,
        Seed = 1337,
        FriendlySpawnCount = 4,
        EnemySpawnCount = 4,
        ResourcePointCount = 4,
        HillHeight = 8.0f,
        CorridorCount = 2,
        PlateauRadius = 0.18f,
        CorridorDepth = 3.0f,
        CorridorWidth = 4.0f,
        HeightScale = 12.0f,
        NoiseFrequency = 0.02f,
        ObstacleDensity = 0.08f,
    };
}
