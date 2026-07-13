using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ui;

/// <summary>
/// Briefing force-composition UI for both friendly and enemy sides.
/// Submits a <see cref="ForceConfig"/> via <see cref="EventBus.RaiseForceConfigSubmitted"/>.
/// </summary>
public partial class EnemyConfigUI : CanvasLayer
{
    private HSlider _friendlyInfSlider = null!;
    private HSlider _friendlyTankSlider = null!;
    private OptionButton _friendlyInfWeapon = null!;
    private OptionButton _friendlyTankWeapon = null!;

    private HSlider _countSlider = null!;
    private HSlider _heavySlider = null!;
    private HSlider _diffSlider = null!;
    private OptionButton _primaryWeapon = null!;
    private OptionButton _heavyWeapon = null!;
    private Label _summary = null!;

    public override void _Ready()
    {
        Layer = 10;
        BuildLayout();

        _friendlyInfSlider.ValueChanged += _ => UpdateSummary();
        _friendlyTankSlider.ValueChanged += _ => UpdateSummary();
        _friendlyInfWeapon.ItemSelected += _ => UpdateSummary();
        _friendlyTankWeapon.ItemSelected += _ => UpdateSummary();
        _countSlider.ValueChanged += _ => UpdateSummary();
        _heavySlider.ValueChanged += _ => UpdateSummary();
        _diffSlider.ValueChanged += _ => UpdateSummary();
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
        panel.CustomMinimumSize = new Vector2(600, 0);
        center.AddChild(panel);

        var margin = new MarginContainer { Name = "Margin" };
        margin.AddThemeConstantOverride("margin_left", 24);
        margin.AddThemeConstantOverride("margin_right", 24);
        margin.AddThemeConstantOverride("margin_top", 24);
        margin.AddThemeConstantOverride("margin_bottom", 24);
        panel.AddChild(margin);

        var vbox = new VBoxContainer { Name = "Content" };
        vbox.AddThemeConstantOverride("separation", 12);
        margin.AddChild(vbox);

        var title = new Label { Text = "双方编成配置" };
        title.AddThemeFontSizeOverride("font_size", 28);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(title);

        var friendTitle = new Label { Text = "— 友军 —" };
        friendTitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(friendTitle);

        _friendlyInfSlider = MakeSlider(vbox, "步兵数量", 2, 16, 6);
        _friendlyTankSlider = MakeSlider(vbox, "坦克数量", 0, 6, 2);
        _friendlyInfWeapon = MakeOption(vbox, "步兵武器", (int)WeaponType.Rifle);
        _friendlyTankWeapon = MakeOption(vbox, "坦克主炮", (int)WeaponType.Cannon);

        var enemyTitle = new Label { Text = "— 敌军 —" };
        enemyTitle.HorizontalAlignment = HorizontalAlignment.Center;
        vbox.AddChild(enemyTitle);

        _countSlider = MakeSlider(vbox, "敌军人数", 4, 30, 12);
        _heavySlider = MakeSlider(vbox, "重装比例 %", 0, 100, 30);
        _diffSlider = MakeSlider(vbox, "难度 (0被动/1标准/2激进)", 0, 2, 1);
        _primaryWeapon = MakeOption(vbox, "主武器 (轻装)", (int)WeaponType.Rifle);
        _heavyWeapon = MakeOption(vbox, "重装武器", (int)WeaponType.Rocket);

        _summary = new Label { Name = "Summary", Text = "" };
        _summary.AddThemeFontSizeOverride("font_size", 15);
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
        opt.AddItem("Sniper (狙击)", (int)WeaponType.Sniper);
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
        string diff = cfg.Difficulty switch { 0 => "被动", 2 => "激进", _ => "标准" };
        _summary.Text =
            $"友军: {cfg.FriendlyInfantry} 步兵 + {cfg.FriendlyTanks} 坦克\n" +
            $"敌军: {cfg.EnemyCount} 人 (轻{light}/重{heavy}) 难度={diff}";
    }

    private ForceConfig BuildConfig() => new()
    {
        FriendlyInfantry = (int)_friendlyInfSlider.Value,
        FriendlyTanks = (int)_friendlyTankSlider.Value,
        FriendlyInfantryWeapon = (WeaponType)_friendlyInfWeapon.GetSelectedId(),
        FriendlyTankWeapon = (WeaponType)_friendlyTankWeapon.GetSelectedId(),
        EnemyCount = (int)_countSlider.Value,
        HeavyRatio = (int)_heavySlider.Value,
        Difficulty = (int)_diffSlider.Value,
        EnemyPrimaryWeapon = (WeaponType)_primaryWeapon.GetSelectedId(),
        EnemyHeavyWeapon = (WeaponType)_heavyWeapon.GetSelectedId(),
    };

    private void OnConfirm()
    {
        var cfg = BuildConfig();
        EventBus.Instance.RaiseLog(
            $"[ForceConfig] friendly={cfg.FriendlyInfantry}+{cfg.FriendlyTanks}t " +
            $"enemy={cfg.EnemyCount} diff={cfg.Difficulty}");
        EventBus.Instance.RaiseForceConfigSubmitted(cfg);
        Hide();
    }
}
