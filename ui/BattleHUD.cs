using System.Text;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Ai;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// CanvasLayer in-battle HUD. Top bar: resources / elapsed time / unit counts.
/// Bottom panel: active plan summary + stop button. Left: scrollable log.
/// Subscribes to <see cref="EventBus.LogMessage"/>, <see cref="EventBus.UnitDied"/>,
/// and <see cref="EventBus.BattleEnded"/>. Frees self on battle end.
/// </summary>
public partial class BattleHUD : CanvasLayer
{
    private Label _manpower = null!;
    private Label _supplies = null!;
    private Label _elapsed = null!;
    private Label _friendly = null!;
    private Label _enemy = null!;
    private Label _planName = null!;
    private Label _planDesc = null!;
    private VBoxContainer _logList = null!;
    private ScrollContainer _logScroll = null!;
    private Label _killFlash = null!;

    private float _battleTime;
    private float _killFlashTimer;
    private float _updateAccum;
    private const float UpdateInterval = 0.2f; // 5Hz throttle

    public override void _Ready()
    {
        Layer = 5;
        BuildLayout();

        var saOverlay = new SituationalAwarenessOverlay();
        AddChild(saOverlay);

        EventBus.Instance.LogMessage += OnLogMessage;
        EventBus.Instance.UnitDied += OnUnitDied;
        EventBus.Instance.BattleEnded += OnBattleEnded;
        EventBus.Instance.PlanExecuting += OnPlanExecuting;
        EventBus.Instance.BattleStarted += OnBattleStarted;
    }

    private void BuildLayout()
    {
        // --- Top bar ---
        var topBar = new Panel { Name = "TopBar" };
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topBar.CustomMinimumSize = new Vector2(0, 56);
        topBar.SelfModulate = new Color(0, 0, 0, 0.55f);
        AddChild(topBar);

        var topRow = new HBoxContainer { Name = "TopRow" };
        topRow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        topRow.AddThemeConstantOverride("separation", 28);
        AddChild(topRow);

        _manpower = MakeStat(topRow, "人力: 0");
        _supplies = MakeStat(topRow, "补给: 0");
        _elapsed = MakeStat(topRow, "时间: 0.0s");
        _friendly = MakeStat(topRow, "友军: 0");
        _enemy = MakeStat(topRow, "敌军: 0");

        // --- Left log panel ---
        _logScroll = new ScrollContainer { Name = "LogScroll" };
        _logScroll.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        _logScroll.OffsetTop = 64;
        _logScroll.OffsetBottom = -120;
        _logScroll.CustomMinimumSize = new Vector2(340, 0);
        _logScroll.SelfModulate = new Color(0, 0, 0, 0.4f);
        AddChild(_logScroll);

        _logList = new VBoxContainer { Name = "LogList" };
        _logList.AddThemeConstantOverride("separation", 2);
        _logList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _logScroll.AddChild(_logList);

        // --- Bottom panel ---
        var bottom = new Panel { Name = "BottomPanel" };
        bottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        bottom.CustomMinimumSize = new Vector2(0, 100);
        bottom.SelfModulate = new Color(0, 0, 0, 0.6f);
        AddChild(bottom);

        var bottomRow = new HBoxContainer { Name = "BottomRow" };
        bottomRow.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bottomRow.AddThemeConstantOverride("separation", 24);
        AddChild(bottomRow);

        var planCol = new VBoxContainer { Name = "PlanInfo" };
        planCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        planCol.AddThemeConstantOverride("separation", 4);
        bottomRow.AddChild(planCol);

        _planName = new Label { Text = "当前方案: (等待选择)" };
        _planName.AddThemeFontSizeOverride("font_size", 18);
        planCol.AddChild(_planName);

        _planDesc = new Label { Text = "" };
        _planDesc.AddThemeFontSizeOverride("font_size", 14);
        _planDesc.Modulate = new Color(0.85f, 0.85f, 0.85f);
        planCol.AddChild(_planDesc);

        var stopBtn = new Button { Text = "停止" };
        stopBtn.CustomMinimumSize = new Vector2(140, 48);
        stopBtn.Pressed += OnStop;
        bottomRow.AddChild(stopBtn);

        // --- Kill flash (centered) ---
        _killFlash = new Label { Text = "" };
        _killFlash.SetAnchorsPreset(Control.LayoutPreset.Center);
        _killFlash.AddThemeFontSizeOverride("font_size", 36);
        _killFlash.Modulate = new Color(1f, 0.3f, 0.3f);
        _killFlash.Visible = false;
        AddChild(_killFlash);
    }

    private static Label MakeStat(Control parent, string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 18);
        parent.AddChild(lbl);
        return lbl;
    }

    public override void _Process(double delta)
    {
        _battleTime += (float)delta;

        // Kill flash decay
        if (_killFlashTimer > 0f)
        {
            _killFlashTimer -= (float)delta;
            if (_killFlashTimer <= 0f) _killFlash.Visible = false;
        }

        // Throttled 5Hz refresh of dynamic counts.
        _updateAccum += (float)delta;
        if (_updateAccum < UpdateInterval) return;
        _updateAccum = 0f;

        var rm = ResourceManager.Instance;
        _manpower.Text = $"人力: {rm?.Manpower ?? 0}";
        _supplies.Text = $"补给: {rm?.Supplies ?? 0}";
        _elapsed.Text = $"时间: {_battleTime:F1}s";

        int f = 0, e = 0;
        var registry = UnitRegistry.Instance;
        if (registry != null)
        {
            foreach (var u in registry.All)
            {
                if (u.State == UnitState.Dead) continue;
                if (u.IsFriendly) f++; else e++;
            }
        }
        _friendly.Text = $"友军: {f}";
        _enemy.Text = $"敌军: {e}";
    }

    private void OnLogMessage(string message)
    {
        var line = new Label
        {
            Text = $"[{_battleTime:F1}s] {message}"
        };
        line.AddThemeFontSizeOverride("font_size", 12);
        _logList.AddChild(line);

        // Keep log bounded to the most recent 200 lines.
        while (_logList.GetChildCount() > 200)
        {
            var old = _logList.GetChild(0);
            _logList.RemoveChild(old);
            old.QueueFree();
        }

        // Scroll to bottom: set ScrollVertical to its maximum (content - viewport).
        _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;
    }

    private void OnUnitDied(AIWarSandbox.Units.Unit unit)
    {
        if (unit == null) return;
        bool friendly = unit.IsFriendly;
        _killFlash.Text = friendly ? "友军阵亡!" : "击杀!";
        _killFlash.Modulate = friendly
            ? new Color(1f, 0.3f, 0.3f)
            : new Color(0.3f, 1f, 0.4f);
        _killFlash.Visible = true;
        _killFlashTimer = 0.8f;
    }

    private void OnPlanExecuting(Plan plan)
    {
        if (plan == null) return;
        _planName.Text = $"当前方案: {plan.Name} [{plan.Type}]";
        _planDesc.Text = plan.Description;
    }

    private void OnStop()
    {
        TacticalAIManager.Instance?.Halt();
        EventBus.Instance?.RaiseLog("[BattleHUD] Stop pressed — issuing halt.");
    }

    private void OnBattleStarted()
    {
        _battleTime = 0f;
    }

    private void OnBattleEnded(bool victory)
    {
        EventBus.Instance?.RaiseLog($"[BattleHUD] Battle ended — victory={victory}. Freeing HUD.");
        QueueFree();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.LogMessage -= OnLogMessage;
            EventBus.Instance.UnitDied -= OnUnitDied;
            EventBus.Instance.BattleEnded -= OnBattleEnded;
            EventBus.Instance.PlanExecuting -= OnPlanExecuting;
            EventBus.Instance.BattleStarted -= OnBattleStarted;
        }
    }
}
