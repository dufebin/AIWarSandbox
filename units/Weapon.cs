using Godot;

namespace AIWarSandbox.Units;

[GlobalClass]
public partial class Weapon : Resource
{
    [Export] public WeaponType Type { get; set; } = WeaponType.Rifle;
    [Export] public int Damage { get; set; } = 10;
    [Export] public float Range { get; set; } = 30f;
    [Export] public float RateOfFire { get; set; } = 2f;
    [Export] public float ProjectileSpeed { get; set; } = 60f;
    [Export] public float SplashRadius { get; set; } = 0f;
    /// <summary>Damage multiplier applied against high-armor targets (Armor &gt;= 0.3).</summary>
    [Export] public float AntiArmorMult { get; set; } = 1f;

    /// <summary>Cooldown in seconds between shots. Derived from RateOfFire when zero.</summary>
    public float CooldownSec
    {
        get => _cooldownSec > 0f ? _cooldownSec : (RateOfFire > 0f ? 1f / RateOfFire : 1f);
        set => _cooldownSec = value;
    }
    private float _cooldownSec = 0f;

    /// <summary>Engine time (seconds) of the last successful shot.</summary>
    public double LastFireTime { get; private set; } = -1e9;

    /// <summary>Shared RNG for hit rolls — avoids allocating one per shot.</summary>
    private static readonly RandomNumberGenerator Rng = new();

    /// <summary>True when enough time has elapsed since the last shot to fire again.</summary>
    public bool CanFire() => Time.GetTicksMsec() / 1000.0 - LastFireTime >= CooldownSec;

    /// <summary>Spec-conformant overload taking a <see cref="Combatant"/> attacker.</summary>
    public void Fire(Combatant from, Unit target) => Fire((Unit)from, target);

    /// <summary>
    /// Fires the weapon at <paramref name="target"/> from <paramref name="from"/>.
    /// Instant-hit weapons (Rifle/MG) apply damage directly; projectile weapons
    /// (Cannon/Rocket/Missile) spawn a flying <see cref="Projectile"/>.
    /// This overload accepts any <see cref="Unit"/> (e.g. a <see cref="Structure"/>) as the attacker.
    /// </summary>
    public void Fire(Unit from, Unit target)
    {
        if (!CanFire() || target == null || target.State == UnitState.Dead) return;
        LastFireTime = Time.GetTicksMsec() / 1000.0;

        var muzzle = from.GlobalPosition + new Vector3(0f, 1f, 0f);
        var targetPos = target.GlobalPosition + new Vector3(0f, 1f, 0f);
        var dist = muzzle.DistanceTo(targetPos);

        // Hit chance falls off with distance: full at point-blank, ~0.6 at max range.
        float hitChance = 1f - Mathf.Clamp(dist / Mathf.Max(0.1f, Range) * 0.5f, 0f, 0.4f);
        bool willHit = Rng.Randf() <= hitChance;

        bool instant = Type == WeaponType.Rifle || Type == WeaponType.Mg || Type == WeaponType.Sniper;
        int finalDmg = Damage;
        if (target is Combatant ct && ct.Armor >= 0.3f)
            finalDmg = Mathf.Max(1, Mathf.RoundToInt(Damage * AntiArmorMult));

        if (instant)
        {
            if (willHit)
                target.TakeDamage(finalDmg);
            // Physics-free tracer (no RigidBody allocation).
            var tracer = TracerVisual.Create(muzzle, (targetPos - muzzle).Normalized() * ProjectileSpeed);
            from.GetTree().CurrentScene.AddChild(tracer);
            CombatFx.MuzzleFlash(from.GetTree().CurrentScene, muzzle);
            SfxBus.Play(from, SfxBus.Kind.Fire);
            GD.Print($"[Weapon] {from.Name} instant-fire {Type} -> {target.Name} (hit={willHit}, dmg={finalDmg})");
        }
        else
        {
            var proj = Projectile.Create(muzzle, (targetPos - muzzle).Normalized() * ProjectileSpeed, this, from);
            proj.HomingTarget = target;
            proj.WillHit = willHit;
            proj.Damage = finalDmg;
            from.GetTree().CurrentScene.AddChild(proj);
            CombatFx.MuzzleFlash(from.GetTree().CurrentScene, muzzle);
            SfxBus.Play(from, SfxBus.Kind.Fire);
            GD.Print($"[Weapon] {from.Name} fires {Type} projectile -> {target.Name} (hit={willHit})");
        }
    }

    public static Weapon ForType(WeaponType type)
    {
        var s = WeaponStats.GetStats(type);
        return new Weapon
        {
            Type = type,
            Damage = s.Damage,
            Range = s.Range,
            // RateOfFire is the reciprocal of cooldown (preserved for legacy Combatant reload logic).
            RateOfFire = s.CooldownSec > 0f ? 1f / s.CooldownSec : 1f,
            ProjectileSpeed = s.ProjectileSpeed,
            SplashRadius = s.SplashRadius,
            AntiArmorMult = s.AntiArmorMult,
            _cooldownSec = s.CooldownSec
        };
    }
}
