using System.Collections.Generic;
using Godot;
using AIWarSandbox.Ai;

namespace AIWarSandbox.Autoloads;

/// <summary>
/// Sensor-fusion singleton: holds imperfect "tracks" of observed units rather than
/// ground truth. Tracks age out and lose confidence over time, mimicking fog of war.
/// Combatants should target tracks (via <see cref="NearestKnownEnemy"/>) instead of
/// the live <see cref="UnitRegistry"/> lists.
/// </summary>
public partial class IntelligenceRegistry : Node
{
    public static IntelligenceRegistry Instance { get; private set; } = null!;

    /// <summary>
    /// An imperfect observation of a unit. May be stale (LinkedUnitId null) once the
    /// contact is lost but the track has not yet decayed.
    /// </summary>
    public class Track
    {
        public int TrackId;
        public Vector3 LastKnownPosition;
        public float Confidence;          // 0..1
        public float LastSeenAge;         // seconds since last sensor refresh
        public string Classification = "unknown"; // infantry|vehicle|structure|unknown
        public bool IsFriendly;
        public int? LinkedUnitId;         // real unit GetInstanceId(), null if lost
    }

    private readonly Dictionary<int, Track> _tracks = new();
    private int _nextTrackId = 1;

    public IReadOnlyDictionary<int, Track> AllTracks => _tracks;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>
    /// Create or refresh a track. Resets <see cref="Track.LastSeenAge"/> on update.
    /// Returns the track id (assigned on creation, preserved on update).
    /// </summary>
    public void UpdateTrack(int trackId, Vector3 pos, string classification, float confidence)
    {
        if (_tracks.TryGetValue(trackId, out var existing))
        {
            existing.LastKnownPosition = pos;
            existing.Classification = classification;
            existing.Confidence = confidence;
            existing.LastSeenAge = 0f;
        }
        else
        {
            _tracks[trackId] = new Track
            {
                TrackId = trackId,
                LastKnownPosition = pos,
                Classification = classification,
                Confidence = confidence,
                LastSeenAge = 0f
            };
        }

        EventBus.Instance?.RaiseTrackUpdated(_tracks[trackId]);
    }

    /// <summary>
    /// Allocate a fresh track id. Callers that observe a brand-new contact should use
    /// this to get an id before calling <see cref="UpdateTrack"/>.
    /// </summary>
    public int AllocateTrackId() => _nextTrackId++;

    public void RemoveTrack(int trackId)
    {
        if (_tracks.Remove(trackId))
            EventBus.Instance?.RaiseTrackLost(trackId);
    }

    public void Clear()
    {
        var ids = new List<int>(_tracks.Keys);
        _tracks.Clear();
        foreach (var id in ids)
            EventBus.Instance?.RaiseTrackLost(id);
    }

    public override void _Process(double delta)
    {
        if (_tracks.Count == 0) return;
        float dt = (float)delta;

        // Iterate over a snapshot of keys so we can mutate the dictionary during loop.
        var stale = new List<int>();
        foreach (var kv in _tracks)
        {
            var t = kv.Value;
            t.LastSeenAge += dt;

            if (t.LastSeenAge > 30f)
            {
                t.Confidence -= dt * 0.1f;
                if (t.Confidence <= 0f)
                {
                    stale.Add(kv.Key);
                    continue;
                }
                EventBus.Instance?.RaiseTrackUpdated(t);
            }
        }

        foreach (var id in stale)
        {
            _tracks.Remove(id);
            EventBus.Instance?.RaiseTrackLost(id);
        }
    }

    /// <summary>
    /// Nearest non-friendly track with confidence above the actionable threshold.
    /// Returns null if no enemy is currently tracked.
    /// </summary>
    public Track? NearestKnownEnemy(Vector3 pos)
    {
        Track? best = null;
        float bestSqr = float.MaxValue;
        foreach (var kv in _tracks)
        {
            var t = kv.Value;
            if (t.IsFriendly) continue;
            if (t.Confidence <= 0.2f) continue;
            float sqr = pos.DistanceSquaredTo(t.LastKnownPosition);
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = t;
            }
        }
        return best;
    }
}
