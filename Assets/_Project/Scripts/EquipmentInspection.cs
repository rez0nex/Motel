using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class EquipmentInspection : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        public string Prompt => "E — проверить показания диагностики";
        public void Configure(MotelLoopController controller, DialogueController window) { loop = controller; dialogue = window; }
        public void Interact()
        {
            if (dialogue.IsOpen || loop.IsPaused || loop.Transitioning) return;
            if (dialogue.ShowFor(this, "Диагностика служебного агрегата",
                "КРИТИЧЕСКИЙ ПЕРЕГРЕВ. Повышенные вибрация и давление. Требуется вывести людей из здания.\nПоказания записаны в наблюдения — J после закрытия.",
                new DialogueController.Choice("Запомнить показания и закрыть.", dialogue.Close)))
                loop.RememberEquipmentOverheat();
        }
    }
}
