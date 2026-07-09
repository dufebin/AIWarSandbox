using System.Collections.Generic;
using Godot;
using AIWarSandbox.World;

namespace AIWarSandbox.Ai;

/// <summary>
/// Grid-based A* pathfinder over the terrain. Cells whose sampled height
/// exceeds <see cref="ImpassableHeight"/> (the central plateau) are treated
/// as impassable for ground units.
/// </summary>
public static class Pathfinder
{
    /// <summary>Grid cell size in metres.</summary>
    public const float CellSize = 4f;

    /// <summary>Heights above this (the plateau crown) are impassable to ground units.</summary>
    public const float ImpassableHeight = 4.0f;

    /// <summary>Max A* expansions before bailing (keeps the sandbox responsive).</summary>
    private const int MaxExpansions = 20000;

    /// <summary>Straight-line path resampled every 4m, terrain-snapped. Fallback when A* fails.</summary>
    public static List<Vector3> StraightPath(Vector3 from, Vector3 to, TerrainGenerator? terrain)
    {
        var path = new List<Vector3>();
        float dist = from.DistanceTo(to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / CellSize));
        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            var p = from.Lerp(to, t);
            if (terrain != null) p.Y = terrain.SampleHeight(p.X, p.Z);
            path.Add(p);
        }
        return path;
    }

    public static Vector3 OffsetFlank(Vector3 from, Vector3 to, float sideOffset)
    {
        var dir = to - from;
        dir.Y = 0;
        if (dir.LengthSquared() < 0.01f) return to;
        var perp = new Vector3(-dir.Z, 0, dir.X).Normalized();
        return to + perp * sideOffset;
    }

    /// <summary>
    /// A* on a 4m grid. Returns terrain-snapped waypoints from <paramref name="start"/>
    /// (exclusive) to <paramref name="end"/> (inclusive). Falls back to a straight
    /// resampled path if no route is found or terrain is null.
    /// </summary>
    public static List<Vector3> FindPath(Vector3 start, Vector3 end, TerrainGenerator? terrain)
    {
        if (terrain == null) return StraightPath(start, end, null);

        var sx = Mathf.RoundToInt(start.X / CellSize);
        var sz = Mathf.RoundToInt(start.Z / CellSize);
        var ex = Mathf.RoundToInt(end.X / CellSize);
        var ez = Mathf.RoundToInt(end.Z / CellSize);

        // Short-circuit trivially close requests.
        if (sx == ex && sz == ez) return new List<Vector3> { Snap(end, terrain) };

        var startNode = (sx, sz);
        var goalNode = (ex, ez);

        var open = new PriorityQueue<(int, int), float>();
        var cameFrom = new Dictionary<(int, int), (int, int)>();
        var gScore = new Dictionary<(int, int), float> { [startNode] = 0f };
        open.Enqueue(startNode, Heuristic(startNode, goalNode));

        int expansions = 0;
        bool found = false;

        while (open.Count > 0 && expansions < MaxExpansions)
        {
            var current = open.Dequeue();
            expansions++;

            if (current == goalNode) { found = true; break; }
            // Close enough: snap to goal.
            if (Heuristic(current, goalNode) <= 1.5f) { goalNode = current; found = true; break; }

            foreach (var nb in Neighbors(current))
            {
                if (!IsPassable(nb, terrain)) continue;
                float tentative = gScore[current] + StepCost(current, nb, terrain);
                if (!gScore.TryGetValue(nb, out float existing) || tentative < existing)
                {
                    cameFrom[nb] = current;
                    gScore[nb] = tentative;
                    open.Enqueue(nb, tentative + Heuristic(nb, goalNode));
                }
            }
        }

        if (!found) return StraightPath(start, end, terrain);

        // Reconstruct grid path.
        var gridPath = new List<(int, int)>();
        var node = goalNode;
        while (node != startNode)
        {
            gridPath.Add(node);
            if (!cameFrom.TryGetValue(node, out var prev)) break;
            node = prev;
        }
        gridPath.Reverse();

        // Convert to world waypoints, terrain-snapped.
        var path = new List<Vector3>(gridPath.Count + 1);
        foreach (var (cx, cz) in gridPath)
            path.Add(Snap(new Vector3(cx * CellSize, 0, cz * CellSize), terrain));
        // Ensure exact end point.
        path[^1] = Snap(end, terrain);
        return path;
    }

    private static float Heuristic((int, int) a, (int, int) b)
    {
        int dx = a.Item1 - b.Item1;
        int dz = a.Item2 - b.Item2;
        // Octile distance.
        int diag = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dz));
        int straight = Mathf.Abs(dx) + Mathf.Abs(dz) - 2 * diag;
        return diag * 1.4142f + straight;
    }

    private static IEnumerable<(int, int)> Neighbors((int, int) c)
    {
        // 8-connected.
        for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (dx == 0 && dz == 0) continue;
                yield return (c.Item1 + dx, c.Item2 + dz);
            }
    }

    private static float StepCost((int, int) a, (int, int) b, TerrainGenerator terrain)
    {
        int dx = Mathf.Abs(a.Item1 - b.Item1);
        int dz = Mathf.Abs(a.Item2 - b.Item2);
        float baseCost = (dx + dz == 2) ? 1.4142f : 1f;
        // Slope penalty: steeper terrain is costlier.
        var wa = new Vector3(a.Item1 * CellSize, 0, a.Item2 * CellSize);
        var wb = new Vector3(b.Item1 * CellSize, 0, b.Item2 * CellSize);
        float ha = terrain.SampleHeight(wa.X, wa.Z);
        float hb = terrain.SampleHeight(wb.X, wb.Z);
        float slope = Mathf.Abs(hb - ha) / CellSize;
        return baseCost * (1f + slope * 2f);
    }

    private static bool IsPassable((int, int) cell, TerrainGenerator terrain)
    {
        var w = new Vector3(cell.Item1 * CellSize, 0, cell.Item2 * CellSize);
        float h = terrain.SampleHeight(w.X, w.Z);
        return h <= ImpassableHeight;
    }

    private static Vector3 Snap(Vector3 v, TerrainGenerator terrain)
    {
        v.Y = terrain.SampleHeight(v.X, v.Z);
        return v;
    }
}
