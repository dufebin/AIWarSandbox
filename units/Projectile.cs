using Godot;

namespace AIWarSandbox.Units;

public partial class Projectile : RigidBody3D
{
    public int Damage { get; set; } = 10;
    public float SplashRadius { get; set; } = 0f;
    public Unit? Source { get; set; }
    public float Lifetime { get; set; } = 4f;

    /// <summary>Optional homing target. When set, the projectile steers toward it each frame.</summary>
    public Unit? HomingTarget { get; set; }

    /// <summary>Pre-computed hit result for homing projectiles (distance-based hit chance).</summary>
    public bool WillHit { get; set; } = true;

    /// <summary>Flight speed used by the homing integrator (separate from physics velocity).</summary>
    public float FlightSpeed { get; set; } = 30f;

    private float _age;
    private bool _dead;

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = 4;
        BodyEntered += OnBodyEntered;
        CollisionLayer = 8u;
        CollisionMask = 2u | 4u;
        GravityScale = 0f;

        var mesh = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.25f, Height = 0.5f },
            Name = "ProjMesh"
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.7f, 0.2f),
            EmissionEnergyMultiplier = 1.5f,
            EmissionEnabled = true,
            Emission = new Color(1f, 0.6f, 0.1f)
        };
        mesh.MaterialOverride = mat;
        AddChild(mesh);
    }

    /// <summary>Initializes a homing projectile spawned by a weapon.</summary>
    public void Initialize(Combatant from, Unit target, int damage, float speed)
    {
        Source = from;
        HomingTarget = target;
        Damage = damage;
        FlightSpeed = speed;
        GlobalPosition = from.GlobalPosition + new Vector3(0f, 1f, 0f);
    }

    public override void _Process(double delta)
    {
        if (_dead) return;
        _age += (float)delta;
        if (_age >= Lifetime)
        {
            QueueFree();
            return;
        }

        // Homing behavior: steer toward the target's current position.
        if (HomingTarget != null)
        {
            if (HomingTarget.State == UnitState.Dead || !IsInstanceValid(HomingTarget))
            {
                // Target died mid-flight — let the projectile fly on and expire.
                HomingTarget = null;
            }
            else
            {
                var aim = HomingTarget.GlobalPosition + new Vector3(0f, 1f, 0f);
                var to = aim - GlobalPosition;
                var dist = to.Length();
                if (dist < 0.6f)
                {
                    ResolveHit(HomingTarget);
                    return;
                }
                var dir = to.Normalized();
                LinearVelocity = dir * FlightSpeed;
                LookAt(aim, Vector3.Up);
            }
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (_dead) return;
        _dead = true;

        if (SplashRadius > 0f)
            ApplySplash();
        else if (body is Unit u && u != Source)
            u.TakeDamage(Damage);

        QueueFree();
    }

    private void ResolveHit(Unit target)
    {
        if (_dead) return;
        _dead = true;
        if (SplashRadius > 0f)
            ApplySplash();
        else if (WillHit && target != Source && target.State != UnitState.Dead)
            target.TakeDamage(Damage);
        QueueFree();
    }

    private void ApplySplash()
    {
        var registry = AIWarSandbox.Autoloads.UnitRegistry.Instance;
        if (registry == null) return;
        foreach (var u in registry.All)
        {
            if (u == Source || u.State == UnitState.Dead) continue;
            if (u.GlobalPosition.DistanceTo(GlobalPosition) <= SplashRadius)
                u.TakeDamage(Damage);
        }
    }

    public static Projectile Create(Vector3 origin, Vector3 velocity, Weapon weapon, Unit source)
    {
        var p = new Projectile
        {
            Damage = weapon.Damage,
            SplashRadius = weapon.SplashRadius,
            Source = source,
            FlightSpeed = weapon.ProjectileSpeed,
            Position = origin
        };
        p.LinearVelocity = velocity;
        return p;
    }
}
