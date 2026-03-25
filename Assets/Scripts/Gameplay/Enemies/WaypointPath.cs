using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Defines a waypoint path that enemies follow. Place in the scene with child Transforms as waypoints.
/// Both server and client sample this path using normalized progress (0-1) to compute positions.
/// This is the shared data that ensures server and client agree on where an enemy is at any given progress.
/// </summary>
public class WaypointPath : MonoBehaviour
{
    [SerializeField, InfoBox("Add child Transforms as waypoints. Order = path order.")]
    private List<Transform> waypoints = new();

    private float[] _segmentStartProgress;
    private float _totalLength;
    private bool _isComputed;

    public float TotalLength
    {
        get
        {
            if (!_isComputed) ComputePath();
            return _totalLength;
        }
    }

    public int WaypointCount => waypoints.Count;

    private void Awake()
    {
        ComputePath();
    }

    private void ComputePath()
    {
        if (waypoints.Count < 2)
        {
            _totalLength = 0f;
            _isComputed = true;
            return;
        }

        _segmentStartProgress = new float[waypoints.Count];
        _totalLength = 0f;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            _totalLength += Vector3.Distance(
                waypoints[i].position,
                waypoints[i + 1].position
            );
        }

        float accumulated = 0f;
        _segmentStartProgress[0] = 0f;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            accumulated += Vector3.Distance(
                waypoints[i].position,
                waypoints[i + 1].position
            );
            _segmentStartProgress[i + 1] = accumulated / _totalLength;
        }

        _isComputed = true;
    }

    /// <summary>
    /// Samples the path at a normalized progress value (0 = start, 1 = end).
    /// Returns a world-space position in server coordinates.
    /// </summary>
    public Vector3 SamplePosition(float normalizedProgress)
    {
        if (!_isComputed) ComputePath();
        if (waypoints.Count == 0) return transform.position;
        if (waypoints.Count == 1) return waypoints[0].position;

        normalizedProgress = Mathf.Clamp01(normalizedProgress);

        if (normalizedProgress <= 0f) return waypoints[0].position;
        if (normalizedProgress >= 1f) return waypoints[^1].position;

        // Find which segment this progress falls into
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            float segStart = _segmentStartProgress[i];
            float segEnd = _segmentStartProgress[i + 1];

            if (normalizedProgress >= segStart && normalizedProgress <= segEnd)
            {
                float segmentT = (normalizedProgress - segStart) / (segEnd - segStart);
                return Vector3.Lerp(waypoints[i].position, waypoints[i + 1].position, segmentT);
            }
        }

        return waypoints[^1].position;
    }

    /// <summary>
    /// Returns the position of a specific waypoint by index (server coordinates).
    /// </summary>
    public Vector3 GetWaypointPosition(int index)
    {
        return waypoints[Mathf.Clamp(index, 0, waypoints.Count - 1)].position;
    }

#if UNITY_EDITOR
    [Button("Auto-Populate from Children")]
    private void AutoPopulateWaypoints()
    {
        waypoints.Clear();
        foreach (Transform child in transform)
            waypoints.Add(child);
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        Gizmos.color = Color.green;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawSphere(waypoints[i].position, 0.15f);
        }
    }
#endif
}
