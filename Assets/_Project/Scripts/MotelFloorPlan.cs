using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class MotelFloorPlan : MonoBehaviour
    {
        [SerializeField] private DialogueController dialogue;
        public string Prompt => "E — рассмотреть план номеров 7 и 8";
        public void Configure(DialogueController window) => dialogue = window;
        public void Interact()
        {
            if (dialogue == null) return;
            dialogue.ShowFor(this, "План мотеля · номера 7 и 8",
                "Коридор проходит перед номерами 7 и 8. Между жилыми частями отмечена соединительная дверь. Со стороны №7 перед ней стоит небольшой шкаф: его можно сдвинуть вдоль стены. Основной вход в №8 обозначен «Подсобка».",
                new DialogueController.Choice("Отойти от плана.", dialogue.Close));
        }
    }
}
