using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Motel
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-100)]
    public sealed class MotelLoopController : MonoBehaviour
    {
        [SerializeField] private FirstPersonController player;
        [SerializeField] private TextMesh receptionClock;
        [SerializeField] private GameObject equipmentIndicator;
        [SerializeField] private GameObject intactEquipment;
        [SerializeField] private GameObject damagedEquipment;
        [SerializeField] private Light[] accidentLights;
        [SerializeField] private bool debugSixtySecondCycle;
        [SerializeField] private DialogueController dialogue;

        private static ObservationStore knowledge;
        private static bool? sessionDebug;
        private static bool resumeAfterReload;
        private static MotelLoopController active;
        private readonly AttemptClock clock = new AttemptClock();
        private bool paused = true, observationsOpen, applicationFocused;
        private bool resumedThisFrame;
        private float flash, fade;
        private string lastClockText;
        private GUIStyle titleStyle, bodyStyle;
        private Vector2 observationScroll;

        public double Elapsed => clock.Elapsed;
        public bool IsPaused => paused;
        public bool Transitioning => clock.AccidentRequested;
        public bool DebugCycle => sessionDebug ?? debugSixtySecondCycle;
        public bool DialogueOpen => dialogue != null && dialogue.IsOpen;
        // Scene-owned evidence expires on reload; persistent knowledge below does not.
        public bool EquipmentInspectedThisCycle { get; private set; }
        public bool SuppressInteractionUI => observationsOpen || Transitioning || DialogueOpen;
        public static bool KnowsCutoff => knowledge != null && knowledge.HasCutoffFact;
        public static bool KnowsLeonidRescue => knowledge != null && knowledge.HasLeonidFact;
        public static bool KnowsEquipmentOverheat => knowledge != null && knowledge.HasEquipmentFact;
        public static bool KnowsVadimRescue => knowledge != null && knowledge.HasVadimFact;

        public void ConfigureDialogue(DialogueController window) => dialogue = window;

        public void RefreshPlayerControl()
        {
            if (player == null) return;
            bool blocked = paused || observationsOpen || Transitioning || DialogueOpen;
            player.SetControlBlocked(blocked);
            if (blocked) player.FreeCursor();
            else player.CaptureCursor();
        }

        public void RememberLeonidRescue()
        {
            if (!Transitioning) knowledge.RememberLeonidRescue();
        }

        public void RememberVadimRescue()
        {
            if (!Transitioning) knowledge.RememberVadimRescue();
        }

        public void RememberEquipmentOverheat()
        {
            if (Transitioning) return;
            EquipmentInspectedThisCycle = true;
            knowledge.RememberEquipmentOverheat();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginSession()
        {
            knowledge = null;
            sessionDebug = null;
            resumeAfterReload = false;
            active = null;
            Time.timeScale = 1;
        }

        public void Configure(FirstPersonController movement, TextMesh display, GameObject indicator,
            GameObject intact, GameObject damaged, Light[] lights)
        {
            player = movement;
            receptionClock = display;
            equipmentIndicator = indicator;
            intactEquipment = intact;
            damagedEquipment = damaged;
            accidentLights = lights;
        }

        private void Awake()
        {
            if (active != null && active != this)
            {
                Debug.LogError("Only one MotelLoopController is allowed.", this);
                enabled = false;
                return;
            }
            active = this;
            if (player == null || receptionClock == null || equipmentIndicator == null ||
                intactEquipment == null || damagedEquipment == null || accidentLights == null || accidentLights.Length == 0)
            {
                Debug.LogError("Motel loop references are incomplete.", this);
                enabled = false;
                return;
            }
            knowledge ??= new ObservationStore(Path.Combine(Application.persistentDataPath, "observations-v1.json"));
            sessionDebug ??= debugSixtySecondCycle;
            applicationFocused = Application.isFocused;
            player.ExternalCursorControl = true;
            damagedEquipment.SetActive(false);
            RefreshClock();
        }

        private void Start()
        {
            bool resume = resumeAfterReload;
            resumeAfterReload = false;
            SetPaused(!resume || !applicationFocused);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                observationsOpen = false;
                if (dialogue != null) dialogue.Close();
                SetPaused(true);
            }
            else if (!Transitioning && !DialogueOpen && keyboard != null && keyboard.jKey.wasPressedThisFrame)
            {
                observationsOpen = !observationsOpen;
                SetPaused(observationsOpen);
            }
            else if (paused && !observationsOpen && applicationFocused && Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 position = Mouse.current.position.ReadValue();
                if (position.x >= 0 && position.y >= 0 && position.x < Screen.width && position.y < Screen.height)
                    SetPaused(false);
            }

            // Debug speed is changed deliberately while paused; it persists across scene reloads.
            if (paused && !observationsOpen && !Transitioning && keyboard != null && keyboard.f6Key.wasPressedThisFrame)
                sessionDebug = !DebugCycle;

            Time.timeScale = paused ? 0 : 1;
            player.SetControlBlocked(paused || observationsOpen || Transitioning || DialogueOpen);
            // The delta of the click frame belongs to the preceding paused interval.
            clock.Advance(Time.unscaledDeltaTime, paused || resumedThisFrame, DebugCycle);
            resumedThisFrame = false;
            RefreshClock();
            if (clock.Expired) RequestAccident(false);
        }

        private void SetPaused(bool value)
        {
            if (paused && !value) resumedThisFrame = true;
            paused = value;
            Time.timeScale = value ? 0 : 1;
            RefreshPlayerControl();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused && player != null) SetPaused(true);
        }

        private void RefreshClock()
        {
            string text = clock.DisplayTime;
            if (text == lastClockText) return;
            lastClockText = text;
            receptionClock.text = text;
            receptionClock.GetComponent<WorldSign>().RefreshNow();
        }

        public bool RequestAccident(bool fromCutoff)
        {
            if (!isActiveAndEnabled || paused || !clock.TryRequestAccident()) return false;
            if (fromCutoff)
            {
                equipmentIndicator.SetActive(false);
                knowledge.RememberCutoff();
            }
            if (dialogue != null) dialogue.Close();
            player.SetControlBlocked(true);
            StartCoroutine(AccidentAndReload(fromCutoff));
            return true;
        }

        private IEnumerator AccidentAndReload(bool fromCutoff)
        {
            // Let the player see the equipment go dark before the same accident sequence.
            float delay = fromCutoff ? 0.55f : 0;
            for (float t = 0; t < delay; t += Time.deltaTime) yield return null;
            equipmentIndicator.SetActive(false);
            intactEquipment.SetActive(false);
            damagedEquipment.SetActive(true);
            foreach (Light light in accidentLights)
                if (light != null) { light.color = new Color(1, 0.22f, 0.08f); light.intensity *= 0.15f; }
            for (float t = 0; t < 2.6f; t += Time.deltaTime)
            {
                flash = Mathf.Clamp01(1 - t / 0.22f) * 0.7f;
                fade = Mathf.Clamp01((t - 1.6f) / 1);
                yield return null;
            }
            fade = 1;
            resumeAfterReload = true;
            string path = gameObject.scene.path;
            AsyncOperation loading;
#if UNITY_EDITOR
            // Also works when the scene has not been added to the user's Build Profile.
            loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            loading = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
            if (loading == null)
            {
                resumeAfterReload = false;
                SetPaused(true);
                Debug.LogError("Could not reload the motel scene.", this);
            }
        }

        private void OnDisable()
        {
            if (active != this) return;
            active = null;
            Time.timeScale = 1;
            if (player != null)
            {
                player.ExternalCursorControl = false;
                player.SetControlBlocked(false);
                player.FreeCursor();
            }
        }

        private void OnGUI()
        {
            if (player == null || active != this) return;
            DrawOverlay(Color.white, flash);
            DrawOverlay(Color.black, fade);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
                titleStyle.normal.textColor = Color.white;
                bodyStyle.normal.textColor = Color.white;
            }
            if (DebugCycle)
                GUI.Label(new Rect(12, 10, Screen.width - 24, 32), "ОТЛАДКА · полный цикл за 60 с", bodyStyle);
            if (!Transitioning && !DialogueOpen)
                GUI.Label(new Rect(12, Screen.height - 36, Screen.width - 24, 30), "J — наблюдения" +
                    (paused && !observationsOpen ? "    |    F6 — обычная / отладочная скорость" : ""), bodyStyle);
            if ((Transitioning || DialogueOpen) && paused)
                GUI.Label(new Rect(12, Screen.height / 2f, Screen.width - 24, 50), "Пауза · щёлкните, чтобы продолжить", titleStyle);
            if (!observationsOpen) return;
            float width = Mathf.Min(660, Screen.width - 24), height = Mathf.Min(450, Screen.height - 50);
            Rect panel = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 16, panel.y + 14, width - 32, 40), "Наблюдения", titleStyle);
            string[] known = knowledge.Facts;
            string facts = known.Length > 0 ? "• " + string.Join("\n\n• ", known) : "Пока нет наблюдений.";
            bool warning = !string.IsNullOrEmpty(knowledge.Warning);
            Rect viewport = new Rect(panel.x + 24, panel.y + 66, width - 48, Mathf.Max(25, height - (warning ? 205 : 135)));
            float textWidth = viewport.width - 20;
            float textHeight = Mathf.Max(viewport.height, bodyStyle.CalcHeight(new GUIContent(facts), textWidth));
            observationScroll = GUI.BeginScrollView(viewport, observationScroll, new Rect(0, 0, textWidth, textHeight));
            GUI.Label(new Rect(0, 0, textWidth, textHeight), facts, bodyStyle);
            GUI.EndScrollView();
            if (!string.IsNullOrEmpty(knowledge.Warning))
                GUI.Label(new Rect(panel.x + 24, panel.yMax - 130, width - 48, 65), knowledge.Warning, bodyStyle);
            if (GUI.Button(new Rect(panel.x + width / 2 - 100, panel.yMax - 55, 200, 38), "Закрыть · J"))
            {
                observationsOpen = false;
                SetPaused(false);
            }
        }

        private static void DrawOverlay(Color color, float alpha)
        {
            if (alpha <= 0) return;
            Color previous = GUI.color;
            color.a = alpha;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
