using System.Collections.Generic;
using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ai;

/// <summary>
/// Pure geometry helpers for placing a squad into standard formations,
/// plus the legacy <see cref="Split"/> grouping used by OrderExecutor.
/// </summary>
public static class FormationPlanner
{
    // ---- New geometry-based formation slots (Plan.Waypoints / Slots / UnitAssignments) ----

    /// <summary>Line abreast, perpendicular to <paramref name="facing"/>.</summary>
    public static List<Vector3> LineFormation(Vector3 center, Vector3 facing, int count, float spacing)
    {
        var slots = new List<Vector3>(count);
        if (count <= 0) return slots;
        Vector3 fwd = facing;
        fwd.Y = 0;
        if (fwd.LengthSquared() < 1e-4f) fwd = Vector3.Forward;
        fwd = fwd.Normalized();
        Vector3 right = new(-fwd.Z, 0, fwd.X);

        int half = count / 2;
        for (int i = 0; i < count; i++)
        {
            int offset = i - half; // -half .. +half
            // For even counts, stagger so no unit sits exactly on center duplicate.
            if (count % 2 == 0) offset = (i < half ? -(i + 1) : (i - half + 1));
            slots.Add(center + right * (offset * spacing));
        }
        return slots;
    }

    /// <summary>Single file along <paramref name="facing"/>.</summary>
    public static List<Vector3> ColumnFormation(Vector3 center, Vector3 facing, int count, float spacing)
    {
        var slots = new List<Vector3>(count);
        if (count <= 0) return slots;
        Vector3 fwd = facing;
        fwd.Y = 0;
        if (fwd.LengthSquared() < 1e-4f) fwd = Vector3.Forward;
        fwd = fwd.Normalized();

        for (int i = 0; i < count; i++)
            slots.Add(center - fwd * (i * spacing));
        return slots;
    }

    /// <summary>Wedge: apex at center, two trailing ranks fanning out behind.</summary>
    public static List<Vector3> WedgeFormation(Vector3 center, Vector3 facing, int count, float spacing)
    {
        var slots = new List<Vector3>(count);
        if (count <= 0) return slots;
        Vector3 fwd = facing;
        fwd.Y = 0;
        if (fwd.LengthSquared() < 1e-4f) fwd = Vector3.Forward;
        fwd = fwd.Normalized();
        Vector3 right = new(-fwd.Z, 0, fwd.X);

        int idx = 0;
        int rank = 0;
        while (idx < count)
        {
            if (rank == 0)
            {
                slots.Add(center);
                idx++; rank++; continue;
            }
            // Each rank contributes up to 2 slots, one each side of axis, depth = rank.
            for (int side = -1; side <= 1 && idx < count; side += 2)
            {
                slots.Add(center - fwd * (rank * spacing) + right * (side * rank * spacing * 0.5f));
                idx++;
            }
            rank++;
        }
        return slots;
    }

    // ---- Legacy squad-grouping used by OrderExecutor.AssignOrders (unchanged contract) ----

    public static List<FormationAssignment> Split(List<Unit> friendly, Vector3 objective, PlanType type)
    {
        var form = new List<FormationAssignment>();
        if (friendly.Count == 0) return form;

        var combatants = new List<Unit>();
        foreach (var u in friendly)
            if (u is Combatant && u.State != UnitState.Dead)
                combatants.Add(u);

        if (combatants.Count == 0) return form;

        switch (type)
        {
            case PlanType.FrontalAssault:
            case PlanType.ReconInForce:
                form.Add(new FormationAssignment
                {
                    Role = "vanguard",
                    Units = new List<Unit>(combatants),
                    AttackVector = (objective - combatants[0].GlobalPosition).Normalized()
                });
                break;

            case PlanType.FlankingManeuver:
                int mid = combatants.Count / 2;
                var left = combatants.GetRange(0, mid);
                var right = combatants.GetRange(mid, combatants.Count - mid);
                var baseDir = objective - combatants[0].GlobalPosition;
                var perp = new Vector3(-baseDir.Z, 0, baseDir.X).Normalized();
                form.Add(new FormationAssignment { Role = "left_flank", Units = left, AttackVector = perp });
                form.Add(new FormationAssignment { Role = "right_flank", Units = right, AttackVector = -perp });
                break;

            case PlanType.AirborneDrop:
                form.Add(new FormationAssignment
                {
                    Role = "drop_force",
                    Units = new List<Unit>(combatants),
                    AttackVector = (objective - combatants[0].GlobalPosition).Normalized()
                });
                break;
        }

        return form;
    }

    private static Vector3 perpDir(Vector3 objective, Unit anchor)
    {
        var d = objective - anchor.GlobalPosition;
        d.Y = 0;
        if (d.LengthSquared() < 0.01f) return Vector3.Forward;
        return new Vector3(-d.Z, 0, d.X).Normalized();
    }
}
