using System.Collections.Generic;
using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class LeraEvacuee : MonoBehaviour
    {
        public enum Stage { InRoom, Following, Safe }
        [SerializeField] private FamilyEpisode family;
        [SerializeField] private int age = 14;
        public int Age => age;
        [SerializeField] private Transform safePoint;
        [SerializeField, Min(0.1f)] private float speed = 1.7f;
        private readonly List<Vector3> trail = new List<Vector3>();
        private readonly Vector3[] target = new Vector3[1];
        private CharacterController body;
        private EvacuationMotor motor;
        private int next;
        private bool settling, arrivalRequested;
        private string segmentBlocker;
        private bool askingAboutMother, heardMotherReport;
        private FamilyEpisode.MotherWhereabouts displayedMother;
        public bool HasMotherReport => heardMotherReport;
        public Stage CurrentStage { get; private set; }
        public bool WaitingForPlayer { get; private set; }
        public bool WaitingForPath => motor != null && motor.WaitingForPath;
        public string BlockingObject => motor?.BlockingObject;
        public bool HeadingToSafePoint => settling;
        public bool ArrivalRequested => arrivalRequested;
        public string ArrivalBlocker { get; private set; }
        public string Prompt => "E — поговорить с Лерой";
        private DialogueController Window => family.Dialogue;

        public void Configure(FamilyEpisode episode, Transform destination) { family = episode; safePoint = destination; age = 14; }
        private void Awake()
        {
            CurrentStage = Stage.InRoom;
            trail.Clear(); next = 0; settling = arrivalRequested = false; WaitingForPlayer = false;
            askingAboutMother = heardMotherReport = false;
            body = GetComponent<CharacterController>();
            target[0] = transform.position;
            motor = new EvacuationMotor(body, family.Loop, Window, this, target, safePoint, speed);
        }

        public void Interact()
        {
            if (!family.CanInteract) return;
            askingAboutMother = false;
            if (CurrentStage == Stage.Safe)
            {
                Show("Место сбора", family.Marina.CurrentStage == MarinaEvacuee.Stage.Safe && family.CanSee(this, family.Marina)
                        ? "Мама здесь. Мы подождём вместе на парковке."
                        : "Я останусь у места сбора. Пожалуйста, убедитесь, что с мамой всё в порядке.",
                    new DialogueController.Choice("Оставайся здесь.", Window.Close));
                return;
            }
            if (CurrentStage == Stage.Following)
            {
                Show("Идём к выходу", WaitingForPlayer
                        ? "Я отстала. Вернитесь ближе, и я продолжу за вами."
                        : WaitingForPath ? "Не могу пройти. Откройте дверь или освободите путь, я подожду."
                        : heardMotherReport ? "Спасибо, что рассказали про маму. Я иду за вами." : "Я иду за вами. Покажите, куда дальше.",
                    new DialogueController.Choice("Продолжим, идём к парковке.", Window.Close),
                    new DialogueController.Choice("Хочу сказать, где сейчас твоя мама.", Mother));
                return;
            }
            family.Loop.RememberFamilyPlacement();
            Show("Номер 8", "Я Лера, дочь Марины. Нас разместили в соседних номерах. Что происходит? Здесь слышен странный гул.",
                new DialogueController.Choice("Здесь опасно. Я проведу тебя к месту сбора на парковке.", Warn),
                new DialogueController.Choice("Нужно выйти из здания. Расскажу, где сейчас твоя мама.", Mother),
                new DialogueController.Choice("Я сейчас вернусь.", Window.Close));
        }

        private void Mother()
        {
            askingAboutMother = true;
            displayedMother = family.CurrentMotherWhereabouts;
            var shown = displayedMother;
            Show("Мама", "А мама? Она тоже выходит?",
                new DialogueController.Choice(family.MotherLocation, () => HearMother(shown)),
                new DialogueController.Choice("Я сначала уточню, где она. Подожди.", Window.Close));
        }
        private void HearMother(FamilyEpisode.MotherWhereabouts reported)
        {
            if (family.CurrentMotherWhereabouts != reported) { Mother(); return; }
            askingAboutMother = false; heardMotherReport = true;
            string reply = reported switch
            {
                FamilyEpisode.MotherWhereabouts.InRoom => "Пожалуйста, предупредите её. Я пойду с вами к месту сбора.",
                FamilyEpisode.MotherWhereabouts.LeavingBuilding => "Хорошо. Тогда пойдём к месту сбора.",
                FamilyEpisode.MotherWhereabouts.Courtyard => "Значит, встретимся на парковке. Покажите мне путь.",
                _ => "Хорошо, тогда ведите меня к ней."
            };
            Show("Мама", reply,
                new DialogueController.Choice(CurrentStage == Stage.Following ? "Продолжим, держись рядом." : "Идём, я покажу путь.",
                    CurrentStage == Stage.Following ? (System.Action)Window.Close : Follow),
                new DialogueController.Choice("Я скоро вернусь.", Window.Close));
        }
        private void Warn()
        {
            askingAboutMother = false;
            Show("Покажите выход", "Хорошо, покажите путь. Я пойду за вами.",
                new DialogueController.Choice("Следуй за мной к месту сбора на парковке.", Follow),
                new DialogueController.Choice("Я скоро вернусь.", Window.Close));
        }
        private void Follow()
        {
            if (CurrentStage != Stage.InRoom || family.Loop.Transitioning) return;
            CurrentStage = Stage.Following; trail.Clear(); trail.Add(family.Player.position); next = 0;
            motor.Begin(); Window.Close();
        }
        private void Update()
        {
            if (askingAboutMother && Window.IsOpen && Window.Participant == this &&
                displayedMother != family.CurrentMotherWhereabouts) Mother();
            if (CurrentStage != Stage.Following) { motor.Tick(); return; }
            if (family.Loop.IsPaused || family.Loop.Transitioning) return;
            Vector3 player = family.Player.position;
            if (Horizontal(player, trail[trail.Count - 1]) >= 0.35f) trail.Add(player);
            // Once led to the parking area, physically walk the last clear metres to the reserved slot.
            // Keep following if another guest blocks that approach; the player can lead around them.
            bool clearArrival = ClearSegment(safePoint.position);
            ArrivalBlocker = clearArrival ? null : segmentBlocker;
            if (Horizontal(player, safePoint.position) <= 2.5f && Horizontal(transform.position, safePoint.position) <= 4)
                arrivalRequested = true;
            if (!clearArrival) settling = false;
            else if (FamilyEpisode.InSafeArea(transform.position) || arrivalRequested &&
                     Horizontal(transform.position, safePoint.position) <= 4) settling = true;
            float gap = Horizontal(player, transform.position);
            WaitingForPlayer = !settling && gap > 8;
            if (settling) target[0] = safePoint.position;
            else if (WaitingForPlayer || gap <= 1.1f) target[0] = transform.position;
            else
            {
                // A returning player can reconnect the trail, but only through a clear body-sized segment.
                if (gap <= 2.5f && ClearSegment(player)) { trail.Clear(); trail.Add(player); next = 0; }
                while (next < trail.Count && Horizontal(trail[next], transform.position) < 0.12f) next++;
                target[0] = next < trail.Count ? trail[next] : player;
            }
            if (motor.Tick()) { CurrentStage = Stage.Safe; WaitingForPlayer = false; family.Loop.RememberLeraRescue(); }
        }
        private bool ClearSegment(Vector3 point)
        {
            segmentBlocker = null;
            Vector3 direction = point - transform.position; direction.y = 0;
            Vector3 center = transform.position + body.center;
            float end = body.height / 2 - body.radius - 0.08f;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(center - Vector3.up * end, center + Vector3.up * end,
                         body.radius + 0.035f, direction.normalized, direction.magnitude, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(transform) && !hit.collider.transform.IsChildOf(family.Player))
                { segmentBlocker = hit.collider.name; return false; }
            return true;
        }
        private static float Horizontal(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        private void Show(string topic, string line, params DialogueController.Choice[] choices)
            => Window.ShowTopicFor(this, "Лера", topic, line, choices);
        private void OnControllerColliderHit(ControllerColliderHit hit) => motor?.ReportCollision(hit);
    }
}
