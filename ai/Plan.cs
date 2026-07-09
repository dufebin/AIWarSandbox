using System.Collections.Generic;
using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ai;

public partial class Plan : Resource
{
    public PlanType Type { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public RiskLevel Risk { get; set; }

    // Rate of friendly losses expected, 0..1.
    public float ExpectedCasualtyRate { get; set; }
    // Estimated probability of success, 0..1.
    public float SuccessProbability { get; set; }

    // Legacy integer casualty estimate (kept for backwards compat with UI/TacticalAIManager).
    public int EstimatedCasualties { get; set; }
    // Legacy duration in seconds (float). Used by older UI binding paths.
    public float EstimatedDuration { get; set; }
    // New briefing field: whole-seconds duration.
    public int EstimatedDurationSec { get; set; }

    public Vector3 Objective { get; set; }

    /// <summary>Rally point through to attack positions.</summary>
    public List<Vector3> Waypoints { get; set; } = new();

    /// <summary>unit index -> target world position (formation slot).</summary>
    public Dictionary<int, Vector3> UnitAssignments { get; set; } = new();

    /// <summary>Convenience: ordered formation slots (parallel to assignment keys).</summary>
    public List<FormationSlot> Slots { get; set; } = new();

    // Legacy formation assignment list (still consumed by OrderExecutor.AssignOrders).
    public List<FormationAssignment> Formations { get; set; } = new();

    /// <summary>Monte Carlo outcome distribution from <see cref="PlanSimulator"/>. Null until simulated.</summary>
    public PlanSimulator.OutcomeDistribution? Outcome { get; set; }

    /// <summary>Confidence score from <see cref="ConfidenceScorer"/>. Null until scored.</summary>
    public ConfidenceScorer.PlanConfidence? Confidence { get; set; }

    /// <summary>Human-readable evidence summary (zh-CN) from the confidence scorer.</summary>
    public string? EvidenceSummary { get; set; }
}

public partial class FormationAssignment : Resource
{
    public List<Unit> Units { get; set; } = new();
    public string Role { get; set; } = "main";
    public Vector3 RallyPoint { get; set; }
    public Vector3 AttackVector { get; set; }
}

/// <summary>A single formation slot: world position + role label.</summary>
public partial class FormationSlot : Resource
{
    public Vector3 Position { get; set; }
    public string Role { get; set; } = "line";
}
