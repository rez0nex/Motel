using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class ModuleBypassNote : MonoBehaviour
    {
        public const string Text = "Средний модуль сгорел. Пуск можно пустить в обход";
        [SerializeField] private DialogueController dialogue;
        [SerializeField, TextArea] private string content = Text;
        public string Prompt => "E — прочитать записку сменщика";
        public void Configure(DialogueController window) => dialogue = window;
        public void Configure(DialogueController window, string text) { dialogue = window; content = text; }
        public string Content => content;
        public void Interact()
        {
            if (dialogue == null || dialogue.IsOpen) return;
            dialogue.ShowFor(this, "Записка сменщика", content,
                new DialogueController.Choice("Закрыть записку.", dialogue.Close));
        }
    }
}
