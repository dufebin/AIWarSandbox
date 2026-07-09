using Godot;

namespace AIWarSandbox.World;

public partial class TerrainGenerator : Node3D
{
    private MapConfig _config = null!;
    private FastNoiseLite _noise = new();

    public MapConfig Config
    {
        get => _config;
        set
        {
            _config = value;
            _noise.Seed = value.Seed;
            _noise.Frequency = value.NoiseFrequency;
            _noise.FractalOctaves = 4;
        }
    }

    public float SampleHeight(float worldX, float worldZ)
    {
        return ComputeHeight(worldX, worldZ);
    }

    public Vector3 SnapToGround(Vector3 world)
    {
        world.Y = SampleHeight(world.X, world.Z);
        return world;
    }

    public void Generate(MapConfig config)
    {
        Config = config;

        for (int i = GetChildCount() - 1; i >= 0; i--)
            GetChild(i).QueueFree();

        var mesh = BuildTerrainMesh();
        var mi = new MeshInstance3D { Mesh = mesh, Name = "TerrainMesh" };
        mi.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.32f, 0.42f, 0.28f),
            Roughness = 0.95f
        };
        AddChild(mi);

        var body = new StaticBody3D { Name = "TerrainBody" };
        body.CollisionLayer = 1u;
        body.CollisionMask = 0u;
        var shape = new ConcavePolygonShape3D { Data = mesh.GetFaces() };
        var col = new CollisionShape3D { Shape = shape };
        body.AddChild(col);
        AddChild(body);

        BuildZoneMarkers();
    }

    /// <summary>
    /// Combined height field: base noise + central plateau + cover corridors.
    /// </summary>
    private float ComputeHeight(float worldX, float worldZ)
    {
        float baseH = _noise.GetNoise2D(worldX, worldZ) * _config.HeightScale;

        // Central plateau: rises smoothly inside PlateauRadius (as fraction of half-size).
        float half = _config.Size * 0.5f;
        float plateauR = half * _config.PlateauRadius;
        float dist = Mathf.Sqrt(worldX * worldX + worldZ * worldZ);
        // Smoothstep falloff so the plateau edges roll off naturally.
        float plateauMask = Smoothstep(plateauR * 1.4f, plateauR * 0.7f, dist);
        baseH += plateauMask * _config.HillHeight;

        // Cover corridors: trench along N-S (x near 0) and E-W (z near 0).
        // Each corridor lowers terrain within CorridorWidth of its axis,
        // but the central plateau wins where they overlap.
        float corridorAmount = 0f;
        if (_config.CorridorCount >= 1)
        {
            // N-S corridor: |x| < width
            float t = Smoothstep(_config.CorridorWidth, 0f, Mathf.Abs(worldX));
            corridorAmount = Mathf.Max(corridorAmount, t);
        }
        if (_config.CorridorCount >= 2)
        {
            // E-W corridor: |z| < width
            float t = Smoothstep(_config.CorridorWidth, 0f, Mathf.Abs(worldZ));
            corridorAmount = Mathf.Max(corridorAmount, t);
        }
        // Don't carve inside the plateau core.
        float carveMask = corridorAmount * (1f - plateauMask);
        baseH -= carveMask * _config.CorridorDepth;

        return baseH;
    }

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        float t = Mathf.Clamp((x - edge0) / (edge1 - edge0 + 1e-6f), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private ArrayMesh BuildTerrainMesh()
    {
        int size = _config.Size;
        float half = size * 0.5f;

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                float x0 = x - half, x1 = x + 1 - half;
                float z0 = z - half, z1 = z + 1 - half;

                var v00 = new Vector3(x0, ComputeHeight(x0, z0), z0);
                var v10 = new Vector3(x1, ComputeHeight(x1, z0), z0);
                var v11 = new Vector3(x1, ComputeHeight(x1, z1), z1);
                var v01 = new Vector3(x0, ComputeHeight(x0, z1), z1);

                var n1 = (v10 - v00).Cross(v01 - v00).Normalized();
                var n2 = (v01 - v11).Cross(v10 - v11).Normalized();

                st.SetNormal(n1);
                st.AddVertex(v00);
                st.AddVertex(v10);
                st.AddVertex(v01);

                st.SetNormal(n2);
                st.AddVertex(v10);
                st.AddVertex(v11);
                st.AddVertex(v01);
            }
        }

        st.Index();
        return st.Commit();
    }

    /// <summary>
    /// Color-coded translucent zones: green = friendly (SW), red = enemy (NE),
    /// gray = neutral center. Purely visual.
    /// </summary>
    private void BuildZoneMarkers()
    {
        float half = _config.Size * 0.5f;
        float zoneR = half * 0.35f;

        var friendlyZone = MakeZonePlate(
            new Vector3(-half * 0.6f, 0.2f, -half * 0.6f),
            zoneR,
            new Color(0.2f, 0.6f, 0.2f, 0.25f),
            "FriendlyZone");
        var enemyZone = MakeZonePlate(
            new Vector3(half * 0.6f, 0.2f, half * 0.6f),
            zoneR,
            new Color(0.6f, 0.2f, 0.2f, 0.25f),
            "EnemyZone");
        var neutralZone = MakeZonePlate(
            new Vector3(0, 0.2f, 0),
            half * _config.PlateauRadius * 1.4f,
            new Color(0.5f, 0.5f, 0.5f, 0.18f),
            "NeutralZone");

        AddChild(friendlyZone);
        AddChild(enemyZone);
        AddChild(neutralZone);
    }

    private static MeshInstance3D MakeZonePlate(Vector3 pos, float radius, Color color, string name)
    {
        var plane = new PlaneMesh { Size = new Vector2(radius * 2f, radius * 2f) };
        var mi = new MeshInstance3D { Mesh = plane, Name = name, Position = pos };
        mi.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            Roughness = 1.0f,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
        return mi;
    }
}
