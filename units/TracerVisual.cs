using Godot;

namespace AIWarSandbox.Units;

/// <summary>
/// Physics-free visual tracer for instant-hit weapons (avoids RigidBody cost).
/// Created by Weapon.Fire; unique; no data I/O.
/// </summary>
public partial class TracerVisual : Node3D
{
    private Vector3 _vel;
    private float _life = 0.15f;
    private float _age;

    public static TracerVisual Create(Vector3 origin, Vector3 velocity)
    {
        return new TracerVisual
        {
            Name = "Tracer",
            _vel = velocity,
            Position = origin,
        };
    }

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.9f, 0.3f),
                EmissionEnabled = true,
                Emission = new Color(1f, 0.8f, 0.2f),
                EmissionEnergyMultiplier = 2f,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            }
        });
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        GlobalPosition += _vel * dt;
        _age += dt;
        if (_age >= _life) QueueFree();
    }
}
