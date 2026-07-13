using Godot;
using AIWarSandbox.Kb;

namespace AIWarSandbox.Units;

public enum StructureKind
{
    Base,
    Bunker,
    Turret,
    Barracks
}

public partial class Structure : Unit
{
    public StructureKind Kind { get; set; } = StructureKind.Base;

    /// <summary>Defensive weapon (MG by default). Structures do not move.</summary>
    public Weapon DefenseWeapon { get; set; } = new();

    private float _defenseReloadLeft;

    public Structure()
    {
        MaxHealth = 1000;
    }

    public override void _Ready()
    {
        base._Ready();
        EntityGraph.Instance?.Register(this, EntityType.Structure, Name, IsFriendly);
        CollisionLayer = 4u;
        CollisionMask = 1u;

        DefenseWeapon = Weapon.ForType(WeaponType.Mg);
        _defenseReloadLeft = DefenseWeapon.CooldownSec;

        // Try to load a 3D model from GLB; fall back to box mesh on failure.
        var modelPath = IsFriendly
            ? "res://models/poly_pizza/structure_house.glb"
            : "res://models/poly_pizza/structure_farmhouse.glb";

        if (ResourceLoader.Exists(modelPath) &&
            ResourceLoader.Load<PackedScene>(modelPath).Instantiate() is Node3D instance)
        {
            instance.Name = "StructureModel";
            instance.Scale = new Vector3(1.5f, 1.5f, 1.5f);
            AddChild(instance);
        }
        else
        {
            // Fallback: box mesh
            var mesh = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(6f, 6f, 6f) },
                Name = "StructureMesh"
            };
            mesh.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = IsFriendly
                    ? new Color(0.2f, 0.4f, 0.85f)
                    : new Color(0.85f, 0.2f, 0.2f)
            };
            AddChild(mesh);
        }

        var body = new StaticBody3D { Name = "StructureBody" };
        body.CollisionLayer = 4u;
        var shape = new BoxShape3D { Size = new Vector3(6f, 6f, 6f) };
        body.AddChild(new CollisionShape3D { Shape = shape });
        AddChild(body);
    }

    public override void _Process(double delta)
    {
        // Structures don't move, but they defend themselves with the mounted MG.
        _defenseReloadLeft -= (float)delta;
        if (_defenseReloadLeft > 0f) return;

        var registry = AIWarSandbox.Autoloads.UnitRegistry.Instance;
        if (registry == null) return;
        var candidates = IsFriendly ? registry.Enemy : registry.Friendly;

        Unit? nearest = null;
        float bestSqr = float.MaxValue;
        float rangeSqr = DefenseWeapon.Range * DefenseWeapon.Range;
        foreach (var u in candidates)
        {
            if (u == null || u.State == UnitState.Dead) continue;
            var sqr = GlobalPosition.DistanceSquaredTo(u.GlobalPosition);
            if (sqr < bestSqr && sqr <= rangeSqr)
            {
                bestSqr = sqr;
                nearest = u;
            }
        }
        if (nearest != null && DefenseWeapon.CanFire())
        {
            // Structures use the weapon's instant-fire path. Create a transient attacker
            // reference by invoking the static helper directly on the target.
            // Treat the structure itself as the source for damage attribution.
            DefenseWeapon.Fire(this, nearest);
            _defenseReloadLeft = DefenseWeapon.CooldownSec;
        }
        else
        {
            _defenseReloadLeft = 0.1f;
        }
    }

    /// <summary>
    /// Structures are immobile; moving a structure is a no-op.
    /// </summary>
    public void MoveTo(Vector3 dest) { /* no-op */ }

    protected override void Die()
    {
        // Base Die() already raises RaiseUnitDied and RaiseStructureDestroyed (since this is a Structure).
        base.Die();
    }
}
