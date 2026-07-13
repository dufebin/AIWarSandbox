using System.Collections.Generic;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;
using AIWarSandbox.World;

namespace AIWarSandbox.Ai;

/// <summary>
/// Enemy command AI. On BattleStarted, periodically issues advance/rally/reinforce
/// orders to idle enemy combatants. Aggression scales with Difficulty.
/// Friendly units remain under OrderExecutor + player micro.
/// </summary>
public partial class EnemyDirector : Node
{
    private TerrainGenerator? _terrain;
    private int _difficulty = 1;
    private bool _active;
    private float _timer;
    private float _interval = 2f;
    private int _wave;

    /// <summary>0=passive, 1=normal, 2=aggressive.</summary>
    public int Difficulty
    {
        get => _difficulty;
        set
        {
            _difficulty = Mathf.Clamp(value, 0, 2);
            _interval = _difficulty switch
            {
                0 => 3.5f,
                2 => 1.2f,
                _ => 2.0f,
            };
        }
    }

    public void Bind(TerrainGenerator terrain) => _terrain = terrain;

    public override void _Ready()
    {
        EventBus.Instance.BattleStarted += OnBattleStarted;
        EventBus.Instance.BattleEnded += OnBattleEnded;
        EventBus.Instance.DamageDealt += OnDamageDealt;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance == null) return;
        EventBus.Instance.BattleStarted -= OnBattleStarted;
        EventBus.Instance.BattleEnded -= OnBattleEnded;
        EventBus.Instance.DamageDealt -= OnDamageDealt;
    }

    private void OnBattleStarted()
    {
        _active = true;
        _timer = 0.5f;
        _wave = 0;
        EventBus.Instance.RaiseLog($"[EnemyDirector] Active — difficulty={Difficulty}, interval={_interval:F1}s");
    }

    private void OnBattleEnded(bool _) => _active = false;

    public override void _Process(double delta)
    {
        if (!_active) return;
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = _interval;
        IssueOrders();
    }

    private void IssueOrders()
    {
        var registry = UnitRegistry.Instance;
        if (registry == null) return;

        var friendlyBase = FindBase(true);
        var enemyCombatants = new List<Combatant>();
        foreach (var u in registry.Enemy)
        {
            if (u is Combatant c && c.State != UnitState.Dead)
                enemyCombatants.Add(c);
        }
        if (enemyCombatants.Count == 0) return;

        _wave++;
        float fraction = Difficulty switch { 0 => 0.25f, 2 => 0.7f, _ => 0.45f };
        int advanceCount = Mathf.Max(1, Mathf.RoundToInt(enemyCombatants.Count * fraction));

        bool flank = Difficulty >= 2 && (_wave % 2 == 0);
        Vector3 objective = friendlyBase?.GlobalPosition ?? Vector3.Zero;
        if (flank)
            objective += new Vector3((_wave % 4 < 2) ? 18f : -18f, 0f, 0f);

        if (_terrain != null)
            objective = _terrain.SnapToGround(objective);

        int ordered = 0;
        foreach (var c in enemyCombatants)
        {
            if (c.CurrentTarget != null && c.CurrentTarget.State != UnitState.Dead)
                continue;

            if (HasNearbyHostile(c, registry.Friendly, 55f))
            {
                c.AcquireTarget();
                continue;
            }

            if (ordered >= advanceCount) break;

            float angle = ordered * 0.7f;
            var dest = objective + new Vector3(Mathf.Cos(angle) * 3f, 0f, Mathf.Sin(angle) * 3f);
            if (_terrain != null) dest = _terrain.SnapToGround(dest);
            c.MoveTo(dest);
            ordered++;
        }

        if (ordered > 0)
            EventBus.Instance.RaiseLog($"[EnemyDirector] Wave {_wave}: {ordered} units advancing");
    }

    private void OnDamageDealt(Unit target, int damage)
    {
        if (!_active || target == null || target.IsFriendly) return;
        if (target is not Combatant victim || victim.State == UnitState.Dead) return;

        var registry = UnitRegistry.Instance;
        if (registry == null) return;

        const float callRadius = 22f;
        int called = 0;
        int maxCall = Difficulty >= 2 ? 4 : 2;
        foreach (var u in registry.Enemy)
        {
            if (called >= maxCall) break;
            if (u is not Combatant c || c == victim || c.State == UnitState.Dead) continue;
            if (c.CurrentTarget != null && c.CurrentTarget.State != UnitState.Dead) continue;
            if (c.GlobalPosition.DistanceSquaredTo(victim.GlobalPosition) > callRadius * callRadius) continue;
            c.MoveTo(victim.GlobalPosition);
            called++;
        }
    }

    private static bool HasNearbyHostile(Combatant self, IReadOnlyList<Unit> hostiles, float range)
    {
        float r2 = range * range;
        foreach (var u in hostiles)
        {
            if (u == null || u.State == UnitState.Dead) continue;
            if (self.GlobalPosition.DistanceSquaredTo(u.GlobalPosition) <= r2) return true;
        }
        return false;
    }

    private static Structure? FindBase(bool friendly)
    {
        var list = friendly ? UnitRegistry.Instance.Friendly : UnitRegistry.Instance.Enemy;
        foreach (var u in list)
        {
            if (u is Structure s && s.Kind == StructureKind.Base && s.State != UnitState.Dead)
                return s;
        }
        return null;
    }
}
