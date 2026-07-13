using System.Collections.Generic;
using Godot;
using AIWarSandbox.Ai;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;
using AIWarSandbox.Ui;
using AIWarSandbox.World;

namespace AIWarSandbox.Scenes;

public partial class MainScene : Node3D
{
    private TerrainGenerator _terrain = new();
    private RTSCamera _camera = new();
    private BattleHUD _hud = new();
    private PlanSelector _planSelector = new();
    private EndScreen _endScreen = new();
    private UnitCommandController _commandController = new();
    private PauseMenu _pauseMenu = new();
    private ForceConfig _forceCfg = ForceConfig.CreateDefault();
    private EnemyConfig _enemyCfg = EnemyConfig.CreateDefault();
    private SensorModel _sensorModel = new();
    private MapSource? _realMapSource;
    private ImportedTerrainGenerator? _importedTerrain;
    private MapConfig _mapConfig = MapConfig.CreateFirstMap();
    private bool _forcesSpawned;

    public override void _Ready()
    {
        AddChild(_terrain);
        var config = _mapConfig;
        _terrain.Generate(config);

        if (_realMapSource != null)
        {
            _importedTerrain = new ImportedTerrainGenerator();
            AddChild(_importedTerrain);
            _ = BuildImportedTerrainAsync(_realMapSource, config);
            _terrain.Visible = false;
            GD.Print("[MainScene] Real-map mode enabled — importing satellite + elevation tiles");
        }

        PlaceSpawnPoints(config);
        PlaceResourcePoints(config);
        SpawnBases(config);

        TacticalAIManager.Instance.Bind(_terrain);

        SetupCamera();
        AddChild(_hud);
        AddChild(_planSelector);
        AddChild(_endScreen);
        AddChild(_sensorModel);
        AddChild(_commandController);
        AddChild(_pauseMenu);

        var minimap = new Minimap();
        minimap.Bind(_mapConfig.Size, _camera);
        AddChild(minimap);
        AddChild(new FogOfWarViz());

        EventBus.Instance.ForceConfigSubmitted += OnForceConfigSubmitted;
        EventBus.Instance.ConfigSubmitted += OnConfigSubmitted;
        GameManager.Instance.TransitionTo(GameState.Briefing);
        ShowBriefing();

        GD.Print($"[MainScene] Ready — terrain size={config.Size} seed={config.Seed}");
    }

    public void ConfigureRealMap(MapSource source) => _realMapSource = source;

    private async System.Threading.Tasks.Task BuildImportedTerrainAsync(MapSource source, MapConfig config)
    {
        var importer = new MapImporter { Source = source };
        AddChild(importer);
        try
        {
            var (satellite, heightmap, w, h) = await importer.ImportAsync();
            _importedTerrain!.Build(source, satellite, heightmap);
            GD.Print($"[MainScene] Imported terrain built — {w}x{h}px");
        }
        catch (System.Exception ex)
        {
            GD.PushError($"[MainScene] Map import failed: {ex.Message}");
        }
        finally
        {
            importer.QueueFree();
        }
    }

    public override void _Process(double delta)
    {
        if (UnitRegistry.Instance == null) return;
        var friendlies = UnitRegistry.Instance.Friendly;
        var all = UnitRegistry.Instance.All;
        if (friendlies.Count > 0 && all.Count > 0)
            _sensorModel.Tick(friendlies, all);
    }

    private void SetupCamera()
    {
        _camera.Position = new Vector3(0, 45, 40);
        _camera.Rotation = new Vector3(Mathf.DegToRad(-45), 0, 0);
        AddChild(_camera);
        _camera.MakeCurrent();
        _commandController.BindCamera(_camera);
    }

    private void ShowBriefing()
    {
        AddChild(new BriefingUI());
    }

    private void OnForceConfigSubmitted(ForceConfig cfg)
    {
        if (_forcesSpawned) return;
        _forcesSpawned = true;
        _forceCfg = cfg;
        _enemyCfg = cfg.ToEnemyConfig();
        SpawnForces(cfg);
        var plans = TacticalAIManager.Instance.GeneratePlans(_enemyCfg);
        _planSelector.ShowPlans(plans);
        GameManager.Instance.TransitionTo(GameState.Briefing);
    }

    /// <summary>Legacy path: wrap EnemyConfig into ForceConfig defaults.</summary>
    private void OnConfigSubmitted(EnemyConfig cfg)
    {
        if (_forcesSpawned) return;
        var fc = ForceConfig.CreateDefault();
        fc.EnemyCount = cfg.EnemyCount;
        fc.HeavyRatio = cfg.HeavyRatio;
        fc.Difficulty = cfg.Difficulty;
        fc.EnemyPrimaryWeapon = cfg.EnemyPrimaryWeapon;
        fc.EnemyHeavyWeapon = cfg.EnemyHeavyWeapon;
        OnForceConfigSubmitted(fc);
    }

    private void PlaceSpawnPoints(MapConfig config)
    {
        float half = config.Size * 0.5f;
        for (int i = 0; i < config.FriendlySpawnCount; i++)
        {
            var sp = new SpawnPoint
            {
                IsFriendly = true,
                Role = $"friendly_{i}",
                Position = _terrain.SnapToGround(new Vector3(-half + 4f + i * 3f, 0, -half + 4f))
            };
            _terrain.AddChild(sp);
        }
        for (int i = 0; i < config.EnemySpawnCount; i++)
        {
            var sp = new SpawnPoint
            {
                IsFriendly = false,
                Role = $"enemy_{i}",
                Position = _terrain.SnapToGround(new Vector3(half - 4f - i * 3f, 0, half - 4f))
            };
            _terrain.AddChild(sp);
        }
    }

    private void PlaceResourcePoints(MapConfig config)
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)config.Seed };
        for (int i = 0; i < config.ResourcePointCount; i++)
        {
            var pos = new Vector3(
                rng.RandfRange(-config.Size * 0.4f, config.Size * 0.4f),
                0,
                rng.RandfRange(-config.Size * 0.4f, config.Size * 0.4f));
            var rp = new ResourcePoint
            {
                PointName = $"Sector_{i}",
                Position = _terrain.SnapToGround(pos)
            };
            _terrain.AddChild(rp);
        }
    }

    private void SpawnBases(MapConfig config)
    {
        float half = config.Size * 0.5f;
        AddChild(UnitFactory.CreateStructure(StructureKind.Base, true,
            _terrain.SnapToGround(new Vector3(-half + 6f, 0, -half + 6f))));
        AddChild(UnitFactory.CreateStructure(StructureKind.Base, false,
            _terrain.SnapToGround(new Vector3(half - 6f, 0, half - 6f))));
    }

    private void SpawnForces(ForceConfig cfg)
    {
        float half = _mapConfig.Size * 0.5f;
        var friendlyUnits = new List<Unit>();

        Vector3 fOrigin = new(-half + 10f, 0, -half + 12f);
        Vector3 fRally = _terrain.SnapToGround(new Vector3(-half * 0.35f, 0, -half * 0.35f));

        for (int i = 0; i < cfg.FriendlyInfantry; i++)
        {
            var pos = _terrain.SnapToGround(fOrigin + new Vector3(i * 1.6f, 0, (i % 3) * 1.2f));
            var u = UnitFactory.CreateInfantry(cfg.FriendlyInfantryWeapon, true, pos);
            AddChild(u);
            friendlyUnits.Add(u);
            if (u is Combatant c) c.MoveTo(fRally + new Vector3(i * 1.5f, 0, 0));
        }
        for (int i = 0; i < cfg.FriendlyTanks; i++)
        {
            var pos = _terrain.SnapToGround(fOrigin + new Vector3(4f + i * 3.5f, 0, -2f));
            var v = UnitFactory.CreateVehicle(cfg.FriendlyTankWeapon, true, pos);
            AddChild(v);
            friendlyUnits.Add(v);
            if (v is Combatant c) c.MoveTo(fRally + new Vector3(i * 3f, 0, -2f));
        }

        Vector3 eOrigin = new(half - 10f, 0, half - 12f);
        Vector3 eRally = _terrain.SnapToGround(new Vector3(half * 0.35f, 0, half * 0.35f));

        int heavy = cfg.EnemyCount * cfg.HeavyRatio / 100;
        int light = cfg.EnemyCount - heavy;
        for (int i = 0; i < light; i++)
        {
            var pos = _terrain.SnapToGround(eOrigin + new Vector3(-i * 1.6f, 0, -(i % 3) * 1.2f));
            var e = UnitFactory.CreateInfantry(cfg.EnemyPrimaryWeapon, false, pos);
            AddChild(e);
            if (e is Combatant c) c.MoveTo(eRally + new Vector3(-i * 1.5f, 0, 0));
        }
        for (int i = 0; i < heavy; i++)
        {
            var pos = _terrain.SnapToGround(eOrigin + new Vector3(-4f - i * 2.5f, 0, 2f));
            var e = UnitFactory.CreateInfantry(cfg.EnemyHeavyWeapon, false, pos);
            AddChild(e);
            if (e is Combatant c) c.MoveTo(eRally + new Vector3(-i * 2f, 0, 2f));
        }

        GD.Print($"[MainScene] Forces spawned — friendly={friendlyUnits.Count} enemy={cfg.EnemyCount}");
        RegisterSensors(friendlyUnits);
    }

    public Unit? SpawnReinforcement(bool tank)
    {
        float half = _mapConfig.Size * 0.5f;
        var pos = _terrain.SnapToGround(new Vector3(-half + 10f + GD.Randf() * 4f, 0, -half + 12f));
        Unit u = tank
            ? UnitFactory.CreateVehicle(_forceCfg.FriendlyTankWeapon, true, pos)
            : UnitFactory.CreateInfantry(_forceCfg.FriendlyInfantryWeapon, true, pos);
        AddChild(u);
        if (u is Combatant c)
        {
            c.MoveTo(_terrain.SnapToGround(new Vector3(-half * 0.3f, 0, -half * 0.3f)));
            _sensorModel.ObserverSensors[(int)u.GetInstanceId()] = new[]
            {
                SensorModel.DefaultVisual(),
                SensorModel.DefaultRadar()
            };
        }
        return u;
    }

    private void RegisterSensors(List<Unit> friendlies)
    {
        foreach (var u in friendlies)
        {
            if (u is not Combatant) continue;
            _sensorModel.ObserverSensors[(int)u.GetInstanceId()] = new[]
            {
                SensorModel.DefaultVisual(),
                SensorModel.DefaultRadar()
            };
        }
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey k && k.Pressed && k.Keycode == Key.Escape)
        {
            if (GameManager.Instance.State == GameState.Battle)
                _pauseMenu.Toggle();
        }
    }
}
