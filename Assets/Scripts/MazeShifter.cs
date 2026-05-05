using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the periodic wall-shift mechanic.
/// Every ShiftInterval seconds a random fraction of ShiftingWalls slide to their
/// alternate positions, changing the maze layout — just like in the film.
///
/// Attach to the MazeManager GameObject (created automatically by MazeManager).
/// Listens to MazeGenerator.OnMazeReady to refresh the wall list after each generation.
/// </summary>
public class MazeShifter : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds between each maze shift cycle.")]
    public float ShiftInterval = 30f;

    [Header("Fraction of walls that move each cycle")]
    [Range(0.1f, 1f)]
    [Tooltip("0.3 = 30 % of all shifting walls slide on each cycle.")]
    public float ShiftFraction = 0.3f;

    // -----------------------------------------------------------------
    private readonly List<ShiftingWall> _walls = new();
    private float _timer;

    // =================================================================
    void OnEnable()  => MazeGenerator.OnMazeReady += OnMazeReady;
    void OnDisable() => MazeGenerator.OnMazeReady -= OnMazeReady;

    void OnMazeReady()
    {
        _walls.Clear();
        _walls.AddRange(FindObjectsByType<ShiftingWall>(FindObjectsInactive.Exclude));
        _timer = ShiftInterval;   // reset timer on every new maze
        Debug.Log($"[MazeShifter] registered {_walls.Count} shifting walls. Interval={ShiftInterval}s");
    }

    // =================================================================
    void Update()
    {
        if (_walls.Count == 0) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = ShiftInterval;
        TriggerShift();
    }

    // =================================================================
    private void TriggerShift()
    {
        int count = Mathf.Max(1, Mathf.RoundToInt(_walls.Count * ShiftFraction));

        // Fisher-Yates shuffle on a copy so order changes each cycle
        var pool = new List<ShiftingWall>(_walls);
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        for (int i = 0; i < count; i++)
            pool[i].ShiftToOther();

        Debug.Log($"[MazeShifter] shifted {count} walls.");
    }

    // =================================================================
    /// <summary>Lets GameManager override interval at runtime (per difficulty).</summary>
    public void SetInterval(float seconds)
    {
        ShiftInterval = seconds;
        _timer = Mathf.Min(_timer, seconds);   // don't wait longer than new interval
    }
}
