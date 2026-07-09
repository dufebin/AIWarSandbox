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
    private EnemyConfig _enemyCfg = EnemyConfig.CreateDefault();
    private SensorModel _sensorModel = new();
    private MapSource? _realMapSource;
    private ImportedTerrainGenerator? _importedTerrain;

    public override void _Ready()
    {
        AddChild(_terrain);
        var config = MapConfig.CreateFirstMap();
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

        EventBus.Instance.ConfigSubmitted += OnConfigSubmitted;
        GameManager.Instance.TransitionTo(GameState.Briefing);
        ShowBriefing();

        GD.Print($"[MainScene] Ready — terrain size={config.Size} seed={config.Seed} realMap={_realMapSource != null}");
    }

    /// <summary>Enable real-map (Gaode satellite + Terrarium elevation) generation. Call before adding to the tree.</summary>
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
    }

    private void ShowBriefing()
    {
        var briefing = new BriefingUI();
        AddChild(briefing);
    }

    private void OnConfigSubmitted(EnemyConfig cfg)
    {
        _enemyCfg = cfg;
        SpawnForces(cfg);
        var plans = TacticalAIManager.Instance.GeneratePlans(cfg);
        _planSelector.ShowPlans(plans);
        GameManager.Instance.TransitionTo(GameState.Briefing);
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
        var friendlyBase = UnitFactory.CreateStructure(StructureKind.Base, true,
            _terrain.SnapToGround(new Vector3(-half + 6f, 0, -half + 6f)));
        AddChild(friendlyBase);

        var enemyBase = UnitFactory.CreateStructure(StructureKind.Base, false,
            _terrain.SnapToGround(new Vector3(half - 6f, 0, half - 6f)));
        AddChild(enemyBase);
    }

    private void SpawnForces(EnemyConfig cfg)
    {
        float half = 64f * 0.5f;
        var friendlyUnits = new List<Unit>();

        for (int i = 0; i < 6; i++)
        {
            var u = UnitFactory.CreateInfantry(WeaponType.Rifle, true,
                _terrain.SnapToGround(new Vector3(-half + 10f + i * 1.5f, 0, -half + 12f)));
            AddChild(u);
            friendlyUnits.Add(u);
        }
        for (int i = 0; i < 2; i++)
        {
            var v = UnitFactory.CreateVehicle(WeaponType.Cannon, true,
                _terrain.SnapToGround(new Vector3(-half + 14f + i * 3f, 0, -half + 10f)));
            AddChild(v);
            friendlyUnits.Add(v);
        }

        int heavy = cfg.EnemyCount * cfg.HeavyRatio / 100;
        int light = cfg.EnemyCount - heavy;
        for (int i = 0; i < light; i++)
        {
            var e = UnitFactory.CreateInfantry(cfg.EnemyPrimaryWeapon, false,
                _terrain.SnapToGround(new Vector3(half - 10f - i * 1.5f, 0, half - 12f)));
            AddChild(e);
        }
        for (int i = 0; i < heavy; i++)
        {
            var e = UnitFactory.CreateInfantry(cfg.EnemyHeavyWeapon, false,
                _terrain.SnapToGround(new Vector3(half - 14f - i * 2f, 0, half - 10f)));
            AddChild(e);
        }

        GD.Print($"[MainScene] Forces spawned — friendly={friendlyUnits.Count} enemy={cfg.EnemyCount}");

        RegisterSensors(friendlyUnits);
    }

    private void RegisterSensors(List<Unit> friendlies)
    {
        foreach (var u in friendlies)
        {
            if (u is not Combatant) continue;
            int id = (int)u.GetInstanceId();
            _sensorModel.ObserverSensors[id] = new[]
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
                TacticalAIManager.Instance.Halt();
        }
    }
}
