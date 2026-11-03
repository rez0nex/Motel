using System.Collections.Generic;
using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class VadimEvacuee : MonoBehaviour
    {
        public enum Stage { Skeptical, NeedsEvidence, Evacuating, Safe, AwaitingArrival, Arriving, AtReception, GoingToRoom }
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private GuestRegister register;
        [SerializeField] private Vector3[] route, arrivalRoute, checkInRoute;
        [SerializeField] private Transform safePoint, receptionPoint, roomPoint;
        [SerializeField] private GameObject presentation;
        [SerializeField] private PrototypeDoor entranceDoor, mainExit;
        [SerializeField, Min(0)] private float arrivalDelay = 10;
        [SerializeField, Min(0.1f)] private float speed = 1.7f;
        private CharacterController body;
        private EvacuationMotor motor;
        private bool openedJourneyDoor, askedForEvidence, bookingChecked;
        private bool roomArrivalComplete;
        private bool awaitingExitDirections;
        public Stage CurrentStage { get; private set; }
        public bool CheckedIn => register.VadimCheckedIn;
        public bool KeyIssued => CheckedIn;
        public bool BookingChecked => bookingChecked;
        public bool AskedForEvidence => askedForEvidence;
        public bool RoomArrivalComplete => roomArrivalComplete;
        public float ArrivalDelay => arrivalDelay;
        public int Waypoint => motor?.Waypoint ?? 0;
        public bool WaitingForPath => motor != null && motor.WaitingForPath;
        public string BlockingObject => motor?.BlockingObject;
        public string Prompt => CurrentStage == Stage.Arriving ? "Вадим подходит к стойке" : "E — поговорить с Вадимом";

        public void Configure(MotelLoopController controller, DialogueController window, GuestRegister book,
            Vector3[] path, Vector3[] arrival, Vector3[] checkIn,
            Transform destination, Transform counter, Transform room, GameObject model,
            PrototypeDoor entrance, PrototypeDoor exit)
        {
            loop = controller; dialogue = window; register = book; route = path; arrivalRoute = arrival;
            checkInRoute = checkIn; safePoint = destination;
            receptionPoint = counter; roomPoint = room; presentation = model; entranceDoor = entrance; mainExit = exit;
        }

        private void Awake()
        {
            CurrentStage = Stage.AwaitingArrival;
            askedForEvidence = bookingChecked = openedJourneyDoor = false;
            roomArrivalComplete = false;
            awaitingExitDirections = false;
            body = GetComponent<CharacterController>(); body.enabled = false;
            presentation.SetActive(false);
        }
        private EvacuationMotor Journey(Vector3[] path, Transform destination, params EvacuationMotor.Gate[] gates)
        {
            var next = new EvacuationMotor(body, loop, dialogue, this, path, destination, speed, gates);
            next.Begin(); return next;
        }

        public void Interact()
        {
            if (loop.IsPaused || loop.Transitioning || dialogue.IsOpen || CurrentStage == Stage.AwaitingArrival || CurrentStage == Stage.Arriving) return;
            if (CurrentStage == Stage.Safe || CurrentStage == Stage.Evacuating)
            {
                Show("Эвакуация", CurrentStage == Stage.Safe
                    ? "Я здесь, на парковке. Спасибо, что объяснили, в чём опасность."
                    : WaitingForPath ? "Не могу пройти. Откройте дверь или освободите проход, я выйду сам."
                    : "Иду к месту сбора на парковке.", new DialogueController.Choice("Хорошо.", dialogue.Close));
                return;
            }
            if (!CheckedIn) { ReceptionTalk(); return; }
            if (!roomArrivalComplete)
            {
                Show("Иду в номер", "Спасибо. Пойду размещусь.", new DialogueController.Choice("Хорошо.", dialogue.Close));
                return;
            }
            RoomTalk();
        }

        private void RoomTalk()
        {
            if (awaitingExitDirections && CanDiscussEvacuation && loop.EquipmentInspectedThisCycle)
            {
                ExitDirections();
                return;
            }
            const string greeting = "Спасибо, я устроился. Только что-то гудит за стеной. Это надолго?";
            if (!MotelLoopController.HasExperiencedExplosion)
            {
                Show("Номер 2", askedForEvidence ? "Удалось выяснить, откуда шум?" : greeting,
                    new DialogueController.Choice("Проверю, откуда шум", CheckNoise),
                    new DialogueController.Choice("Если что-то понадобится, я на стойке", dialogue.Close));
                return;
            }
            if (loop.EquipmentInspectedThisCycle)
                Show("Номер 2", askedForEvidence ? "Вы проверили оборудование? Что показывают приборы?" : greeting,
                    EvidenceChoice(), new DialogueController.Choice("Одну минуту", dialogue.Close));
            else if (askedForEvidence) AskForEvidence();
            else Show("Номер 2", greeting,
                new DialogueController.Choice("В здании опасно. Нужно выйти на парковку", AskForEvidence),
                new DialogueController.Choice("Если что-то понадобится, я на стойке", dialogue.Close));
        }

        private void ReceptionTalk()
        {
            if (bookingChecked) { BookingResult(); return; }
            var choices = new List<DialogueController.Choice>
            {
                new DialogueController.Choice("Добрый вечер. Сейчас проверю бронь", CheckBooking),
                new DialogueController.Choice("Одну минуту", dialogue.Close)
            };
            if (MotelLoopController.HasExperiencedExplosion)
                choices.Add(new DialogueController.Choice("Здесь опасно. Скоро произойдёт взрыв. Вам нельзя оставаться", ReceptionWarning));
            Show("Заселение", "Добрый вечер. У меня бронь на имя Вадим. Номер уже оплачен", choices.ToArray());
        }
        private void ReceptionWarning()
        {
            if (!MotelLoopController.HasExperiencedExplosion || CheckedIn || CurrentStage != Stage.AtReception) return;
            Show("Предупреждение у стойки",
                "Какой ещё взрыв? У меня оплачена бронь, я с дороги. Если что-то неисправно — проверьте. А пока оформите мне номер",
                new DialogueController.Choice("Хорошо. Сейчас проверю вашу бронь", CheckBooking),
                new DialogueController.Choice("Одну минуту", dialogue.Close));
        }
        private void CheckBooking()
        {
            if (CheckedIn || CurrentStage != Stage.AtReception || !register.HasPaidBooking("Вадим")) return;
            bookingChecked = true;
            BookingResult();
        }
        private void BookingResult() => Show("Оплаченная бронь · №2", "Нашли? Я бронировал второй номер",
            new DialogueController.Choice("Да, всё оплачено. Вот ключ от второго номера", CheckIn));
        private void CheckIn()
        {
            if (!bookingChecked || CurrentStage != Stage.AtReception || !register.CheckInVadim()) return;
            CurrentStage = Stage.GoingToRoom; openedJourneyDoor = false;
            motor = Journey(checkInRoute, roomPoint, new EvacuationMotor.Gate(5, entranceDoor));
            Show("Ключ выдан · №2", "Спасибо. Пойду размещусь", new DialogueController.Choice("Хорошего вечера.", dialogue.Close));
        }
        private DialogueController.Choice EvidenceChoice() => new DialogueController.Choice(
            "Я проверил оборудование. Панель показывает критический перегрев. Нужно выйти на парковку", ExplainExit);
        private bool CanDiscussEvacuation => CheckedIn && roomArrivalComplete && MotelLoopController.HasExperiencedExplosion &&
            !loop.IsPaused && !loop.Transitioning && (CurrentStage == Stage.Skeptical || CurrentStage == Stage.NeedsEvidence);
        private void CheckNoise()
        {
            askedForEvidence = true;
            Show("Шум оборудования", "Спасибо. Буду ждать, когда разберётесь с шумом.",
                new DialogueController.Choice("Если что-то понадобится, я на стойке", dialogue.Close));
        }
        private void AskForEvidence()
        {
            if (!CanDiscussEvacuation) return;
            askedForEvidence = true;
            CurrentStage = Stage.NeedsEvidence;
            Show("Проверка оборудования", "Вы проверили оборудование? Этот гул мне тоже не нравится, но я хочу понимать, что случилось",
                new DialogueController.Choice("Сейчас проверю и вернусь", dialogue.Close),
                new DialogueController.Choice("Подождите здесь", dialogue.Close));
        }
        private void ExplainExit()
        {
            if (!CanDiscussEvacuation || !loop.EquipmentInspectedThisCycle) return;
            awaitingExitDirections = true;
            ExitDirections();
        }
        private void ExitDirections()
        {
            Show("Путь к месту сбора", "Критический перегрев? Хорошо, тогда выхожу. Где выход?",
                new DialogueController.Choice("Через ресепшен и главный вход. Место сбора на парковке", Agree));
        }
        private void Agree()
        {
            if (!CanDiscussEvacuation || !loop.EquipmentInspectedThisCycle || !awaitingExitDirections) return;
            awaitingExitDirections = false;
            motor = Journey(route, safePoint, new EvacuationMotor.Gate(4, entranceDoor), new EvacuationMotor.Gate(8, mainExit));
            CurrentStage = Stage.Evacuating; dialogue.Close();
        }
        private void Show(string topic, string line, params DialogueController.Choice[] choices)
            => dialogue.ShowTopicFor(this, "Вадим", topic, line, choices);

        private void Update()
        {
            if (loop.IsPaused || loop.Transitioning) return;
            if (CurrentStage == Stage.AwaitingArrival)
            {
                if (loop.Elapsed < arrivalDelay) return;
                presentation.SetActive(true); body.enabled = true; CurrentStage = Stage.Arriving;
                motor = Journey(arrivalRoute, receptionPoint, new EvacuationMotor.Gate(1, mainExit));
            }
            // A guest uses the entrance/key for arrival and check-in; evacuation still waits for the player.
            PrototypeDoor journeyDoor = CurrentStage == Stage.Arriving && motor.Waypoint == 1 ? mainExit
                : CurrentStage == Stage.GoingToRoom && motor.Waypoint == 5 ? entranceDoor : null;
            if (journeyDoor != null && !openedJourneyDoor)
            {
                if (!journeyDoor.IsOpenRequested) journeyDoor.Toggle();
                openedJourneyDoor = true;
            }
            if (motor == null || !motor.Tick()) return;
            if (CurrentStage == Stage.Arriving) CurrentStage = Stage.AtReception;
            else if (CurrentStage == Stage.GoingToRoom) { roomArrivalComplete = true; CurrentStage = Stage.Skeptical; }
            else if (CurrentStage == Stage.Evacuating) { CurrentStage = Stage.Safe; loop.RememberVadimRescue(); }
        }
        private void OnControllerColliderHit(ControllerColliderHit hit) => motor?.ReportCollision(hit);
    }
}
