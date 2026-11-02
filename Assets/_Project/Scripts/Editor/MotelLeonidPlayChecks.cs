using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    // Runs the real scene over frames and reloads. Knowledge is redirected to Temp, never to the user's save.
    [InitializeOnLoad]
    public static class MotelLeonidPlayChecks
    {
        private const string Running = "Motel.LeonidChecks.Running", Prefix = "Motel.LeonidChecks.";
        private static int phase;
        private static MotelLoopController oldLoop;
        private static bool initialized;
        private static double since, elapsed, nextReport;
        private static Vector3 start;
        private static string report;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        static MotelLeonidPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                if (SessionState.GetBool(Prefix + "RestoreBackground", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "RestoreBackground", false);
                }
                if (!SessionState.GetBool(Running, false)) return;
                SessionState.SetBool(Running, false);
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                initialized = false;
                File.WriteAllText("Logs/Leonid-PlayChecks.txt", "CANCELLED: Play Mode was stopped before the test completed.");
            };
        }

        [MenuItem("Motel/Run Automated Leonid Play Checks")]
        public static void Begin()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath)
                throw new InvalidOperationException("Open the saved MotelBlockout outside Play Mode first.");
            string file = Path.GetFullPath("Temp/LeonidPlay-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(file, "{\"version\":1,\"cutoffCausedExplosion\":true}");
            SessionState.SetString(Prefix + "Save", file);
            SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "RestoreBackground", true);
            initialized = false;
            phase = 0;
            nextReport = 0;
            SessionState.SetBool(Running, true);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || EditorApplication.isCompiling) return;
            if (!EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                var loop = Object.FindAnyObjectByType<MotelLoopController>();
                var npc = Object.FindAnyObjectByType<LeonidEvacuee>();
                var window = Object.FindAnyObjectByType<DialogueController>();
                var cane = Object.FindAnyObjectByType<CanePickup>();
                if (loop == null || npc == null || window == null) return;
                double now = EditorApplication.timeSinceStartup;
                if (!initialized)
                {
                    if (Time.frameCount < 4) return; // Let scene Start and the Game View focus settle first.
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
                    // Keep the test player clear of the NPC route. Only the test player is repositioned.
                    MoveTestPlayer();
                    Resume(loop);
                    npc.Interact();
                    Require(npc.CurrentStage == LeonidEvacuee.Stage.Awake && window.IsOpen, "Wake dialogue");
                    elapsed = loop.Elapsed;
                    initialized = true; report = "Wake via episode handler; "; SetPhase(1);
                }
                // Test automation can run while IDE tools steal focus. Keep the normal focus-pause
                // behaviour in the game; resume only this test, except the deliberate pause assertion.
                if (phase != 2 && loop.IsPaused) Resume(loop);
                if (now >= nextReport)
                {
                    nextReport = now + 2;
                    File.AppendAllText("Logs/Leonid-PlayChecks-progress.txt", "phase=" + phase + ", clock=" + loop.Elapsed +
                        ", point=" + npc.Waypoint + ", position=" + npc.transform.position + ", waiting=" + npc.WaitingForPath +
                        ", blocker=" + npc.BlockingObject + "\n");
                }
                if (now - since > 85) throw new Exception("Timed out at phase " + phase + ": " + npc.CurrentStage +
                    ", waypoint=" + npc.Waypoint + ", waiting=" + npc.WaitingForPath + ", position=" + npc.transform.position +
                    ", blocker=" + npc.BlockingObject +
                    ", elapsed=" + loop.Elapsed + ", frame=" + Time.frameCount + ", paused=" + loop.IsPaused + ", dialogue=" + window.IsOpen);

                switch (phase)
                {
                    case 1:
                        if (now - since < 0.5) return;
                        Require(loop.Elapsed > elapsed + 0.15 && Time.timeScale == 1,
                            "Dialogue must not pause time: elapsed=" + loop.Elapsed + ", paused=" + loop.IsPaused + ", frames=" + Time.frameCount);
                        Require(!Object.FindAnyObjectByType<FirstPersonController>().HasControl, "Dialogue movement blocked");
                        Pause(loop); elapsed = loop.Elapsed; SetPhase(2); break;
                    case 2:
                        if (now - since < 0.35) return;
                        Require(loop.Elapsed == elapsed, "Pause during dialogue moved time");
                        window.Choose(0);
                        Require(npc.CurrentStage == LeonidEvacuee.Stage.Awake, "Paused choice executed");
                        Resume(loop); window.Choose(0);
                        Require(npc.CurrentStage == LeonidEvacuee.Stage.NeedsCane && window.ChoiceCount == 1, "Warning and cane request");
                        window.Choose(0); npc.Interact();
                        Require(window.ChoiceCount == 1 && npc.CurrentStage == LeonidEvacuee.Stage.NeedsCane, "Repeat conversation reset intro");
                        window.Close(); cane.Interact();
                        Require(npc.CarryingCane && !cane.gameObject.activeSelf, "Pickup cane after request");
                        npc.Interact(); window.Choose(0);
                        Require(npc.CurrentStage == LeonidEvacuee.Stage.Evacuating && !npc.CarryingCane && !window.IsOpen, "Give cane and depart");
                        Require(!MotelLoopController.KnowsLeonidRescue, "Rescued before safe arrival");
                        report += "dialogue clock, pause, repeat talk, normal cane order; "; SetPhase(3); break;
                    case 3:
                        if (!npc.WaitingForPath || npc.Waypoint != 4) return;
                        start = npc.transform.position; SetPhase(4); break;
                    case 4:
                        if (now - since < 0.5) return;
                        Require(Vector3.Distance(start, npc.transform.position) < 0.05f && npc.transform.position.z > 1.2f, "Walked through closed room door");
                        Door("Room4").Toggle();
                        report += "waits at closed room door; "; SetPhase(5); break;
                    case 5:
                        if (!npc.WaitingForPath || npc.Waypoint != 6) return;
                        Require(npc.transform.position.x < MotelBlockoutPlan.WingEnd, "Walked through emergency exit");
                        Door("Emergency Exit").Toggle();
                        report += "resumed after opening room, waits at exit; "; SetPhase(6); break;
                    case 6:
                        if (npc.CurrentStage != LeonidEvacuee.Stage.Safe)
                        {
                            Require(!MotelLoopController.KnowsLeonidRescue, "Fact before arrival");
                            return;
                        }
                        Require(Vector2.Distance(new Vector2(npc.transform.position.x, npc.transform.position.z),
                            new Vector2(18, -18.1f)) < 0.45f, "Rescued outside safe point");
                        Require(MotelLoopController.KnowsLeonidRescue && MotelLoopController.KnowsCutoff, "Rescue/prior fact lost");
                        var saved = new ObservationStore(SessionState.GetString(Prefix + "Save", ""));
                        Require(saved.HasLeonidFact && saved.HasCutoffFact && !saved.RememberLeonidRescue(), "Rescue save/dedup");
                        var clock = (AttemptClock)typeof(MotelLoopController).GetField("clock", Private).GetValue(loop);
                        clock.Advance(360, false, false);
                        oldLoop = loop;
                        Require(loop.RequestAccident(false) && !loop.RequestAccident(true), "Timer/cutoff double transition after rescue");
                        report += "physical complete route and saved rescue; timer still explodes once after rescue; "; SetPhase(7); break;
                    case 7:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        VerifyReset(loop, npc); MoveTestPlayer(); Resume(loop);
                        cane.Interact();
                        Require(npc.CarryingCane && npc.CurrentStage == LeonidEvacuee.Stage.Sleeping, "Early cane pickup");
                        npc.Interact(); window.Choose(0); window.Choose(0);
                        Require(npc.CurrentStage == LeonidEvacuee.Stage.Evacuating && !window.IsOpen, "Early cane sequence");
                        start = npc.transform.position;
                        report += "reset player/doors/world/cane; cane before wake; "; SetPhase(8); break;
                    case 8:
                        if (Vector3.Distance(npc.transform.position, start) < 0.4f) return;
                        oldLoop = loop;
                        Require(loop.RequestAccident(true) && !loop.RequestAccident(false), "Early accident during evacuation");
                        SetPhase(9); break;
                    case 9:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        VerifyReset(loop, npc); MoveTestPlayer(); Resume(loop);
                        npc.Interact(); Require(window.IsOpen, "Second wake dialogue");
                        oldLoop = loop;
                        Require(loop.RequestAccident(false) && !window.IsOpen && !loop.RequestAccident(true), "Reset closes dialogue once");
                        report += "reset during moving evacuation and dialogue; "; SetPhase(10); break;
                    case 10:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        VerifyReset(loop, npc);
                        Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed during checks");
                        Finish("PASS: " + report + "three reloads, no episode duplicates, prior and rescue facts persist. User save unchanged. Automated handlers/physics; manual input acceptance still required.");
                        break;
                }
            }
            catch (Exception exception) { Finish("FAIL: " + exception); }
        }

        private static void VerifyReset(MotelLoopController loop, LeonidEvacuee npc)
        {
            Require(npc.CurrentStage == LeonidEvacuee.Stage.Sleeping && !npc.CarryingCane && npc.Waypoint == 0, "NPC/cane state did not reset");
            Require(Object.FindObjectsByType<LeonidEvacuee>().Length == 1 &&
                Object.FindObjectsByType<CanePickup>().Length == 1 &&
                Object.FindObjectsByType<DialogueController>().Length == 1, "Episode duplicates/missing cane");
            Require(!Object.FindAnyObjectByType<DialogueController>().IsOpen && loop.Elapsed < 2, "Dialogue/time not reset");
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            Require(Vector2.Distance(new Vector2(player.transform.position.x, player.transform.position.z), new Vector2(1.55f, -13.6f)) < 0.05f &&
                Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 90)) < 0.1f, "Player position/rotation reset");
            Require(!(bool)typeof(PrototypeDoor).GetField("wantsOpen", Private).GetValue(Door("Room4")) &&
                !(bool)typeof(PrototypeDoor).GetField("wantsOpen", Private).GetValue(Door("Emergency Exit")), "Door state not reset");
            Require(!GameObject.Find("Motel Blockout/Time Loop").transform.Find("Prepared Damage").gameObject.activeSelf &&
                GameObject.Find("Motel Blockout/Furniture/Technical Equipment") != null, "Environment not reset");
            Require(MotelLoopController.KnowsLeonidRescue && MotelLoopController.KnowsCutoff, "Knowledge lost after reload");
        }

        private static void SetPhase(int value)
        {
            phase = value; since = EditorApplication.timeSinceStartup;
            File.AppendAllText("Logs/Leonid-PlayChecks-progress.txt", DateTime.UtcNow.ToString("O") + " phase " + value + "\n");
        }
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static void Pause(MotelLoopController loop) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { true });
        private static void Resume(MotelLoopController loop) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { false });
        private static void MoveTestPlayer()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false; player.transform.position = new Vector3(10, 0.05f, -19); cc.enabled = true;
        }
        private static string UserHash()
        {
            string path = Path.Combine(Application.persistentDataPath, "observations-v1.json");
            if (!File.Exists(path)) return "ABSENT";
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Require(bool value, string text) { if (!value) throw new Exception(text); }
        private static void Finish(string text)
        {
            SessionState.SetBool(Running, false);
            Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
            File.WriteAllText("Logs/Leonid-PlayChecks.txt", text);
            if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text);
            initialized = false;
            EditorApplication.isPlaying = false;
        }
    }
}
