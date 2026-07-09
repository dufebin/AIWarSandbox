using Godot;

namespace AIWarSandbox.Units;

public partial class Vehicle : Combatant
{
    public Vehicle()
    {
        MoveSpeed = 4f;   // higher HP, slower than infantry (per spec)
        MaxHealth = 220;
        Armor = 0.35f;
    }

    public override void _Ready()
    {
        base._Ready();
        if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
        {
            body.Mesh = new BoxMesh { Size = new Vector3(2.2f, 1.4f, 3.0f) };
            body.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = IsFriendly ? new Color(0.15f, 0.35f, 0.85f) : new Color(0.85f, 0.15f, 0.15f)
            };
        }
    }

    public void Equip(WeaponType type)
    {
        Weapon = Weapon.ForType(type);
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
    }
}
