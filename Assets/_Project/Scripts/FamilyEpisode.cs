using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class FamilyEpisode : MonoBehaviour
    {
        public enum MotherWhereabouts { InRoom, LeavingBuilding, Courtyard, Safe }
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private Transform player;
        [SerializeField] private MarinaEvacuee marina;
        [SerializeField] private LeraEvacuee lera;
        [SerializeField] private GameObject duplicateKeys;
        public bool HasKeys { get; private set; }
        public MotelLoopController Loop => loop;
        public DialogueController Dialogue => dialogue;
        public Transform Player => player;
        public MarinaEvacuee Marina => marina;
        public LeraEvacuee Lera => lera;
        public bool CanInteract => !loop.IsPaused && !loop.Transitioning && !dialogue.IsOpen;

        private void Awake() => HasKeys = false;

        public void Configure(MotelLoopController controller, DialogueController window, Transform movement,
            MarinaEvacuee mother, LeraEvacuee daughter, GameObject keys)
        {
            loop = controller; dialogue = window; player = movement;
            marina = mother; lera = daughter; duplicateKeys = keys;
        }

        public void GiveKeys()
        {
            if (loop.Transitioning) return;
            HasKeys = true;
            duplicateKeys.SetActive(false);
        }

        public MotherWhereabouts CurrentMotherWhereabouts
        {
            get
            {
                if (marina.CurrentStage == MarinaEvacuee.Stage.Safe) return MotherWhereabouts.Safe;
                Vector3 p = marina.transform.position;
                if (p.x > 26.9f || p.x > 6.3f && p.z < -8.1f + MotelSpatialLayout.OffsetFor(this)) return MotherWhereabouts.Courtyard;
                return marina.CurrentStage == MarinaEvacuee.Stage.InRoom
                    ? MotherWhereabouts.InRoom : MotherWhereabouts.LeavingBuilding;
            }
        }

        // Administrator speech: this belongs in a player choice, never under Lera's name.
        public string MotherLocation => CurrentMotherWhereabouts switch
        {
            MotherWhereabouts.InRoom => "Твоя мама пока в номере 7. Её тоже нужно предупредить.",
            MotherWhereabouts.LeavingBuilding => "Твоя мама ещё в здании, она выходит через запасной выход к парковке.",
            MotherWhereabouts.Courtyard => "Твоя мама уже во дворе, идёт к месту сбора на парковке.",
            _ => "Твоя мама уже у места сбора на парковке. Она ждёт там."
        };

        public bool CanSee(Component observer, Component person)
        {
            Vector3 from = observer.transform.position + Vector3.up * 1.5f;
            Vector3 delta = person.transform.position + Vector3.up * 1.5f - from;
            if (delta.magnitude > 6) return false;
            var hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude + 0.1f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(observer.transform)) continue;
                return hit.collider.transform.IsChildOf(person.transform);
            }
            return false;
        }

        public static bool InSafeArea(Vector3 p) => p.x >= 16.4f && p.x <= 20 && p.z >= -19.1f && p.z <= -16.3f;
    }
}
