using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class EmergencyCutoff : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        public string Prompt => "E — аварийное отключение";
        public void Configure(MotelLoopController owner) => loop = owner;
        public void Interact()
        {
            if (loop != null) loop.RequestAccident(true);
        }
    }
}
