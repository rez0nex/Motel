using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
    public sealed class MarinaEvacuee : MonoBehaviour
    {
        public enum Stage { InRoom, Evacuating, Safe }
        [SerializeField] private FamilyEpisode family;
        [SerializeField] private Vector3[] route;
        [SerializeField] private Transform safePoint;
        [SerializeField] private PrototypeDoor entrance, exit;
        [SerializeField, Min(0.1f)] private float speed = 1.5f;
        private EvacuationMotor motor;
        private bool knowsLeraSafe;
        public bool KnowsLeraIsSafe => knowsLeraSafe;
        public Stage CurrentStage { get; private set; }
        public int Waypoint => motor?.Waypoint ?? 0;
        public bool WaitingForPath => motor != null && motor.WaitingForPath;
        public string BlockingObject => motor?.BlockingObject;
        public string Prompt => "E — поговорить с Мариной";
        private DialogueController Window => family.Dialogue;

        public void Configure(FamilyEpisode episode, Vector3[] path, Transform destination, PrototypeDoor door, PrototypeDoor outside)
        { family = episode; route = path; safePoint = destination; entrance = door; exit = outside; }

        private void Awake()
        {
            CurrentStage = Stage.InRoom;
            knowsLeraSafe = false;
            motor = new EvacuationMotor(GetComponent<CharacterController>(), family.Loop, Window, this, route,
                safePoint, speed, new EvacuationMotor.Gate(4, entrance), new EvacuationMotor.Gate(6, exit));
        }

        public void Interact()
        {
            if (!family.CanInteract) return;
            if (CurrentStage == Stage.Safe)
            {
                Parking();
                return;
            }
            if (CurrentStage == Stage.Evacuating)
            {
                Show("Выход к парковке", WaitingForPath
                        ? "Путь перекрыт. Откройте дверь или освободите проход."
                        : knowsLeraSafe ? "Иду к Лере на парковку. Подожду её у места сбора."
                        : "Иду через запасной выход. Проверю двор и буду ждать у места сбора.",
                    new DialogueController.Choice(CanReportLera ? "Лера уже у места сбора на парковке." : "Хорошо.",
                        CanReportLera ? (System.Action)ReportLera : Window.Close),
                    new DialogueController.Choice(family.HasKeys ? "Ключи уже у меня." : "Дайте ключи от восьмого.",
                        family.HasKeys ? (System.Action)Window.Close : Keys));
                return;
            }
            Show("Номер 7", "Что случилось? Мы с дочерью только устроились.",
                new DialogueController.Choice(CanReportLera ? "Лера уже у места сбора. Вам тоже нужно выйти."
                    : "В здании опасно. Нужно выйти на парковку.", CanReportLera ? (System.Action)ReportAndWarn : ConfirmSearch),
                new DialogueController.Choice("Вы приехали с дочерью? Где она остановилась?", Daughter),
                new DialogueController.Choice(family.HasKeys ? "Я скоро вернусь." : "Можно ключи от соседнего восьмого?",
                    family.HasKeys ? (System.Action)Window.Close : Keys));
        }

        private void Daughter()
        {
            family.Loop.RememberFamilyPlacement();
            Show("О дочери",
                "Да, дочери дали соседний восьмой. У нас общее бронирование, но дополнительное спальное место в седьмом сломано. Поэтому нам дали две комнаты.",
                new DialogueController.Choice("Здесь опасно, нужно выйти.", ConfirmSearch),
                new DialogueController.Choice(family.HasKeys ? "Понятно." : "Дайте комплект ключей от восьмого.",
                    family.HasKeys ? (System.Action)Window.Close : Keys));
        }

        private void Keys()
        {
            bool alreadyHeld = family.HasKeys;
            if (!alreadyHeld) family.GiveKeys();
            Show("Ключи", alreadyHeld ? "Тогда сохраните этот комплект. Он подходит к седьмому и восьмому."
                    : "Вот мой комплект — ключи от седьмого и восьмого. На стойке есть дубликат. Между нашими комнатами тоже есть дверь.",
                new DialogueController.Choice("Спасибо.", Window.Close));
        }

        private void ConfirmSearch()
        {
            Show("Разделим поиск", knowsLeraSafe
                    ? "Спасибо, что сказали про Леру. Тогда я выйду к ней через запасной выход."
                    : "Я проверю двор и выйду к месту сбора. А вы проверьте Леру в соседнем восьмом. Договорились?",
                new DialogueController.Choice(CanReportLera ? "Лера уже у места сбора. Выходите к ней."
                    : knowsLeraSafe ? "Да, идите к ней на парковку." : "Я проверю Леру. Встретимся у места сбора на парковке.",
                    CanReportLera ? (System.Action)AgreeWithReport : Agree),
                new DialogueController.Choice(family.HasKeys ? "Пока подождите." : "Сначала дайте ключи от восьмого.",
                    family.HasKeys ? (System.Action)Window.Close : Keys));
        }

        private void Agree()
        {
            if (CurrentStage != Stage.InRoom || family.Loop.Transitioning) return;
            CurrentStage = Stage.Evacuating; motor.Begin(); Window.Close();
        }

        private bool CanReportLera => !knowsLeraSafe && family.Lera.CurrentStage == LeraEvacuee.Stage.Safe;
        private void ReportAndWarn()
        {
            if (family.Lera.CurrentStage != LeraEvacuee.Stage.Safe) { ConfirmSearch(); return; }
            knowsLeraSafe = true; ConfirmSearch();
        }
        private void AgreeWithReport()
        {
            if (family.Lera.CurrentStage != LeraEvacuee.Stage.Safe) { ConfirmSearch(); return; }
            knowsLeraSafe = true; Agree();
        }
        private void ReportLera()
        {
            if (family.Lera.CurrentStage != LeraEvacuee.Stage.Safe) { Window.Close(); return; }
            knowsLeraSafe = true;
            Show("Лера", family.CanSee(this, family.Lera)
                    ? "Вижу её. Спасибо, что привели Леру. Я останусь рядом."
                    : "Спасибо, что сообщили. Я буду ждать Леру у места сбора.",
                new DialogueController.Choice("Хорошо.", Window.Close));
        }
        private void Parking()
        {
            bool visible = family.CanSee(this, family.Lera);
            bool arrived = family.Lera.CurrentStage == LeraEvacuee.Stage.Safe;
            if (visible && arrived) knowsLeraSafe = true;
            string line = visible && arrived ? "Лера здесь, я её вижу. Спасибо, что привели её. Мы останемся у места сбора."
                : visible && family.Lera.CurrentStage == LeraEvacuee.Stage.Following ? "Вижу, Лера идёт за вами. Я подожду её здесь."
                : knowsLeraSafe ? "Я знаю, что Лера добралась до места сбора. Я останусь здесь и подожду её."
                : "А Леру вы вывели? Я её здесь не вижу. Пожалуйста, проверьте соседний восьмой номер. Я останусь здесь.";
            Show("Место сбора", line,
                new DialogueController.Choice(CanReportLera ? "Лера уже у места сбора на парковке."
                    : arrived && visible ? "Оставайтесь вместе у места сбора."
                    : family.Lera.CurrentStage == LeraEvacuee.Stage.Following ? "Лера идёт со мной. Мы подойдём к вам."
                    : "Я проверю восьмой номер.", CanReportLera ? (System.Action)ReportLera : Window.Close),
                new DialogueController.Choice(family.HasKeys ? "Ключи уже у меня." : "Дайте ключи от восьмого.",
                    family.HasKeys ? (System.Action)Window.Close : Keys));
        }
        private void Show(string topic, string line, params DialogueController.Choice[] choices)
            => Window.ShowTopicFor(this, "Марина", topic, line, choices);

        private void Update()
        {
            if (motor.Tick()) { CurrentStage = Stage.Safe; family.Loop.RememberMarinaRescue(); }
        }
        private void OnControllerColliderHit(ControllerColliderHit hit) => motor?.ReportCollision(hit);
    }
}
