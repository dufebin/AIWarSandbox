using System.Collections.Generic;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Kb;
using AIWarSandbox.Units;
using AIWarSandbox.Ui;
using AIWarSandbox.World;

namespace AIWarSandbox.Ai;

public partial class PlanGenerator : Node
{
    private TerrainGenerator? _terrain;
    private RandomNumberGenerator _rng = new();
    private PlanSimulator _simulator = new();

    public void Bind(TerrainGenerator terrain) => _terrain = terrain;

    public List<Plan> Generate(EnemyConfig enemyCfg)
    {
        // Seed RNG from cfg so the same briefing is reproducible.
        _rng.Seed = (ulong)(enemyCfg.GetHashCode() ^ (enemyCfg.Difficulty * 2654435761u));

        var friendly = new List<Unit>(UnitRegistry.Instance.Friendly);
        var enemyStructures = EnemyStructures();
        Vector3 objective = enemyStructures.Count > 0
            ? enemyStructures[0].GlobalPosition
            : Vector3.Zero;

        Vector3 rally = friendly.Count > 0 ? friendly[0].GlobalPosition : new Vector3(-40, 0, -40);
        float baseDist = rally.DistanceTo(objective);
        Vector3 facing = (objective - rally);
        facing.Y = 0;
        if (facing.LengthSquared() < 1e-3f) facing = Vector3.Forward;
        facing = facing.Normalized();

        int friendlyCount = Mathf.Max(1, friendly.Count);

        var plans = new List<Plan>
        {
            BuildFrontal(friendly, rally, objective, facing, baseDist, friendlyCount, enemyCfg),
            BuildFlanking(friendly, rally, objective, facing, baseDist, friendlyCount, enemyCfg),
            BuildAirborne(friendly, rally, objective, facing, baseDist, friendlyCount, enemyCfg),
            BuildRecon(friendly, rally, objective, facing, baseDist, friendlyCount, enemyCfg)
        };
        return plans;
    }

    private static List<Structure> EnemyStructures()
    {
        var list = new List<Structure>();
        // Prefer the EntityGraph ontology view; fall back to UnitRegistry if the
        // graph is unavailable (e.g. running in a stripped test context).
        var graph = EntityGraph.Instance;
        if (graph != null)
        {
            foreach (var e in graph.ByType(EntityType.Structure))
            {
                if (e.IsFriendly) continue;
                if (e.GodotNode.TryGetTarget(out var node) && node is Structure s && s.State != UnitState.Dead)
                    list.Add(s);
            }
            if (list.Count > 0) return list;
        }
        foreach (var u in UnitRegistry.Instance.Enemy)
            if (u is Structure s && s.State != UnitState.Dead)
                list.Add(s);
        return list;
    }

    /// <summary>Run Monte Carlo simulation and populate plan.Outcome + derived estimates.</summary>
    private void SimulateInto(Plan plan)
    {
        var outcome = _simulator.Simulate(plan);
        plan.Outcome = outcome;
        if (outcome.Simulations > 0)
        {
            plan.EstimatedCasualties = Mathf.RoundToInt(outcome.MeanCasualties);
            plan.SuccessProbability = outcome.SuccessRate;
            plan.EstimatedDuration = outcome.MeanDurationSec;
            plan.EstimatedDurationSec = Mathf.RoundToInt(outcome.MeanDurationSec);
            int friendlyCount = plan.Formations != null && plan.Formations.Count > 0
                ? SumFormationUnits(plan)
                : Mathf.Max(1, UnitRegistry.Instance.Friendly.Count);
            plan.ExpectedCasualtyRate = Mathf.Clamp(outcome.MeanCasualties / friendlyCount, 0f, 1f);
        }
    }

    private static int SumFormationUnits(Plan plan)
    {
        int n = 0;
        foreach (var fa in plan.Formations!)
            n += fa.Units.Count;
        return Mathf.Max(1, n);
    }

    private Plan BuildFrontal(List<Unit> friendly, Vector3 rally, Vector3 obj,
        Vector3 facing, float dist, int count, EnemyConfig cfg)
    {
        float casRate = 0.40f + cfg.Difficulty * 0.02f;
        float succ = 0.70f - cfg.Difficulty * 0.03f;
        var slots = FormationPlanner.LineFormation(obj - facing * 6f, facing, count, 3.0f);
        var path = Pathfinder.FindPath(rally, obj, _terrain);
        var waypoints = new List<Vector3> { rally };
        waypoints.AddRange(path);

        var plan = new Plan
        {
            Type = PlanType.FrontalAssault,
            Name = "正面强攻",
            Description = "全军集中火力沿最短路径直冲敌方基地，速战速决。高风险高回报，依靠数量优势压制守军。",
            Risk = RiskLevel.High,
            ExpectedCasualtyRate = casRate,
            SuccessProbability = succ,
            EstimatedCasualties = Mathf.RoundToInt(count * casRate),
            EstimatedDuration = dist / 12f,
            EstimatedDurationSec = Mathf.RoundToInt(dist / 12f),
            Objective = obj,
            Waypoints = waypoints,
            Slots = ToSlots(slots, "line"),
            Formations = FormationPlanner.Split(friendly, obj, PlanType.FrontalAssault)
        };
        AssignUnits(plan, friendly, slots);
        SimulateInto(plan);
        return plan;
    }

    private Plan BuildFlanking(List<Unit> friendly, Vector3 rally, Vector3 obj,
        Vector3 facing, float dist, int count, EnemyConfig cfg)
    {
        float casRate = 0.15f + cfg.Difficulty * 0.01f;
        float succ = 0.55f - cfg.Difficulty * 0.02f;

        // Approach from a flank offset, avoiding the central plateau.
        Vector3 perp = new(-facing.Z, 0, facing.X);
        Vector3 flankRally = rally + perp * 30f;
        Vector3 flankObj = obj + perp * 25f;

        var pathA = Pathfinder.FindPath(rally, flankRally, _terrain);
        var pathB = Pathfinder.FindPath(flankRally, flankObj, _terrain);

        var waypoints = new List<Vector3> { rally };
        waypoints.AddRange(pathA);
        waypoints.AddRange(pathB);

        var slots = FormationPlanner.WedgeFormation(flankObj - facing * 6f, facing, count, 3.0f);

        var plan = new Plan
        {
            Type = PlanType.FlankingManeuver,
            Name = "侧翼包抄",
            Description = "分兵沿侧翼绕行避开中央高地，以楔形阵从侧后突入。低伤亡，中等成功率。",
            Risk = RiskLevel.Medium,
            ExpectedCasualtyRate = casRate,
            SuccessProbability = succ,
            EstimatedCasualties = Mathf.RoundToInt(count * casRate),
            EstimatedDuration = dist / 9f + 15f,
            EstimatedDurationSec = Mathf.RoundToInt(dist / 9f + 15f),
            Objective = obj,
            Waypoints = waypoints,
            Slots = ToSlots(slots, "wedge"),
            Formations = FormationPlanner.Split(friendly, obj, PlanType.FlankingManeuver)
        };
        AssignUnits(plan, friendly, slots);
        SimulateInto(plan);
        return plan;
    }

    private Plan BuildAirborne(List<Unit> friendly, Vector3 rally, Vector3 obj,
        Vector3 facing, float dist, int count, EnemyConfig cfg)
    {
        float casRate = 0.50f + cfg.Difficulty * 0.02f;
        float succ = 0.80f - cfg.Difficulty * 0.02f;

        // Skip pathfinding: drop directly on the objective.
        var slots = FormationPlanner.ColumnFormation(obj, facing, count, 2.5f);
        var waypoints = new List<Vector3> { rally, obj };

        var plan = new Plan
        {
            Type = PlanType.AirborneDrop,
            Name = "空降突袭",
            Description = "部队直接空降至敌方基地顶部，无视地形与路径。伤亡极高但成功率高，适合奇袭。",
            Risk = RiskLevel.High,
            ExpectedCasualtyRate = casRate,
            SuccessProbability = succ,
            EstimatedCasualties = Mathf.RoundToInt(count * casRate),
            EstimatedDuration = dist / 30f + 5f,
            EstimatedDurationSec = Mathf.RoundToInt(dist / 30f + 5f),
            Objective = obj,
            Waypoints = waypoints,
            Slots = ToSlots(slots, "column"),
            Formations = FormationPlanner.Split(friendly, obj, PlanType.AirborneDrop)
        };
        AssignUnits(plan, friendly, slots);
        SimulateInto(plan);
        return plan;
    }

    private Plan BuildRecon(List<Unit> friendly, Vector3 rally, Vector3 obj,
        Vector3 facing, float dist, int count, EnemyConfig cfg)
    {
        float casRate = 0.10f;
        float succ = 0.30f;
        // Short advance: a third of the way to the objective.
        Vector3 advance = rally + (obj - rally) * (1f / 3f);
        var slots = FormationPlanner.LineFormation(advance, facing, count, 3.0f);
        var path = Pathfinder.FindPath(rally, advance, _terrain);
        var waypoints = new List<Vector3> { rally };
        waypoints.AddRange(path);

        var plan = new Plan
        {
            Type = PlanType.ReconInForce,
            Name = "武力侦察",
            Description = "以线形阵前出至半程试探敌情，遇敌即撤。低伤亡低成功（不以攻克为目标）。",
            Risk = RiskLevel.Low,
            ExpectedCasualtyRate = casRate,
            SuccessProbability = succ,
            EstimatedCasualties = Mathf.RoundToInt(count * casRate),
            EstimatedDuration = dist / 18f,
            EstimatedDurationSec = Mathf.RoundToInt(dist / 18f),
            Objective = advance,
            Waypoints = waypoints,
            Slots = ToSlots(slots, "line"),
            Formations = FormationPlanner.Split(friendly, obj, PlanType.ReconInForce)
        };
        AssignUnits(plan, friendly, slots);
        SimulateInto(plan);
        return plan;
    }

    private static void AssignUnits(Plan plan, List<Unit> friendly, List<Vector3> slots)
    {
        plan.UnitAssignments.Clear();
        for (int i = 0; i < friendly.Count && i < slots.Count; i++)
            plan.UnitAssignments[i] = slots[i];
    }

    private static List<FormationSlot> ToSlots(List<Vector3> positions, string role)
    {
        var list = new List<FormationSlot>(positions.Count);
        foreach (var p in positions)
            list.Add(new FormationSlot { Position = p, Role = role });
        return list;
    }
}
