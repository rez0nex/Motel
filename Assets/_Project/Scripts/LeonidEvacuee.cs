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
        private int waypoint;
        private float verticalSpeed;
        private string lastBlocker;

        public Stage CurrentStage { get; private set; } = Stage.Sleeping;
        public bool CarryingCane { get; private set; }
        public bool WaitingForPath { get; private set; }
        public int Waypoint => waypoint;
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
                dialogue.Show("Леонид · номер 4", firstWake
                        ? "А?.. Я задремал. Говорите поближе, я плохо слышу. Что случилось?"
                        : "Вы что-то хотели сказать? Говорите поближе.",
                    new DialogueController.Choice("В служебной части авария. Нужно выйти на парковку.", Warn),
                    new DialogueController.Choice("Я сейчас вернусь.", dialogue.Close));
            else if (CurrentStage == Stage.NeedsCane) AskForCane();
            else
                dialogue.Show("Леонид · номер 4", CurrentStage == Stage.Safe
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
                dialogue.Show("Леонид · номер 4", "Понял, надо выходить. Без трости мне тяжело. Вы её уже нашли?",
                    new DialogueController.Choice("Вот ваша трость. Я открою путь к парковке.", GiveCane),
                    new DialogueController.Choice("Сейчас подготовлю проход.", dialogue.Close));
            else
                dialogue.Show("Леонид · номер 4", "Выйду с вами, только подайте мою трость: она возле кровати.",
                    new DialogueController.Choice("Сейчас принесу.", dialogue.Close));
        }

        private void GiveCane()
        {
            if (CurrentStage != Stage.NeedsCane || !CarryingCane) return;
            CarryingCane = false;
            heldCane.SetActive(true);
            CurrentStage = Stage.Evacuating;
            dialogue.Close();
        }

        private void Update()
        {
            if (CurrentStage == Stage.Sleeping || loop.IsPaused || loop.Transitioning) return;
            if (character.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(-30, verticalSpeed - 20 * Time.deltaTime);
            character.Move(Vector3.up * verticalSpeed * Time.deltaTime);
            if (CurrentStage != Stage.Evacuating || dialogue.IsOpen) return;

            PrototypeDoor gate = waypoint == 4 ? entranceDoor : waypoint == 6 ? emergencyExit : null;
            if (gate != null && !gate.IsFullyOpen)
            {
                WaitingForPath = true;
                lastBlocker = gate.name;
                return;
            }

            Vector3 delta = route[waypoint] - transform.position;
            delta.y = 0;
            if (delta.magnitude < 0.08f)
            {
                if (waypoint < route.Length - 1) waypoint++;
                else if (Vector2.Distance(new Vector2(transform.position.x, transform.position.z),
                             new Vector2(safePoint.position.x, safePoint.position.z)) <= 0.45f)
                {
                    CurrentStage = Stage.Safe;
                    WaitingForPath = false;
                    loop.RememberLeonidRescue();
                }
                return;
            }
            Vector3 direction = delta.normalized;
            float step = Mathf.Min(speed * Time.deltaTime, delta.magnitude);
            // Check the actual solid world, including moving doors and the player. Never warp to a waypoint.
            Vector3 center = transform.position + character.center;
            // Shorten the test at the feet so a horizontal cast does not treat the floor as a barrier.
            float end = character.height / 2 - character.radius - 0.08f;
            var hits = Physics.CapsuleCastAll(center - Vector3.up * end, center + Vector3.up * end,
                character.radius + 0.035f, direction, step + 0.04f, ~0, QueryTriggerInteraction.Ignore);
            WaitingForPath = false;
            lastBlocker = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue;
                WaitingForPath = true;
                lastBlocker = hit.collider.name;
                return;
            }
            Vector3 previous = transform.position;
            character.Move(direction * step);
            Vector3 movement = transform.position - previous;
            movement.y = 0;
            WaitingForPath = movement.sqrMagnitude < step * step * 0.1f;
            if (!WaitingForPath)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 180 * Time.deltaTime);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (Mathf.Abs(hit.normal.y) < 0.7f) lastBlocker = hit.collider.name;
        }
    }
}
