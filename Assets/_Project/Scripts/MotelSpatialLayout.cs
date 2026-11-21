using UnityEngine;

namespace Motel
{
    // Scene-owned coordinates keep older preview scenes usable with their original layout.
    [DisallowMultipleComponent]
    public sealed class MotelSpatialLayout : MonoBehaviour
    {
        [SerializeField] private float residentialOffsetZ;
        public float ResidentialOffsetZ => residentialOffsetZ;
        public void Configure(float offset) => residentialOffsetZ = offset;
        public static float OffsetFor(Component owner)
        {
            foreach (var layout in FindObjectsByType<MotelSpatialLayout>(FindObjectsInactive.Include))
                if (layout.gameObject.scene == owner.gameObject.scene) return layout.ResidentialOffsetZ;
            return 0;
        }
    }
}
