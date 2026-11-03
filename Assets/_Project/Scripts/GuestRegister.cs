using UnityEngine;

namespace Motel
{
    // Occupancy belongs to this scene/attempt, never to ObservationStore.
    [DisallowMultipleComponent]
    public sealed class GuestRegister : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        public bool VadimCheckedIn { get; private set; }
        public int EntryCount => VadimCheckedIn ? 3 : 2;
        public string Entries => "Принято от предыдущего сменщика:\n№4 — Леонид · 1 гость\n№7 — Марина · 2 гостя" +
            (VadimCheckedIn ? "\nТекущая смена: №2 — Вадим · 1 гость · заселён, ключ выдан" : "");
        public string Prompt => "E — журнал заселения";
        public void Configure(MotelLoopController controller, DialogueController window) { loop = controller; dialogue = window; }
        private void Awake() => VadimCheckedIn = false;
        public bool HasPaidBooking(string guest) => guest == "Вадим";
        public bool CheckInVadim()
        {
            if (VadimCheckedIn || loop.Transitioning) return false;
            VadimCheckedIn = true;
            return true;
        }
        public void Interact()
        {
            if (loop.IsPaused || loop.Transitioning || dialogue.IsOpen) return;
            dialogue.ShowFor(this, "Журнал заселения", Entries,
                new DialogueController.Choice("Закрыть журнал.", dialogue.Close));
        }
    }
}
