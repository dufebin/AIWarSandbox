using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;
using Godot;

namespace AIWarSandbox.Ai;

/// <summary>
/// Monte Carlo plan evaluator. Runs lightweight stochastic simulations of a
/// <see cref="Plan"/> to produce an <see cref="OutcomeDistribution"/> (Palantir
/// Maven what-if style). Pure numerical: no Godot nodes spawned, no rendering.
/// </summary>
public partial class PlanSimulator : Node
{
    private const float TimeStep = 0.5f;          // seconds per sim tick
    private const float TimeoutSec = 120f;        // simulation horizon
    private const int HistogramBuckets = 10;
    private const double WallBudgetMs = 500.0;

    /// <summary>Aggregated outcome of Monte Carlo simulation of a Plan.</summary>
    public class OutcomeDistribution
    {
        public int Simulations;
        public float MeanCasualties;
        public float StdCasualties;
        public float SuccessRate;
        public float MeanDurationSec;
        public float[] CasualtyHistogram = new float[HistogramBuckets];
        public float MinCasualties;
        public float MaxCasualties;
    }

    // Reusable scratch arrays (avoid per-sim allocation in hot loop).
    private float[]? _fHp;
    private float[]? _eHp;
    private int[] _fTarget = Array.Empty<int>();
    private int[] _eTarget = Array.Empty<int>();

    public OutcomeDistribution Simulate(Plan plan, int simulations = 50, int? seed = null)
    {
        var rng = seed.HasValue ? new Random(seed.Value) : new Random();
        var dist = new OutcomeDistribution { Simulations = simulations };

        // --- Snapshot forces from UnitRegistry (one-time, not per sim) ---
        var friendlySnapshot = SnapshotForces(plan, isFriendly: true);
        var enemySnapshot = SnapshotForces(plan, isFriendly: false);

        if (friendlySnapshot.Count == 0 || enemySnapshot.Count == 0)
        {
            // Degenerate: no battle to simulate.
            return dist;
        }

        int fN = friendlySnapshot.Count;
        int eN = enemySnapshot.Count;

        // Pre-size scratch arrays.
        if (_fHp == null || _fHp.Length < fN) { _fHp = new float[fN]; _fTarget = new int[fN]; }
        if (_eHp == null || _eHp.Length < eN) { _eHp = new float[eN]; _eTarget = new int[eN]; }
        Array.Fill(_fTarget, -1);
        Array.Fill(_eTarget, -1);

        var sw = Stopwatch.StartNew();

        int successes = 0;
        int completedSims = 0;
        double sumCas = 0.0, sumCas2 = 0.0;
        double sumDur = 0.0;
        float minCas = float.MaxValue, maxCas = float.MinValue;
        var histogram = new int[HistogramBuckets];

        for (int s = 0; s < simulations; s++)
        {
            // Wall-clock budget guard: stop early if over ~500ms.
            if ((s & 0xF) == 0 && sw.Elapsed.TotalMilliseconds > WallBudgetMs)
            {
                dist.Simulations = completedSims; // note reduced count
                break;
            }

            var outcome = SimulateOne(plan, friendlySnapshot, enemySnapshot, fN, eN, rng);
            completedSims++;

            sumCas += outcome.Casualties;
            sumCas2 += outcome.Casualties * outcome.Casualties;
            sumDur += outcome.DurationSec;
            if (outcome.Casualties < minCas) minCas = outcome.Casualties;
            if (outcome.Casualties > maxCas) maxCas = outcome.Casualties;
            if (outcome.Success) successes++;

            int bucket = (int)MathF.Round(outcome.Casualties / MathF.Max(1f, fN) * (HistogramBuckets - 1));
            bucket = Math.Clamp(bucket, 0, HistogramBuckets - 1);
            histogram[bucket]++;
        }

        if (completedSims == 0)
        {
            dist.Simulations = 0;
            return dist;
        }

        float n = completedSims;
        dist.Simulations = completedSims;
        dist.MeanCasualties = (float)(sumCas / n);
        float variance = (float)((sumCas2 / n) - (sumCas / n) * (sumCas / n));
        dist.StdCasualties = variance > 0f ? MathF.Sqrt(variance) : 0f;
        dist.SuccessRate = successes / n;
        dist.MeanDurationSec = (float)(sumDur / n);
        dist.MinCasualties = minCas == float.MaxValue ? 0f : minCas;
        dist.MaxCasualties = maxCas == float.MinValue ? 0f : maxCas;
        for (int b = 0; b < HistogramBuckets; b++)
            dist.CasualtyHistogram[b] = histogram[b] / n;

        return dist;
    }

    // --- Snapshot model ---

    private struct UnitSnap
    {
        public Vector3 Position;
        public float Hp;
        public float MaxHp;
        public float Damage;
        public float Range;
        public float RoF;
        public float Armor;
        public bool IsBase;
    }

    private List<UnitSnap> SnapshotForces(Plan plan, bool isFriendly)
    {
        var snaps = new List<UnitSnap>();

        // Plan formations drive friendly composition; fall back to registry.
        if (isFriendly && plan.Formations != null && plan.Formations.Count > 0)
        {
            foreach (var fa in plan.Formations)
                foreach (var u in fa.Units)
                    AddSnap(snaps, u);
        }
        else
        {
            var src = isFriendly ? UnitRegistry.Instance.Friendly : UnitRegistry.Instance.Enemy;
            foreach (var u in src)
                if (u != null && u.State != UnitState.Dead)
                    AddSnap(snaps, u);
        }
        return snaps;
    }

    private static void AddSnap(List<UnitSnap> snaps, Unit u)
    {
        if (u == null || u.State == UnitState.Dead) return;
        Weapon? w = null;
        float armor = 0f;
        if (u is Combatant c)
        {
            w = c.Weapon;
            armor = c.Armor;
        }
        else if (u is Structure st)
        {
            w = st.DefenseWeapon;
        }

        snaps.Add(new UnitSnap
        {
            Position = u.GlobalPosition,
            Hp = u.Health,
            MaxHp = u.MaxHealth,
            Damage = w?.Damage ?? 0f,
            Range = w?.Range ?? 0f,
            RoF = w?.RateOfFire ?? 0f,
            Armor = armor,
            IsBase = u is Structure { Kind: StructureKind.Base }
        });
    }

    // --- One simulation ---

    private struct SimResult
    {
        public float Casualties;   // friendly units lost
        public float DurationSec;
        public bool Success;       // enemy base destroyed before friendly base / timeout
    }

    private SimResult SimulateOne(Plan plan, List<UnitSnap> friendly, List<UnitSnap> enemy,
                                  int fN, int eN, Random rng)
    {
        // Copy HP into scratch (positions stay constant in 1D abstract model).
        for (int i = 0; i < fN; i++) _fHp![i] = friendly[i].Hp;
        for (int j = 0; j < eN; j++) _eHp![j] = enemy[j].Hp;

        const float hitChanceBase = 0.7f;
        const float critChance = 0.1f;
        const float critMult = 2f;
        const float dmgVarianceLow = 0.5f;

        float elapsed = 0f;
        int friendlyBaseIdx = IndexOfBase(friendly);
        int enemyBaseIdx = IndexOfBase(enemy);

        while (elapsed < TimeoutSec)
        {
            elapsed += TimeStep;

            // Friendly fire phase.
            int fAlive = 0, eAlive = 0;
            for (int i = 0; i < fN; i++)
            {
                if (_fHp![i] <= 0f) continue;
                fAlive++;
                int tgt = NearestAlive(enemy, _eHp!, i);
                if (tgt < 0) continue;
                ref var fs = ref CollectionsMarshal.AsSpan(friendly)[i]; // safe: list not mutated
                ref var es = ref CollectionsMarshal.AsSpan(enemy)[tgt];
                float dist = fs.Position.DistanceSquaredTo(es.Position);
                if (dist > fs.Range * fs.Range) continue;
                if ((float)rng.NextDouble() > hitChanceBase) continue;
                float dmg = fs.Damage * fs.RoF * TimeStep * (dmgVarianceLow + (float)rng.NextDouble() * 0.5f);
                if ((float)rng.NextDouble() < critChance) dmg *= critMult;
                dmg = MathF.Max(0f, dmg - es.Armor);
                _eHp![tgt] -= dmg;
            }

            // Enemy fire phase.
            for (int j = 0; j < eN; j++)
            {
                if (_eHp![j] <= 0f) continue;
                eAlive++;
                int tgt = NearestAlive(friendly, _fHp!, j);
                if (tgt < 0) continue;
                ref var es = ref CollectionsMarshal.AsSpan(enemy)[j];
                ref var fs = ref CollectionsMarshal.AsSpan(friendly)[tgt];
                float dist = es.Position.DistanceSquaredTo(fs.Position);
                if (dist > es.Range * es.Range) continue;
                if ((float)rng.NextDouble() > hitChanceBase) continue;
                float dmg = es.Damage * es.RoF * TimeStep * (dmgVarianceLow + (float)rng.NextDouble() * 0.5f);
                if ((float)rng.NextDouble() < critChance) dmg *= critMult;
                dmg = MathF.Max(0f, dmg - fs.Armor);
                _fHp![tgt] -= dmg;
            }

            // Termination checks.
            if (enemyBaseIdx >= 0 && _eHp![enemyBaseIdx] <= 0f)
                return new SimResult { Casualties = CountDead(friendly, _fHp!, fN), DurationSec = elapsed, Success = true };
            if (friendlyBaseIdx >= 0 && _fHp![friendlyBaseIdx] <= 0f)
                return new SimResult { Casualties = CountDead(friendly, _fHp!, fN), DurationSec = elapsed, Success = false };
            if (eAlive == 0)
                return new SimResult { Casualties = CountDead(friendly, _fHp!, fN), DurationSec = elapsed, Success = true };
            if (fAlive == 0)
                return new SimResult { Casualties = fN, DurationSec = elapsed, Success = false };
        }

        // Timeout: failure (enemy base not destroyed).
        return new SimResult
        {
            Casualties = CountDead(friendly, _fHp!, fN),
            DurationSec = elapsed,
            Success = false
        };
    }

    private static int IndexOfBase(List<UnitSnap> forces)
    {
        for (int i = 0; i < forces.Count; i++)
            if (forces[i].IsBase) return i;
        // No explicit base: treat first unit as the "base" anchor.
        return forces.Count > 0 ? 0 : -1;
    }

    private static int NearestAlive(List<UnitSnap> forces, float[] hp, int fromIdx)
    {
        // fromIdx is into the *opposite* array semantically, but here `forces`
        // is the target array and fromIdx indexes the attacker array of equal
        // length callers pass. We just need nearest alive among `forces`.
        int best = -1;
        float bestDist = float.MaxValue;
        // Note: position of the attacker is unused for nearest selection in 1D
        // abstract model; we pick nearest-to-origin alive target as a proxy.
        for (int i = 0; i < forces.Count; i++)
        {
            if (hp[i] <= 0f) continue;
            float d = forces[i].Position.LengthSquared();
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private static float CountDead(List<UnitSnap> forces, float[] hp, int n)
    {
        int dead = 0;
        for (int i = 0; i < n; i++)
            if (hp[i] <= 0f) dead++;
        return dead;
    }
}
