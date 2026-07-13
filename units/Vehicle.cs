using Godot;

namespace AIWarSandbox.Units;

public partial class Vehicle : Combatant
{
    /// <summary>GLB model paths for friendly/enemy vehicles. CC0 from Poly Pizza (Quaternius).</summary>
    private static readonly string[] FriendlyModels =
    {
        "res://models/poly_pizza/vehicle_spaceship_1.glb",
        "res://models/poly_pizza/vehicle_car.glb",
    };

    private static readonly string[] EnemyModels =
    {
        "res://models/poly_pizza/vehicle_spaceship_2.glb",
        "res://models/poly_pizza/vehicle_car.glb",
    };

    public Vehicle()
    {
        MoveSpeed = 4f;   // higher HP, slower than infantry (per spec)
        MaxHealth = 220;
        Armor = 0.35f;
    }

    public override void _Ready()
    {
        base._Ready();

        // Try to load a 3D model from GLB; fall back to box mesh on failure.
        var models = IsFriendly ? FriendlyModels : EnemyModels;
        var modelPath = models[GD.RandRange(0, models.Length - 1)];

        if (ResourceLoader.Exists(modelPath) &&
            ResourceLoader.Load<PackedScene>(modelPath).Instantiate() is Node3D instance)
        {
            instance.Name = "Model";
            // Scale to ~2m length (box mesh was 2.2×1.4×3.0)
            instance.Scale = new Vector3(0.8f, 0.8f, 0.8f);
            AddChild(instance);

            // Hide the placeholder Body mesh from base._Ready()
            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
                body.Visible = false;
        }
        else
        {
            // Fallback: box mesh
            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
            {
                body.Mesh = new BoxMesh { Size = new Vector3(2.2f, 1.4f, 3.0f) };
                body.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = IsFriendly ? new Color(0.15f, 0.35f, 0.85f) : new Color(0.85f, 0.15f, 0.15f)
                };
            }
        }
    }

    public void Equip(WeaponType type)
    {
        Weapon = Weapon.ForType(type);
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
    }
}
