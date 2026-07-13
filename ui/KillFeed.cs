using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Top-right kill feed listing recent unit deaths.
/// Instantiated by BattleHUD; unique; no data-file I/O.
/// </summary>
public partial class KillFeed : Control
{
    private VBoxContainer _list = null!;
    private const int MaxLines = 8;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.TopRight);
        OffsetLeft = -320;
        OffsetTop = 64;
        OffsetRight = -16;
        OffsetBottom = 280;
        MouseFilter = MouseFilterEnum.Ignore;

        _list = new VBoxContainer { Name = "Feed" };
        _list.AddThemeConstantOverride("separation", 2);
        AddChild(_list);

        EventBus.Instance.UnitDied += OnUnitDied;
    }

    private void OnUnitDied(Unit u)
    {
        if (u == null) return;
        var line = new Label
        {
            Text = u.IsFriendly ? $"✕ 友军 {u.Name} 阵亡" : $"● 击杀 {u.Name}",
            Modulate = u.IsFriendly ? new Color(1f, 0.45f, 0.4f) : new Color(0.5f, 1f, 0.5f),
        };
        line.AddThemeFontSizeOverride("font_size", 13);
        _list.AddChild(line);
        while (_list.GetChildCount() > MaxLines)
        {
            var old = _list.GetChild(0);
            _list.RemoveChild(old);
            old.QueueFree();
        }
        FadeLine(line);
    }

    private async void FadeLine(Label line)
    {
        var tree = GetTree();
        if (tree == null) return;
        float t = 0f;
        while (t < 4f && GodotObject.IsInstanceValid(line))
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            t += (float)tree.Root.GetProcessDeltaTime();
            if (t > 3f)
            {
                var c = line.Modulate;
                c.A = Mathf.Clamp(1f - (t - 3f), 0f, 1f);
                line.Modulate = c;
            }
        }
        if (GodotObject.IsInstanceValid(line)) line.QueueFree();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.UnitDied -= OnUnitDied;
    }
}
