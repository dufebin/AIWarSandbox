using System.Collections.Generic;
using Godot;
using AIWarSandbox.Ai;
using AIWarSandbox.Ui;
using AIWarSandbox.World;

namespace AIWarSandbox.Autoloads;

public partial class TacticalAIManager : Node
{
    public static TacticalAIManager Instance { get; private set; } = null!;

    private PlanGenerator _generator = new();
    private OrderExecutor _executor = new();
    private ConfidenceScorer _scorer = new();
    private List<Plan> _plans = new();
    private TerrainGenerator? _terrain;

    public IReadOnlyList<Plan> Plans => _plans;
    public Plan? ActivePlan { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        AddChild(_generator);
        AddChild(_executor);
        AddChild(_scorer);
    }

    public void Bind(TerrainGenerator terrain)
    {
        _terrain = terrain;
        _generator.Bind(terrain);
        _executor.Bind(terrain);
    }

    public List<Plan> GeneratePlans(EnemyConfig cfg)
    {
        _plans = _generator.Generate(cfg);
        foreach (var plan in _plans)
        {
            plan.Confidence = _scorer.Score(plan);
            plan.EvidenceSummary = plan.Confidence?.Summary;
        }
        return _plans;
    }

    public void ExecutePlan(int index)
    {
        if (index < 0 || index >= _plans.Count) return;
        ActivePlan = _plans[index];
        _executor.Execute(ActivePlan);
        EventBus.Instance.RaisePlanSelected(index);
    }

    public void Halt() => _executor.Stop();
}
