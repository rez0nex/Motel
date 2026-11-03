using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class LeonidEvacuee : MonoBehaviour
    {
        public enum Stage { Sleeping, Awake, NeedsCane, Evacuating, Safe }

        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private GameObject sleepingModel, standingModel, heldCane;
        [SerializeField] private BoxCollider sleepingTarget;
        [SerializeField] private Vector3[] route;
        [SerializeField] private Transform safePoint;
        [SerializeField] private PrototypeDoor entranceDoor, emergencyExit;
        [SerializeField, Min(0.1f)] private float speed = 1.2f;
        private CharacterController character;
        private EvacuationMotor motor;

        public Stage CurrentStage { get; private set; } = Stage.Sleeping;
        public bool CarryingCane { get; private set; }
        public bool WaitingForPath => motor != null && motor.WaitingForPath;
        public int Waypoint => motor?.Waypoint ?? 0;
        public string BlockingObject => motor?.BlockingObject;
        public string Prompt => CurrentStage == Stage.Sleeping ? "E — разбудить Леонида" : "E — поговорить с Леонидом";

        public void Configure(MotelLoopController controller, DialogueController window, GameObject asleep,
            GameObject awake, GameObject cane, BoxCollider target, Vector3[] path, Transform destination,
            PrototypeDoor entrance, PrototypeDoor exit)
        {
            loop = controller; dialogue = window; sleepingModel = asleep; standingModel = awake;
            heldCane = cane; sleepingTarget = target; route = path; safePoint = destination;
            entranceDoor = entrance; emergencyExit = exit;
        }

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            motor = new EvacuationMotor(character, loop, dialogue, this, route, safePoint, speed,
                new EvacuationMotor.Gate(4, entranceDoor), new EvacuationMotor.Gate(6, emergencyExit));
            CurrentStage = Stage.Sleeping;
            CarryingCane = false;
            sleepingModel.SetActive(true);
            standingModel.SetActive(false);
            heldCane.SetActive(false);
            sleepingTarget.enabled = true;
            character.enabled = false;
        }

        public bool TakeCane()
        {
            if (loop.IsPaused || loop.Transitioning || dialogue.IsOpen || CarryingCane ||
                CurrentStage == Stage.Evacuating || CurrentStage == Stage.Safe) return false;
            CarryingCane = true;
            return true;
        }

        public void Interact()
        {
            if (loop.IsPaused || loop.Transitioning || dialogue.IsOpen) return;
            bool firstWake = CurrentStage == Stage.Sleeping;
            if (firstWake)
            {
                CurrentStage = Stage.Awake;
                sleepingTarget.enabled = false;
                sleepingModel.SetActive(false);
                standingModel.SetActive(true);
                character.enabled = true;
            }
            if (CurrentStage == Stage.Awake)
                dialogue.ShowFor(this, "Леонид · номер 4", firstWake
                        ? "А?.. Я задремал. Говорите поближе, я плохо слышу. Что случилось?"
                        : "Вы что-то хотели сказать? Говорите поближе.",
                    new DialogueController.Choice("В служебной части авария. Нужно выйти на парковку.", Warn),
                    new DialogueController.Choice("Я сейчас вернусь.", dialogue.Close));
            else if (CurrentStage == Stage.NeedsCane) AskForCane();
            else
                dialogue.ShowFor(this, "Леонид · номер 4", CurrentStage == Stage.Safe
                        ? "Спасибо, что помогли выбраться. Я подожду здесь."
                        : WaitingForPath ? "Здесь не пройти. Откройте, пожалуйста, дверь и освободите проход."
                        : "Иду к запасному выходу справа, потом к месту сбора. Не торопите, пожалуйста.",
                    new DialogueController.Choice("Хорошо.", dialogue.Close));
        }

        private void Warn()
        {
            CurrentStage = Stage.NeedsCane;
            AskForCane();
        }

        private void AskForCane()
        {
            if (CarryingCane)
                dialogue.ShowTopicFor(this, "Леонид", "Запасная трость", "Вы уже принесли запасную? Спасибо. С ней я смогу выйти.",
                    new DialogueController.Choice("Возьмите запасную трость. Я открою путь к парковке.", GiveCane),
                    new DialogueController.Choice("Сейчас подготовлю проход.", dialogue.Close));
            else
                dialogue.ShowTopicFor(this, "Леонид", "Трость в машине", LeonidBelongings.Directions,
                    new DialogueController.Choice("Сейчас принесу.", dialogue.Close));
        }

        private void GiveCane()
        {
            if (CurrentStage != Stage.NeedsCane || !CarryingCane) return;
            CarryingCane = false;
            heldCane.SetActive(true);
            CurrentStage = Stage.Evacuating;
            motor.Begin();
            dialogue.Close();
        }

        private void Update()
        {
            if (CurrentStage == Stage.Sleeping) return;
            if (motor.Tick())
            {
                CurrentStage = Stage.Safe;
                loop.RememberLeonidRescue();
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit) => motor?.ReportCollision(hit);
    }
}
