using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Corner minimap: paints friend/foe/base dots, camera focus, click-to-pan.
/// Called from MainScene._Ready; unique UI widget; no data-file I/O.
/// </summary>
public partial class Minimap : CanvasLayer
{
    private const float MapPx = 160f;
    private float _worldSize = 128f;
    private Camera3D? _cam;
    private Control _panel = null!;
    private float _refresh;

    public void Bind(float worldSize, Camera3D cam)
    {
        _worldSize = worldSize;
        _cam = cam;
    }

    public override void _Ready()
    {
        Layer = 6;
        _panel = new Control
        {
            Name = "MinimapPanel",
            CustomMinimumSize = new Vector2(MapPx + 8, MapPx + 8),
        };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _panel.OffsetLeft = -(MapPx + 20);
        _panel.OffsetTop = -(MapPx + 20);
        _panel.OffsetRight = -12;
        _panel.OffsetBottom = -110;
        _panel.GuiInput += OnGuiInput;
        AddChild(_panel);

        var bg = new ColorRect
        {
            Color = new Color(0.05f, 0.08f, 0.05f, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _panel.AddChild(bg);
    }

    public override void _Process(double delta)
    {
        _refresh += (float)delta;
        if (_refresh < 0.1f) return;
        _refresh = 0f;
        RebuildDots();
    }

    private void RebuildDots()
    {
        for (int i = _panel.GetChildCount() - 1; i >= 1; i--)
            _panel.GetChild(i).QueueFree();

        var registry = UnitRegistry.Instance;
        if (registry == null) return;
        float half = _worldSize * 0.5f;

        foreach (var u in registry.All)
        {
            if (u.State == UnitState.Dead) continue;
            var p = WorldToLocal(u.GlobalPosition, half);
            bool isBase = u is Structure s && s.Kind == StructureKind.Base;
            float sz = isBase ? 8f : 5f;
            var dot = new ColorRect
            {
                Color = u.IsFriendly
                    ? (isBase ? new Color(0.3f, 0.7f, 1f) : new Color(0.4f, 0.85f, 1f))
                    : (isBase ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.45f, 0.35f)),
                Position = p - new Vector2(sz * 0.5f, sz * 0.5f),
                Size = new Vector2(sz, sz),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            _panel.AddChild(dot);
        }

        if (_cam != null)
        {
            var cp = WorldToLocal(_cam.GlobalPosition, half);
            _panel.AddChild(new ColorRect
            {
                Color = new Color(1f, 1f, 0.3f),
                Position = cp - new Vector2(4, 4),
                Size = new Vector2(8, 8),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }
    }

    private Vector2 WorldToLocal(Vector3 world, float half)
    {
        float nx = (world.X + half) / _worldSize;
        float nz = (world.Z + half) / _worldSize;
        return new Vector2(nx * MapPx + 4f, nz * MapPx + 4f);
    }

    private void OnGuiInput(InputEvent ev)
    {
        if (ev is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left)
            return;
        if (_cam == null) return;
        float half = _worldSize * 0.5f;
        float nx = Mathf.Clamp((mb.Position.X - 4f) / MapPx, 0f, 1f);
        float nz = Mathf.Clamp((mb.Position.Y - 4f) / MapPx, 0f, 1f);
        float wx = nx * _worldSize - half;
        float wz = nz * _worldSize - half;
        _cam.GlobalPosition = new Vector3(wx, _cam.GlobalPosition.Y, wz + _cam.GlobalPosition.Y * 0.7f);
    }
}
