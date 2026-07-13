using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// CanvasLayer shown on battle end. Displays 胜利!/失败... plus survivor/killed/duration
/// stats. Offers 重新开始 (reload scene) and 退出 (quit). Subscribes to
/// <see cref="EventBus.BattleStarted"/> to start timing and
/// <see cref="EventBus.BattleEnded"/> to reveal results.
/// </summary>
public partial class EndScreen : CanvasLayer
{
    private Label _title = null!;
    private Label _stats = null!;
    private Button _restart = null!;
    private Button _quit = null!;

    private float _battleStartUnix;
    private float _battleDuration;
    private int _friendlyStartCount;
    private int _enemyStartCount;
    private int _enemyKilled;
    private int _friendlyLost;

    public override void _Ready()
    {
        Layer = 20;
        BuildLayout();
        Visible = false;

        EventBus.Instance.BattleStarted += OnBattleStarted;
        EventBus.Instance.BattleEnded += OnBattleEnded;
        EventBus.Instance.UnitDied += OnUnitDied;
    }

    private void BuildLayout()
    {
        var backdrop = new Panel { Name = "Backdrop" };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.SelfModulate = new Color(0, 0, 0, 0.8f);
        AddChild(backdrop);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { Name = "Card" };
        panel.CustomMinimumSize = new Vector2(480, 0);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 32);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Content" };
        vbox.AddThemeConstantOverride("separation", 20);
        margin.AddChild(vbox);

        _title = new Label { Text = "" };
        _title.AddThemeFontSizeOverride("font_size", 56);
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_title);

        _stats = new Label { Text = "" };
        _stats.AddThemeFontSizeOverride("font_size", 20);
        _stats.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(_stats);

        var spacer = new Control { CustomMinimumSize = new Vector2(0, 8) };
        vbox.AddChild(spacer);

        var btnRow = new HBoxContainer { Name = "Buttons" };
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        btnRow.AddThemeConstantOverride("separation", 24);
        vbox.AddChild(btnRow);

        _restart = new Button { Text = "重新开始" };
        _restart.CustomMinimumSize = new Vector2(180, 56);
        _restart.Pressed += OnRestart;
        btnRow.AddChild(_restart);

        _quit = new Button { Text = "退出" };
        _quit.CustomMinimumSize = new Vector2(180, 56);
        _quit.Pressed += OnQuit;
        btnRow.AddChild(_quit);
    }

    private void OnUnitDied(Unit u)
    {
        if (u == null) return;
        if (u.IsFriendly) _friendlyLost++;
        else _enemyKilled++;
    }

    private void OnBattleStarted()
    {
        _battleStartUnix = Time.GetTicksMsec() / 1000f;
        _enemyKilled = 0;
        _friendlyLost = 0;

        var registry = UnitRegistry.Instance;
        _friendlyStartCount = 0;
        _enemyStartCount = 0;
        if (registry != null)
        {
            foreach (var u in registry.All)
            {
                if (u.State == UnitState.Dead) continue;
                if (u.IsFriendly) _friendlyStartCount++; else _enemyStartCount++;
            }
        }
    }

    private void OnBattleEnded(bool victory)
    {
        _battleDuration = (Time.GetTicksMsec() / 1000f) - _battleStartUnix;

        // Dead units are freed immediately, so derive counts from the death tally
        // accumulated over the battle rather than scanning the (now shrunken) registry.
        int friendlyAlive = Mathf.Max(0, _friendlyStartCount - _friendlyLost);
        int enemyDead = _enemyKilled;

        _title.Text = victory ? "胜利!" : "失败...";
        _title.Modulate = victory
            ? new Color(0.3f, 1f, 0.3f)
            : new Color(1f, 0.3f, 0.3f);

        Units.SfxBus.Play(this, victory ? Units.SfxBus.Kind.Victory : Units.SfxBus.Kind.Defeat);

        _stats.Text =
            $"友军幸存: {friendlyAlive} / {_friendlyStartCount}\n" +
            $"敌方击杀: {enemyDead} / {_enemyStartCount}\n" +
            $"战斗时长: {_battleDuration:F1}s";

        Visible = true;
    }

    private void OnRestart()
    {
        GetTree().ReloadCurrentScene();
    }

    private void OnQuit()
    {
        GetTree().Quit();
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.BattleStarted -= OnBattleStarted;
            EventBus.Instance.BattleEnded -= OnBattleEnded;
            EventBus.Instance.UnitDied -= OnUnitDied;
        }
    }
}
