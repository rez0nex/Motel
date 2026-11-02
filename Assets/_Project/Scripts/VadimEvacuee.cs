using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class VadimEvacuee : MonoBehaviour
    {
        public enum Stage { Skeptical, NeedsEvidence, Evacuating, Safe }
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private Vector3[] route;
        [SerializeField] private Transform safePoint;
        [SerializeField] private PrototypeDoor entranceDoor, mainExit;
        [SerializeField, Min(0.1f)] private float speed = 1.7f;
        private EvacuationMotor motor;
        public Stage CurrentStage { get; private set; }
        public int Waypoint => motor?.Waypoint ?? 0;
        public bool WaitingForPath => motor != null && motor.WaitingForPath;
        public string BlockingObject => motor?.BlockingObject;
        public string Prompt => "E — поговорить с Вадимом";

        public void Configure(MotelLoopController controller, DialogueController window, Vector3[] path,
            Transform destination, PrototypeDoor entrance, PrototypeDoor exit)
        {
            loop = controller; dialogue = window; route = path; safePoint = destination;
            entranceDoor = entrance; mainExit = exit;
        }

        private void Awake()
        {
            CurrentStage = Stage.Skeptical;
            motor = new EvacuationMotor(GetComponent<CharacterController>(), loop, dialogue, this, route, safePoint,
                speed, new EvacuationMotor.Gate(4, entranceDoor), new EvacuationMotor.Gate(8, mainExit));
        }

        public void Interact()
        {
            if (loop.IsPaused || loop.Transitioning || dialogue.IsOpen) return;
            if (CurrentStage == Stage.Evacuating || CurrentStage == Stage.Safe)
            {
                dialogue.ShowFor(this, "Вадим · номер 2", CurrentStage == Stage.Safe
                        ? "Я здесь, на парковке. Спасибо, что объяснили, в чём опасность."
                        : WaitingForPath ? "Не могу пройти. Откройте дверь или освободите проход, я выйду сам."
                        : "Иду через ресепшен к главному входу. Дальше — к месту сбора на парковке.",
                    new DialogueController.Choice("Хорошо.", dialogue.Close));
                return;
            }
            if (loop.EquipmentInspectedThisCycle)
                dialogue.ShowFor(this, "Вадим · номер 2", CurrentStage == Stage.NeedsEvidence
                        ? "Удалось выяснить, что случилось? Мне нужен конкретный довод."
                        : "Я с дороги, только устроился. Слышал гул, но хочу отдохнуть. Что случилось?",
                    new DialogueController.Choice("Диагностика показывает критический перегрев. Главный выход через ресепшен ведёт на парковку.", Agree),
                    new DialogueController.Choice("Я сейчас вернусь.", dialogue.Close));
            else if (CurrentStage == Stage.NeedsEvidence) AskForEvidence();
            else
                dialogue.ShowFor(this, "Вадим · номер 2", "Я с дороги, только устроился. Слышал гул, но хочу отдохнуть. Что случилось?",
                    new DialogueController.Choice("Возможна авария. Нужно выйти на парковку.", AskForEvidence),
                    new DialogueController.Choice("Я проверю, что происходит.", AskForEvidence));
        }

        private void AskForEvidence()
        {
            CurrentStage = Stage.NeedsEvidence;
            dialogue.ShowFor(this, "Вадим · номер 2",
                "Один гул ещё не доказывает опасность. Проверьте показания в технической: там есть панель диагностики. Если перегрев критический — скажите прямо, я выйду сам.",
                new DialogueController.Choice("Проверю диагностику в технической.", dialogue.Close));
        }

        private void Agree()
        {
            if (!loop.EquipmentInspectedThisCycle || CurrentStage == Stage.Safe || CurrentStage == Stage.Evacuating) return;
            CurrentStage = Stage.Evacuating;
            motor.Begin();
            dialogue.Close();
        }

        private void Update()
        {
            if (motor.Tick())
            {
                CurrentStage = Stage.Safe;
                loop.RememberVadimRescue();
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit) => motor?.ReportCollision(hit);
    }
}
