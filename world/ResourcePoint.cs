using Godot;
using AIWarSandbox.Kb;

namespace AIWarSandbox.World;

public partial class ResourcePoint : Area3D
{
    [Export] public int SuppliesPerSecond { get; set; } = 2;
    [Export] public string PointName { get; set; } = "Sector";

    public bool IsFriendlyOwned { get; private set; }
    public bool IsEnemyOwned { get; private set; }

    public void Capture(bool byFriendly)
    {
        IsFriendlyOwned = byFriendly;
        IsEnemyOwned = !byFriendly;
    }

    public override void _Ready()
    {
        EntityGraph.Instance?.Register(this, EntityType.SupplyPoint, PointName, false);
        // Octahedron visual (two square pyramids back-to-back).
        var oct = new ArrayMesh();
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        float s = 0.9f;
        var top = new Vector3(0, s, 0);
        var bottom = new Vector3(0, -s, 0);
        var a = new Vector3(s, 0, 0);
        var b = new Vector3(0, 0, s);
        var c = new Vector3(-s, 0, 0);
        var d = new Vector3(0, 0, -s);

        AddTri(st, top, b, a);
        AddTri(st, top, c, b);
        AddTri(st, top, d, c);
        AddTri(st, top, a, d);
        AddTri(st, bottom, a, b);
        AddTri(st, bottom, b, c);
        AddTri(st, bottom, c, d);
        AddTri(st, bottom, d, a);

        st.Index();
        oct = st.Commit();

        var mesh = new MeshInstance3D { Mesh = oct, Name = "ResourceVisual" };
        mesh.Position = new Vector3(0, 1.5f, 0);
        mesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.85f, 0.1f),
            Emission = new Color(1.0f, 0.7f, 0.0f),
            EmissionEnergyMultiplier = 1.5f,
            Roughness = 0.3f,
            Metallic = 0.2f
        };
        AddChild(mesh);

        var light = new OmniLight3D
        {
            Name = "ResourceLight",
            Position = new Vector3(0, 2.2f, 0),
            LightColor = new Color(1.0f, 0.8f, 0.2f),
            LightEnergy = 2.0f,
            OmniRange = 18.0f,
            OmniAttenuation = 1.2f
        };
        AddChild(light);

        var label = new Label3D
        {
            Text = PointName,
            Position = new Vector3(0, 3.0f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Modulate = new Color(1.0f, 0.95f, 0.5f),
            FontSize = 22
        };
        AddChild(label);
    }

    private static void AddTri(SurfaceTool st, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        var n = (v1 - v0).Cross(v2 - v0).Normalized();
        st.SetNormal(n);
        st.AddVertex(v0);
        st.AddVertex(v1);
        st.AddVertex(v2);
    }

    public override void _ExitTree()
    {
        EntityGraph.Instance?.Unregister((int)GetInstanceId());
        base._ExitTree();
    }
}
