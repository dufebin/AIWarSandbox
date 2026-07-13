using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// CanvasLayer showing the mission briefing for Operation: First Strike.
/// Offers "Configure Enemy" (opens <see cref="EnemyConfigUI"/>) and
/// "Skip to Battle (default)" (submits <see cref="EnemyConfig.CreateDefault"/>).
/// Frees itself on <see cref="EventBus.BattleStarted"/>.
/// </summary>
public partial class BriefingUI : CanvasLayer
{
    public override void _Ready()
    {
        Layer = 10;
        BuildLayout();

        EventBus.Instance.BattleStarted += OnBattleStarted;
    }

    private void BuildLayout()
    {
        var backdrop = new Panel { Name = "Backdrop" };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.SelfModulate = new Color(0, 0, 0, 0.7f);
        AddChild(backdrop);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { Name = "Card" };
        panel.CustomMinimumSize = new Vector2(640, 0);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Content" };
        vbox.AddThemeConstantOverride("separation", 16);
        margin.AddChild(vbox);

        var title = new Label { Text = "Operation: First Strike" };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var desc = new Label
        {
            Text =
                "指挥官，敌方在 FirstMap 区域建立了一座前线基地，威胁我方补给线。\n" +
                "我方已部署兵力至西南角，需选择进攻方案摧毁敌方主基地。\n" +
                "请先配置敌情，或使用默认配置直接进入战斗。"
        };
        desc.AddThemeFontSizeOverride("font_size", 18);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(0, 96);
        desc.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(desc);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        vbox.AddChild(spacer);

        var btnRow = new HBoxContainer { Name = "Buttons" };
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(btnRow);

        var configureBtn = new Button { Text = "配置双方编成" };
        configureBtn.CustomMinimumSize = new Vector2(280, 56);
        configureBtn.Pressed += OnConfigureEnemy;
        btnRow.AddChild(configureBtn);

        var skipBtn = new Button { Text = "跳过 (默认编成)" };
        skipBtn.CustomMinimumSize = new Vector2(280, 56);
        skipBtn.Pressed += OnSkipToBattle;
        btnRow.AddChild(skipBtn);
    }

    private void OnConfigureEnemy()
    {
        var ui = new EnemyConfigUI();
        GetParent()?.AddChild(ui);
        Hide();
    }

    private void OnSkipToBattle()
    {
        var cfg = ForceConfig.CreateDefault();
        EventBus.Instance.RaiseLog(
            $"[BriefingUI] Skip-to-battle — friendly={cfg.FriendlyInfantry}+{cfg.FriendlyTanks}t enemy={cfg.EnemyCount}");
        EventBus.Instance.RaiseForceConfigSubmitted(cfg);
        Hide();
    }

    private void OnBattleStarted()
    {
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
            EventBus.Instance.BattleStarted -= OnBattleStarted;
    }
}
