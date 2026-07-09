using Godot;
using AIWarSandbox.Units;

namespace AIWarSandbox.Autoloads;

public enum GameState
{
    Boot,
    Setup,
    Briefing,
    Battle,
    End
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    private GameState _state = GameState.Boot;
    private bool _checked;

    public GameState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            GD.Print($"[GameManager] State -> {_state}");
        }
    }

    public override void _Ready()
    {
        Instance = this;
        State = GameState.Setup;
        EventBus.Instance.StructureDestroyed += OnStructureDestroyed;
    }

    public void TransitionTo(GameState next) => State = next;

    public void BeginBattle()
    {
        if (State != GameState.Briefing) return;
        State = GameState.Battle;
        EventBus.Instance.RaiseBattleStarted();
    }

    public override void _Process(double delta)
    {
        if (_state != GameState.Battle || _checked) return;
        CheckWinLose();
    }

    private void CheckWinLose()
    {
        bool friendlyBaseAlive = false;
        bool enemyBaseAlive = false;
        foreach (var u in UnitRegistry.Instance.All)
        {
            if (u is not Structure s || s.Kind != StructureKind.Base) continue;
            if (s.State == UnitState.Dead) continue;
            if (s.IsFriendly) friendlyBaseAlive = true;
            else enemyBaseAlive = true;
        }

        if (!enemyBaseAlive)
        {
            _checked = true;
            State = GameState.End;
            EventBus.Instance.RaiseBattleEnded(true);
            return;
        }
        if (!friendlyBaseAlive)
        {
            _checked = true;
            State = GameState.End;
            EventBus.Instance.RaiseBattleEnded(false);
        }
    }

    private void OnStructureDestroyed(Structure s)
    {
        if (s.Kind != StructureKind.Base) return;
        GD.Print($"[GameManager] Base destroyed: friendly={s.IsFriendly}");
    }
}
