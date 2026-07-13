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
    private float _checkAccum;
    private const float CheckInterval = 0.4f; // throttle win/lose scan (was every frame)

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
        // BattleStarted is raised by OrderExecutor when a plan is executed. Nothing
        // else drives the state machine into Battle, so subscribe here — otherwise
        // CheckWinLose never runs and the match can never be won or lost.
        EventBus.Instance.BattleStarted += OnBattleStarted;
    }

    public void TransitionTo(GameState next) => State = next;

    public void BeginBattle()
    {
        if (State != GameState.Briefing) return;
        State = GameState.Battle;
        EventBus.Instance.RaiseBattleStarted();
    }

    /// <summary>Enters Battle state without re-raising BattleStarted (avoids recursion).</summary>
    private void OnBattleStarted()
    {
        _checked = false;
        _checkAccum = 0f;
        State = GameState.Battle;
    }

    public override void _Process(double delta)
    {
        if (_state != GameState.Battle || _checked) return;
        _checkAccum += (float)delta;
        if (_checkAccum < CheckInterval) return;
        _checkAccum = 0f;
        CheckWinLose();
    }

    private void CheckWinLose()
    {
        bool friendlyBaseAlive = false;
        bool enemyBaseAlive = false;
        int friendlyCombatants = 0;
        int enemyCombatants = 0;
        foreach (var u in UnitRegistry.Instance.All)
        {
            if (u.State == UnitState.Dead) continue;
            if (u is Structure s)
            {
                if (s.Kind != StructureKind.Base) continue;
                if (s.IsFriendly) friendlyBaseAlive = true;
                else enemyBaseAlive = true;
            }
            else if (u is Combatant)
            {
                if (u.IsFriendly) friendlyCombatants++;
                else enemyCombatants++;
            }
        }

        // Victory: enemy base destroyed, OR the enemy has no fighting units left.
        if (!enemyBaseAlive || enemyCombatants == 0)
        {
            _checked = true;
            State = GameState.End;
            EventBus.Instance.RaiseBattleEnded(true);
            return;
        }
        // Defeat: friendly base destroyed, OR all friendly combatants are gone
        // (the lone base can't take the objective, so the operation has failed).
        if (!friendlyBaseAlive || friendlyCombatants == 0)
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
