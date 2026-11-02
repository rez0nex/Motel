using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class RoomKeysPickup : MonoBehaviour
    {
        [SerializeField] private FamilyEpisode family;
        public string Prompt => "E — взять дубликат ключей 7/8";
        public void Configure(FamilyEpisode episode) => family = episode;
        public void Interact() { if (family.CanInteract && !family.HasKeys) family.GiveKeys(); }
    }
}
