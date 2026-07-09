using Godot;

namespace AIWarSandbox.World;

/// <summary>
/// Configuration for real-world satellite + elevation ingestion from
/// Gaode (AutoNavi) satellite tiles and AWS Terrarium elevation tiles.
/// </summary>
[GlobalClass]
public partial class MapSource : Resource
{
    /// <summary>North edge of the bounding box (degrees).</summary>
    [Export] public double NorthLat { get; set; } = 40.0421;
    /// <summary>South edge of the bounding box (degrees).</summary>
    [Export] public double SouthLat { get; set; } = 39.8421;
    /// <summary>East edge of the bounding box (degrees).</summary>
    [Export] public double EastLng { get; set; } = 116.4921;
    /// <summary>West edge of the bounding box (degrees).</summary>
    [Export] public double WestLng { get; set; } = 116.2921;

    /// <summary>Slippy-map zoom level (Gaode satellite is typically 1..18).</summary>
    [Export] public int Zoom { get; set; } = 14;

    /// <summary>Vertical scale applied to decoded Terrarium heights when building the mesh.</summary>
    [Export] public float HeightScale { get; set; } = 1.0f;

    /// <summary>If true, the importer fetches Terrarium elevation tiles and the terrain
    /// generator displaces the mesh by the heightmap. If false, terrain stays flat.</summary>
    [Export] public bool UseRealTerrain { get; set; } = true;

    /// <summary>Directory where downloaded tiles are cached (Godot path).</summary>
    [Export] public string CacheDir { get; set; } = "user://map_cache";

    /// <summary>Beijing area bounding box preset (Tiananmen + surroundings).</summary>
    public static MapSource CreateDefault() => new()
    {
        NorthLat = 40.0421,
        SouthLat = 39.8421,
        EastLng = 116.4921,
        WestLng = 116.2921,
        Zoom = 14,
        HeightScale = 1.0f,
        UseRealTerrain = true,
        CacheDir = "user://map_cache",
    };
}
