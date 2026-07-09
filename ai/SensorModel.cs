using System.Collections.Generic;
using Godot;
using AIWarSandbox.Autoloads;
using AIWarSandbox.Units;

namespace AIWarSandbox.Ai;

/// <summary>
/// Maven-style sensor-fusion node. Each friendly observer carries one or more
/// <see cref="Sensor"/>s; this module ticks them against the ground-truth
/// <see cref="UnitRegistry"/> and writes imperfect <see cref="IntelligenceRegistry.Track"/>s.
/// </summary>
public partial class SensorModel : Node
{
    public enum SensorKind { Visual, Radar, Satellite }

    public class Sensor
    {
        public SensorKind Kind;
        public float Range;
        public float FieldOfView;        // degrees, 360 = omni
        public float UpdateInterval;     // seconds between sweeps
        public float ClassificationAccuracy; // 0..1

        // Per-observer cooldown accumulator keyed externally by the caller via
        // ObserverSensors; we keep a simple elapsed timer here for convenience.
        [System.NonSerialized] public float TimeSinceLastSweep;
    }

    /// <summary>
    /// Friendly observer instance id -> sensor loadout. Populated by MainScene /
    /// OrderExecutor from each friendly Combatant. We cannot attach a Sensor[] to
    /// Combatant directly without editing that file (see EDIT SPEC).
    /// </summary>
    public Dictionary<int, Sensor[]> ObserverSensors { get; } = new();

    public static Sensor DefaultVisual() => new()
    {
        Kind = SensorKind.Visual,
        Range = 35f,
        FieldOfView = 90f,
        UpdateInterval = 0.5f,
        ClassificationAccuracy = 0.85f
    };

    public static Sensor DefaultRadar() => new()
    {
        Kind = SensorKind.Radar,
        Range = 80f,
        FieldOfView = 360f,
        UpdateInterval = 1.5f,
        ClassificationAccuracy = 0.5f
    };

    public static Sensor DefaultSatellite() => new()
    {
        Kind = SensorKind.Satellite,
        Range = 200f,
        FieldOfView = 360f,
        UpdateInterval = 5f,
        ClassificationAccuracy = 0.4f
    };

    // Maps a real enemy unit instance id -> assigned track id, so repeated sweeps
    // refresh the same track instead of spawning duplicates.
    private readonly Dictionary<int, int> _unitToTrack = new();

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        foreach (var kvp in ObserverSensors)
        {
            foreach (var s in kvp.Value)
                s.TimeSinceLastSweep += dt;
        }
    }

    /// <summary>
    /// Run one sensor sweep for the given observers against all units. For each
    /// observer, each of its sensors that has matured past its UpdateInterval fires
    /// against every enemy unit within range + FOV (LOS check deferred per spec).
    /// Detected enemies are written as tracks into <see cref="IntelligenceRegistry"/>.
    /// </summary>
    public void Tick(IReadOnlyList<Unit> observers, IReadOnlyList<Unit> allUnits)
    {
        var intel = IntelligenceRegistry.Instance;
        if (intel == null) return;

        foreach (var observer in observers)
        {
            if (observer == null || observer.State == UnitState.Dead) continue;
            if (!observer.IsFriendly) continue; // only friendlies observe
            int observerId = (int)observer.GetInstanceId();
            if (!ObserverSensors.TryGetValue(observerId, out var sensors)) continue;

            Vector3 obsPos = observer.GlobalPosition;

            foreach (var sensor in sensors)
            {
                if (sensor.TimeSinceLastSweep < sensor.UpdateInterval) continue;
                sensor.TimeSinceLastSweep = 0f;

                float rangeSqr = sensor.Range * sensor.Range;
                float cosHalfFov = Mathf.Cos(Mathf.DegToRad(sensor.FieldOfView * 0.5f));
                bool omni = sensor.FieldOfView >= 360f;

                // Visual sensors need a facing; if the observer is a Combatant use its
                // velocity, else assume forward = -Z (Godot default).
                Vector3 facing = (observer is Combatant c && c.Velocity.LengthSquared() > 1e-3f)
                    ? c.Velocity.Normalized()
                    : Vector3.Forward;

                foreach (var target in allUnits)
                {
                    if (target == null || target == observer) continue;
                    if (target.State == UnitState.Dead) continue;
                    if (target.IsFriendly == observer.IsFriendly) continue; // only enemies

                    Vector3 to = target.GlobalPosition - obsPos;
                    float distSqr = to.LengthSquared();
                    if (distSqr > rangeSqr) continue;

                    if (!omni)
                    {
                        Vector3 dir = to.Normalized();
                        if (dir.Dot(facing) < cosHalfFov) continue;
                    }

                    // Confidence: visual gets full ClassificationAccuracy (positive ID),
                    // radar/satellite get reduced confidence (lower fidelity).
                    float confidence;
                    bool positiveId;
                    switch (sensor.Kind)
                    {
                        case SensorKind.Visual:
                            confidence = sensor.ClassificationAccuracy;
                            positiveId = true;
                            break;
                        case SensorKind.Radar:
                            confidence = sensor.ClassificationAccuracy * 0.8f;
                            positiveId = false;
                            break;
                        default: // Satellite
                            confidence = sensor.ClassificationAccuracy * 0.6f;
                            positiveId = false;
                            break;
                    }

                    string classification = Classify(target, sensor, positiveId);

                    int targetUnitId = (int)target.GetInstanceId();
                    if (!_unitToTrack.TryGetValue(targetUnitId, out int trackId))
                    {
                        trackId = intel.AllocateTrackId();
                        _unitToTrack[targetUnitId] = trackId;
                    }

                    var track = new IntelligenceRegistry.Track
                    {
                        TrackId = trackId,
                        LastKnownPosition = target.GlobalPosition,
                        Classification = classification,
                        Confidence = confidence,
                        LastSeenAge = 0f,
                        IsFriendly = false,
                        LinkedUnitId = positiveId ? targetUnitId : null
                    };

                    // UpdateTrack resets LastSeenAge and raises TrackUpdated. We pass
                    // the richer Track object via a small helper to preserve LinkedUnitId.
                    ApplyTrack(intel, track);
                }
            }
        }
    }

    private static void ApplyTrack(IntelligenceRegistry intel, IntelligenceRegistry.Track track)
    {
        // UpdateTrack handles position/class/confidence + age reset + event. We then
        // patch LinkedUnitId / IsFriendly which UpdateTrack does not set.
        intel.UpdateTrack(track.TrackId, track.LastKnownPosition, track.Classification, track.Confidence);
        if (intel.AllTracks.TryGetValue(track.TrackId, out var stored))
        {
            stored.IsFriendly = track.IsFriendly;
            stored.LinkedUnitId = track.LinkedUnitId;
        }
    }

    private static string Classify(Unit target, Sensor sensor, bool positiveId)
    {
        if (!positiveId)
        {
            // Radar/satellite cannot reliably distinguish subtypes.
            if (target is Structure) return "structure";
            return "unknown";
        }

        // Visual with high accuracy -> precise classification, else fall back.
        bool accurate = sensor.ClassificationAccuracy >= 0.7f;
        if (target is Infantry) return accurate ? "infantry" : "unknown";
        if (target is Vehicle) return accurate ? "vehicle" : "unknown";
        if (target is Structure) return "structure";
        return "unknown";
    }

    /// <summary>
    /// Forget the track mapping for a unit that has died, so its track id can be
    /// recycled. Callers (e.g. on UnitDied) should invoke this then let the track
    /// age out naturally in IntelligenceRegistry._Process.
    /// </summary>
    public void ForgetUnit(int unitInstanceId) => _unitToTrack.Remove(unitInstanceId);
}
