using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class EmergencyCutoff : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private TechnicalHatch cover;
        public TechnicalHatch Cover => cover;
        public string Prompt => cover != null && !cover.IsFullyOpen ? cover.IsOpen ? "Крышка открывается…" : "E — открыть крышку аварийного отключения" : "E — аварийное отключение";
        public void Configure(MotelLoopController owner) => loop = owner;
        public void ConfigureCover(TechnicalHatch shield) => cover = shield;
        public void Interact()
        {
            if (loop == null || loop.IsPaused || loop.SuppressInteractionUI) return;
            if (cover != null && !cover.IsFullyOpen)
            { if (!cover.IsOpen) cover.Interact(); return; }
            loop.RequestAccident(true);
        }
    }
}
