using UnityEngine;

// Clock setting, as the Seek endpoint takes it: initial minutes + increment seconds
[System.Serializable]
public struct TimeControl
{
    [Tooltip("Initial clock in minutes. The API accepts 0-180.")]
    public float minutes;

    [Tooltip("Increment in seconds. The API accepts 0-180.")]
    public int increment;

    public TimeControl(float minutes, int increment)
    {
        this.minutes = minutes;
        this.increment = increment;
    }

    public float InitialSeconds => minutes * 60f;

    // Lichess's estimate of how long the game will take/basis for which speed it falls under
    public float EstimatedSeconds => LichessSpeed.EstimatedSeconds(InitialSeconds, increment);

    public string Speed => LichessSpeed.FromClock(InitialSeconds, increment);

    // Board API accepts Rapid, Classical and Correspondence only
    //
    // Blitz is allowed for direct challenges, games vs AI, and bulk pairing, so this
    // gate should only be used for Seek
    public bool IsBoardApiEligible => EstimatedSeconds >= LichessSpeed.RapidFloorSeconds;

    // "10+0", "15+10", etc.
    public string Label => minutes.ToString("0.##") + "+" + increment;
}