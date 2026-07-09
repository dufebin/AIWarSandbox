using System.Collections.Generic;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;
using AIWarSandbox.World;

namespace AIWarSandbox.Ai;

public partial class OrderExecutor : Node
{
    private TerrainGenerator? _terrain;
    private Plan? _active;
    private bool _halted;
    private float _replanTimer;
    private const float ReplanInterval = 2f;

    public void Bind(TerrainGenerator terrain) => _terrain = terrain;

    public void Execute(Plan plan)
    {
        _active = plan;
        _halted = false;

        // Briefing event: UI / listeners can show the plan summary.
        EventBus.Instance.RaisePlanExecuting(plan);
        EventBus.Instance.RaiseLog($"[OrderExecutor] Executing plan '{plan.Name}' ({plan.Type}). " +
            $"Expected casualty rate {plan.ExpectedCasualtyRate:P0}, success {plan.SuccessProbability:P0}.");

        // Emit per-unit order logs from the new UnitAssignments map.
        // We do NOT modify Unit.cs; if a MoveTo/Attack API exists we call it, else we just log.
        LogUnitAssignments(plan);

        // Also drive the legacy formation-based movement/attack loop.
        AssignOrders();
        EventBus.Instance.RaiseBattleStarted();
    }

    public void Stop()
    {
        _halted = true;
        _active = null;
        EventBus.Instance.RaiseLog("[OrderExecutor] Halted. No further orders will be issued.");
    }

    public override void _Process(double delta)
    {
        if (_halted || _active == null) return;
        _replanTimer -= (float)delta;
        if (_replanTimer <= 0f)
        {
            _replanTimer = ReplanInterval;
            AssignOrders();
        }
    }

    /// <summary>
    /// Logs each friendly unit's assigned slot target. Uses Unit.MoveTo when the API
    /// is available (Combatant exposes it); otherwise just logs via EventBus.
    /// </summary>
    private static void LogUnitAssignments(Plan plan)
    {
        var friendly = UnitRegistry.Instance.Friendly;
        foreach (var kv in plan.UnitAssignments)
        {
            int idx = kv.Key;
            Vector3 target = kv.Value;
            if (idx < 0 || idx >= friendly.Count) continue;
            var u = friendly[idx];
            if (u.State == UnitState.Dead) continue;

            EventBus.Instance.RaiseLog($"[Order] {u.Name} -> {target}");
            if (u is Combatant c) c.MoveTo(target);
        }
    }

    private void AssignOrders()
    {
        if (_active == null) return;
        var enemy = UnitRegistry.Instance.Enemy;
        foreach (var formation in _active.Formations)
        {
            for (int i = 0; i < formation.Units.Count; i++)
            {
                var u = formation.Units[i];
                if (u is not Combatant c || c.State == UnitState.Dead) continue;

                var target = NearestEnemy(c, enemy);
                if (target != null)
                {
                    c.Attack(target);
                    continue;
                }

                Vector3 dest = formation.AttackVector * (i * 2.5f) + _active.Objective;
                if (_terrain != null) dest.Y = _terrain.SampleHeight(dest.X, dest.Z);
                c.MoveTo(dest);
            }
        }
    }

    private static Unit? NearestEnemy(Combatant c, IReadOnlyList<Unit> enemy)
    {
        Unit? best = null;
        float bestD = float.MaxValue;
        foreach (var e in enemy)
        {
            if (e.State == UnitState.Dead) continue;
            float d = c.GlobalPosition.DistanceSquaredTo(e.GlobalPosition);
            if (d < bestD) { bestD = d; best = e; }
        }
        return best;
    }
}
