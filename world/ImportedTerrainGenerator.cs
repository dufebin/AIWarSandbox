using Godot;

namespace AIWarSandbox.World;

/// <summary>
/// Builds a 3D terrain mesh from imported satellite + heightmap data.
/// Mirrors <see cref="TerrainGenerator"/>'s mesh + collision approach but
/// sources its heightfield from a Terrarium-decoded <see cref="float"/> array
/// and its albedo from a satellite <see cref="ImageTexture"/> instead of a
/// procedural noise color.
/// </summary>
public partial class ImportedTerrainGenerator : Node3D
{
    private float[,] _heightmap = null!;
    private int _gridW;
    private int _gridH;
    private float _worldSize;
    private float _cellSize;
    private float _half;

    /// <summary>
    /// Build the terrain: displaced mesh with satellite albedo + concave collision
    /// on layer 1. The terrain is centered at origin, spanning [-size/2, size/2]
    /// on X and Z, where size equals the smaller of the heightmap's two pixel dims
    /// so cells are square.
    /// </summary>
    public void Build(MapSource source, ImageTexture satellite, float[,] heightmap)
    {
        _heightmap = heightmap;
        _gridW = heightmap.GetLength(0); // x dimension
        _gridH = heightmap.GetLength(1); // y dimension (mapped to Z)

        // Pick a square world size from the smaller dimension so cells stay square.
        int gridSize = System.Math.Min(_gridW, _gridH);
        _worldSize = gridSize;
        _cellSize = 1.0f; // one world unit per heightmap cell
        _half = _worldSize * 0.5f;

        // Clear any previous build.
        for (int i = GetChildCount() - 1; i >= 0; i--)
            GetChild(i).QueueFree();

        var mesh = BuildTerrainMesh(source);

        var mi = new MeshInstance3D { Mesh = mesh, Name = "ImportedTerrainMesh" };
        var mat = new StandardMaterial3D
        {
            AlbedoTexture = satellite,
            Roughness = 0.95f,
            Metallic = 0.0f,
        };
        mi.MaterialOverride = mat;
        AddChild(mi);

        var body = new StaticBody3D { Name = "ImportedTerrainBody" };
        body.CollisionLayer = 1u;
        body.CollisionMask = 0u;
        var shape = new ConcavePolygonShape3D { Data = mesh.GetFaces() };
        var col = new CollisionShape3D { Shape = shape };
        body.AddChild(col);
        AddChild(body);
    }

    /// <summary>
    /// Bilinearly sample the heightmap at world coordinates. Out-of-range
    /// coordinates clamp to the edge. Returns the elevation in meters (already
    /// scaled by <see cref="MapSource.HeightScale"/> at import time).
    /// </summary>
    public float SampleHeight(float worldX, float worldZ)
    {
        if (_heightmap == null)
            return 0.0f;

        // World -> grid coords. Center the terrain at origin.
        float gx = worldX + _half;
        float gz = worldZ + _half;

        // Clamp to valid range.
        gx = Mathf.Clamp(gx, 0f, _gridW - 1);
        gz = Mathf.Clamp(gz, 0f, _gridH - 1);

        int x0 = (int)gx;
        int z0 = (int)gz;
        int x1 = System.Math.Min(x0 + 1, _gridW - 1);
        int z1 = System.Math.Min(z0 + 1, _gridH - 1);

        float fx = gx - x0;
        float fz = gz - z0;

        float h00 = _heightmap[x0, z0];
        float h10 = _heightmap[x1, z0];
        float h01 = _heightmap[x0, z1];
        float h11 = _heightmap[x1, z1];

        // Bilinear interpolation.
        float h0 = h00 * (1f - fx) + h10 * fx;
        float h1 = h01 * (1f - fx) + h11 * fx;
        return h0 * (1f - fz) + h1 * fz;
    }

    /// <summary>World-space height at (x, z); convenience alias for AI/placement code.</summary>
    public Vector3 SnapToGround(Vector3 world)
    {
        world.Y = SampleHeight(world.X, world.Z);
        return world;
    }

    private ArrayMesh BuildTerrainMesh(MapSource source)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        // One quad per cell. We iterate over the smaller square so the mesh is
        // regular even if the heightmap is non-square (extra rows/cols ignored).
        int cells = System.Math.Min(_gridW, _gridH) - 1;

        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                float x0 = x - _half, x1 = x + 1 - _half;
                float z0 = z - _half, z1 = z + 1 - _half;

                var v00 = new Vector3(x0, _heightmap[x, z], z0);
                var v10 = new Vector3(x1, _heightmap[x + 1, z], z0);
                var v11 = new Vector3(x1, _heightmap[x + 1, z + 1], z1);
                var v01 = new Vector3(x0, _heightmap[x, z + 1], z1);

                var n1 = (v10 - v00).Cross(v01 - v00).Normalized();
                var n2 = (v01 - v11).Cross(v10 - v11).Normalized();

                // UVs map the satellite texture onto the mesh 1:1 in grid space.
                float u0 = (float)x / _gridW;
                float u1 = (float)(x + 1) / _gridW;
                float v0 = (float)z / _gridH;
                float v1 = (float)(z + 1) / _gridH;

                st.SetNormal(n1);
                st.SetUV(new Vector2(u0, v0));
                st.AddVertex(v00);
                st.SetUV(new Vector2(u1, v0));
                st.AddVertex(v10);
                st.SetUV(new Vector2(u0, v1));
                st.AddVertex(v01);

                st.SetNormal(n2);
                st.SetUV(new Vector2(u1, v0));
                st.AddVertex(v10);
                st.SetUV(new Vector2(u1, v1));
                st.AddVertex(v11);
                st.SetUV(new Vector2(u0, v1));
                st.AddVertex(v01);
            }
        }

        st.Index();
        return st.Commit();
    }
}
