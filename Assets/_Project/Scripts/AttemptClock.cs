using System;

namespace Motel
{
    // Pure attempt state. The scene owns effects and reload; this latch accepts one accident.
    public sealed class AttemptClock
    {
        public const double Duration = 360;
        public double Elapsed { get; private set; }
        public bool AccidentRequested { get; private set; }
        public bool Expired => Elapsed >= Duration;
        public string DisplayTime => TimeSpan.FromSeconds(20 * 3600 + 16 * 60 + (int)Elapsed).ToString(@"hh\:mm\:ss");

        public void Advance(double realSeconds, bool paused, bool debugSixtySeconds)
        {
            if (paused || AccidentRequested || realSeconds <= 0 || double.IsNaN(realSeconds) || double.IsInfinity(realSeconds)) return;
            Elapsed = Math.Min(Duration, Elapsed + realSeconds * (debugSixtySeconds ? 6 : 1));
        }

        public bool TryRequestAccident()
        {
            if (AccidentRequested) return false;
            AccidentRequested = true;
            return true;
        }
    }
}
