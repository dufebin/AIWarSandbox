using Godot;

namespace AIWarSandbox.Units;

public partial class Infantry : Combatant
{
    /// <summary>Enemies use the sci-fi gun pack; friendlies use realistic toon-shooter guns.</summary>
    private bool UsesSciFiGear => !IsFriendly;

    public Infantry()
    {
        MoveSpeed = 6f;
        MaxHealth = 80;
        Armor = 0.1f;
    }

    public override void _Ready()
    {
        base._Ready();

        // Quaternius character (Toon Shooter / Ultimate Modular packs); fall back to a capsule.
        if (ModelLibrary.RandomInfantry(IsFriendly) is { } instance)
        {
            instance.Name = "Model";
            instance.Scale = new Vector3(
                ModelLibrary.InfantryScale, ModelLibrary.InfantryScale, ModelLibrary.InfantryScale);
            AddChild(instance);
            ModelLibrary.PlayIdle(instance);
            AttachWeaponModel(instance);

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

    /// <summary>Parents a held-weapon model roughly at hand height (best-effort; no bone rig needed).</summary>
    private void AttachWeaponModel(Node3D characterModel)
    {
        if (Weapon == null) return;
        if (ModelLibrary.Weapon(Weapon.Type, UsesSciFiGear) is not { } gun) return;
        gun.Name = "HeldWeapon";
        gun.Scale = new Vector3(
            ModelLibrary.WeaponScale, ModelLibrary.WeaponScale, ModelLibrary.WeaponScale);
        // Approximate right-hand position, forward-facing.
        gun.Position = new Vector3(0.35f, 1.0f, 0.35f);
        characterModel.AddChild(gun);
    }

    public void Equip(WeaponType type)
    {
        Weapon = Weapon.ForType(type);
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
    }
}
