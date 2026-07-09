using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// CanvasLayer UI for editing an <see cref="EnemyConfig"/>. Sliders + option
/// buttons feed a fresh <see cref="EnemyConfig"/> instance submitted through
/// <see cref="EventBus.RaiseConfigSubmitted"/>. Kept separate from the POD
/// <see cref="EnemyConfig"/> data class.
/// </summary>
public partial class EnemyConfigUI : CanvasLayer
{
    private HSlider _countSlider = null!;
    private HSlider _heavySlider = null!;
    private OptionButton _primaryWeapon = null!;
    private OptionButton _heavyWeapon = null!;
    private Label _summary = null!;

    public override void _Ready()
    {
        Layer = 10;
        BuildLayout();

        _countSlider.ValueChanged += _ => UpdateSummary();
        _heavySlider.ValueChanged += _ => UpdateSummary();
        _primaryWeapon.ItemSelected += _ => UpdateSummary();
        _heavyWeapon.ItemSelected += _ => UpdateSummary();

        UpdateSummary();
    }

    private void BuildLayout()
    {
        var backdrop = new Panel { Name = "Backdrop" };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.SelfModulate = new Color(0, 0, 0, 0.65f);
        AddChild(backdrop);

        var center = new CenterContainer { Name = "Center" };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);

        var panel = new Panel { Name = "Card" };
        panel.CustomMinimumSize = new Vector2(560, 0);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Content" };
        vbox.AddThemeConstantOverride("separation", 14);
        margin.AddChild(vbox);

        var title = new Label { Text = "敌方配置" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        _countSlider = MakeSlider(vbox, "敌军人数", 4, 30, 12);
        _heavySlider = MakeSlider(vbox, "重装比例 %", 0, 100, 30);

        _primaryWeapon = MakeOption(vbox, "主武器 (轻装)", 0);
        _heavyWeapon = MakeOption(vbox, "重装武器", (int)WeaponType.Rocket);

        _summary = new Label { Name = "Summary", Text = "" };
        _summary.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(_summary);

        var confirm = new Button { Text = "确认" };
        confirm.CustomMinimumSize = new Vector2(0, 48);
        confirm.Pressed += OnConfirm;
        vbox.AddChild(confirm);
    }

    private static HSlider MakeSlider(Control parent, string label, int min, int max, int val)
    {
        var row = new VBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label { Text = label });
        var s = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Value = val,
            Step = 1,
            CustomMinimumSize = new Vector2(0, 28)
        };
        row.AddChild(s);
        return s;
    }

    private static OptionButton MakeOption(Control parent, string label, int selected)
    {
        var row = new VBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label { Text = label });
        var opt = new OptionButton();
        opt.AddItem("Rifle (步枪)", (int)WeaponType.Rifle);
        opt.AddItem("MG (机枪)", (int)WeaponType.Mg);
        opt.AddItem("Cannon (炮)", (int)WeaponType.Cannon);
        opt.AddItem("Rocket (火箭)", (int)WeaponType.Rocket);
        int selIdx = opt.GetItemIndex(selected);
        opt.Select(selIdx >= 0 ? selIdx : 0);
        row.AddChild(opt);
        return opt;
    }

    private void UpdateSummary()
    {
        var cfg = BuildConfig();
        int heavy = cfg.EnemyCount * cfg.HeavyRatio / 100;
        int light = cfg.EnemyCount - heavy;
        _summary.Text = $"预计敌军: {cfg.EnemyCount} 人\n  轻装 ({cfg.EnemyPrimaryWeapon}): {light}\n  重装 ({cfg.EnemyHeavyWeapon}): {heavy}";
    }

    private EnemyConfig BuildConfig() => new()
    {
        EnemyCount = (int)_countSlider.Value,
        HeavyRatio = (int)_heavySlider.Value,
        EnemyPrimaryWeapon = (WeaponType)(int)_primaryWeapon.GetSelectedMetadata(),
        EnemyHeavyWeapon = (WeaponType)(int)_heavyWeapon.GetSelectedMetadata(),
    };

    private void OnConfirm()
    {
        var cfg = BuildConfig();
        EventBus.Instance.RaiseLog(
            $"[EnemyConfigUI] Submitted — count={cfg.EnemyCount} heavy%={cfg.HeavyRatio} " +
            $"primary={cfg.EnemyPrimaryWeapon} heavy={cfg.EnemyHeavyWeapon}");
        EventBus.Instance.RaiseConfigSubmitted(cfg);
        Hide();
    }
}
