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
    [InitializeOnLoad]
    public static class MotelFamilyPlayChecks
    {
        private const string Prefix = "Motel.FamilyChecks.";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Step { public string Name; public Func<bool> Tick; }
        private static readonly List<Step> steps = new List<Step>();
        private static int cycle, cursor, frame, firstCycle;
        private static bool initialized, reloading, pace, playReady, waitTimed;
        private static double stepStart, lastReport, reloadStart;
        private static MotelLoopController loop, oldLoop;
        private static FamilyEpisode family;
        private static DialogueController window;
        private static CharacterController player;

        static MotelFamilyPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) { playReady = true; return; }
                if (state != PlayModeStateChange.EnteredEditMode) return;
                playReady = false;
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (!SessionState.GetBool(Prefix + "Running", false)) return;
                SessionState.SetBool(Prefix + "Running", false);
                File.WriteAllText("Logs/Family-PlayChecks.txt", "CANCELLED: Play Mode stopped before completion.");
            };
        }

        [MenuItem("Motel/Run Automated Four Guest Checks")]
        public static void Begin() => BeginAt(0);

        public static void BeginAt(int startingCycle, bool waitForOrdinaryTimer = true)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath) throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            string save = Path.GetFullPath("Temp/FamilyPlay-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(save, startingCycle == 0 ? "{\"version\":1,\"cutoffCausedExplosion\":true}" :
                "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true,\"equipmentOverheatInspected\":true,\"vadimRescued\":true,\"familyPlacementKnown\":true,\"marinaRescued\":true,\"leraRescued\":true}");
            SessionState.SetString(Prefix + "Save", save); SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true); SessionState.SetBool(Prefix + "Running", true);
            initialized = reloading = playReady = false; cycle = firstCycle = startingCycle; cursor = frame = 0; lastReport = 0;
            waitTimed = waitForOrdinaryTimer;
            File.WriteAllText("Logs/Family-PlayChecks.txt", "RUNNING: waiting for EnteredPlayMode.");
            File.WriteAllText("Logs/Family-PlayChecks-progress.txt", "Player walks at 3 m/s; Marina 1.5, Lera/Vadim 1.7, Leonid 1.2; normal 360s clock. Handler-driven dialogue has no human reading time.\n");
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!playReady || !SessionState.GetBool(Prefix + "Running", false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                loop = Object.FindAnyObjectByType<MotelLoopController>(); family = Object.FindAnyObjectByType<FamilyEpisode>();
                window = Object.FindAnyObjectByType<DialogueController>();
                var movement = Object.FindAnyObjectByType<FirstPersonController>();
                if (loop == null || family == null || window == null || movement == null) return;
                player = movement.GetComponent<CharacterController>();
                if (!initialized)
                {
                    if (Time.frameCount < 4) return;
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
                    initialized = true; BuildRoute();
                }
                if (loop.IsPaused) Resume(); // Automation only; normal focus pause is unchanged.
                if (reloading)
                {
                    if (ReferenceEquals(loop, oldLoop))
                    { Require(EditorApplication.timeSinceStartup - reloadStart < 15, "Reload timeout"); return; }
                    if (cycle == 0 && waitTimed) Require(oldLoop.Elapsed >= 360 && oldLoop.Transitioning, "Four rescues cancelled the ordinary timed accident");
                    File.AppendAllText("Logs/Family-PlayChecks-progress.txt", "RESET VERIFIED after cycle=" + cycle + " previousElapsed=" + oldLoop.Elapsed.ToString("F2") + "\n");
                    CheckReset(); reloading = false; cycle++;
                    if (cycle == 3)
                    {
                        Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed");
                        Finish("PASS: cycles " + firstCycle + "..2 (times/scenarios in progress log); physical four-guest route within 360s; player walked 3m/s with follower pacing, unchanged NPC speeds; keys, mother whereabouts, same-cycle parking talks, distance wait/rejoin, repeat talks and reset during following; cycle states/keys/doors reset, seven independent persistent facts without copies, no ending. User save unchanged. Handler-driven input, manual acceptance still required.");
                        return;
                    }
                    Resume(); BuildRoute();
                }
                if (loop.Transitioning) return;
                // Always resume and detect scene replacement before de-duplicating player movement.
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                Require(!loop.DebugCycle, "Debug speed is not allowed for route timing");
                Require(loop.Elapsed < 350, "Route did not finish within the normal cycle; elapsed=" + loop.Elapsed);
                if (cursor >= steps.Count) return;
                if (EditorApplication.timeSinceStartup >= lastReport)
                {
                    lastReport = EditorApplication.timeSinceStartup + 2;
                    File.AppendAllText("Logs/Family-PlayChecks-progress.txt", "cycle=" + cycle + " t=" + loop.Elapsed.ToString("F2") +
                        " step=" + cursor + "/" + steps[cursor].Name + " P=" + player.transform.position +
                        " M=" + family.Marina.CurrentStage + "/" + family.Marina.transform.position + "/" + family.Marina.BlockingObject +
                        " L=" + family.Lera.CurrentStage + "/" + family.Lera.transform.position + "/" + family.Lera.BlockingObject +
                        " arrival=" + family.Lera.ArrivalRequested + "/" + family.Lera.HeadingToSafePoint + "/" + family.Lera.ArrivalBlocker +
                        " V=" + Object.FindAnyObjectByType<VadimEvacuee>().CurrentStage + "/" + Object.FindAnyObjectByType<VadimEvacuee>().BlockingObject +
                        " Leonid=" + Object.FindAnyObjectByType<LeonidEvacuee>().CurrentStage + "/" + Object.FindAnyObjectByType<LeonidEvacuee>().BlockingObject + "\n");
                }
                Require(loop.Elapsed - stepStart < 45, "Step timeout: " + steps[cursor].Name + "; player=" + player.transform.position +
                    "; Marina=" + family.Marina.transform.position + "/" + family.Marina.BlockingObject +
                    "; Lera=" + family.Lera.transform.position + "/" + family.Lera.BlockingObject);
                if (steps[cursor].Tick()) { cursor++; stepStart = loop.Elapsed; }
            }
            catch (Exception e) { Finish("FAIL: cycle=" + cycle + " step=" + cursor + " " + e); }
        }

        private static void BuildRoute()
        {
            steps.Clear(); cursor = 0; stepStart = loop.Elapsed; pace = true;
            Act("initial states", () =>
            {
                Require(!family.HasKeys && family.Marina.CurrentStage == MarinaEvacuee.Stage.InRoom &&
                    family.Lera.CurrentStage == LeraEvacuee.Stage.InRoom, "Initial family states: keys=" + family.HasKeys);
                Require(!Door("Room8").GetComponent<RoomEightLock>().CanOpen, "Outside room8 opens without keys");
                Door("Room8").Toggle(); Require(!WantsOpen(Door("Room8")), "Locked door toggle bypassed key");
            });
            if (cycle == 0)
            {
                Act("desk duplicate", () => { var keys = Object.FindAnyObjectByType<RoomKeysPickup>(); Near(keys); keys.Interact(); Require(family.HasKeys, "Desk keys"); });
                Diagnose(); Enter(8);
                Act("Lera before Marina", () => { Require(family.MotherLocation.Contains("в номере 7"), "Mother prematurely outside"); StartLera(); });
                ConnectionFromEight(); Exit(7); LeadMain(true);
                Wait("Lera rescued first", () => family.Lera.CurrentStage == LeraEvacuee.Stage.Safe);
                Act("Lera saved without Marina", () =>
                {
                    Require(family.Marina.CurrentStage == MarinaEvacuee.Stage.InRoom && !MotelLoopController.KnowsMarinaRescue &&
                        MotelLoopController.KnowsLeraRescue, "Rescue order mixed");
                    Require(family.MotherLocation.Contains("в номере 7"), "Past knowledge substituted for mother location");
                });
                ReturnMain(); Enter(7); StartMarina(false); Exit(7);
                Walk(24.5f, 0.65f); Open("Emergency Exit"); RescueLeonid(); RescueVadim();
                Walk(4.8f, 0); Walk(4.8f, -11.8f); Walk(4.8f, -13.6f); Open("Main Entrance");
                Walk(8, -13.6f); Walk(10, -16); Walk(19, -16.5f);
            }
            else if (cycle == 1)
            {
                Diagnose(); Walk(4.8f, -13.6f); Open("Main Entrance"); Walk(4.8f, 0);
                Enter(7); StartMarina(true); Exit(7);
                RescueVadim(); Walk(24.5f, 0.65f); Open("Emergency Exit"); RescueLeonid();
                Walk(25.4f, 0); Walk(29.2f, 0); Walk(29.2f, -12); Walk(25, -16.8f); Walk(19, -16.5f);
                Wait("Marina at parking without Lera", () => family.Marina.CurrentStage == MarinaEvacuee.Stage.Safe);
                Act("parking asks in same cycle", () =>
                {
                    Near(family.Marina); family.Marina.Interact(); Require(Line().Contains("А Леру") &&
                        family.Lera.CurrentStage == LeraEvacuee.Stage.InRoom, "Missing same-cycle search dialogue"); window.Close();
                });
                Walk(25, -16); Walk(28.34f, -12); Walk(28.34f, 0); Walk(25.4f, 0); Enter(8);
                Act("mother truly outside", () => { Require(family.MotherLocation.Contains("у места сбора"), "Mother location"); StartLera(); });
                Exit(8); Walk(25.4f, 0); Walk(28.34f, 0); Walk(28.34f, -12); Walk(25, -16.0f);
                Walk(18, -16.4f); Walk(18, -15.8f);
            }
            else
            {
                // No key or conversation is required to enter 8 through 7.
                Walk(1.55f, -11.4f); Walk(4.8f, -11.4f); Walk(4.8f, 0); Enter(7);
                var r7 = Room(7); var r8 = Room(8);
                Walk(r7.X(2.5f), r7.Z(3.25f)); Walk(r7.X(2.5f), r7.Z(4.25f)); Walk(r7.X(4), r7.Z(4.25f));
                Open("Connection 7-8"); Walk(r8.X(0.5f), r8.Z(4.25f)); Walk(r8.X(0.5f), r8.Z(4.82f));
                Walk(r8.X(2.09f), r8.Z(4.82f)); Walk(r8.X(2.09f), r8.Z(3.25f)); Walk(r8.X(1.15f), r8.Z(3.25f));
                Act("inside opens without keys", () => { Require(!family.HasKeys && Door("Room8").GetComponent<RoomEightLock>().CanOpen, "Inside release"); StartLera(); });
                Walk(r8.X(1.15f), r8.Z(1.7f)); Walk(r8.X(0.8f), r8.Z(1.7f)); Open("Room8"); Walk(r8.X(0.8f), 0);
                Act("allow distance test", () => pace = false); Walk(4.8f, 0);
                Wait("Lera waits for distant player", () => family.Lera.WaitingForPlayer);
                steps.Add(new Step { Name = "return close to Lera", Tick = () =>
                {
                    Vector3 delta = family.Lera.transform.position - player.transform.position; delta.y = 0;
                    if (delta.magnitude <= 3.5f) return true;
                    player.Move(delta.normalized * Mathf.Min(3 * Time.deltaTime, delta.magnitude)); return false;
                }});
                Wait("Lera rejoins", () => !family.Lera.WaitingForPlayer);
                Act("reset during following", () => { Require(family.Lera.CurrentStage == LeraEvacuee.Stage.Following, "Lera stopped following"); BeginReload(); });
                return;
            }
            Wait("four physically safe", AllSafe);
            Act("timed completion", () =>
            {
                File.AppendAllText("Logs/Family-PlayChecks-progress.txt", "ROUTE COMPLETED cycle=" + cycle + " activeSeconds=" + loop.Elapsed.ToString("F2") + "\n");
                Require(loop.Elapsed < 360 && !loop.Transitioning && family.MotherLocation.Contains("у места сбора"), "Four-guest timing/ending");
                var store = Store(); Require(store.HasMarinaFact && store.HasLeraFact && store.HasLeonidFact && store.HasVadimFact && store.Facts.Length == 7, "Rescue facts not independent");
            });
            Walk(19, -16.5f);
            Act("parking reunion", () => { Near(family.Marina); family.Marina.Interact(); Require(Line().Contains("вижу"), "Marina ignores present Lera"); window.Close(); });
            if (cycle == 0 && waitTimed)
                Act("wait for normal 20:22 accident after four rescues", () =>
                { oldLoop = loop; reloading = true; reloadStart = EditorApplication.timeSinceStartup + 360 - loop.Elapsed; });
            else Act("one accident after four rescues", BeginReload);
        }

        private static void Diagnose()
        {
            Walk(1.55f, -11.4f); Walk(4.8f, -11.4f); Walk(4.8f, -9.2f); Open("Technical");
            Walk(2.7f, -9.2f); Walk(2.3f, -8.3f); Close("Technical");
            Act("diagnostic world action", () => { var panel = Object.FindAnyObjectByType<EquipmentInspection>(); Near(panel); panel.Interact(); window.Close(); Require(loop.EquipmentInspectedThisCycle, "Fresh diagnostics"); });
            Open("Technical"); Walk(2.7f, -9.2f); Walk(4.8f, -9.2f); Walk(4.8f, 0);
        }
        private static void StartMarina(bool getKeys)
        {
            Act("ask about daughter", () =>
            {
                Near(family.Marina); family.Marina.Interact(); window.Choose(1);
                Require(Line().Contains("спальное место") && MotelLoopController.KnowsFamilyPlacement, "Placement explanation");
                window.Choose(1); if (getKeys) { Require(family.HasKeys, "Marina keys"); window.Choose(0); }
                family.Marina.Interact(); window.Choose(0);
                Require(family.Marina.CurrentStage == MarinaEvacuee.Stage.InRoom, "Marina left before confirmation");
                window.Choose(0); Require(family.Marina.CurrentStage == MarinaEvacuee.Stage.Evacuating, "Marina did not agree");
            });
        }
        private static void StartLera()
        {
            Near(family.Lera); family.Lera.Interact(); window.Choose(0);
            Require(window.Speaker == "Лера" && window.Topic == "Покажите выход" && !Line().Contains("Твоя мама"),
                "Lera spoke an administrator's report");
            window.Choose(0); Require(family.Lera.CurrentStage == LeraEvacuee.Stage.Following, "Lera follow");
            family.Lera.Interact(); Require(window.IsOpen && family.Lera.CurrentStage == LeraEvacuee.Stage.Following, "Repeat follow dialogue"); window.Close();
        }
        private static void RescueVadim()
        {
            Enter(2); Act("convince Vadim", () => { var actor = Object.FindAnyObjectByType<VadimEvacuee>(); Near(actor); actor.Interact(); window.Choose(0); Require(actor.CurrentStage == VadimEvacuee.Stage.Evacuating, "Vadim regression"); }); Exit(2);
        }
        private static void RescueLeonid()
        {
            Enter(4); var r = Room(4); Walk(r.X(1.75f), r.Z(4.9f));
            Act("Leonid and cane", () =>
            {
                var cane = Object.FindAnyObjectByType<CanePickup>(); Near(cane); cane.Interact();
                var actor = Object.FindAnyObjectByType<LeonidEvacuee>(); Near(actor); actor.Interact(); window.Choose(0); window.Choose(0);
                Require(actor.CurrentStage == LeonidEvacuee.Stage.Evacuating, "Leonid regression");
            });
            Walk(r.X(1.15f), r.Z(3.25f)); Exit(4);
        }
        private static void Enter(int number)
        {
            var r = Room(number); Walk(r.X(0.8f), 0); Open("Room" + number);
            Walk(r.X(0.8f), r.Z(1.7f)); Walk(r.X(1.15f), r.Z(1.7f)); Walk(r.X(1.15f), r.Z(3.25f));
        }
        private static void Exit(int number)
        {
            var r = Room(number); Walk(r.X(1.15f), r.Z(1.7f)); Walk(r.X(0.8f), r.Z(1.7f)); Open("Room" + number); Walk(r.X(0.8f), 0);
        }
        private static void ConnectionFromEight()
        {
            var r8 = Room(8); var r7 = Room(7);
            // Walk around Lera and the open leaf rather than asking the player to walk through her.
            Walk(r8.X(2.09f), r8.Z(3.25f)); Open("Connection 7-8");
            Walk(r8.X(2.09f), r8.Z(4.82f)); Walk(r8.X(0.5f), r8.Z(4.82f)); Walk(r8.X(0.5f), r8.Z(4.25f));
            Walk(r7.X(3), r7.Z(4.25f)); Walk(r7.X(2.5f), r7.Z(3.25f)); Walk(r7.X(1.15f), r7.Z(3.25f));
        }
        private static void LeadMain(bool testDoor)
        {
            Walk(4.8f, 0); Walk(4.8f, -11.8f); Walk(4.8f, -13.6f); Open("Main Entrance"); Walk(8, -13.6f);
            if (testDoor)
            {
                Act("close gate behind player", () => Door("Main Entrance").Toggle());
                Wait("Lera stopped by closed main door", () => family.Lera.WaitingForPath && family.Lera.transform.position.x < 6);
                Open("Main Entrance");
            }
            Walk(10, -16); Walk(19.5f, -16.4f);
        }
        private static void ReturnMain()
        { Walk(10, -16); Walk(8, -13.6f); Walk(4.8f, -13.6f); Walk(4.8f, -11.8f); Walk(4.8f, 0); }
        private static void Open(string name)
        {
            Act("open " + name, () => { var door = Door(name); Require(Vector3.Distance(player.transform.position + Vector3.up, door.transform.position + Vector3.up) < 2.5f, "Door too far: " + name); if (!WantsOpen(door)) door.Toggle(); });
            Wait("swing " + name, () => Door(name).IsFullyOpen);
        }
        private static void Close(string name)
        {
            Act("close " + name, () => { if (WantsOpen(Door(name))) Door(name).Toggle(); });
            Wait("closed " + name, () => Mathf.Abs((float)typeof(PrototypeDoor).GetField("currentAngle", Private).GetValue(Door(name))) < 0.5f);
        }
        private static void Walk(float x, float z)
        {
            var destination = new Vector3(x, 0, z);
            steps.Add(new Step { Name = "walk " + x + "," + z, Tick = () =>
            {
                Vector3 delta = destination - player.transform.position; delta.y = 0;
                if (delta.magnitude < 0.07f) return true;
                if (pace && family.Lera.CurrentStage == LeraEvacuee.Stage.Following &&
                    Vector3.Distance(player.transform.position, family.Lera.transform.position) > 4.5f) return false;
                player.Move(delta.normalized * Mathf.Min(3 * Time.deltaTime, delta.magnitude));
                return false;
            }});
        }
        private static void Act(string name, Action action) => steps.Add(new Step { Name = name, Tick = () => { action(); return true; } });
        private static void Wait(string name, Func<bool> condition) => steps.Add(new Step { Name = name, Tick = condition });
        private static bool AllSafe() => family.Marina.CurrentStage == MarinaEvacuee.Stage.Safe && family.Lera.CurrentStage == LeraEvacuee.Stage.Safe &&
            Object.FindAnyObjectByType<VadimEvacuee>().CurrentStage == VadimEvacuee.Stage.Safe && Object.FindAnyObjectByType<LeonidEvacuee>().CurrentStage == LeonidEvacuee.Stage.Safe;
        private static void BeginReload()
        {
            oldLoop = loop; Require(loop.RequestAccident(false) && !loop.RequestAccident(true) && !window.IsOpen, "One shared accident");
            reloading = true; reloadStart = EditorApplication.timeSinceStartup;
        }
        private static void CheckReset()
        {
            Require(!family.HasKeys && !loop.EquipmentInspectedThisCycle && !window.IsOpen &&
                family.Marina.CurrentStage == MarinaEvacuee.Stage.InRoom && family.Lera.CurrentStage == LeraEvacuee.Stage.InRoom &&
                Object.FindAnyObjectByType<LeonidEvacuee>().CurrentStage == LeonidEvacuee.Stage.Sleeping &&
                Object.FindAnyObjectByType<VadimEvacuee>().CurrentStage == VadimEvacuee.Stage.Skeptical, "Cycle states not reset");
            Require(!WantsOpen(Door("Room8")) && !WantsOpen(Door("Main Entrance")) && !WantsOpen(Door("Connection 7-8")), "Doors not restored");
            Require(Object.FindObjectsByType<MarinaEvacuee>().Length == 1 && Object.FindObjectsByType<LeraEvacuee>().Length == 1 &&
                Object.FindObjectsByType<RoomKeysPickup>().Length == 1 && Object.FindObjectsByType<DialogueController>().Length == 1 &&
                Store().Facts.Length == 7, "Copies, missing duplicate keys or lost knowledge");
            var r7 = Room(7); var r8 = Room(8);
            Require(Vector3.Distance(family.Marina.transform.position, new Vector3(r7.X(1.8f), 0.05f, r7.Z(4))) < 0.15f &&
                Vector3.Distance(family.Lera.transform.position, new Vector3(r8.X(1.4f), 0.05f, r8.Z(4))) < 0.15f, "Family positions not restored");
            var store = Store(); Require(!store.RememberMarinaRescue() && !store.RememberLeraRescue() && !store.RememberFamilyPlacement(), "Duplicate knowledge");
        }
        private static void Near(Component actor)
        {
            var collider = actor.GetComponentsInChildren<Collider>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            Vector3 from = player.transform.position + Vector3.up * 1.55f, to = collider.bounds.center;
            bool hitTarget = Physics.Raycast(from, (to - from).normalized, out var hit, 2.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Require(Vector3.Distance(from, to) <= 2.5f && hitTarget && hit.collider.GetComponentInParent(actor.GetType()) == actor,
                "Interaction range/first solid hit: " + actor.name + "; player=" + player.transform.position + "; hit=" + (hitTarget ? hit.collider.name : "none"));
        }
        private static MotelGuestRoomModule.Layout Room(int n) => MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == n);
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static bool WantsOpen(PrototypeDoor door) => (bool)typeof(PrototypeDoor).GetField("wantsOpen", Private).GetValue(door);
        private static string Line() => (string)typeof(DialogueController).GetField("line", Private).GetValue(window);
        private static ObservationStore Store() => new ObservationStore(SessionState.GetString(Prefix + "Save", ""));
        private static void Resume() => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { false });
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
            File.WriteAllText("Logs/Family-PlayChecks.txt", text);
            if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text);
            EditorApplication.isPlaying = false;
        }
    }
}
