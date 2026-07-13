using Godot;
using AIWarSandbox.Autoloads;

namespace AIWarSandbox.Ui;

/// <summary>
/// Pause overlay with speed controls. ProcessMode=Always so it works while paused.
/// Instantiated by MainScene; unique; no data-file I/O.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    private Label _speedLbl = null!;
    private bool _shown;
    private float _savedScale = 1f;

    public override void _Ready()
    {
        Layer = 30;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;
        Build();
        EventBus.Instance.BattleEnded += _ =>
        {
            Visible = false;
            _shown = false;
            Engine.TimeScale = 1f;
            if (GetTree() != null) GetTree().Paused = false;
        };
    }

    private void Build()
    {
        var backdrop = new Panel { Name = "Backdrop" };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.SelfModulate = new Color(0, 0, 0, 0.55f);
        AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { CustomMinimumSize = new Vector2(360, 0) };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var title = new Label { Text = "暂停" };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        _speedLbl = new Label { Text = "速度: 1x" };
        _speedLbl.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_speedLbl);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 10);
        vbox.AddChild(row);

        AddSpeedBtn(row, "暂停", 0f);
        AddSpeedBtn(row, "1x", 1f);
        AddSpeedBtn(row, "2x", 2f);
        AddSpeedBtn(row, "4x", 4f);

        var resume = new Button { Text = "继续 (Esc)" };
        resume.CustomMinimumSize = new Vector2(0, 44);
        resume.Pressed += () => Toggle();
        vbox.AddChild(resume);
    }

    private void AddSpeedBtn(Control parent, string text, float scale)
    {
        var b = new Button { Text = text };
        b.CustomMinimumSize = new Vector2(70, 36);
        b.Pressed += () => SetSpeed(scale);
        parent.AddChild(b);
    }

    private void SetSpeed(float scale)
    {
        if (scale <= 0f)
        {
            GetTree().Paused = true;
            _speedLbl.Text = "速度: 暂停";
        }
        else
        {
            GetTree().Paused = false;
            Engine.TimeScale = scale;
            _savedScale = scale;
            _speedLbl.Text = $"速度: {scale:0.##}x";
            if (_shown) { Visible = false; _shown = false; }
        }
    }

    public void Toggle()
    {
        _shown = !_shown;
        Visible = _shown;
        if (_shown)
        {
            GetTree().Paused = true;
            _speedLbl.Text = $"速度: 暂停 (恢复 {_savedScale:0.##}x)";
        }
        else
        {
            GetTree().Paused = false;
            Engine.TimeScale = _savedScale > 0f ? _savedScale : 1f;
        }
    }
}
