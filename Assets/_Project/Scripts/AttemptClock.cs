using System;

namespace Motel
{
    // Pure attempt state. The scene owns effects and reload; this latch accepts one accident.
    public sealed class AttemptClock
    {
        public const double Duration = 360;
        public const double RescueDelay = 8;
        public double Elapsed { get; private set; }
        public bool AccidentRequested { get; private set; }
        public double? RescueAccidentAt { get; private set; }
        public bool RescueAccidentDue => RescueAccidentAt.HasValue && Elapsed >= RescueAccidentAt.Value;
        public bool Expired => Elapsed >= Duration;
        public string DisplayTime => TimeSpan.FromSeconds(20 * 3600 + 16 * 60 + (int)Elapsed).ToString(@"hh\:mm\:ss");

        public void Advance(double realSeconds, bool paused, bool debugSixtySeconds)
        {
            if (paused || AccidentRequested || realSeconds <= 0 || double.IsNaN(realSeconds) || double.IsInfinity(realSeconds)) return;
            double deadline = Math.Min(Duration, RescueAccidentAt ?? Duration);
            Elapsed = Math.Min(deadline, Elapsed + realSeconds * (debugSixtySeconds ? 6 : 1));
        }

        // One deadline on the same game clock: repeated arrivals cannot postpone it.
        public bool TryScheduleRescueAccident(bool allArrived)
        {
            if (!allArrived || AccidentRequested || RescueAccidentAt.HasValue) return false;
            RescueAccidentAt = Elapsed + RescueDelay;
            return true;
        }

        public bool TryRequestAccident()
        {
            if (AccidentRequested) return false;
            AccidentRequested = true;
            return true;
        }
    }
}
