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

    // Bucket boundaries, in estimated seconds
    public const float BulletFloorSeconds = 30f;
    public const float BlitzFloorSeconds = 180f;
    public const float RapidFloorSeconds = 480f;
    public const float ClassicalFloorSeconds = 1500f;

    // Lichess estimates a game's duration as (initial + 40 * increment) seconds
    public static float EstimatedSeconds(float initialSeconds, float incrementSeconds) =>
        initialSeconds + 40f * incrementSeconds;

    // Buckets a clock into a speed
    // Exists only for the IDLE state, where no game exists yet
    public static string FromClock(float initialSeconds, float incrementSeconds)
    {
        float estimatedSeconds = EstimatedSeconds(initialSeconds, incrementSeconds);

        if (estimatedSeconds < BulletFloorSeconds) return UltraBullet;
        if (estimatedSeconds < BlitzFloorSeconds) return Bullet;
        if (estimatedSeconds < RapidFloorSeconds) return Blitz;
        if (estimatedSeconds < ClassicalFloorSeconds) return Rapid;
        return Classical;
    }
}