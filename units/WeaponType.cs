namespace AIWarSandbox.Units;

public enum WeaponType
{
    Rifle,
    Mg,
    Cannon,
    Missile,
    Rocket
}

/// <summary>
/// Static lookup table of baseline weapon statistics per <see cref="WeaponType"/>.
/// Values follow the combat-system spec: damage / range / cooldown (sec) / projectile speed.
/// </summary>
public readonly record struct WeaponStats(
    WeaponType Type,
    int Damage,
    float Range,
    float CooldownSec,
    float ProjectileSpeed,
    float SplashRadius = 0f)
{
    public static WeaponStats GetStats(WeaponType type) => type switch
    {
        WeaponType.Rifle  => new WeaponStats(WeaponType.Rifle,  10, 30f, 0.8f, 50f),
        WeaponType.Mg     => new WeaponStats(WeaponType.Mg,      5, 25f, 0.2f, 60f),
        WeaponType.Cannon => new WeaponStats(WeaponType.Cannon, 40, 50f, 2.5f, 30f, 3f),
        WeaponType.Rocket => new WeaponStats(WeaponType.Rocket, 60, 60f, 4.0f, 20f, 5f),
        // Missile kept as a legacy alias (used by locked UI) mapped to rocket-style stats.
        WeaponType.Missile => new WeaponStats(WeaponType.Missile, 60, 60f, 4.0f, 20f, 5f),
        _ => new WeaponStats(WeaponType.Rifle, 10, 30f, 0.8f, 50f)
    };
}
