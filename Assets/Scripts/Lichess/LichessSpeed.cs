// Lichess "speed" (perf) keys and the clock-to-speed classification
//
// These match the Lichess API's Speed enum and are the keys used inside 
// an account's 'perfs' object, so they double as rating lookup keys
public static class LichessSpeed
{
    public const string UltraBullet = "ultraBullet";
    public const string Bullet = "bullet";
    public const string Blitz = "blitz";
    public const string Rapid = "rapid";
    public const string Classical = "classical";
    public const string Correspondence = "correspondence";

    // Estimates a game's duration as (initial + 40 * increment) seconds and
    // buckets that into a speed
    // Exists only for the IDLE state, where no game exists yet
    public static string FromClock(float initialSeconds, float incrementSeconds)
    {
        float estimatedSeconds = initialSeconds + 40f * incrementSeconds;

        if (estimatedSeconds < 30f) return UltraBullet;
        if (estimatedSeconds < 180f) return Bullet;
        if (estimatedSeconds < 480f) return Blitz;
        if (estimatedSeconds < 1500f) return Rapid;
        return Classical;
    }
}