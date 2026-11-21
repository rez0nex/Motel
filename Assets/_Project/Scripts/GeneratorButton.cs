using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class GeneratorButton : MonoBehaviour
    {
        public enum Command { Start, Restart }
        [SerializeField] private ModuleBypassPanel panel;
        [SerializeField] private Command command;
        public Command Action => command;
        public string Prompt => command == Command.Start ? "E — Пуск" : "E — Перезапуск";
        public void Configure(ModuleBypassPanel owner, Command action) { panel = owner; command = action; }
        public void Interact()
        { if (command == Command.Start) panel.StartGenerator(); else panel.RestartGenerator(); }
    }
}
