using UnityEngine;

namespace Motel
{
    [DisallowMultipleComponent]
    public sealed class ModuleBypassPanel : MonoBehaviour
    {
        public const double LaunchDelay = 2.5;
        [SerializeField] private MotelLoopController loop;
        [SerializeField] private TechnicalHatch cabinetDoor;
        [SerializeField] private Transform[] physicalCells;
        [SerializeField] private Material wireOn, wireOff;
        [SerializeField] private GameObject startIndicator;
        private BypassCircuit circuit;
        private double launchAt;
        private bool restartWithBypass;
        private string feedback;
        private double feedbackUntil;
        private GUIStyle titleStyle, textStyle, centeredStyle;
        public BypassCircuit Circuit => circuit;
        public bool IsOpen { get; private set; }
        public bool LaunchPending { get; private set; }
        public bool HasLaunched { get; private set; }
        public bool Started { get; private set; }
        public string Feedback => feedback;
        public TechnicalHatch CabinetDoor => cabinetDoor;
        public string Prompt => cabinetDoor != null && !cabinetDoor.IsFullyOpen ?
            cabinetDoor.IsOpen ? "Дверца открывается…" : "Сначала откройте дверцу шкафа" : "E — собрать обход модуля";
        public bool CanLaunch => IsOpen && !HasLaunched && !loop.IsPaused && !loop.Transitioning && circuit.IsComplete;
        public void Configure(MotelLoopController controller) { loop = controller; controller.ConfigureBypass(this); }
        public void ConfigureWorld(TechnicalHatch door, Transform[] cells, Material powered, Material idle, GameObject indicator)
        { cabinetDoor = door; physicalCells = cells; wireOn = powered; wireOff = idle; startIndicator = indicator; }
        private void Awake()
        {
            circuit = new BypassCircuit(); IsOpen = LaunchPending = HasLaunched = Started = false;
            if (startIndicator != null) startIndicator.SetActive(false);
            SyncWorld();
        }
        public void Interact()
        {
            if (loop == null || loop.IsPaused || loop.SuppressInteractionUI || HasLaunched ||
                cabinetDoor != null && !cabinetDoor.IsFullyOpen) return;
            IsOpen = true; loop.RefreshPlayerControl();
        }
        public void Close() { IsOpen = false; if (loop != null) loop.RefreshPlayerControl(); }
        public bool Rotate(int cell)
        {
            if (!IsOpen || HasLaunched || loop.IsPaused || loop.Transitioning) return false;
            bool changed = circuit.Rotate(cell); if (changed) SyncWorld(); return changed;
        }
        public bool StartGenerator()
        {
            if (!WorldCommandAllowed) return false;
            if (!circuit.IsComplete) { ShowFeedback("Администратор: «Не работает…»"); return false; }
            Started = true; if (startIndicator != null) startIndicator.SetActive(true);
            ShowFeedback("Цепь восстановлена. Перегрев сохраняется"); return true;
        }
        private bool WorldCommandAllowed => loop != null && !loop.IsPaused && !loop.SuppressInteractionUI && !HasLaunched;
        private void ShowFeedback(string text) { feedback = text; feedbackUntil = loop.Elapsed + 4; }
        public bool RestartGenerator()
        {
            if (!WorldCommandAllowed) return false;
            restartWithBypass = circuit.IsComplete;
            HasLaunched = LaunchPending = true; launchAt = loop.Elapsed + LaunchDelay;
            ShowFeedback("Перезапуск выполнен"); return true;
        }
        // Kept for older editor fixtures; this is now the yellow physical restart, never a UI button.
        public bool Launch()
        {
            Close(); return RestartGenerator();
        }
        private void Update()
        {
            if (!LaunchPending) return;
            if (loop.Transitioning) { LaunchPending = false; return; }
            if (loop.IsPaused || loop.Elapsed < launchAt) return;
            // The shared latch chooses between the timer/cutoff/panel; only its winner owns the cause.
            if (restartWithBypass ? loop.RequestBypassAccident() : loop.RequestAccident(false)) LaunchPending = false;
        }
        private void SyncWorld()
        {
            if (!circuit.IsComplete) { Started = false; if (startIndicator != null) startIndicator.SetActive(false); }
            if (physicalCells == null) return;
            for (int cell = 0; cell < physicalCells.Length; cell++)
            {
                Transform tile = physicalCells[cell]; if (tile == null) continue;
                int ports = circuit.Ports(cell);
                for (int side = 0; side < 4; side++) tile.GetChild(side).gameObject.SetActive((ports & (1 << side)) != 0);
                tile.GetChild(4).gameObject.SetActive(ports != 0);
                foreach (var r in tile.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.transform != tile) r.sharedMaterial = circuit.IsPowered(cell) ? wireOn : wireOff;
            }
        }
        private void OnDisable() { IsOpen = false; }
        private void OnGUI()
        {
            if (loop == null || loop.Transitioning) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true };
                centeredStyle = new GUIStyle(textStyle) { alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = textStyle.normal.textColor = centeredStyle.normal.textColor = Color.white;
            }
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 960f, Screen.height / 600f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            if (!IsOpen)
            {
                if (!string.IsNullOrEmpty(feedback) && loop.Elapsed < feedbackUntil)
                {
                    Rect toast = new Rect((width - 770) / 2, 40, 770, 65);
                    Fill(toast, new Color(0.06f, 0.13f, 0.10f, 0.96f)); GUI.Label(toast, feedback, centeredStyle);
                }
                GUI.matrix = previous; return;
            }
            Rect panel = new Rect((width - 900) / 2, (height - 560) / 2, 900, 560);
            Fill(panel, new Color(0.035f, 0.045f, 0.055f, 0.98f));
            GUI.Label(new Rect(panel.x, panel.y + 10, 900, 35), "Обход сгоревшего модуля", titleStyle);
            GUI.Label(new Rect(panel.x, panel.y + 49, 900, 28), "Клик — повернуть секцию", centeredStyle);
            float x = panel.x + 274, y = panel.y + 88;
            bool previousEnabled = GUI.enabled; GUI.enabled = !loop.IsPaused;
            for (int cell = 0; cell < 16; cell++)
            {
                Rect tile = new Rect(x + (cell % 4) * 90, y + (cell / 4) * 90, 82, 82);
                if (circuit.CanRotate(cell) && GUI.Button(tile, GUIContent.none)) Rotate(cell);
                bool powered = circuit.IsPowered(cell);
                Fill(new Rect(tile.x + 3, tile.y + 3, 76, 76), cell == BypassCircuit.Burned ? new Color(0.26f, 0.08f, 0.07f) :
                    powered ? new Color(0.08f, 0.25f, 0.19f) : new Color(0.12f, 0.15f, 0.18f));
                if (cell == BypassCircuit.Burned) { GUI.Label(tile, "СГОРЕЛ", centeredStyle); continue; }
                int ports = circuit.Ports(cell); if (ports == 0) continue;
                Color wire = powered ? new Color(0.35f, 1, 0.65f) : new Color(0.57f, 0.62f, 0.67f);
                if ((ports & BypassCircuit.N) != 0) Fill(new Rect(tile.center.x - 5, tile.y + 3, 10, 41), wire);
                if ((ports & BypassCircuit.E) != 0) Fill(new Rect(tile.center.x, tile.center.y - 5, 38, 10), wire);
                if ((ports & BypassCircuit.S) != 0) Fill(new Rect(tile.center.x - 5, tile.center.y, 10, 38), wire);
                if ((ports & BypassCircuit.W) != 0) Fill(new Rect(tile.x + 3, tile.center.y - 5, 41, 10), wire);
                Fill(new Rect(tile.center.x - 7, tile.center.y - 7, 14, 14), wire);
            }
            Rect input = new Rect(panel.x + 36, y + 106, 180, 54), output = new Rect(panel.x + 670, y + 106, 195, 54);
            Fill(input, circuit.IsPowered(BypassCircuit.Input) ? new Color(0.08f, 0.3f, 0.19f) : new Color(0.15f, 0.19f, 0.21f));
            Fill(output, circuit.IsComplete ? new Color(0.08f, 0.3f, 0.19f) : new Color(0.15f, 0.19f, 0.21f));
            GUI.Label(input, "Вход", centeredStyle); GUI.Label(output, "Блок запуска", centeredStyle);
            Fill(new Rect(input.xMax, input.center.y - 3, x - input.xMax, 6), new Color(0.35f, 0.8f, 0.57f));
            Fill(new Rect(x + 352, output.center.y - 3, output.x - x - 352, 6), circuit.IsComplete ? new Color(0.35f, 1, 0.65f) : Color.gray);
            GUI.Label(new Rect(panel.x, panel.y + 449, 900, 28), circuit.IsComplete ? "Сигнал дошёл до блока запуска" : "Цепь разорвана", centeredStyle);
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(panel.x + 40, panel.y + 485, 480, 36), "Команды — кнопками на генераторе", centeredStyle);
            if (GUI.Button(new Rect(panel.x + 570, panel.y + 484, 185, 36), "Закрыть")) Close();
            GUI.Label(new Rect(panel.x, panel.y + 526, 900, 28), "Время смены идёт · Escape — закрыть и поставить на паузу", centeredStyle);
            GUI.matrix = previous;
        }
        private static void Fill(Rect rect, Color color)
        { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
    }
}
