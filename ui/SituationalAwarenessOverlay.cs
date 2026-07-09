using System.Collections.Generic;
using System.Linq;
using Godot;
using AIWarSandbox.Ai;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Palantir-Maven-style situational-awareness overlay. A <see cref="CanvasLayer"/>
/// at <see cref="Layer"/> = 8 providing:
///  - A top-right stats panel (sensor coverage %, active track count, mean track age).
///  - A 256x256 <see cref="SubViewport"/> minimap drawn via <see cref="_Draw"/>
///    showing friendly units (green), enemy tracks (red, alpha = confidence),
///    uncertainty rings (radius = 5 + age*0.5), threat rings around enemy
///    clusters, and the selected plan's objective marker.
/// The minimap is camera-relative (centered on the RTS camera's ground position)
/// at 1m = 1px, clamped to a 200m radius.
/// Subscribes to <see cref="EventBus.PlanSelected"/> to highlight the chosen
/// plan's objective.
/// </summary>
public partial class SituationalAwarenessOverlay : CanvasLayer
{
    private const int MinimapSize = 256;
    private const float MaxWorldRadiusM = 200f;   // 1m = 1px, so ±128px covers ±128m...
    private const float RefreshInterval = 0.25f;  // 4Hz

    // Stats panel labels.
    private Label _coverageLabel = null!;
    private Label _trackCountLabel = null!;
    private Label _meanAgeLabel = null!;

    // Minimap.
    private MinimapCanvas _minimap = null!;
    private float _refreshAccum;

    // Currently highlighted plan objective (set when a plan is selected).
    private Vector3 _highlightedObjective;
    private bool _hasObjective;

    public override void _Ready()
    {
        Layer = 8;
        BuildLayout();

        EventBus.Instance.PlanSelected += OnPlanSelected;
    }

    private void BuildLayout()
    {
        // --- Top-right stats panel ---
        var panel = new Panel { Name = "SAStats" };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.CustomMinimumSize = new Vector2(240, 96);
        panel.OffsetLeft = -260;
        panel.OffsetTop = 8;
        panel.SelfModulate = new Color(0, 0, 0, 0.6f);
        AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Stats" };
        vbox.AddThemeConstantOverride("separation", 4);
        margin.AddChild(vbox);

        var title = new Label { Text = "态势感知" };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.Modulate = new Color(0.8f, 0.9f, 1f);
        vbox.AddChild(title);

        _coverageLabel = MakeStat(vbox, "传感器覆盖: 0%");
        _trackCountLabel = MakeStat(vbox, "活动航迹: 0");
        _meanAgeLabel = MakeStat(vbox, "平均航龄: 0.0s");

        // --- Minimap (bottom-right) ---
        // SubViewport hosts the custom _Draw canvas. We size the viewport and its
        // inner control to MinimapSize x MinimapSize.
        var vpContainer = new SubViewportContainer { Name = "MinimapContainer" };
        vpContainer.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        vpContainer.CustomMinimumSize = new Vector2(MinimapSize, MinimapSize);
        vpContainer.OffsetLeft = -(MinimapSize + 16);
        vpContainer.OffsetTop = -(MinimapSize + 16);
        vpContainer.OffsetRight = -16;
        vpContainer.OffsetBottom = -16;
        AddChild(vpContainer);

        var vp = new SubViewport
        {
            Name = "MinimapVP",
            Size = new Vector2I(MinimapSize, MinimapSize),
            TransparentBg = true,
            RenderTargetClearMode = SubViewport.ClearMode.Always,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always
        };
        vpContainer.AddChild(vp);

        _minimap = new MinimapCanvas
        {
            Name = "MinimapCanvas",
            CustomMinimumSize = new Vector2(MinimapSize, MinimapSize)
        };
        _minimap.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vp.AddChild(_minimap);
    }

    private static Label MakeStat(Control parent, string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        parent.AddChild(lbl);
        return lbl;
    }

    public override void _Process(double delta)
    {
        _refreshAccum += (float)delta;
        if (_refreshAccum < RefreshInterval) return;
        _refreshAccum = 0f;

        var tracks = GetTracks();
        var units = UnitRegistry.Instance != null
            ? UnitRegistry.Instance.All
            : System.Array.Empty<Unit>();

        // --- Stats panel ---
        int total = tracks.Count;
        int friendlyTracks = 0;
        float ageSum = 0f;
        foreach (var t in tracks)
        {
            if (t.IsFriendly) friendlyTracks++;
            ageSum += t.LastSeenAge;
        }
        float meanAge = total > 0 ? ageSum / total : 0f;
        // Sensor coverage heuristic: fraction of friendly units that have at
        // least one friendly track co-located (proxy for "covered by sensors").
        int friendlyUnits = 0;
        int coveredUnits = 0;
        foreach (var u in units)
        {
            if (u == null || u.State == UnitState.Dead || !u.IsFriendly) continue;
            friendlyUnits++;
            if (IsUnitCovered(u.GlobalPosition, tracks)) coveredUnits++;
        }
        float coverage = friendlyUnits > 0
            ? Mathf.Clamp((float)coveredUnits / friendlyUnits, 0f, 1f)
            : 0f;

        _coverageLabel.Text = $"传感器覆盖: {coverage * 100f:F0}%";
        _trackCountLabel.Text = $"活动航迹: {total}";
        _meanAgeLabel.Text = $"平均航龄: {meanAge:F1}s";

        // --- Minimap ---
        Vector3 camCenter = GetCameraGroundPosition();
        _minimap.Update(camCenter, MaxWorldRadiusM, tracks, units,
            _hasObjective ? _highlightedObjective : (Vector3?)null);
    }

    private static bool IsUnitCovered(Vector3 pos, IReadOnlyList<IntelligenceRegistry.Track> tracks)
    {
        const float coverRadius = 25f;
        foreach (var t in tracks)
        {
            if (!t.IsFriendly) continue;
            if (t.LastKnownPosition.DistanceTo(pos) <= coverRadius) return true;
        }
        return false;
    }

    private static IReadOnlyList<IntelligenceRegistry.Track> GetTracks()
    {
        var registry = IntelligenceRegistry.Instance;
        if (registry == null) return System.Array.Empty<IntelligenceRegistry.Track>();
        return registry.AllTracks.Values.ToList();
    }

    /// <summary>
    /// Resolve the RTS camera's ground-projected position. Falls back to the
    /// origin if no suitable camera is found (e.g. running headless).
    /// </summary>
    private Vector3 GetCameraGroundPosition()
    {
        var cam = GetViewport()?.GetCamera3D();
        if (cam != null)
        {
            var p = cam.GlobalPosition;
            return new Vector3(p.X, 0f, p.Z);
        }
        return Vector3.Zero;
    }

    private void OnPlanSelected(int index)
    {
        var mgr = TacticalAIManager.Instance;
        if (mgr == null) return;
        var plans = mgr.Plans;
        if (index < 0 || index >= plans.Count) return;
        _highlightedObjective = plans[index].Objective;
        _hasObjective = true;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlanSelected -= OnPlanSelected;
        }
    }

    /// <summary>
    /// Custom-draw canvas for the minimap. Redrawn each refresh tick by
    /// <see cref="SituationalAwarenessOverlay._Process"/> calling
    /// <see cref="Update"/> with the latest world state.
    /// </summary>
    private partial class MinimapCanvas : Control
    {
        private Vector3 _center;
        private float _worldRadius = MaxWorldRadiusM;
        private IReadOnlyList<IntelligenceRegistry.Track> _tracks = System.Array.Empty<IntelligenceRegistry.Track>();
        private IReadOnlyList<Unit> _units = System.Array.Empty<Unit>();
        private Vector3? _objective;

        public void Update(Vector3 center, float worldRadius,
            IReadOnlyList<IntelligenceRegistry.Track> tracks, IReadOnlyList<Unit> units,
            Vector3? objective)
        {
            _center = center;
            _worldRadius = Mathf.Max(worldRadius, 1f);
            _tracks = tracks;
            _units = units;
            _objective = objective;
            QueueRedraw();
        }

        public override void _Draw()
        {
            Vector2 size = Size;
            Vector2 origin = size * 0.5f;

            // Background.
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.04f, 0.06f, 0.08f, 0.75f), true);

            // Range rings (every 50m).
            for (int r = 50; r <= (int)_worldRadius; r += 50)
            {
                float px = WorldToPx(r);
                DrawArc(origin, px, 0f, Mathf.Tau, 48,
                    new Color(1f, 1f, 1f, 0.08f), 1f);
            }

            // Crosshair.
            DrawLine(new Vector2(origin.X, 0), new Vector2(origin.X, size.Y),
                new Color(1f, 1f, 1f, 0.1f), 1f);
            DrawLine(new Vector2(0, origin.Y), new Vector2(size.X, origin.Y),
                new Color(1f, 1f, 1f, 0.1f), 1f);

            // Friendly units: green dots.
            foreach (var u in _units)
            {
                if (u == null || u.State == UnitState.Dead) continue;
                if (!u.IsFriendly) continue;
                Vector2 p = WorldToMinimap(u.GlobalPosition, origin);
                if (!IsVisible(p, size)) continue;
                DrawCircle(p, 3f, new Color(0.2f, 1f, 0.3f, 0.95f));
            }

            // Enemy tracks: red dots (alpha = confidence) + uncertainty rings.
            // Collect enemy positions for clustering/threat rings.
            var enemyPositions = new List<Vector2>();
            foreach (var t in _tracks)
            {
                if (t.IsFriendly) continue;
                Vector2 p = WorldToMinimap(t.LastKnownPosition, origin);
                if (!IsVisible(p, size)) continue;

                float alpha = Mathf.Clamp(t.Confidence, 0.1f, 1f);
                DrawCircle(p, 3f, new Color(1f, 0.2f, 0.2f, alpha));

                // Uncertainty ring: radius grows with staleness.
                float ringPx = 5f + t.LastSeenAge * 0.5f;
                DrawArc(p, ringPx, 0f, Mathf.Tau, 36,
                    new Color(1f, 0.6f, 0.2f, 0.35f), 1f);

                enemyPositions.Add(p);
            }

            // Threat rings around enemy clusters (simple proximity clustering).
            DrawThreatRings(enemyPositions);

            // Objective marker (selected plan).
            if (_objective.HasValue)
            {
                Vector2 p = WorldToMinimap(_objective.Value, origin);
                if (IsVisible(p, size))
                {
                    // Diamond marker + pulsing ring.
                    DrawArc(p, 8f, 0f, Mathf.Tau, 4, new Color(1f, 0.95f, 0.2f, 0.9f), 2f);
                    DrawArc(p, 14f, 0f, Mathf.Tau, 36, new Color(1f, 0.95f, 0.2f, 0.4f), 1f);
                }
            }

            // Border.
            DrawRect(new Rect2(Vector2.Zero, size), new Color(0.5f, 0.7f, 1f, 0.4f), false, 2f);
        }

        private void DrawThreatRings(List<Vector2> enemyPositions)
        {
            const float clusterDist = 18f;     // px
            const float threatRadius = 22f;     // px
            var used = new bool[enemyPositions.Count];
            for (int i = 0; i < enemyPositions.Count; i++)
            {
                if (used[i]) continue;
                var cluster = new List<Vector2> { enemyPositions[i] };
                used[i] = true;
                for (int j = i + 1; j < enemyPositions.Count; j++)
                {
                    if (used[j]) continue;
                    if (enemyPositions[j].DistanceTo(enemyPositions[i]) <= clusterDist)
                    {
                        cluster.Add(enemyPositions[j]);
                        used[j] = true;
                    }
                }
                if (cluster.Count < 2) continue;
                // Centroid.
                Vector2 c = Vector2.Zero;
                foreach (var p in cluster) c += p;
                c /= cluster.Count;
                DrawArc(c, threatRadius, 0f, Mathf.Tau, 40,
                    new Color(1f, 0.1f, 0.1f, 0.45f), 2f);
            }
        }

        // World meters -> minimap pixels for a scalar radius.
        private float WorldToPx(float worldMeters)
        {
            // Map ±worldRadius to ±MinimapSize/2 px.
            return (worldMeters / _worldRadius) * (MinimapSize * 0.5f);
        }

        private Vector2 WorldToMinimap(Vector3 world, Vector2 origin)
        {
            // Ground-plane (X,Z) -> minimap (X,-Z) so north is up.
            float dx = world.X - _center.X;
            float dz = world.Z - _center.Z;
            float px = (dx / _worldRadius) * (MinimapSize * 0.5f);
            float py = (-dz / _worldRadius) * (MinimapSize * 0.5f);
            return origin + new Vector2(px, py);
        }

        private static bool IsVisible(Vector2 p, Vector2 size)
        {
            return p.X >= -4f && p.X <= size.X + 4f && p.Y >= -4f && p.Y <= size.Y + 4f;
        }
    }
}
