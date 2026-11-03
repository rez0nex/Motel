using UnityEngine;

namespace Motel
{
    // Scene-owned belongings: no inventory, no persisted item ownership.
    [DisallowMultipleComponent]
    public sealed class LeonidBelongings : MonoBehaviour
    {
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private DialogueController dialogue;
        [SerializeField] private LeonidEvacuee leonid;
        [SerializeField] private GameObject keys, spareCane;
        [SerializeField] private Transform trunkLid;
        private float lidAngle;
        public bool HasCarKeys { get; private set; }
        public bool IsTrunkOpen { get; private set; }
        public bool SpareTaken { get; private set; }
        public bool CanTakeSpare => HasCarKeys && IsTrunkOpen && lidAngle >= 74 && !SpareTaken;
        public bool CanInteract => !loop.IsPaused && !loop.Transitioning && !dialogue.IsOpen;
        public const string Directions = "С этой тростью я далеко не уйду. В багажнике запасная, складная. Машина синяя, у вывески «МОТЕЛЬ» на краю парковки, ближе к запасному выходу. Ключи на тумбочке.";

        public void Configure(MotelLoopController controller, DialogueController window, LeonidEvacuee owner,
            GameObject keyObject, GameObject caneObject, Transform lid)
        { loop = controller; dialogue = window; leonid = owner; keys = keyObject; spareCane = caneObject; trunkLid = lid; }
        private void Awake()
        {
            HasCarKeys = IsTrunkOpen = SpareTaken = false; lidAngle = 0;
            keys.SetActive(true); spareCane.SetActive(false); trunkLid.localRotation = Quaternion.identity;
        }
        public void InspectBrokenCane()
        {
            if (!CanInteract) return;
            dialogue.ShowFor(this, "Сломанная трость", "Крепление рукояти треснуло и разошлось. Трость не выдержит вес человека — пользоваться ею нельзя.",
                new DialogueController.Choice("Закончить осмотр.", dialogue.Close));
        }
        public void TakeKeys()
        {
            if (!CanInteract || HasCarKeys) return;
            HasCarKeys = true; keys.SetActive(false);
        }
        public void ToggleTrunk()
        {
            if (!CanInteract) return;
            if (!HasCarKeys)
            {
                dialogue.ShowFor(this, "Багажник синей машины", "Багажник заперт. Нужны ключи от этой машины.",
                    new DialogueController.Choice("Пока оставить закрытым.", dialogue.Close));
                return;
            }
            IsTrunkOpen = !IsTrunkOpen;
        }
        public bool TakeSpare()
        {
            if (!CanInteract || !CanTakeSpare || !leonid.TakeCane()) return false;
            SpareTaken = true; spareCane.SetActive(false); return true;
        }
        private void Update()
        {
            if (loop.IsPaused || loop.Transitioning) return;
            lidAngle = Mathf.MoveTowards(lidAngle, IsTrunkOpen ? 75 : 0, 90 * Time.deltaTime);
            trunkLid.localRotation = Quaternion.Euler(lidAngle, 0, 0);
            spareCane.SetActive(IsTrunkOpen && lidAngle >= 74 && !SpareTaken);
        }
    }
}
