using UnityEditor;

namespace Motel.Editor
{
    // Keep the old menu/API: the former room-at-t=0 fixture is superseded by actual arrival/check-in scenarios.
    // Joint physical routes are covered by MotelFamilyPlayChecks (all four), including Leonid.
    public static class MotelVadimPlayChecks
    {
        [MenuItem("Motel/Run Automated Joint Evacuation Checks")]
        public static void Begin() => MotelShiftPlayChecks.Begin();
    }
}
