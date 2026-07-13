using Godot;
using AIWarSandbox.Kb;

namespace AIWarSandbox.Units;

public enum UnitState
{
    Idle,
    Moving,
    Attacking,
    Dead
}

public enum CombatStance
{
    Aggressive,
    HoldGround,
    StandGround
}

public partial class Unit : CharacterBody3D
{
    public bool IsFriendly { get; set; } = true;
    public UnitState State { get; protected set; } = UnitState.Idle;

    public int MaxHealth { get; set; } = 100;
    public int Health { get; protected set; } = 100;

    public int MaxHp { get => MaxHealth; set => MaxHealth = value; }
    public int Hp { get => Health; set => Health = value; }

    protected virtual float HealthBarHeight => 2.4f;

    private float _deathFade;
    private bool _dying;

    public override void _Ready()
    {
        AIWarSandbox.Autoloads.UnitRegistry.Instance?.Register(this);
        EntityGraph.Instance?.Register(this, EntityType.Platform, Name, IsFriendly);
        Health = MaxHealth;
        AddChild(HealthBar3D.For(this, HealthBarHeight));
        AddChild(TeamMarker.For(this));
        AIWarSandbox.Autoloads.EventBus.Instance?.RaiseUnitSpawned(this);
        GD.Print($"[Unit] {Name} spawned friendly={IsFriendly} hp={Health}/{MaxHealth}");
    }

    public override void _ExitTree()
    {
        AIWarSandbox.Autoloads.UnitRegistry.Instance?.Unregister(this);
        EntityGraph.Instance?.Unregister((int)GetInstanceId());
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!_dying) return;
        _deathFade += (float)delta;
        // Tip over + shrink for death feel (Node3D has no Modulate).
        float t = Mathf.Clamp(_deathFade / 0.9f, 0f, 1f);
        Rotation = new Vector3(Mathf.DegToRad(75f * t), Rotation.Y, Rotation.Z);
        Scale = Vector3.One * Mathf.Lerp(1f, 0.15f, t);
        if (_deathFade >= 0.9f)
            QueueFree();
    }

    public virtual void TakeDamage(int amount)
    {
        if (State == UnitState.Dead) return;
        Health = Mathf.Max(0, Health - amount);
        AIWarSandbox.Autoloads.EventBus.Instance?.RaiseDamageDealt(this, amount);
        var scene = GetTree()?.CurrentScene;
        if (scene != null)
            CombatFx.HitSpark(scene, GlobalPosition + new Vector3(0, 1f, 0));
        GD.Print($"[Unit] {Name} took {amount} dmg -> hp={Health}/{MaxHealth}");
        if (Health <= 0) Die();
    }

    protected virtual void Die()
    {
        State = UnitState.Dead;
        var bus = AIWarSandbox.Autoloads.EventBus.Instance;
        if (bus != null)
        {
            bus.RaiseUnitDied(this);
            if (this is Structure s) bus.RaiseStructureDestroyed(s);
        }
        GD.Print($"[Unit] {Name} died");
        SfxBus.Play(this, SfxBus.Kind.Death);
        SetPhysicsProcess(false);
        CollisionLayer = 0u;
        CollisionMask = 0u;
        _dying = true;
        _deathFade = 0f;
        SetProcess(true);
    }
}
