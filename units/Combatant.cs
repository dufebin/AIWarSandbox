using Godot;

namespace AIWarSandbox.Units;

public partial class Combatant : Unit
{
    public Weapon Weapon { get; protected set; } = new();

    public float AttackRange => Weapon?.Range ?? 0f;

    public Combatant? Target
    {
        get => CurrentTarget as Combatant;
        set => CurrentTarget = value;
    }

    public Unit? CurrentTarget { get; protected set; }
    public float MoveSpeed { get; protected set; } = 8f;
    public float Armor { get; protected set; } = 0f;
    public CombatStance Stance { get; set; } = CombatStance.Aggressive;

    protected float _reloadLeft;
    private Vector3 _moveDest;
    private bool _hasMoveDest;

    private const float TurnSpeed = 9f;
    private const float Gravity = 26f;
    private const float DetectionRange = 55f;

    public override void _Ready()
    {
        base._Ready();
        _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
        CollisionLayer = 2u;
        CollisionMask = 1u | 4u;

        AddChild(new CollisionShape3D
        {
            Name = "BodyShape",
            Shape = new CapsuleShape3D { Radius = 0.4f, Height = 1.6f },
            Position = new Vector3(0f, 0.8f, 0f),
        });

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.8f, 1.6f, 0.8f) },
            Position = new Vector3(0f, 0.8f, 0f),
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

        Vector3 horiz = Vector3.Zero;
        if (_hasMoveDest)
        {
            var to = _moveDest - GlobalPosition;
            to.Y = 0;
            if (to.Length() < 0.4f)
            {
                _hasMoveDest = false;
                if (State != UnitState.Attacking) State = UnitState.Idle;
            }
            else
            {
                horiz = to.Normalized() * MoveSpeed;
                State = UnitState.Moving;
                FaceToward(_moveDest, dt);
            }
        }

        float vy = Velocity.Y;
        if (IsOnFloor()) vy = Mathf.Max(vy, -1f);
        else vy -= Gravity * dt;
        Velocity = new Vector3(horiz.X, vy, horiz.Z);
        MoveAndSlide();

        // StandGround: no auto-acquire; only fire if already has an explicit target in range.
        if (Stance == CombatStance.StandGround)
        {
            if (CurrentTarget != null && CurrentTarget.State != UnitState.Dead)
            {
                var dist = GlobalPosition.DistanceTo(CurrentTarget.GlobalPosition);
                if (dist <= AttackRange && Weapon.CanFire())
                {
                    State = UnitState.Attacking;
                    FaceToward(CurrentTarget.GlobalPosition, dt);
                    Weapon.Fire(this, CurrentTarget);
                    _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
                }
            }
            return;
        }

        if (CurrentTarget == null || CurrentTarget.State == UnitState.Dead)
            AcquireTarget();

        if (CurrentTarget != null && CurrentTarget.State != UnitState.Dead)
        {
            var dist = GlobalPosition.DistanceTo(CurrentTarget.GlobalPosition);
            if (dist <= AttackRange)
            {
                if (State != UnitState.Moving) State = UnitState.Attacking;
                FaceToward(CurrentTarget.GlobalPosition, dt);
                if (Weapon.CanFire())
                {
                    Weapon.Fire(this, CurrentTarget);
                    _reloadLeft = 1f / Mathf.Max(0.1f, Weapon.RateOfFire);
                }
            }
            else if (!_hasMoveDest && Stance == CombatStance.Aggressive)
            {
                // HoldGround: do not chase outside weapon range.
                MoveTo(CurrentTarget.GlobalPosition);
            }
        }
        else
        {
            CurrentTarget = null;
            if (State == UnitState.Attacking) State = UnitState.Idle;
        }
    }

    public void AcquireTarget()
    {
        if (Stance == CombatStance.StandGround) return;

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
        // HoldGround only looks within weapon range; Aggressive uses detection range.
        float acquireRange = Stance == CombatStance.HoldGround ? AttackRange : DetectionRange;
        float rangeSqr = acquireRange * acquireRange;
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

    private void FaceToward(Vector3 worldTarget, float dt)
    {
        var to = worldTarget - GlobalPosition;
        to.Y = 0f;
        if (to.LengthSquared() < 0.0004f) return;
        float targetYaw = Mathf.Atan2(to.X, to.Z);
        float yaw = Mathf.LerpAngle(Rotation.Y, targetYaw, Mathf.Min(1f, dt * TurnSpeed));
        Rotation = new Vector3(Rotation.X, yaw, Rotation.Z);
    }

    public void Attack(Unit target) => CurrentTarget = target;

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

    protected virtual void Fire(Unit target) => Weapon.Fire(this, target);
}
