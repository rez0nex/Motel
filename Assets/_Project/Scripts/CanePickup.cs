using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class CanePickup : MonoBehaviour
    {
        [SerializeField] private LeonidEvacuee owner;
        public string Prompt => "E — взять трость Леонида";
        public void Configure(LeonidEvacuee leonid) => owner = leonid;
        public void Interact()
        {
            if (owner != null && owner.TakeCane()) gameObject.SetActive(false);
        }
    }
}
