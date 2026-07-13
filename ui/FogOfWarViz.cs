using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Soft fog-of-war: hides unknown enemy combatants using IntelligenceRegistry + proximity.
/// Bases stay visible as objectives. Added by MainScene.
/// </summary>
public partial class FogOfWarViz : Node
{
    private float _accum;

    public override void _Process(double delta)
    {
        _accum += (float)delta;
        if (_accum < 0.25f) return;
        _accum = 0f;

        var registry = UnitRegistry.Instance;
        var intel = IntelligenceRegistry.Instance;
        if (registry == null || intel == null) return;

        foreach (var u in registry.Enemy)
        {
            if (u == null || u.State == UnitState.Dead || !GodotObject.IsInstanceValid(u)) continue;
            if (u is Structure) { u.Visible = true; continue; }
            u.Visible = IsKnown(intel, u);
        }
    }

    private static bool IsKnown(IntelligenceRegistry intel, Unit u)
    {
        int id = (int)u.GetInstanceId();
        foreach (var kv in intel.AllTracks)
        {
            var t = kv.Value;
            if (t.LinkedUnitId == id && t.Confidence >= 0.25f)
                return true;
        }
        foreach (var f in UnitRegistry.Instance.Friendly)
        {
            if (f.State == UnitState.Dead) continue;
            if (f.GlobalPosition.DistanceSquaredTo(u.GlobalPosition) < 40f * 40f)
                return true;
        }
        return false;
    }
}
