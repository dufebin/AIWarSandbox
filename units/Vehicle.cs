using Godot;

namespace AIWarSandbox.Units;

public partial class Vehicle : Combatant
{
    private Node3D? _turret;

    public Vehicle()
    {
        MoveSpeed = 4f;
        MaxHealth = 220;
        Armor = 0.35f;
    }

    public override void _Ready()
    {
        base._Ready();

        if (ModelLibrary.RandomTank() is { } instance)
        {
            instance.Name = "Model";
            instance.Scale = new Vector3(
                ModelLibrary.TankScale, ModelLibrary.TankScale, ModelLibrary.TankScale);
            AddChild(instance);
            ModelLibrary.PlayIdle(instance);
            ApplyFactionTint(instance);

            _turret = FindNamed(instance, "Turret") as Node3D
                      ?? FindNamed(instance, "turret") as Node3D
                      ?? FindNamed(instance, "Cannon") as Node3D;
            if (_turret == null)
            {
                _turret = new Node3D { Name = "TurretPivot", Position = new Vector3(0, 1.2f, 0) };
                instance.AddChild(_turret);
            }

            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
                body.Visible = false;
        }
        else
        {
            if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
            {
                body.Mesh = new BoxMesh { Size = new Vector3(2.2f, 1.4f, 3.0f) };
                body.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = IsFriendly ? new Color(0.15f, 0.35f, 0.85f) : new Color(0.85f, 0.15f, 0.15f)
                };
            }
            _turret = new Node3D { Name = "TurretPivot", Position = new Vector3(0, 1.0f, 0) };
            AddChild(_turret);
            var barrel = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.25f, 0.25f, 1.6f) },
                Position = new Vector3(0, 0, 0.8f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.2f, 0.2f) }
            };
            _turret.AddChild(barrel);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (_turret == null || CurrentTarget == null || CurrentTarget.State == UnitState.Dead) return;
        var to = CurrentTarget.GlobalPosition - GlobalPosition;
        to.Y = 0f;
        if (to.LengthSquared() < 0.01f) return;
        float worldYaw = Mathf.Atan2(to.X, to.Z);
        float localYaw = worldYaw - Rotation.Y;
        float next = Mathf.LerpAngle(_turret.Rotation.Y, localYaw, Mathf.Min(1f, (float)delta * 6f));
        _turret.Rotation = new Vector3(0, next, 0);
    }

    private static Node? FindNamed(Node root, string name)
    {
        if (root.Name == name) return root;
        foreach (var c in root.GetChildren())
        {
            var found = FindNamed(c, name);
            if (found != null) return found;
        }
        return null;
    }

    private void ApplyFactionTint(Node node)
    {
        var tint = IsFriendly ? new Color(0.65f, 0.75f, 1.0f) : new Color(1.0f, 0.6f, 0.6f);
        if (node is MeshInstance3D mi)
        {
            mi.MaterialOverlay = new StandardMaterial3D
            {
                AlbedoColor = new Color(tint.R, tint.G, tint.B, 0.28f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
        }
        foreach (var child in node.GetChildren())
            ApplyFactionTint(child);
    }

    public void Equip(WeaponType type)
    {
        Weapon = Weapon.ForType(type);
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
    }
}
