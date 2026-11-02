using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class RoomEightLock : MonoBehaviour
    {
        [SerializeField] private FamilyEpisode family;
        [SerializeField] private Bounds inside;
        public bool CanOpen => family.HasKeys || inside.Contains(family.Player.position);
        public void Configure(FamilyEpisode episode, Bounds interior) { family = episode; inside = interior; }
    }
}
