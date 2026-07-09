using Godot;

namespace AIWarSandbox.Units;

public static class UnitFactory
{
    public static Infantry CreateInfantry(WeaponType weapon, bool isFriendly, Vector3 pos)
    {
        var u = new Infantry { IsFriendly = isFriendly };
        u.Equip(weapon);
        u.Position = pos;
        return u;
    }

    public static Vehicle CreateVehicle(WeaponType weapon, bool isFriendly, Vector3 pos)
    {
        var u = new Vehicle { IsFriendly = isFriendly };
        u.Equip(weapon);
        u.Position = pos;
        return u;
    }

    public static Structure CreateStructure(StructureKind kind, bool isFriendly, Vector3 pos)
    {
        var s = new Structure
        {
            IsFriendly = isFriendly,
            Kind = kind,
            Position = pos
        };
        return s;
    }
}
