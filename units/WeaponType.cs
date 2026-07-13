namespace AIWarSandbox.Units;

public enum WeaponType
{
    Rifle,
    Mg,
    Cannon,
    Missile,
    Rocket,
    Sniper
}

/// <summary>
/// Static lookup table of baseline weapon statistics per <see cref="WeaponType"/>.
/// Roles: Rifle=general, Mg=suppression/close, Sniper=long/slow,
/// Rocket=anti-armor splash, Cannon=tank main gun.
/// AntiArmorMult multiplies damage vs high-armor targets (Vehicle).
/// </summary>
public readonly record struct WeaponStats(
    WeaponType Type,
    int Damage,
    float Range,
    float CooldownSec,
    float ProjectileSpeed,
    float SplashRadius = 0f,
    float AntiArmorMult = 1f)
{
    public static WeaponStats GetStats(WeaponType type) => type switch
    {
        WeaponType.Rifle  => new WeaponStats(WeaponType.Rifle,  12, 32f, 0.75f, 55f, 0f, 0.6f),
        WeaponType.Mg     => new WeaponStats(WeaponType.Mg,      6, 22f, 0.18f, 65f, 0f, 0.4f),
        WeaponType.Sniper => new WeaponStats(WeaponType.Sniper, 35, 70f, 2.8f, 80f, 0f, 0.8f),
        WeaponType.Cannon => new WeaponStats(WeaponType.Cannon, 45, 48f, 2.4f, 35f, 2.5f, 1.8f),
        WeaponType.Rocket => new WeaponStats(WeaponType.Rocket, 55, 55f, 3.8f, 22f, 5f, 2.2f),
        WeaponType.Missile => new WeaponStats(WeaponType.Missile, 55, 55f, 3.8f, 22f, 5f, 2.2f),
        _ => new WeaponStats(WeaponType.Rifle, 12, 32f, 0.75f, 55f)
    };
}
