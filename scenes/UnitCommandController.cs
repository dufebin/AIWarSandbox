using System.Collections.Generic;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Scenes;

/// <summary>
/// RTS control: select, move, attack, stance hotkeys, control groups 1-9.
/// </summary>
public partial class UnitCommandController : Node3D
{
    private Camera3D? _cam;
    private RTSCamera? _rtsCam;
    private readonly List<Combatant> _selected = new();
    private readonly List<Combatant>?[] _groups = new List<Combatant>[10];

    private CanvasLayer _overlay = null!;
    private ColorRect _box = null!;
    private Vector2 _dragStart;
    private bool _dragging;
    private float _lastClickTime;
    private Vector2 _lastClickPos;
    private int _lastGroupKey = -1;
    private float _lastGroupTime;

    private const float ClickThreshold = 6f;
    private const float DoubleClickSec = 0.35f;

    public void BindCamera(RTSCamera cam)
    {
        _rtsCam = cam;
        _cam = cam;
    }

    public override void _Ready()
    {
        _overlay = new CanvasLayer { Layer = 8, Name = "SelectionOverlay" };
        AddChild(_overlay);

        _box = new ColorRect
        {
            Name = "DragBox",
            Color = new Color(0.3f, 0.8f, 1f, 0.18f),
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _overlay.AddChild(_box);
    }

    public override void _Process(double delta)
    {
        _cam ??= GetViewport().GetCamera3D();
        for (int i = _selected.Count - 1; i >= 0; i--)
        {
            var u = _selected[i];
            if (u == null || !IsInstanceValid(u) || u.State == UnitState.Dead)
            {
                if (u != null && IsInstanceValid(u)) SetRing(u, false);
                _selected.RemoveAt(i);
            }
        }
    }

    public override void _UnhandledInput(InputEvent ev)
    {
        _cam ??= GetViewport().GetCamera3D();
        if (_cam == null) return;

        if (ev is InputEventKey key && key.Pressed && !key.Echo)
            HandleHotkeys(key);

        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _dragStart = mb.Position;
                    _dragging = true;
                    _box.Position = _dragStart;
                    _box.Size = Vector2.Zero;
                    _box.Visible = true;
                }
                else if (_dragging)
                {
                    _dragging = false;
                    _box.Visible = false;
                    bool additive = Input.IsKeyPressed(Key.Shift);
                    float now = Time.GetTicksMsec() / 1000f;
                    bool dbl = now - _lastClickTime < DoubleClickSec
                               && _lastClickPos.DistanceTo(mb.Position) < ClickThreshold * 2f;
                    _lastClickTime = now;
                    _lastClickPos = mb.Position;

                    if (_dragStart.DistanceTo(mb.Position) < ClickThreshold)
                    {
                        if (dbl) SelectSameType(mb.Position);
                        else SinglePick(mb.Position, additive);
                    }
                    else
                        BoxSelect(_dragStart, mb.Position, additive);
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
            {
                IssueCommand(mb.Position);
            }
        }
        else if (ev is InputEventMouseMotion mm && _dragging)
        {
            var min = new Vector2(Mathf.Min(_dragStart.X, mm.Position.X), Mathf.Min(_dragStart.Y, mm.Position.Y));
            _box.Position = min;
            _box.Size = (mm.Position - _dragStart).Abs();
        }
    }

    private void HandleHotkeys(InputEventKey key)
    {
        // Stance: Z=Aggressive, X=HoldGround, C=StandGround
        if (key.Keycode == Key.Z) { SetStance(CombatStance.Aggressive); return; }
        if (key.Keycode == Key.X) { SetStance(CombatStance.HoldGround); return; }
        if (key.Keycode == Key.C) { SetStance(CombatStance.StandGround); return; }

        // Ctrl+A = select all friendlies
        if (key.Keycode == Key.A && key.CtrlPressed)
        {
            ClearSelection();
            var registry = UnitRegistry.Instance;
            if (registry == null) return;
            foreach (var u in registry.Friendly)
                if (u is Combatant c && c.State != UnitState.Dead) AddToSelection(c);
            return;
        }

        // F = focus camera on selection
        if (key.Keycode == Key.F && _selected.Count > 0 && _rtsCam != null)
        {
            Vector3 sum = Vector3.Zero;
            foreach (var c in _selected) sum += c.GlobalPosition;
            _rtsCam.FocusOn(sum / _selected.Count);
            return;
        }

        // Control groups 1-9
        int slot = key.Keycode switch
        {
            Key.Key1 => 1, Key.Key2 => 2, Key.Key3 => 3, Key.Key4 => 4, Key.Key5 => 5,
            Key.Key6 => 6, Key.Key7 => 7, Key.Key8 => 8, Key.Key9 => 9,
            _ => -1
        };
        if (slot < 0) return;

        if (key.CtrlPressed)
        {
            _groups[slot] = new List<Combatant>(_selected);
            EventBus.Instance?.RaiseLog($"[Command] 编组 {slot}: {_selected.Count} 单位");
        }
        else
        {
            RecallGroup(slot);
        }
    }

    private void RecallGroup(int slot)
    {
        var g = _groups[slot];
        if (g == null || g.Count == 0) return;
        ClearSelection();
        foreach (var c in g)
            if (c != null && IsInstanceValid(c) && c.State != UnitState.Dead)
                AddToSelection(c);

        float now = Time.GetTicksMsec() / 1000f;
        if (_lastGroupKey == slot && now - _lastGroupTime < 0.4f && _selected.Count > 0 && _rtsCam != null)
        {
            Vector3 sum = Vector3.Zero;
            foreach (var c in _selected) sum += c.GlobalPosition;
            _rtsCam.FocusOn(sum / _selected.Count);
        }
        _lastGroupKey = slot;
        _lastGroupTime = now;
    }

    private void SetStance(CombatStance stance)
    {
        if (_selected.Count == 0) return;
        foreach (var c in _selected) c.Stance = stance;
        string name = stance switch
        {
            CombatStance.HoldGround => "驻守",
            CombatStance.StandGround => "固守",
            _ => "进攻"
        };
        EventBus.Instance?.RaiseLog($"[Command] {_selected.Count} 单位阵态 → {name}");
    }

    private void SinglePick(Vector2 screenPos, bool additive)
    {
        if (!additive) ClearSelection();
        if (RayPickUnit(screenPos) is Combatant c && c.IsFriendly && c.State != UnitState.Dead)
            AddToSelection(c);
    }

    private void SelectSameType(Vector2 screenPos)
    {
        if (RayPickUnit(screenPos) is not Combatant picked || !picked.IsFriendly) return;
        ClearSelection();
        bool wantVehicle = picked is Vehicle;
        var registry = UnitRegistry.Instance;
        if (registry == null || _cam == null) return;
        foreach (var u in registry.Friendly)
        {
            if (u is not Combatant c || c.State == UnitState.Dead) continue;
            if ((c is Vehicle) != wantVehicle) continue;
            if (_cam.IsPositionBehind(c.GlobalPosition)) continue;
            AddToSelection(c);
        }
    }

    private void BoxSelect(Vector2 a, Vector2 b, bool additive)
    {
        if (!additive) ClearSelection();
        var rect = new Rect2(
            new Vector2(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)),
            (b - a).Abs());

        var registry = UnitRegistry.Instance;
        if (registry == null || _cam == null) return;
        foreach (var u in registry.Friendly)
        {
            if (u is not Combatant c || c.State == UnitState.Dead) continue;
            if (_cam.IsPositionBehind(c.GlobalPosition)) continue;
            var sp = _cam.UnprojectPosition(c.GlobalPosition + new Vector3(0, 1f, 0));
            if (rect.HasPoint(sp)) AddToSelection(c);
        }
    }

    private void AddToSelection(Combatant c)
    {
        if (_selected.Contains(c)) return;
        _selected.Add(c);
        SetRing(c, true);
    }

    private void ClearSelection()
    {
        foreach (var c in _selected)
            if (c != null && IsInstanceValid(c)) SetRing(c, false);
        _selected.Clear();
    }

    private void IssueCommand(Vector2 screenPos)
    {
        if (_selected.Count == 0) return;
        var scene = GetTree()?.CurrentScene;

        if (RayPickUnit(screenPos) is { } picked && !picked.IsFriendly && picked.State != UnitState.Dead)
        {
            foreach (var c in _selected) c.Attack(picked);
            if (scene != null)
                CombatFx.CommandPing(scene, picked.GlobalPosition, new Color(1f, 0.2f, 0.2f));
            EventBus.Instance?.RaiseLog($"[Command] {_selected.Count} 单位受命攻击 {picked.Name}");
            return;
        }

        if (GroundPoint(screenPos) is not { } dest) return;
        int perRow = Mathf.CeilToInt(Mathf.Sqrt(_selected.Count));
        const float spacing = 2.2f;
        for (int i = 0; i < _selected.Count; i++)
        {
            int row = i / perRow, col = i % perRow;
            var offset = new Vector3((col - perRow * 0.5f) * spacing, 0, row * spacing);
            _selected[i].MoveTo(dest + offset);
        }
        if (scene != null)
            CombatFx.CommandPing(scene, dest, new Color(0.3f, 0.9f, 0.4f));
        EventBus.Instance?.RaiseLog($"[Command] {_selected.Count} 单位移动至 ({dest.X:F0}, {dest.Z:F0})");
    }

    private Unit? RayPickUnit(Vector2 screenPos)
    {
        if (_cam == null) return null;
        var from = _cam.ProjectRayOrigin(screenPos);
        var to = from + _cam.ProjectRayNormal(screenPos) * 2000f;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollisionMask = 2u | 4u;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0 || !hit.ContainsKey("collider")) return null;
        var collider = hit["collider"].As<Node>();
        while (collider != null && collider is not Unit) collider = collider.GetParent();
        return collider as Unit;
    }

    private Vector3? GroundPoint(Vector2 screenPos)
    {
        if (_cam == null) return null;
        var origin = _cam.ProjectRayOrigin(screenPos);
        var dir = _cam.ProjectRayNormal(screenPos);
        var query = PhysicsRayQueryParameters3D.Create(origin, origin + dir * 4000f);
        query.CollisionMask = 1u;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count > 0 && hit.ContainsKey("position"))
            return hit["position"].AsVector3();
        return new Plane(Vector3.Up, 0f).IntersectsRay(origin, dir);
    }

    private static void SetRing(Combatant c, bool on)
    {
        var existing = c.GetNodeOrNull<MeshInstance3D>("SelRing");
        if (on)
        {
            if (existing != null) return;
            var ring = new MeshInstance3D
            {
                Name = "SelRing",
                Mesh = new TorusMesh { InnerRadius = 0.8f, OuterRadius = 1.05f },
                Position = new Vector3(0, 0.08f, 0),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.3f, 1f, 0.5f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    EmissionEnabled = true,
                    Emission = new Color(0.2f, 1f, 0.4f),
                },
            };
            c.AddChild(ring);
        }
        else
        {
            existing?.QueueFree();
        }
    }
}
