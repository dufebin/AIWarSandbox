using System.Collections.Generic;
using System.Linq;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ai;

/// <summary>
/// Scores a <see cref="Plan"/> against current intelligence from
/// <see cref="IntelligenceRegistry"/> and friendly/enemy disposition from
/// <see cref="UnitRegistry"/>. Produces a weighted confidence in [0,1]
/// combining intel coverage, intel freshness, and local force ratio.
/// </summary>
public partial class ConfidenceScorer : Node
{
    // Weights (sum = 1.0).
    private const float W_Coverage = 0.4f;
    private const float W_Freshness = 0.3f;
    private const float W_ForceRatio = 0.3f;

    // Tunables.
    private const float RallyCoverageRadius = 20f;          // meters
    private const float StalenessHorizonSec = 30f;          // age beyond which intel is "stale"
    private const float ObjectiveRadius = 50f;              // meters — "near objective"

    public class PlanConfidence
    {
        /// <summary>Weighted overall confidence in [0,1].</summary>
        public float Overall;
        /// <summary>Fraction of plan rally points covered by recent friendly sensor tracks.</summary>
        public float IntelCoverage;
        /// <summary>1 - clamp(mean(LastSeenAge)/30, 0, 1) over enemy tracks near objective.</summary>
        public float IntelFreshness;
        /// <summary>friendly / (friendly + enemy) strength near the objective, clamped to [0,1].</summary>
        public float ForceRatio;
        /// <summary>Human-readable summary string (zh-CN).</summary>
        public string Summary = "";
    }

    public PlanConfidence Score(Plan plan)
    {
        var result = new PlanConfidence();

        if (plan == null)
        {
            result.Summary = "覆盖率0% | 新鲜度0% | 兵力比0%";
            return result;
        }

        var tracks = GetAllTracks();
        var friendly = UnitRegistry.Instance != null
            ? UnitRegistry.Instance.Friendly
            : System.Array.Empty<Unit>();
        var enemy = UnitRegistry.Instance != null
            ? UnitRegistry.Instance.Enemy
            : System.Array.Empty<Unit>();

        Vector3 objective = plan.Objective;

        // --- IntelCoverage ---
        // Fraction of formation rally points that lie within RallyCoverageRadius of a
        // friendly track (proxy for "covered by friendly sensors").
        int rallyTotal = 0;
        int rallyCovered = 0;
        if (plan.Formations != null && plan.Formations.Count > 0)
        {
            foreach (var form in plan.Formations)
            {
                Vector3 rally = form.RallyPoint;
                rallyTotal++;
                if (IsRallyCovered(rally, tracks))
                    rallyCovered++;
            }
        }
        // Fallback: if no formations, treat plan waypoints as the rally set so the
        // scorer still produces a meaningful number for hand-built plans.
        if (rallyTotal == 0 && plan.Waypoints != null && plan.Waypoints.Count > 0)
        {
            foreach (var wp in plan.Waypoints)
            {
                rallyTotal++;
                if (IsRallyCovered(wp, tracks))
                    rallyCovered++;
            }
        }
        result.IntelCoverage = rallyTotal > 0
            ? Mathf.Clamp((float)rallyCovered / rallyTotal, 0f, 1f)
            : 0f;

        // --- IntelFreshness ---
        // Mean LastSeenAge over enemy tracks near the objective, mapped to [0,1]
        // freshness = 1 - clamp(meanAge/Horizon, 0, 1).
        float freshness;
        var enemyNearObj = tracks
            .Where(t => !t.IsFriendly && t.LastKnownPosition.DistanceTo(objective) <= ObjectiveRadius)
            .ToList();
        if (enemyNearObj.Count == 0)
        {
            freshness = 1f;
        }
        else
        {
            float meanAge = 0f;
            foreach (var t in enemyNearObj) meanAge += t.LastSeenAge;
            meanAge /= enemyNearObj.Count;
            freshness = 1f - Mathf.Clamp(meanAge / StalenessHorizonSec, 0f, 1f);
        }
        result.IntelFreshness = Mathf.Clamp(freshness, 0f, 1f);

        // --- ForceRatio ---
        int fCount = 0;
        int eCount = 0;
        foreach (var u in friendly)
        {
            if (u == null || u.State == UnitState.Dead) continue;
            if (u.GlobalPosition.DistanceTo(objective) <= ObjectiveRadius) fCount++;
        }
        foreach (var u in enemy)
        {
            if (u == null || u.State == UnitState.Dead) continue;
            if (u.GlobalPosition.DistanceTo(objective) <= ObjectiveRadius) eCount++;
        }
        if (fCount + eCount == 0)
        {
            foreach (var u in friendly)
                if (u != null && u.State != UnitState.Dead) fCount++;
            foreach (var u in enemy)
                if (u != null && u.State != UnitState.Dead) eCount++;
        }
        float ratio = (fCount + eCount) > 0
            ? (float)fCount / (float)(fCount + eCount)
            : 0.5f;
        result.ForceRatio = Mathf.Clamp(ratio, 0f, 1f);

        // --- Overall ---
        result.Overall = Mathf.Clamp(
            W_Coverage * result.IntelCoverage
            + W_Freshness * result.IntelFreshness
            + W_ForceRatio * result.ForceRatio,
            0f, 1f);

        result.Summary =
            $"覆盖率{result.IntelCoverage * 100f:F0}% | " +
            $"新鲜度{result.IntelFreshness * 100f:F0}% | " +
            $"兵力比{result.ForceRatio * 100f:F0}%";

        return result;
    }

    private static bool IsRallyCovered(Vector3 rally, IReadOnlyList<IntelligenceRegistry.Track> tracks)
    {
        foreach (var t in tracks)
        {
            if (!t.IsFriendly) continue;
            float dist = t.LastKnownPosition.DistanceTo(rally);
            if (dist <= RallyCoverageRadius) return true;
        }
        return false;
    }

    private static IReadOnlyList<IntelligenceRegistry.Track> GetAllTracks()
    {
        var registry = IntelligenceRegistry.Instance;
        if (registry == null) return System.Array.Empty<IntelligenceRegistry.Track>();
        return registry.AllTracks.Values.ToList();
    }
}
