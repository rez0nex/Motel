using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    [InitializeOnLoad]
    public static class MotelBypassPlayChecks
    {
        private const string Prefix = "Motel.BypassChecks.", Report = "Logs/Bypass-PlayChecks.txt";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static MotelLoopController loop, oldLoop;
        private static ModuleBypassPanel panel;
        private static int scenario, step, frame;
        private static bool ready, initialized, reloading, intentionalPause;
        private static bool expectedBypass;
        private static int expectedFacts;
        private static double start, wallStart, launchStart, deadline;
        private static Vector3 spawn;
        private static string save;
        private static int approachStep;

        static MotelBypassPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ready = false;
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (SessionState.GetBool(Prefix + "Finished", false))
                {
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetInt(Prefix + "ExitCode", 1));
                    return;
                }
                if (SessionState.GetBool(Prefix + "Running", false)) Finish(false, "Play Mode stopped before completion");
            };
        }

        // CLI entry point: one editor, no Player build; source scenes remain untouched except generated MotelBlockout.
        public static void RunBatch()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Prototype.unity");
                MotelBlockoutSceneCreator.BuildAndSave(true);
                MotelBlockoutSceneCreator.BuildAndSave(true);
                EditorSceneManager.OpenScene(MotelBlockoutSceneCreator.ScenePath);
                MotelBypassChecks.ValidateCurrent(); MotelFamilyChecks.ValidateCurrent(); MotelConnectionChecks.ValidateCurrent();
                Begin();
            }
            catch (Exception e)
            {
                File.WriteAllText(Report, "FAIL before Play: " + e); Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Motel/Run Module Bypass Play Checks")]
        public static void Begin()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty ||
                SceneManager.GetActiveScene().path != MotelBlockoutSceneCreator.ScenePath)
                throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true); SessionState.SetBool(Prefix + "Running", true);
            SessionState.SetBool(Prefix + "Finished", false);
            initialized = ready = reloading = intentionalPause = false; scenario = step = frame = 0;
            approachStep = 0;
            deadline = EditorApplication.timeSinceStartup + 150;
            File.WriteAllText(Report, "RUNNING: waiting for Play Mode");
            File.WriteAllText("Logs/Bypass-PlayProgress.txt", "Isolated Temp saves; handlers, not mouse automation. Normal clock; timer boundary advanced only in test fixtures.\n");
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
        }

        private static void Tick()
        {
            if (!ready || !SessionState.GetBool(Prefix + "Running", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                Require(EditorApplication.timeSinceStartup < deadline, "Overall timeout at scenario=" + scenario + " step=" + step);
                loop = Object.FindAnyObjectByType<MotelLoopController>(); panel = Object.FindAnyObjectByType<ModuleBypassPanel>();
                if (loop == null || panel == null) return;
                if (!initialized)
                {
                    if (Time.frameCount < 4) return;
                    FreshKnowledge(); spawn = Object.FindAnyObjectByType<FirstPersonController>().transform.position;
                    ValidateSaveCompatibility(); initialized = true;
                }
                if (loop.IsPaused && !intentionalPause) Pause(false);
                if (reloading)
                {
                    if (ReferenceEquals(loop, oldLoop)) return;
                    Require(MotelLoopController.KnowsBypassExplosion == expectedBypass && Store().Facts.Length == expectedFacts, "Wrong persistent cause/facts");
                    Require(!panel.IsOpen && !panel.HasLaunched && !panel.LaunchPending && !panel.Circuit.IsComplete &&
                        panel.Circuit.Ports(0) == 3 && !loop.EquipmentInspectedThisCycle, "Panel/evidence not reset");
                    Require(Vector3.Distance(Object.FindAnyObjectByType<FirstPersonController>().transform.position, spawn) < 0.15f, "Player spawn not restored");
                    var doors = Object.FindObjectsByType<PrototypeDoor>();
                    Require(doors.All(d => !d.IsOpenRequested), "Door not restored");
                    Require(Object.FindObjectsByType<ModuleBypassPanel>().Length == 1 && Object.FindObjectsByType<ModuleBypassNote>().Length == 1, "Duplicate panel/note");
                    Log("RESET scenario=" + scenario + " bypass=" + expectedBypass + " facts=" + expectedFacts);
                    reloading = false; scenario++; step = 0; start = loop.Elapsed;
                    if (scenario == 7)
                    {
                        Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User observations changed");
                        Finish(true, "open-board clock/control; broken/solved chain; close/reopen; explicit one-shot physical restart; paused 2.5 game-second delay and debug speed; two completed bypass accidents with one fact; timer while open and timer winning pending start; manual reload cancels pending start without a fact; no delayed second reload; diagnostics/cutoff regression; reset/layout/player/doors; old-save compatibility; user file unchanged. Handler-driven input; manual UI acceptance remains.");
                        return;
                    }
                }
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                switch (scenario)
                {
                    case 0: PersistenceAndPause(); break;
                    case 1: SuccessfulStart(true); break;
                    case 2: SuccessfulStart(false); break;
                    case 3: TimerWhileOpen(); break;
                    case 4: TimerWins(); break;
                    case 5: ManualCancel(); break;
                    case 6: ExistingControls(); break;
                }
            }
            catch (Exception e) { Debug.LogException(e); Finish(false, "scenario=" + scenario + " step=" + step + " " + e); }
        }

        private static void PersistenceAndPause()
        {
            if (step == 0)
            {
                if (!ApproachPanel()) return;
                OpenFixture(); var note = Object.FindAnyObjectByType<ModuleBypassNote>(); FirstRay(note); note.Interact();
                var dialogue = Object.FindAnyObjectByType<DialogueController>();
                Require(dialogue.IsOpen && (string)typeof(DialogueController).GetField("line", Private).GetValue(dialogue) == note.Content,
                    "Note text/window incorrect");
                dialogue.Close(); Require(!loop.EquipmentInspectedThisCycle && Store().Facts.Length == 0, "Note grants diagnostics/knowledge");
                FirstRay(panel);
                OpenFixture(); panel.Interact(); Require(panel.IsOpen && loop.SuppressInteractionUI && Blocked(), "Panel does not own control");
                Require(!panel.Circuit.IsComplete && !panel.Rotate(5) && !panel.Rotate(15), "Unsolved/burned/empty accepted");
                Require(panel.Rotate(0), "Clickable section refused"); start = loop.Elapsed; step++;
                ScreenCapture.CaptureScreenshot("Logs/Bypass-UI-Broken.png");
            }
            else if (step == 1 && loop.Elapsed - start >= 0.3)
            {
                Require(!loop.Transitioning && panel.IsOpen, "Panel pauses clock or explodes without launch");
                panel.Close(); Require(!Blocked(), "Close does not return control");
                OpenFixture(); panel.Interact(); Require(panel.Circuit.Ports(0) == 6, "Close/reopen loses rotation");
                Pause(true); intentionalPause = true; start = loop.Elapsed; wallStart = EditorApplication.timeSinceStartup; step++;
            }
            else if (step == 2 && EditorApplication.timeSinceStartup - wallStart >= 0.3)
            {
                Require(loop.Elapsed == start && !panel.Rotate(0), "Paused board/time changes");
                panel.Close(); intentionalPause = false; Pause(false);
                Log("PASS: broken chain, active modal clock, control, close/reopen and pause");
                Reload(false, 0, true);
            }
        }
        private static void SuccessfulStart(bool first)
        {
            if (step == 0)
            {
                if (!first) typeof(MotelLoopController).GetField("sessionDebug", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (bool?)true);
                OpenFixture(); panel.Interact(); SolvePanel(); start = loop.Elapsed; step++;
                if (first) ScreenCapture.CaptureScreenshot("Logs/Bypass-UI-Solved.png");
            }
            else if (step == 1 && loop.Elapsed - start >= 0.3)
            {
                Require(panel.CanLaunch && !loop.Transitioning && !panel.HasLaunched, "Solution auto-starts or cannot launch");
                panel.Close(); OpenFixture(); panel.Interact(); Require(panel.Circuit.IsComplete, "Solved board lost on close");
                launchStart = loop.Elapsed; Require(panel.Launch() && !panel.Launch() && !panel.IsOpen && !Blocked(), "Explicit start/control/duplicate guard");
                wallStart = EditorApplication.timeSinceStartup;
                if (first) { Pause(true); intentionalPause = true; start = loop.Elapsed; wallStart = EditorApplication.timeSinceStartup; step = 2; }
                else step = 3;
            }
            else if (step == 2 && EditorApplication.timeSinceStartup - wallStart >= 0.3)
            {
                Require(loop.Elapsed == start && panel.LaunchPending && !loop.Transitioning, "Paused delay runs");
                intentionalPause = false; Pause(false); step = 3;
            }
            else if (step == 3)
            {
                if (!loop.Transitioning) { Require(loop.Elapsed < launchStart + ModuleBypassPanel.LaunchDelay + 0.5, "Launch delay missed"); return; }
                Require(loop.Elapsed >= launchStart + ModuleBypassPanel.LaunchDelay && !panel.IsOpen, "Early start or panel not closed");
                Require(!loop.RequestAccident(true) && !loop.RequestBypassAccident(), "Second accident accepted");
                if (first) Require(!MotelLoopController.KnowsBypassExplosion, "Fact recorded before completed explosion/reload");
                Log("PASS: explicit bypass accident after " + (loop.Elapsed - launchStart).ToString("F3") + " game seconds; repeat=" + !first + "; debug=" + loop.DebugCycle);
                Reload(true, 1, false);
            }
        }
        private static void TimerWhileOpen()
        {
            if (step == 0)
            {
                typeof(MotelLoopController).GetField("sessionDebug", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, (bool?)false);
                FreshKnowledge(); OpenFixture(); panel.Interact(); panel.Rotate(0); SetNearExpiry(); step++;
            }
            else if (loop.Transitioning)
            {
                Require(!panel.IsOpen && !loop.RequestBypassAccident(), "Timer did not close/lock panel");
                Log("PASS: ordinary timer expires while panel is open"); Reload(false, 0, false);
            }
        }
        private static void TimerWins()
        {
            if (step == 0)
            {
                FreshKnowledge(); OpenFixture(); panel.Interact(); SolvePanel(); SetNearExpiry();
                Require(panel.Launch(), "Cannot launch near timer boundary"); step++;
            }
            else if (loop.Transitioning)
            {
                Require(loop.Elapsed >= AttemptClock.Duration && !loop.RequestBypassAccident(), "Timer/panel race accepted twice");
                Log("PASS: timer wins before 2.5 second pending launch, no bypass fact"); Reload(false, 0, false);
            }
        }
        private static void ExistingControls()
        {
            if (step == 0) { start = loop.Elapsed; step++; }
            else if (step == 1 && loop.Elapsed - start >= 3.1)
            {
                Require(!loop.Transitioning && !panel.LaunchPending && !MotelLoopController.KnowsBypassExplosion, "Old pending launch survives reload");
                FreshKnowledge(); Object.FindAnyObjectByType<EquipmentInspection>().Interact();
                Require(loop.EquipmentInspectedThisCycle && !loop.Transitioning && Store().HasEquipmentFact && !panel.HasLaunched, "Safe diagnostics regressed");
                Object.FindAnyObjectByType<DialogueController>().Close();
                var cutoff = Object.FindAnyObjectByType<EmergencyCutoff>();
                cutoff.Cover.Interact(); cutoff.Cover.Leaf.localRotation = cutoff.Cover.OpenRotation; cutoff.Interact();
                Require(loop.Transitioning && Store().HasCutoffFact && !Store().HasBypassFact && !loop.RequestBypassAccident(), "Cutoff regressed");
                Log("PASS: no delayed second explosion; separate diagnostics and existing cutoff"); Reload(false, 2, false);
            }
        }
        private static void ManualCancel()
        {
            if (step == 0) { start = loop.Elapsed; step++; }
            else if (loop.Elapsed - start >= 3.1)
            {
                Require(!loop.Transitioning && !panel.LaunchPending && !MotelLoopController.KnowsBypassExplosion, "Timer winner left a delayed accident");
                FreshKnowledge(); OpenFixture(); panel.Interact(); SolvePanel(); Require(panel.Launch(), "Manual-cancel fixture cannot start");
                Require(!MotelLoopController.KnowsBypassExplosion, "Start button grants fact before an explosion");
                Log("PASS: timer winner has no delayed second reload; manually reload immediately after successful Start");
                Reload(false, 0, true);
            }
        }
        private static void SolvePanel()
        {
            int[] cells = { 0, 1, 2, 6, 10, 11, 7 }, turns = { 1, 1, 2, 1, 2, 2, 2 };
            for (int i = 0; i < cells.Length; i++) for (int t = 0; t < turns[i]; t++) Require(panel.Rotate(cells[i]), "Rotate rejected");
            Require(panel.Circuit.IsComplete, "Reference route fails in panel");
        }
        private static bool ApproachPanel()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>().GetComponent<CharacterController>();
            Vector3[] route = { new Vector3(1.55f, 0, -11.4f), new Vector3(4.8f, 0, -11.4f), new Vector3(4.8f, 0, MotelBlockoutPlan.TechnicalCenterZ),
                new Vector3(4.8f, 0, MotelBlockoutPlan.TechnicalCenterZ), MotelTechnicalRoomSceneSetup.World(3.12f,0,.8f), MotelBypassSceneSetup.Approach };
            if (approachStep >= route.Length) return true;
            if (approachStep == 4)
            {
                var door = GameObject.Find("Motel Blockout/Doors/Technical").GetComponent<PrototypeDoor>();
                if (!door.IsOpenRequested) { FirstRay(door); door.Toggle(); }
                if (!door.IsFullyOpen) return false;
            }
            Vector3 delta = route[approachStep] - player.transform.position; delta.y = 0;
            if (delta.magnitude < 0.06f) { approachStep++; return false; }
            player.Move(delta.normalized * Mathf.Min(3 * Time.deltaTime, delta.magnitude)); return false;
        }
        // Board regressions use an open-door fixture; real hinges/covers are exercised by Technical Room Play Checks.
        private static void OpenFixture()
        { var door=panel.CabinetDoor; if(door==null)return; if(!door.IsOpen)door.Interact(); door.Leaf.localRotation=door.OpenRotation; Physics.SyncTransforms(); }
        private static void FirstRay(Component target)
        {
            var body = Object.FindAnyObjectByType<FirstPersonController>().GetComponent<CharacterController>();
            var collider = target.GetComponentInChildren<Collider>();
            Vector3 origin = body.transform.position + Vector3.up * 1.6f, direction = collider.bounds.center - origin;
            Require(Physics.Raycast(origin, direction.normalized, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.transform.IsChildOf(target.transform), "Not first solid within interaction range: " + target.name);
        }
        private static void Reload(bool bypass, int facts, bool manual)
        {
            oldLoop = loop; expectedBypass = bypass; expectedFacts = facts; reloading = true;
            if (manual) EditorSceneManager.LoadSceneAsyncInPlayMode(MotelBlockoutSceneCreator.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        }
        private static void FreshKnowledge()
        {
            save = Path.GetFullPath("Temp/BypassPlay-" + Guid.NewGuid().ToString("N") + ".json");
            typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, new ObservationStore(save));
        }
        private static ObservationStore Store() => new ObservationStore(save);
        private static void SetNearExpiry()
        {
            var clock = (AttemptClock)typeof(MotelLoopController).GetField("clock", Private).GetValue(loop);
            clock.Advance(359.85 - loop.Elapsed, false, false);
        }
        private static bool Blocked() => (bool)typeof(FirstPersonController).GetField("controlBlocked", Private)
            .GetValue(Object.FindAnyObjectByType<FirstPersonController>());
        private static void Pause(bool value) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { value });
        private static void ValidateSaveCompatibility()
        {
            string path = Path.GetFullPath("Temp/BypassLegacy-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true,\"equipmentOverheatInspected\":true,\"vadimRescued\":true,\"familyPlacementKnown\":true,\"marinaRescued\":true,\"leraRescued\":true}");
            var store = new ObservationStore(path);
            Require(store.Facts.Length == 7 && !store.HasBypassFact && !store.HasExperiencedExplosion, "Legacy facts infer new knowledge");
            Require(store.RememberBypassExplosion(), "New fact not stored"); store = new ObservationStore(path);
            Require(store.Facts.Length == 8 && store.HasBypassFact && !store.RememberBypassExplosion(), "New fact duplicates or erases old facts");
        }
        private static void Finish(bool success, string message)
        {
            File.WriteAllText(Report, (success ? "PASS: " : "FAIL: ") + message);
            SessionState.SetBool(Prefix + "Running", false); SessionState.SetBool(Prefix + "Finished", true);
            SessionState.SetInt(Prefix + "ExitCode", success ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
        private static string UserHash()
        {
            string path = Path.Combine(Application.persistentDataPath, "observations-v1.json");
            if (!File.Exists(path)) return "missing";
            using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Log(string value) => File.AppendAllText("Logs/Bypass-PlayProgress.txt", value + "\n");
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
