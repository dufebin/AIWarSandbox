using Godot;

namespace AIWarSandbox.Units;

public partial class Combatant : Unit
{
    public Weapon Weapon { get; protected set; } = new();

    /// <summary>Effective attack range, derived from the equipped <see cref="Weapon"/>.</summary>
    public float AttackRange => Weapon?.Range ?? 0f;

    /// <summary>Primary combat target (nullable). Alias of <see cref="CurrentTarget"/>.</summary>
    public Combatant? Target
    {
        get => CurrentTarget as Combatant;
        set => CurrentTarget = value;
    }

    public Unit? CurrentTarget { get; protected set; }
    public float MoveSpeed { get; protected set; } = 8f;
    public float Armor { get; protected set; } = 0f;

    protected float _reloadLeft;
    private Vector3 _moveDest;
    private bool _hasMoveDest;

    public override void _Ready()
    {
        base._Ready();
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
        CollisionLayer = 2u;
        CollisionMask = 1u | 2u;

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.8f, 1.6f, 0.8f) },
            Name = "Body"
        };
        mesh.MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = IsFriendly ? new Color(0.2f, 0.4f, 0.9f) : new Color(0.9f, 0.2f, 0.2f)
        };
        AddChild(mesh);
    }

    public override void _PhysicsProcess(double delta)
    {
        var dt = (float)delta;

        if (_hasMoveDest)
        {
            var to = _moveDest - GlobalPosition;
            to.Y = 0;
            if (to.Length() < 0.3f)
            {
                _hasMoveDest = false;
                if (State != UnitState.Attacking) State = UnitState.Idle;
            }
            else
            {
                var dir = to.Normalized();
                Velocity = new Vector3(dir.X * MoveSpeed, Velocity.Y, dir.Z * MoveSpeed);
                State = UnitState.Moving;
                MoveAndSlide();
            }
        }
        else
        {
            Velocity = new Vector3(0, Velocity.Y, 0);
        }

        // Acquire / maintain target.
        if (CurrentTarget == null || CurrentTarget.State == UnitState.Dead)
        {
            AcquireTarget();
        }

        if (CurrentTarget != null && CurrentTarget.State != UnitState.Dead)
        {
            var dist = GlobalPosition.DistanceTo(CurrentTarget.GlobalPosition);
            if (dist <= AttackRange)
            {
                if (State != UnitState.Moving) State = UnitState.Attacking;
                // Use the weapon's own cooldown via CanFire/Fire.
                if (Weapon.CanFire())
                {
                    Weapon.Fire(this, CurrentTarget);
                    _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
                }
            }
            else if (!_hasMoveDest)
            {
                MoveTo(CurrentTarget.GlobalPosition);
            }
        }
        else
        {
            CurrentTarget = null;
            if (State == UnitState.Attacking) State = UnitState.Idle;
        }
    }

    /// <summary>
    /// Friendly combatants target through the fog of war: query
    /// <see cref="IntelligenceRegistry.NearestKnownEnemy"/> for a high-confidence
    /// track, resolve it to a live unit via <see cref="IntelligenceRegistry.Track.LinkedUnitId"/>,
    /// and fall back to ground truth within attack range. Enemy combatants use
    /// ground truth directly (asymmetric AI).
    /// </summary>
    public void AcquireTarget()
    {
        var registry = AIWarSandbox.Autoloads.UnitRegistry.Instance;
        if (registry == null) return;

        if (IsFriendly)
        {
            var intel = AIWarSandbox.Autoloads.IntelligenceRegistry.Instance;
            if (intel != null)
            {
                var track = intel.NearestKnownEnemy(GlobalPosition);
                if (track != null && track.LinkedUnitId.HasValue)
                {
                    foreach (var u in registry.Enemy)
                    {
                        if (u == null || u.State == UnitState.Dead) continue;
                        if ((int)u.GetInstanceId() == track.LinkedUnitId.Value)
                        {
                            CurrentTarget = u;
                            return;
                        }
                    }
                }
            }
        }

        var candidates = IsFriendly ? registry.Enemy : registry.Friendly;
        Unit? nearest = null;
        float bestSqr = float.MaxValue;
        float rangeSqr = AttackRange * AttackRange;
        foreach (var u in candidates)
        {
            if (u == null || u.State == UnitState.Dead) continue;
            var sqr = GlobalPosition.DistanceSquaredTo(u.GlobalPosition);
            if (sqr < bestSqr && sqr <= rangeSqr)
            {
                bestSqr = sqr;
                nearest = u;
            }
        }
        CurrentTarget = nearest;
    }

    public void MoveTo(Vector3 dest)
    {
        _moveDest = dest;
        _hasMoveDest = true;
    }

    public void Attack(Unit target)
    {
        CurrentTarget = target;
    }

    public void Hold()
    {
        _hasMoveDest = false;
        CurrentTarget = null;
        State = UnitState.Idle;
    }

    public override void TakeDamage(int amount)
    {
        if (State == UnitState.Dead) return;
        int mitigated = Mathf.Max(1, Mathf.RoundToInt(amount * (1f - Armor)));
        base.TakeDamage(mitigated);
    }

    protected virtual void Fire(Unit target)
    {
        // Legacy direct-fire path; delegates to the weapon system.
        Weapon.Fire(this, target);
    }
}
