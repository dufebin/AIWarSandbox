using Godot;

namespace AIWarSandbox.Units;

public partial class Infantry : Combatant
{
    public Infantry()
    {
        MoveSpeed = 6f;
        MaxHealth = 80;
        Armor = 0.1f;
    }

    public override void _Ready()
    {
        base._Ready();
        if (GetNodeOrNull<MeshInstance3D>("Body") is { } body)
        {
            body.Mesh = new CapsuleMesh { Radius = 0.4f, Height = 1.6f };
            body.MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = IsFriendly ? new Color(0.2f, 0.5f, 0.9f) : new Color(0.9f, 0.2f, 0.2f)
            };
        }
    }

    public void Equip(WeaponType type)
    {
        Weapon = Weapon.ForType(type);
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
    }
}
