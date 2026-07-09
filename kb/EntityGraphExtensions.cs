using System.Collections.Generic;
using Godot;

namespace AIWarSandbox.Kb;

public static class EntityGraphExtensions
{
    /// <summary>
    /// Returns TerrainFeature and Structure entities within <paramref name="radius"/> of <paramref name="pos"/>.
    /// </summary>
    public static List<TacticalEntity> ObstaclesNear(this EntityGraph g, Vector3 pos, float radius)
    {
        float maxSqr = radius * radius;
        var result = new List<TacticalEntity>();
        foreach (var e in g.All.Values)
        {
            if (e.Type != EntityType.TerrainFeature && e.Type != EntityType.Structure)
                continue;
            if (e.Position.DistanceSquaredTo(pos) <= maxSqr)
                result.Add(e);
        }
        return result;
    }

    /// <summary>
    /// Returns SupplyPoint entities owned by the requested side that have Supports relations
    /// to at least one platform.
    /// </summary>
    public static List<TacticalEntity> SupplyLines(this EntityGraph g, bool friendly)
    {
        var result = new List<TacticalEntity>();
        foreach (var e in g.All.Values)
        {
            if (e.Type != EntityType.SupplyPoint) continue;
            if (e.IsFriendly != friendly) continue;
            if (e.GetRelations(EntityRelation.Supports).Count > 0)
                result.Add(e);
        }
        return result;
    }
}
