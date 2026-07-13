using Godot;

namespace AIWarSandbox.Units;

/// <summary>
/// Central catalog of Quaternius CC0 GLB models (downloaded via
/// <c>models/download_quaternius.py</c>). Keeps all model paths, per-category
/// scales and instantiation logic in one place so unit classes stay small and
/// artists can retarget assets without touching gameplay code.
///
/// All models are self-contained GLB (embedded textures) under
/// <c>res://models/quaternius/&lt;pack&gt;/</c>. Every accessor degrades gracefully:
/// a missing file simply drops out of the random pool, and callers fall back to
/// a primitive mesh when nothing is available.
/// </summary>
public static class ModelLibrary
{
    private const string Q = "res://models/quaternius/";

    // --- Infantry ------------------------------------------------------------
    public static readonly string[] FriendlyInfantry =
    {
        Q + "toon_shooter/character_soldier.glb",
        Q + "modular_men/swat.glb",
        Q + "modular_men/worker.glb",
        Q + "modular_men/adventurer.glb",
        Q + "modular_women/soldier.glb",
        Q + "modular_men/casual_character.glb",
    };

    public static readonly string[] EnemyInfantry =
    {
        Q + "toon_shooter/character_enemy.glb",
        Q + "toon_shooter/character_hazmat.glb",
        Q + "modular_men/punk.glb",
        Q + "modular_women/witch.glb",
        Q + "modular_women/sci_fi_character.glb",
    };

    // --- Vehicles (Animated Tanks pack) -------------------------------------
    public static readonly string[] Tanks =
    {
        Q + "tanks/tank.glb",
        Q + "tanks/tank_2.glb",
        Q + "tanks/tank_3.glb",
        Q + "tanks/tank_4.glb",
    };

    // --- Structures ----------------------------------------------------------
    public static readonly string[] FriendlyStructures =
    {
        Q + "toon_shooter/structure.glb",
        Q + "toon_shooter/structure_2.glb",
    };

    public static readonly string[] EnemyStructures =
    {
        Q + "toon_shooter/structure_3.glb",
        Q + "toon_shooter/shipping_container_structure.glb",
    };

    public static readonly string BunkerModel = Q + "toon_shooter/sack_trench.glb";
    public static readonly string TurretModel = Q + "toon_shooter/water_tank.glb";

    // --- Weapons (held models, optional visual attachment) -------------------
    // Realistic toon-shooter guns keyed to gameplay WeaponType.
    private static string WeaponPath(WeaponType type) => type switch
    {
        WeaponType.Rifle => Q + "toon_shooter/ak47.glb",
        WeaponType.Mg => Q + "toon_shooter/smg.glb",
        WeaponType.Cannon => Q + "toon_shooter/short_cannon.glb",
        WeaponType.Rocket => Q + "toon_shooter/rocket_launcher.glb",
        WeaponType.Missile => Q + "toon_shooter/rocket_launcher.glb",
        _ => Q + "toon_shooter/pistol.glb",
    };

    // Sci-Fi Modular Gun pack — used for the sci-fi/hazmat enemy faction.
    private static string SciFiWeaponPath(WeaponType type) => type switch
    {
        WeaponType.Rifle => Q + "scifi_guns/scifi_assault_rifle.glb",
        WeaponType.Mg => Q + "scifi_guns/scifi_smg.glb",
        WeaponType.Cannon => Q + "scifi_guns/scifi_grenade_launcher.glb",
        WeaponType.Rocket => Q + "scifi_guns/scifi_grenade_launcher.glb",
        WeaponType.Missile => Q + "scifi_guns/scifi_grenade_launcher.glb",
        _ => Q + "scifi_guns/scifi_pistol.glb",
    };

    // --- Recommended local scales (Quaternius units ~1.8-2m tall at scale 1) --
    public const float InfantryScale = 0.85f;
    public const float TankScale = 0.9f;
    public const float StructureScale = 1.6f;
    public const float WeaponScale = 0.7f;

    private static readonly RandomNumberGenerator Rng = new();

    /// <summary>Loads and instantiates a GLB scene, or null if it can't be loaded.</summary>
    public static Node3D? Instantiate(string resPath)
    {
        if (string.IsNullOrEmpty(resPath) || !ResourceLoader.Exists(resPath)) return null;
        return ResourceLoader.Load<PackedScene>(resPath)?.Instantiate() as Node3D;
    }

    /// <summary>Picks a random existing path from the pool, or null if none exist.</summary>
    public static string? PickExisting(string[] pool)
    {
        // Gather only paths that actually resolved (assets may be partially present).
        int count = 0;
        System.Span<int> valid = stackalloc int[pool.Length];
        for (int i = 0; i < pool.Length; i++)
            if (ResourceLoader.Exists(pool[i])) valid[count++] = i;
        if (count == 0) return null;
        return pool[valid[Rng.RandiRange(0, count - 1)]];
    }

    public static Node3D? RandomInfantry(bool isFriendly)
        => Instantiate(PickExisting(isFriendly ? FriendlyInfantry : EnemyInfantry) ?? "");

    public static Node3D? RandomTank()
        => Instantiate(PickExisting(Tanks) ?? "");

    public static Node3D? Structure(StructureKind kind, bool isFriendly)
    {
        string? path = kind switch
        {
            StructureKind.Bunker => BunkerModel,
            StructureKind.Turret => TurretModel,
            _ => PickExisting(isFriendly ? FriendlyStructures : EnemyStructures),
        };
        return Instantiate(path ?? "");
    }

    /// <summary>Held-weapon model for a unit. <paramref name="sciFi"/> selects the sci-fi gun pack.</summary>
    public static Node3D? Weapon(WeaponType type, bool sciFi)
        => Instantiate(sciFi ? SciFiWeaponPath(type) : WeaponPath(type));

    /// <summary>
    /// Finds an <see cref="AnimationPlayer"/> under <paramref name="model"/> and starts a
    /// looping idle/walk clip if the GLB shipped with animations. No-op otherwise.
    /// </summary>
    public static void PlayIdle(Node3D model)
    {
        var anim = FindAnimationPlayer(model);
        if (anim == null) return;
        var names = anim.GetAnimationList();
        if (names.Length == 0) return;

        string chosen = names[0];
        foreach (var n in names)
        {
            var lower = n.ToLowerInvariant();
            if (lower.Contains("idle") || lower.Contains("stand"))
            {
                chosen = n;
                break;
            }
        }
        // Loop the clip so the pose doesn't freeze on the last frame.
        var clip = anim.GetAnimation(chosen);
        if (clip != null) clip.LoopMode = Animation.LoopModeEnum.Linear;
        anim.Play(chosen);
    }

    private static AnimationPlayer? FindAnimationPlayer(Node node)
    {
        if (node is AnimationPlayer ap) return ap;
        foreach (var child in node.GetChildren())
            if (FindAnimationPlayer(child) is { } found) return found;
        return null;
    }
}
