using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class CanePickup : MonoBehaviour
    {
        [SerializeField] private LeonidEvacuee owner;
        [SerializeField] private LeonidBelongings belongings;
        public string Prompt => "E — взять запасную складную трость";
        public void Configure(LeonidEvacuee leonid, LeonidBelongings supplies) { owner = leonid; belongings = supplies; }
        public void Interact()
        {
            if (owner != null && belongings != null) belongings.TakeSpare();
        }
    }
}
