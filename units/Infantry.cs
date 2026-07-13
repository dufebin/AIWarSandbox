using Godot;

namespace AIWarSandbox.Units;

public partial class Infantry : Combatant
{
    /// <summary>GLB model paths for friendly/enemy infantry. CC0 from Poly Pizza (Quaternius).</summary>
    private static readonly string[] FriendlyModels =
    {
        "res://models/poly_pizza/infantry_swat.glb",
        "res://models/poly_pizza/infantry_adventurer.glb",
        "res://models/poly_pizza/infantry_animated.glb",
    };

    private static readonly string[] EnemyModels =
    {
        "res://models/poly_pizza/infantry_zombie.glb",
        "res://models/poly_pizza/infantry_robot_enemy.glb",
        "res://models/poly_pizza/infantry_character_base.glb",
    };

    public Infantry()
    {
        MoveSpeed = 6f;
        MaxHealth = 80;
        Armor = 0.1f;
    }

    public override void _Ready()
    {
        base._Ready();

        // Try to load a 3D model from GLB; fall back to capsule mesh on failure.
        var models = IsFriendly ? FriendlyModels : EnemyModels;
        var modelPath = models[GD.RandRange(0, models.Length - 1)];

        if (ResourceLoader.Exists(modelPath) &&
            ResourceLoader.Load<PackedScene>(modelPath).Instantiate() is Node3D instance)
        {
            instance.Name = "Model";
            // Scale to ~1.6m height (infantry capsule was radius 0.4, height 1.6)
            instance.Scale = new Vector3(0.5f, 0.5f, 0.5f);
            AddChild(instance);

            // Hide the placeholder Body mesh from base._Ready()
            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
                body.Visible = false;
        }
        else
        {
            // Fallback: capsule mesh
            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
            {
                body.Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.6f };
                body.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = IsFriendly ? new Color(0.2f, 0.5f, 0.9f) : new Color(0.9f, 0.2f, 0.2f)
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
