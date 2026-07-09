using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Plain data class (POD) describing the enemy force composition chosen during
/// the briefing flow. Kept as a <see cref="Resource"/> so it can be passed
/// through <see cref="AIWarSandbox.Autoloads.EventBus.ConfigSubmitted"/>.
/// The interactive CanvasLayer that edits one of these lives in
/// <see cref="EnemyConfigUI"/>.
/// </summary>
[GlobalClass]
public partial class EnemyConfig : Resource
{
    [Export] public int EnemyCount { get; set; } = 12;
    [Export] public int HeavyRatio { get; set; } = 30;
    [Export] public int RangedRatio { get; set; } = 40;
    [Export] public int Difficulty { get; set; } = 1;
    [Export] public WeaponType EnemyPrimaryWeapon { get; set; } = WeaponType.Rifle;
    [Export] public WeaponType EnemyHeavyWeapon { get; set; } = WeaponType.Rocket;

    /// <summary>Default enemy configuration used by "Skip to Battle" and tests.</summary>
    public static EnemyConfig CreateDefault() => new()
    {
        EnemyCount = 12,
        HeavyRatio = 30,
        EnemyPrimaryWeapon = WeaponType.Rifle,
        EnemyHeavyWeapon = WeaponType.Rocket,
    };
}
