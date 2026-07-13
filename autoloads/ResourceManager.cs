using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Autoloads;

public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; } = null!;

    /// <summary>Reinforcement tickets: drained by friendly casualties.</summary>
    public int Manpower { get; private set; } = 100;
    /// <summary>Logistics reserve: slowly consumed while the battle rages.</summary>
    public int Supplies { get; private set; } = 100;

    private const int ManpowerPerLoss = 15;
    private const float SupplyDrainPerSec = 1.5f;
    private float _supplyFrac;

    public override void _Ready()
    {
        Instance = this;
        EventBus.Instance.BattleStarted += OnBattleStarted;
        EventBus.Instance.UnitDied += OnUnitDied;
    }

    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.BattleStarted -= OnBattleStarted;
            EventBus.Instance.UnitDied -= OnUnitDied;
        }
    }

    public override void _Process(double delta)
    {
        // Bleed supplies only during an active battle so the HUD reads as live logistics.
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Battle) return;
        if (Supplies <= 0) return;
        _supplyFrac += (float)delta * SupplyDrainPerSec;
        if (_supplyFrac >= 1f)
        {
            int whole = Mathf.FloorToInt(_supplyFrac);
            _supplyFrac -= whole;
            Supplies = Mathf.Max(0, Supplies - whole);
        }
    }

    private void OnBattleStarted()
    {
        Manpower = 100;
        Supplies = 100;
        _supplyFrac = 0f;
    }

    private void OnUnitDied(Unit u)
    {
        if (u != null && u.IsFriendly && u is Combatant)
            Manpower = Mathf.Max(0, Manpower - ManpowerPerLoss);
    }

    public bool TrySpend(int manpowerCost, int suppliesCost)
    {
        if (Manpower < manpowerCost || Supplies < suppliesCost) return false;
        Manpower -= manpowerCost;
        Supplies -= suppliesCost;
        return true;
    }

    public void Reset(int manpower, int supplies)
    {
        Manpower = manpower;
        Supplies = supplies;
    }
}
