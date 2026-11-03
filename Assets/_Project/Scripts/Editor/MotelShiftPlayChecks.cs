using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    // Isolated knowledge, handler-driven dialogue, real CharacterController journeys and scene reloads.
    [InitializeOnLoad]
    public static class MotelShiftPlayChecks
    {
        private const string Prefix = "Motel.ShiftChecks.";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Step { public string Name; public Func<bool> Tick; }
        private static readonly List<Step> steps = new List<Step>();
        private static bool ready, initialized, reloading, intentionalPause;
        private static int cycle, cursor, frame;
        private static double since, reportAt, sampleTime, realTime;
        private static Vector3 samplePosition;
        private static MotelLoopController loop, oldLoop;
        private static VadimEvacuee vadim;
        private static GuestRegister register;
        private static DialogueController window;
        private const string Introduction = "Добрый вечер. У меня бронь на имя Вадим. Номер уже оплачен";
        private const string RoomGreeting = "Спасибо, я устроился. Только что-то гудит за стеной. Это надолго?";

        static MotelShiftPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ready = false;
                SessionState.SetBool(Prefix + "ManualPending", false);
                if (SessionState.GetBool(Prefix + "Manual", false))
                {
                    SessionState.SetBool(Prefix + "Manual", false);
                    Debug.Log(UserHash() == SessionState.GetString(Prefix + "UserHash", "")
                        ? "Isolated Vadim play finished; user save unchanged." : "WARNING: user save hash changed during isolated play.");
                }
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (SessionState.GetBool(Prefix + "Running", false)) Finish("CANCELLED: editor left Play Mode.");
            };
        }

        [MenuItem("Motel/Play Vadim First Encounter (Isolated Save)")]
        public static void PlayFirstEncounter()
        {
            Guard();
            SessionState.SetString(Prefix + "Save", Path.GetFullPath("Temp/VadimFirst-" + Guid.NewGuid().ToString("N") + ".json"));
            SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Manual", true); SessionState.SetBool(Prefix + "ManualPending", true);
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }

        [MenuItem("Motel/Run Shift and Check-in Play Checks")]
        public static void Begin()
        {
            Guard();
            string save = Path.GetFullPath("Temp/ShiftPlay-" + Guid.NewGuid().ToString("N") + ".json");
            // ALL old observations must still leave explosion experience false.
            File.WriteAllText(save, "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true,\"equipmentOverheatInspected\":true,\"vadimRescued\":true,\"familyPlacementKnown\":true,\"marinaRescued\":true,\"leraRescued\":true}");
            SessionState.SetString(Prefix + "Save", save); SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true); SessionState.SetBool(Prefix + "Running", true);
            ready = initialized = reloading = intentionalPause = false; cycle = cursor = frame = 0; reportAt = 0;
            File.WriteAllText("Logs/Shift-PlayChecks.txt", "RUNNING: revised Vadim scenario");
            File.WriteAllText("Logs/Shift-PlayChecks-progress.txt", "Ordinary 360-second setting, unchanged guest speeds. Seven legacy facts, NO explosion experience.\n");
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        private static void Guard()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath) throw new Exception("Open saved MotelBlockout in edit mode.");
        }

        private static void Tick()
        {
            if (!ready || EditorApplication.isCompiling || !EditorApplication.isPlaying || Time.frameCount < 4) return;
            if (SessionState.GetBool(Prefix + "ManualPending", false))
            {
                SessionState.SetBool(Prefix + "ManualPending", false); InjectStore();
                Debug.Log("Fresh isolated Vadim session: " + SessionState.GetString(Prefix + "Save", "") + ". User observations are not used or overwritten.");
            }
            if (!SessionState.GetBool(Prefix + "Running", false)) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                loop = Object.FindAnyObjectByType<MotelLoopController>(); vadim = Object.FindAnyObjectByType<VadimEvacuee>();
                register = Object.FindAnyObjectByType<GuestRegister>(); window = Object.FindAnyObjectByType<DialogueController>();
                if (loop == null || vadim == null || register == null || window == null) return;
                if (!initialized) { InjectStore(); initialized = true; Build(); }
                if (!intentionalPause && loop.IsPaused) Pause(false);
                if (reloading)
                {
                    if (ReferenceEquals(loop, oldLoop))
                    {
                        Require(EditorApplication.timeSinceStartup - realTime < 15, "Reload timeout");
                        if (cycle == 1) Require(!MotelLoopController.HasExperiencedExplosion, "Experience granted before the new attempt");
                        return;
                    }
                    Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed");
                    reloading = false; cycle++;
                    CheckReset();
                    File.AppendAllText("Logs/Shift-PlayChecks-progress.txt", "RESET VERIFIED cycle=" + cycle + " experienced=" + MotelLoopController.HasExperiencedExplosion + "\n");
                    if (cycle == 4)
                    {
                        Finish("PASS: first play with/without fresh diagnostics and seven legacy observations; manual scene reload does not grant experience; real timed accident/reload grants it; ordinary reception greeting, rejection of counter evacuation even with fresh diagnostics; interrupted check-in, mandatory physical room arrival, fresh evidence before/after request, NPC/player line ownership, explicit exit confirmation, physical evacuation, repeats and explosion reset. User save unchanged.");
                        return;
                    }
                    Build();
                }
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                if (cursor >= steps.Count) return;
                Require(EditorApplication.timeSinceStartup - since < 55, "Timeout: " + steps[cursor].Name);
                if (EditorApplication.timeSinceStartup > reportAt)
                {
                    reportAt = EditorApplication.timeSinceStartup + 2;
                    File.AppendAllText("Logs/Shift-PlayChecks-progress.txt", "cycle=" + cycle + " t=" + loop.Elapsed.ToString("F2") +
                        " step=" + steps[cursor].Name + " state=" + vadim.CurrentStage + " point=" + vadim.Waypoint +
                        " pos=" + vadim.transform.position + " block=" + vadim.BlockingObject + "\n");
                }
                if (steps[cursor].Tick()) { cursor++; since = EditorApplication.timeSinceStartup; }
            }
            catch (Exception e) { Finish("FAIL: cycle=" + cycle + " step=" + cursor + " " + e); }
        }

        private static void Build()
        {
            steps.Clear(); cursor = 0; since = EditorApplication.timeSinceStartup;
            Act("initial state", () => { CheckReset(); MovePlayerAside(); });
            if (cycle == 0)
            {
                Act("pause schedule", () => { sampleTime = loop.Elapsed; realTime = EditorApplication.timeSinceStartup; intentionalPause = true; Pause(true); });
                Wait("paused clock and arrival", () =>
                {
                    if (EditorApplication.timeSinceStartup - realTime < 0.8) return false;
                    Require(Math.Abs(loop.Elapsed - sampleTime) < 0.001 && vadim.CurrentStage == VadimEvacuee.Stage.AwaitingArrival, "Paused arrival advanced");
                    intentionalPause = false; Pause(false); return true;
                });
            }
            if (cycle == 1 || cycle == 2) Act("fresh diagnostics before first conversation", Inspect);
            Wait("scheduled arrival", () =>
            {
                if (loop.Elapsed < vadim.ArrivalDelay - 0.1) Require(vadim.CurrentStage == VadimEvacuee.Stage.AwaitingArrival, "Early guest");
                if (vadim.CurrentStage == VadimEvacuee.Stage.AwaitingArrival) return false;
                Require(loop.Elapsed >= vadim.ArrivalDelay && !vadim.CheckedIn && !vadim.RoomArrivalComplete, "False arrival/check-in"); return true;
            });
            Wait("walk through main entrance", () => vadim.CurrentStage == VadimEvacuee.Stage.AtReception);
            Act("wait for absent administrator", () => { sampleTime = loop.Elapsed; samplePosition = vadim.transform.position; });
            Wait("still at counter", () =>
            {
                if (loop.Elapsed - sampleTime < 1) return false;
                Require(vadim.CurrentStage == VadimEvacuee.Stage.AtReception && Vector3.Distance(samplePosition, vadim.transform.position) < 0.05f, "Guest left counter"); return true;
            });
            Act("ordinary introduction and gated choices", () =>
            {
                vadim.Interact(); Require(Line() == Introduction && window.Speaker == "Вадим" && window.Topic == "Заселение", "Wrong introduction/owner");
                Require(window.ChoiceCount == (cycle < 2 ? 2 : 3) && !Choices().Any(c => c.Text.Contains("перегрев")), "Counter diagnostics offered");
                Require(Choices()[0].Text == "Добрый вечер. Сейчас проверю бронь" && Choices()[1].Text == "Одну минуту", "First choices");
                if (cycle < 2) NoDangerChoices();
                RejectInvalidAgree();
                window.Close();
                if (cycle == 2)
                {
                    vadim.Interact(); window.Choose(2);
                    Require(Line() == "Какой ещё взрыв? У меня оплачена бронь, я с дороги. Если что-то неисправно — проверьте. А пока оформите мне номер" &&
                        window.ChoiceCount == 2 && !vadim.AskedForEvidence, "Counter warning skipped registration/leaked request");
                    Require(Choices()[0].Text == "Хорошо. Сейчас проверю вашу бронь" && !Choices().Any(c => c.Text.Contains("перегрев")), "Wrong counter response");
                    window.Choose(0);
                }
                else { vadim.Interact(); window.Choose(0); }
                Require(vadim.BookingChecked && !vadim.KeyIssued && register.EntryCount == 2 && Line() == "Нашли? Я бронировал второй номер", "Booking check issued key");
                window.Close(); vadim.Interact();
                Require(Line() == "Нашли? Я бронировал второй номер" && window.ChoiceCount == 1 && !vadim.KeyIssued, "Interrupted booking lost state");
                Require(Choices()[0].Text == "Да, всё оплачено. Вот ключ от второго номера", "Key choice belongs to player");
                window.Choose(0);
                Require(Line() == "Спасибо. Пойду размещусь" && register.EntryCount == 3 && vadim.CheckedIn &&
                    vadim.CurrentStage == VadimEvacuee.Stage.GoingToRoom && !vadim.RoomArrivalComplete, "Check-in not atomic");
                window.Close(); vadim.Interact(); NoDangerChoices(); RejectInvalidAgree(); window.Close();
                Require(!register.CheckInVadim() && register.EntryCount == 3, "Duplicate entry/key");
            });
            Wait("physical arrival in room2", () => vadim.RoomArrivalComplete);
            Act("first room conversation", () =>
            {
                vadim.Interact(); Require(Line() == RoomGreeting && window.Speaker == "Вадим", "Counter warning contaminated room greeting");
                if (cycle < 2)
                {
                    NoDangerChoices(); RejectInvalidAgree();
                    Require(Choices()[0].Text == "Проверю, откуда шум" && Choices()[1].Text == "Если что-то понадобится, я на стойке", "First-life room choices");
                    if (cycle == 0)
                    {
                        window.Choose(0); Require(Line().Contains("шумом"), "Noise response"); window.Close(); Inspect();
                        vadim.Interact(); NoDangerChoices(); RejectInvalidAgree();
                        Require(!MotelLoopController.HasExperiencedExplosion, "Inspection/legacy facts granted experience");
                    }
                    window.Close();
                }
                else if (cycle == 3)
                {
                    window.Choose(0);
                    Require(Line() == "Вы проверили оборудование? Этот гул мне тоже не нравится, но я хочу понимать, что случилось" &&
                        Choices()[0].Text == "Сейчас проверю и вернусь" && Choices()[1].Text == "Подождите здесь", "Warning question/answer mismatch");
                    window.Choose(1); vadim.Interact(); Require(window.ChoiceCount == 2, "Repeat question lost"); window.Close();
                    Inspect(); vadim.Interact();
                }
                if (cycle >= 2)
                {
                    var beforeEvidence = vadim.CurrentStage;
                    RejectInvalidAgree(); Require(vadim.CurrentStage == beforeEvidence, "Route can start before NPC asks for directions");
                    Require(Choices()[0].Text == "Я проверил оборудование. Панель показывает критический перегрев. Нужно выйти на парковку", "Fresh evidence unavailable");
                    window.Choose(0); Require(Line() == "Критический перегрев? Хорошо, тогда выхожу. Где выход?" && vadim.CurrentStage != VadimEvacuee.Stage.Evacuating,
                        "NPC speaks player line/route started before directions");
                    Require(Choices()[0].Text == "Через ресепшен и главный вход. Место сбора на парковке", "Exit answer belongs to player");
                    if (cycle == 2)
                    {
                        window.Close(); vadim.Interact();
                        Require(Line() == "Критический перегрев? Хорошо, тогда выхожу. Где выход?" &&
                            vadim.CurrentStage != VadimEvacuee.Stage.Evacuating, "Interrupted directions were forgotten/started the route");
                        Close("Room2"); Close("Main Entrance");
                    }
                    window.Choose(0); Require(vadim.CurrentStage == VadimEvacuee.Stage.Evacuating && !window.IsOpen, "No evacuation after directions");
                    var motor = typeof(VadimEvacuee).GetField("motor", Private).GetValue(vadim);
                    RejectInvalidAgree();
                    Require(ReferenceEquals(motor, typeof(VadimEvacuee).GetField("motor", Private).GetValue(vadim)), "Repeated agreement recreated route");
                }
            });
            if (cycle == 0) Act("ordinary manual scene reload", () => Reload(false, true));
            else if (cycle == 1)
                Act("first actual timed accident boundary", () =>
                {
                    Require(!MotelLoopController.HasExperiencedExplosion, "Premature experience");
                    // Advance only this test's clock to its real boundary; Update requests the production accident.
                    ((AttemptClock)typeof(MotelLoopController).GetField("clock", Private).GetValue(loop)).Advance(360, false, false);
                    oldLoop = loop; reloading = true; realTime = EditorApplication.timeSinceStartup;
                });
            else if (cycle == 2)
            {
                Wait("closed room door waits", () => vadim.WaitingForPath && vadim.Waypoint == 4);
                Act("open room door", () => Door("Room2").Toggle());
                Wait("closed main entrance waits", () => vadim.WaitingForPath && vadim.Waypoint == 8);
                Act("open main entrance", () => Door("Main Entrance").Toggle());
                Wait("physical safe arrival", () => vadim.CurrentStage == VadimEvacuee.Stage.Safe);
                Act("repeat safe conversation and unique knowledge", () =>
                {
                    Require(Vector3.Distance(vadim.transform.position, new Vector3(17, 0.05f, -18.1f)) < 0.5f, "False rescue");
                    vadim.Interact(); Require(Line().Contains("парковке"), "Repeat safe conversation"); window.Close();
                    Require(!Store().RememberVadimRescue() && Store().Facts.Length == 7, "Duplicated/lost facts"); Reload(true);
                });
            }
            else Act("explosion resets active evacuation and open dialogue", () => { vadim.Interact(); Require(window.IsOpen, "Moving dialogue"); Reload(false); });
        }
        private static void CheckReset()
        {
            Require(vadim.CurrentStage == VadimEvacuee.Stage.AwaitingArrival && !vadim.CheckedIn && !vadim.KeyIssued &&
                !vadim.BookingChecked && !vadim.AskedForEvidence && !vadim.RoomArrivalComplete && !loop.EquipmentInspectedThisCycle && !window.IsOpen && loop.Elapsed < 2,
                "Attempt states leaked");
            Require(!(bool)typeof(VadimEvacuee).GetField("awaitingExitDirections", Private).GetValue(vadim), "Agreement leaked across reset");
            Require(MotelLoopController.HasExperiencedExplosion == (cycle >= 2) && Store().HasExperiencedExplosion == (cycle >= 2), "Wrong experience persistence/migration");
            Require(register.EntryCount == 2 && !register.Entries.Contains("№8") && !register.Entries.Contains("трость") && !register.Entries.Contains("слух"), "Register/spoilers");
            Require(Object.FindObjectsByType<VadimEvacuee>().Length == 1 && Object.FindObjectsByType<GuestRegister>().Length == 1 &&
                Object.FindAnyObjectByType<LeonidEvacuee>().CurrentStage == LeonidEvacuee.Stage.Sleeping && Store().Facts.Length == 7 && !loop.DebugCycle, "Duplicates, legacy knowledge or other episode reset");
        }
        private static void NoDangerChoices() => Require(!Choices().Any(c => new[] { "взрыв", "опасно", "парковк", "перегрев", "эвакуа" }.Any(s => c.Text.Contains(s))), "Premature danger/evacuation choice");
        private static void RejectInvalidAgree() => typeof(VadimEvacuee).GetMethod("Agree", Private).Invoke(vadim, null);
        private static void Inspect() { Object.FindAnyObjectByType<EquipmentInspection>().Interact(); Require(loop.EquipmentInspectedThisCycle, "Inspection did not occur"); window.Close(); }
        private static void Reload(bool cutoff, bool manual = false)
        {
            oldLoop = loop; reloading = true; realTime = EditorApplication.timeSinceStartup;
            if (manual) UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
            else Require(loop.RequestAccident(cutoff) && !loop.RequestAccident(!cutoff) && !window.IsOpen, "Repeated accident/dialogue survived");
        }
        private static void InjectStore() => typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
        private static ObservationStore Store() => new ObservationStore(SessionState.GetString(Prefix + "Save", ""));
        private static void MovePlayerAside()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>(); var cc = player.GetComponent<CharacterController>();
            cc.enabled = false; player.transform.position = new Vector3(10, 0.05f, -23); cc.enabled = true;
        }
        private static void Act(string name, Action action) => steps.Add(new Step { Name = name, Tick = () => { action(); return true; } });
        private static void Wait(string name, Func<bool> test) => steps.Add(new Step { Name = name, Tick = test });
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static void Close(string name) { if (Door(name).IsOpenRequested) Door(name).Toggle(); }
        private static void Pause(bool paused) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { paused });
        private static string Line() => (string)typeof(DialogueController).GetField("line", Private).GetValue(window);
        private static DialogueController.Choice[] Choices() => (DialogueController.Choice[])typeof(DialogueController).GetField("choices", Private).GetValue(window);
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static string UserHash()
        {
            string path = Path.Combine(Application.persistentDataPath, "observations-v1.json");
            if (!File.Exists(path)) return "ABSENT";
            using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Finish(string text)
        {
            SessionState.SetBool(Prefix + "Running", false);
            Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
            File.WriteAllText("Logs/Shift-PlayChecks.txt", text);
            if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text);
            EditorApplication.isPlaying = false;
        }
    }
}
