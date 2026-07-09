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

public partial class Unit : CharacterBody3D
{
    public bool IsFriendly { get; set; } = true;
    public UnitState State { get; protected set; } = UnitState.Idle;

    public int MaxHealth { get; set; } = 100;
    public int Health { get; protected set; } = 100;

    /// <summary>Alias for <see cref="MaxHealth"/>.</summary>
    public int MaxHp { get => MaxHealth; set => MaxHealth = value; }
    /// <summary>Alias for <see cref="Health"/>.</summary>
    public int Hp { get => Health; set => Health = value; }

    public override void _Ready()
    {
        AIWarSandbox.Autoloads.UnitRegistry.Instance?.Register(this);
        EntityGraph.Instance?.Register(this, EntityType.Platform, Name, IsFriendly);
        Health = MaxHealth;
        AIWarSandbox.Autoloads.EventBus.Instance?.RaiseUnitSpawned(this);
        GD.Print($"[Unit] {Name} spawned friendly={IsFriendly} hp={Health}/{MaxHealth}");
    }

    public override void _ExitTree()
    {
        AIWarSandbox.Autoloads.UnitRegistry.Instance?.Unregister(this);
        EntityGraph.Instance?.Unregister((int)GetInstanceId());
        base._ExitTree();
    }

    public virtual void TakeDamage(int amount)
    {
        if (State == UnitState.Dead) return;
        Health = Mathf.Max(0, Health - amount);
        AIWarSandbox.Autoloads.EventBus.Instance?.RaiseDamageDealt(this, amount);
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
        // Hide + disable instead of immediate QueueFree so death events can still reference the node.
        Hide();
        SetPhysicsProcess(false);
        SetProcess(false);
        QueueFree();
    }
}
