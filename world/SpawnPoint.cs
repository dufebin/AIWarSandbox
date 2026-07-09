using Godot;

namespace AIWarSandbox.World;

public partial class SpawnPoint : Marker3D
{
    [Export] public bool IsFriendly { get; set; } = true;
    [Export] public string Role { get; set; } = "main";

    public Vector3 WorldPosition => GlobalPosition;

    public override void _Ready()
    {
        var cyl = new CylinderMesh
        {
            TopRadius = 0.6f,
            BottomRadius = 0.6f,
            Height = 1.2f
        };
        var mesh = new MeshInstance3D { Mesh = cyl, Name = "SpawnVisual" };
        mesh.Position = new Vector3(0, 0.6f, 0);
        mesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = IsFriendly
                ? new Color(0.2f, 0.45f, 0.95f)
                : new Color(0.95f, 0.25f, 0.2f),
            EmissionEnergyMultiplier = 0.6f,
            Emission = IsFriendly
                ? new Color(0.1f, 0.25f, 0.6f)
                : new Color(0.6f, 0.1f, 0.1f),
            Roughness = 0.5f
        };
        AddChild(mesh);

        var label = new Label3D
        {
            Text = $"{(IsFriendly ? "F" : "E")}:{Role}",
            Position = new Vector3(0, 1.8f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.FixedY,
            Modulate = IsFriendly
                ? new Color(0.5f, 0.8f, 1.0f)
                : new Color(1.0f, 0.6f, 0.5f),
            FontSize = 24
        };
        AddChild(label);
    }
}
