using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Combined force composition for both sides, submitted through briefing UI.
/// Extends the old enemy-only config with player-side infantry/tank counts.
/// </summary>
[GlobalClass]
public partial class ForceConfig : Resource
{
    [Export] public int FriendlyInfantry { get; set; } = 6;
    [Export] public int FriendlyTanks { get; set; } = 2;
    [Export] public WeaponType FriendlyInfantryWeapon { get; set; } = WeaponType.Rifle;
    [Export] public WeaponType FriendlyTankWeapon { get; set; } = WeaponType.Cannon;

    [Export] public int EnemyCount { get; set; } = 12;
    [Export] public int HeavyRatio { get; set; } = 30;
    [Export] public int Difficulty { get; set; } = 1;
    [Export] public WeaponType EnemyPrimaryWeapon { get; set; } = WeaponType.Rifle;
    [Export] public WeaponType EnemyHeavyWeapon { get; set; } = WeaponType.Rocket;

    public EnemyConfig ToEnemyConfig() => new()
    {
        EnemyCount = EnemyCount,
        HeavyRatio = HeavyRatio,
        Difficulty = Difficulty,
        EnemyPrimaryWeapon = EnemyPrimaryWeapon,
        EnemyHeavyWeapon = EnemyHeavyWeapon,
    };

    public static ForceConfig CreateDefault() => new()
    {
        FriendlyInfantry = 6,
        FriendlyTanks = 2,
        FriendlyInfantryWeapon = WeaponType.Rifle,
        FriendlyTankWeapon = WeaponType.Cannon,
        EnemyCount = 12,
        HeavyRatio = 30,
        Difficulty = 1,
        EnemyPrimaryWeapon = WeaponType.Rifle,
        EnemyHeavyWeapon = WeaponType.Rocket,
    };
}
