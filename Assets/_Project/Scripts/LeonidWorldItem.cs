using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class LeonidWorldItem : MonoBehaviour
    {
        public enum ItemKind { BrokenCane, CarKeys, Trunk }
        [SerializeField] private LeonidBelongings belongings;
        [SerializeField] private ItemKind kind;
        public ItemKind Kind => kind;
        public string Prompt => kind == ItemKind.BrokenCane ? "E — осмотреть сломанную трость"
            : kind == ItemKind.CarKeys ? "E — взять ключи от синей машины"
            : belongings.IsTrunkOpen ? "E — закрыть багажник"
            : belongings.HasCarKeys ? "E — открыть багажник" : "E — багажник заперт · нужны ключи от машины";
        public void Configure(LeonidBelongings task, ItemKind item) { belongings = task; kind = item; }
        public void Interact()
        {
            if (kind == ItemKind.BrokenCane) belongings.InspectBrokenCane();
            else if (kind == ItemKind.CarKeys) belongings.TakeKeys();
            else belongings.ToggleTrunk();
        }
    }
}
