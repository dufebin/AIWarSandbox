using System.Collections.Generic;
using Godot;
using AIWarSandbox.Ai;
using AIWarSandbox.Autoloads;

namespace AIWarSandbox.Ui;

/// <summary>
/// CanvasLayer showing the generated <see cref="Plan"/> cards. Each card has a
/// "选择此方案" button that raises <see cref="EventBus.RaisePlanSelected"/> and
/// kicks off execution via <see cref="TacticalAIManager.ExecutePlan"/> (which in
/// turn raises <see cref="EventBus.BattleStarted"/>). Subscribes to
/// <see cref="EventBus.PlanSelected"/> for verification logging.
/// </summary>
public partial class PlanSelector : CanvasLayer
{
    private List<Plan> _plans = new();
    private GridContainer _grid = null!;
    private Label _header = null!;
    private int _verifiedIndex = -1;

    public override void _Ready()
    {
        Layer = 10;
        BuildLayout();
        Visible = false;

        EventBus.Instance.PlanSelected += OnPlanSelected;
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
        panel.CustomMinimumSize = new Vector2(900, 0);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Content" };
        vbox.AddThemeConstantOverride("separation", 16);
        margin.AddChild(vbox);

        _header = new Label { Text = "选择进攻方案" };
        _header.AddThemeFontSizeOverride("font_size", 26);
        _header.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_header);

        _grid = new GridContainer { Name = "PlanGrid" };
        _grid.Columns = 2;
        _grid.AddThemeConstantOverride("h_separation", 18);
        _grid.AddThemeConstantOverride("v_separation", 18);
        vbox.AddChild(_grid);
    }

    /// <summary>Called by MainScene after <see cref="EventBus.ConfigSubmitted"/>.</summary>
    public void ShowPlans(List<Plan> plans)
    {
        _plans = plans ?? new List<Plan>();
        foreach (var child in _grid.GetChildren())
            child.QueueFree();

        for (int i = 0; i < _plans.Count; i++)
        {
            _grid.AddChild(MakeCard(_plans[i], i));
        }

        _header.Text = $"选择进攻方案 ({_plans.Count} 套)";
        Visible = true;
    }

    private Panel MakeCard(Plan p, int index)
    {
        var card = new Panel { Name = $"Card{index}" };
        card.CustomMinimumSize = new Vector2(420, 0);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        card.AddChild(margin);

        var col = new VBoxContainer { Name = "Body" };
        col.AddThemeConstantOverride("separation", 6);
        margin.AddChild(col);

        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 10);
        col.AddChild(nameRow);

        var icon = new Label { Text = $"[{p.Type.ToString().ToUpperInvariant()}]" };
        icon.AddThemeFontSizeOverride("font_size", 16);
        icon.Modulate = new Color(1f, 0.85f, 0.3f);
        nameRow.AddChild(icon);

        var name = new Label { Text = p.Name };
        name.AddThemeFontSizeOverride("font_size", 20);
        nameRow.AddChild(name);

        var desc = new Label { Text = p.Description };
        desc.AddThemeFontSizeOverride("font_size", 14);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(0, 48);
        col.AddChild(desc);

        int casualtyPct = (int)System.Math.Round(p.ExpectedCasualtyRate * 100f);
        int successPct = (int)System.Math.Round(p.SuccessProbability * 100f);
        int durationSec = p.EstimatedDurationSec > 0
            ? p.EstimatedDurationSec
            : (int)System.Math.Round(p.EstimatedDuration);

        var stats = new Label
        {
            Text =
                $"预计伤亡率: {casualtyPct}%\n" +
                $"成功率: {successPct}%\n" +
                $"预计时长: {durationSec}s"
        };
        stats.AddThemeFontSizeOverride("font_size", 14);
        col.AddChild(stats);

        if (p.Confidence != null)
        {
            int confPct = (int)System.Math.Round(p.Confidence.Overall * 100f);
            var conf = new Label
            {
                Text = $"决策置信度: {confPct}%\n{p.Confidence.Summary}"
            };
            conf.AddThemeFontSizeOverride("font_size", 13);
            conf.Modulate = new Color(0.7f, 0.9f, 1f);
            col.AddChild(conf);
        }

        if (p.Outcome != null && p.Outcome.Simulations > 0)
        {
            var o = p.Outcome;
            var outcome = new Label
            {
                Text =
                    $"蒙特卡洛 ({o.Simulations}次): 伤亡 {o.MeanCasualties:F1}±{o.StdCasualties:F1} " +
                    $"[{o.MinCasualties:F0}~{o.MaxCasualties:F0}] 成功 {o.SuccessRate * 100f:F0}%"
            };
            outcome.AddThemeFontSizeOverride("font_size", 13);
            outcome.Modulate = new Color(0.9f, 0.85f, 0.7f);
            col.AddChild(outcome);
        }

        var btn = new Button { Text = "选择此方案" };
        btn.CustomMinimumSize = new Vector2(0, 40);
        int captured = index;
        btn.Pressed += () => Select(captured);
        col.AddChild(btn);

        return card;
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _plans.Count) return;
        EventBus.Instance.RaiseLog($"[PlanSelector] Player selected plan {index}: {_plans[index].Name}");

        // Raise the event per spec. ExecutePlan also raises PlanSelected + BattleStarted;
        // we raise explicitly first to guarantee the signal fires even if ExecutePlan no-ops.
        EventBus.Instance.RaisePlanSelected(index);

        // Drive the actual battle start (cannot modify MainScene/GameManager — these are
        // the locked entry points). ExecutePlan -> OrderExecutor.Execute -> RaiseBattleStarted.
        TacticalAIManager.Instance.ExecutePlan(index);

        Hide();
    }

    private void OnPlanSelected(int index)
    {
        _verifiedIndex = index;
        GD.Print($"[PlanSelector] Verified PlanSelected event received: index={index}");
    }

    private void OnBattleStarted()
    {
        // Match legacy behavior: hide once battle is underway.
        Hide();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PlanSelected -= OnPlanSelected;
            EventBus.Instance.BattleStarted -= OnBattleStarted;
        }
    }
}
