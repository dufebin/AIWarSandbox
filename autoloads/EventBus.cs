using Godot;
using AIWarSandbox.Ai;
using AIWarSandbox.Ui;

namespace AIWarSandbox.Autoloads;

public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
    }

    public event System.Action<string>? ErrorLogged;
    public event System.Action<string>? LogMessage;
    public event System.Action? BattleStarted;
    public event System.Action<bool>? BattleEnded;
    public event System.Action<int>? PlanSelected;
    public event System.Action<Plan>? PlanExecuting;
    public event System.Action<EnemyConfig>? ConfigSubmitted;
    public event System.Action<AIWarSandbox.Units.Unit>? UnitSpawned;
    public event System.Action<AIWarSandbox.Units.Unit>? UnitDied;
    public event System.Action<AIWarSandbox.Units.Unit, int>? DamageDealt;
    public event System.Action<AIWarSandbox.Units.Structure>? StructureDestroyed;
    public event System.Action<IntelligenceRegistry.Track>? TrackUpdated;
    public event System.Action<int>? TrackLost;

    public void LogError(string message)
    {
        GD.PushError(message);
        ErrorLogged?.Invoke(message);
    }

    public void RaiseLog(string message) => LogMessage?.Invoke(message);
    public void RaiseBattleStarted() => BattleStarted?.Invoke();
    public void RaiseBattleEnded(bool victory) => BattleEnded?.Invoke(victory);
    public void RaisePlanSelected(int planIndex) => PlanSelected?.Invoke(planIndex);
    public void RaisePlanExecuting(Plan plan) => PlanExecuting?.Invoke(plan);
    public void RaiseConfigSubmitted(EnemyConfig cfg) => ConfigSubmitted?.Invoke(cfg);
    public void RaiseUnitSpawned(AIWarSandbox.Units.Unit u) => UnitSpawned?.Invoke(u);
    public void RaiseUnitDied(AIWarSandbox.Units.Unit u) => UnitDied?.Invoke(u);
    public void RaiseDamageDealt(AIWarSandbox.Units.Unit target, int damage) => DamageDealt?.Invoke(target, damage);
    public void RaiseStructureDestroyed(AIWarSandbox.Units.Structure s) => StructureDestroyed?.Invoke(s);
    public void RaiseTrackUpdated(IntelligenceRegistry.Track t) => TrackUpdated?.Invoke(t);
    public void RaiseTrackLost(int trackId) => TrackLost?.Invoke(trackId);
}
