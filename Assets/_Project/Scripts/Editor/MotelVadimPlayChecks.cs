using System;
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
    public static class MotelVadimPlayChecks
    {
        private const string Prefix = "Motel.VadimChecks.", Running = Prefix + "Running";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool initialized, separateArrivalSeen;
        private static int phase;
        private static double since, nextReport, clockSample;
        private static Vector3 sampleV, sampleL;
        private static MotelLoopController oldLoop;
        private static GameObject barrier;

        static MotelVadimPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.EnteredEditMode) return;
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (!SessionState.GetBool(Running, false)) return;
                SessionState.SetBool(Running, false);
                initialized = false;
                File.WriteAllText("Logs/Vadim-PlayChecks.txt", "CANCELLED: Play Mode stopped before completion.");
            };
        }

        [MenuItem("Motel/Run Automated Joint Evacuation Checks")]
        public static void Begin()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath) throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            string path = Path.GetFullPath("Temp/VadimPlay-" + Guid.NewGuid().ToString("N") + ".json");
            // An old observation must not provide fresh evidence in this attempt.
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true,\"equipmentOverheatInspected\":true}");
            SessionState.SetString(Prefix + "Save", path);
            SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true);
            initialized = separateArrivalSeen = false; phase = 0; nextReport = 0;
            SessionState.SetBool(Running, true);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Running, false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                var loop = Object.FindAnyObjectByType<MotelLoopController>();
                var vadim = Object.FindAnyObjectByType<VadimEvacuee>();
                var leonid = Object.FindAnyObjectByType<LeonidEvacuee>();
                var window = Object.FindAnyObjectByType<DialogueController>();
                if (loop == null || vadim == null || leonid == null || window == null) return;
                double now = EditorApplication.timeSinceStartup;
                if (!initialized)
                {
                    if (Time.frameCount < 4) return;
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
                    MovePlayer(); Resume(loop);
                    CheckFreshTalk(loop, vadim, window);
                    window.Choose(0);
                    Require(vadim.CurrentStage == VadimEvacuee.Stage.NeedsEvidence && window.IsOpen,
                        "Generic warning incorrectly evacuates Vadim");
                    window.Choose(0); vadim.Interact();
                    Require(vadim.CurrentStage == VadimEvacuee.Stage.NeedsEvidence && window.ChoiceCount == 1,
                        "Repeated talk lost request");
                    window.Close();
                    Object.FindAnyObjectByType<EquipmentInspection>().Interact();
                    Require(loop.EquipmentInspectedThisCycle && MotelLoopController.KnowsEquipmentOverheat && !MotelLoopController.KnowsVadimRescue &&
                        !MotelLoopController.KnowsLeonidRescue, "Inspection mixed knowledge flags");
                    window.Choose(0); vadim.Interact(); window.Choose(0);
                    Require(vadim.CurrentStage == VadimEvacuee.Stage.Evacuating && !window.IsOpen, "Evidence did not convince Vadim");
                    StartLeonid(leonid, window);
                    barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    barrier.name = "Test Route Barrier";
                    barrier.transform.position = new Vector3(9.5f, 1.1f, 0);
                    barrier.transform.localScale = new Vector3(0.2f, 2.2f, 2.1f);
                    barrier.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/BlockoutMaterials/Equipment.mat");
                    initialized = true; SetPhase(1);
                }
                if (loop.IsPaused) Resume(loop); // Only automation: normal focus pause remains in the game.
                if (now - since > 85) throw new Exception("Timeout phase=" + phase + "; Vadim=" + vadim.CurrentStage +
                    " point=" + vadim.Waypoint + " pos=" + vadim.transform.position + " blocked=" + vadim.BlockingObject +
                    "; Leonid=" + leonid.CurrentStage + " point=" + leonid.Waypoint + " pos=" + leonid.transform.position + " blocked=" + leonid.BlockingObject);
                if (now >= nextReport)
                {
                    nextReport = now + 2;
                    File.AppendAllText("Logs/Vadim-PlayChecks-progress.txt", "phase=" + phase + " time=" + loop.Elapsed +
                        " V=" + vadim.CurrentStage + "/" + vadim.Waypoint + "/" + vadim.transform.position + "/" + vadim.BlockingObject +
                        " L=" + leonid.CurrentStage + "/" + leonid.Waypoint + "/" + leonid.transform.position + "/" + leonid.BlockingObject + "\n");
                }
                switch (phase)
                {
                    case 1:
                        if (!vadim.WaitingForPath || vadim.Waypoint != 4 || !leonid.WaitingForPath || leonid.Waypoint != 4) return;
                        sampleV = vadim.transform.position; sampleL = leonid.transform.position; SetPhase(2); break;
                    case 2:
                        if (now - since < 0.4) return;
                        Require(Vector3.Distance(sampleV, vadim.transform.position) < 0.06f &&
                            Vector3.Distance(sampleL, leonid.transform.position) < 0.06f, "Closed room doors crossed");
                        Door("Room2").Toggle(); Door("Room4").Toggle(); Door("Emergency Exit").Toggle();
                        SetPhase(3); break;
                    case 3:
                        if (!vadim.WaitingForPath || vadim.BlockingObject != "Test Route Barrier") return;
                        sampleV = vadim.transform.position; SetPhase(4); break;
                    case 4:
                        if (now - since < 0.4) return;
                        Require(Vector3.Distance(sampleV, vadim.transform.position) < 0.06f, "Vadim crossed solid route barrier");
                        Object.Destroy(barrier); barrier = null; SetPhase(5); break;
                    case 5:
                        if (!vadim.WaitingForPath || vadim.Waypoint != 8) return;
                        Require(vadim.transform.position.x < 6, "Main entrance crossed while closed");
                        Door("Main Entrance").Toggle(); SetPhase(6); break;
                    case 6:
                        bool safeV = vadim.CurrentStage == VadimEvacuee.Stage.Safe;
                        bool safeL = leonid.CurrentStage == LeonidEvacuee.Stage.Safe;
                        Require(MotelLoopController.KnowsVadimRescue == safeV && MotelLoopController.KnowsLeonidRescue == safeL,
                            "Rescue flags mixed or learned before arrival");
                        if (safeV && !safeL) separateArrivalSeen = true;
                        if (!safeV || !safeL) return;
                        Require(separateArrivalSeen, "Independent arrival checkpoint not observed");
                        CheckArrival(loop, vadim, leonid);
                        oldLoop = loop;
                        Require(loop.RequestAccident(true) && !loop.RequestAccident(false), "Double accident after two rescues");
                        SetPhase(7); break;
                    case 7:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        CheckReset(loop, vadim, leonid, window);
                        MovePlayer(); Resume(loop);
                        foreach (string name in new[] { "Room2", "Room4", "Main Entrance", "Emergency Exit" }) Door(name).Toggle();
                        StartLeonid(leonid, window); SetPhase(8); break;
                    case 8:
                        if (now - since < 1) return;
                        sampleL = leonid.transform.position; clockSample = loop.Elapsed;
                        CheckFreshTalk(loop, vadim, window);
                        SetPhase(9); break;
                    case 9:
                        if (now - since < 1) return;
                        Require(loop.Elapsed > clockSample + 0.3 && Vector3.Distance(sampleL, leonid.transform.position) > 0.4f,
                            "Vadim dialogue froze time or Leonid");
                        Require(!Object.FindAnyObjectByType<FirstPersonController>().HasControl, "Dialogue did not block player");
                        window.Choose(0);
                        Require(vadim.CurrentStage == VadimEvacuee.Stage.NeedsEvidence && window.IsOpen,
                            "Old observation allowed evacuation without current inspection");
                        window.Close();
                        Object.FindAnyObjectByType<EquipmentInspection>().Interact();
                        window.Close(); vadim.Interact(); window.Choose(0);
                        Require(loop.EquipmentInspectedThisCycle && vadim.CurrentStage == VadimEvacuee.Stage.Evacuating && !window.IsOpen,
                            "Fresh inspection after request did not allow agreement");
                        SetPhase(10); break;
                    case 10:
                        if (vadim.CurrentStage != VadimEvacuee.Stage.Safe || leonid.CurrentStage != LeonidEvacuee.Stage.Safe) return;
                        CheckArrival(loop, vadim, leonid);
                        vadim.Interact(); Require(window.IsOpen, "Repeat safe dialogue");
                        oldLoop = loop;
                        Require(loop.RequestAccident(false) && !window.IsOpen && !loop.RequestAccident(true), "Transition failed to close Vadim dialogue once");
                        SetPhase(11); break;
                    case 11:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        CheckReset(loop, vadim, leonid, window);
                        MovePlayer(); Resume(loop);
                        Object.FindAnyObjectByType<EquipmentInspection>().Interact(); window.Close();
                        Require(loop.EquipmentInspectedThisCycle && vadim.CurrentStage == VadimEvacuee.Stage.Skeptical,
                            "Early inspection changed Vadim conversation state");
                        vadim.Interact();
                        Require(DialogueLine(window) == InitialLine && Choices(window)[0].Text.StartsWith("Диагностика показывает"),
                            "Early inspection must offer proof with initial greeting, without a prior request");
                        window.Choose(0);
                        Require(vadim.CurrentStage == VadimEvacuee.Stage.Evacuating && !window.IsOpen, "Early inspection cannot convince directly");
                        oldLoop = loop; Require(loop.RequestAccident(false), "Reset after early inspection failed");
                        SetPhase(12); break;
                    case 12:
                        if (ReferenceEquals(loop, oldLoop)) return;
                        CheckReset(loop, vadim, leonid, window);
                        Resume(loop); CheckFreshTalk(loop, vadim, window); window.Choose(0);
                        Require(vadim.CurrentStage == VadimEvacuee.Stage.NeedsEvidence, "Request/consent leaked across cycles");
                        window.Close();
                        Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed");
                        Finish("PASS: old observation retained but fresh inspection required; initial greeting after reset; inspection after request and before first talk; repeated talks retain cycle state; three resets clear inspection/request/consent without losing knowledge. Independent rescue facts; closed room/main doors and physical barrier; both guests physically safe in one 360s cycle in both dispatch orders; Leonid moves during Vadim dialogue; no duplicates. User save unchanged. Automated handlers/physics, manual input review still required.");
                        break;
                }
            }
            catch (Exception e) { Finish("FAIL: " + e); }
        }

        private static void StartLeonid(LeonidEvacuee leonid, DialogueController window)
        {
            Object.FindAnyObjectByType<CanePickup>().Interact();
            leonid.Interact(); window.Choose(0); window.Choose(0);
            Require(leonid.CurrentStage == LeonidEvacuee.Stage.Evacuating, "Leonid regression: cane/consent sequence");
        }
        private static void CheckArrival(MotelLoopController loop, VadimEvacuee vadim, LeonidEvacuee leonid)
        {
            Require(loop.Elapsed < 360 && !loop.Transitioning, "Two guests cannot finish in the normal cycle");
            Require(Vector2.Distance(new Vector2(vadim.transform.position.x, vadim.transform.position.z), new Vector2(17, -18.1f)) < 0.45f &&
                Vector2.Distance(new Vector2(leonid.transform.position.x, leonid.transform.position.z), new Vector2(18, -18.1f)) < 0.45f &&
                Vector3.Distance(vadim.transform.position, leonid.transform.position) > 0.85f, "Arrival slots blocked or false rescue");
            var store = new ObservationStore(SessionState.GetString(Prefix + "Save", ""));
            Require(store.Facts.Length == 4 && !store.RememberVadimRescue() && !store.RememberLeonidRescue() &&
                !store.RememberEquipmentOverheat(), "Knowledge persistence/dedup");
        }
        private static void CheckReset(MotelLoopController loop, VadimEvacuee vadim, LeonidEvacuee leonid, DialogueController window)
        {
            Require(vadim.CurrentStage == VadimEvacuee.Stage.Skeptical && leonid.CurrentStage == LeonidEvacuee.Stage.Sleeping &&
                vadim.Waypoint == 0 && leonid.Waypoint == 0 && !leonid.CarryingCane && !window.IsOpen &&
                !loop.EquipmentInspectedThisCycle && loop.Elapsed < 2, "Cycle states did not reset");
            Require(Object.FindObjectsByType<VadimEvacuee>().Length == 1 && Object.FindObjectsByType<LeonidEvacuee>().Length == 1 &&
                Object.FindObjectsByType<EquipmentInspection>().Length == 1 && Object.FindObjectsByType<DialogueController>().Length == 1 &&
                Object.FindObjectsByType<CanePickup>().Length == 1, "Duplicate/missing world objects");
            Require(MotelLoopController.KnowsVadimRescue && MotelLoopController.KnowsLeonidRescue &&
                MotelLoopController.KnowsEquipmentOverheat && MotelLoopController.KnowsCutoff, "Knowledge reset or mixed");
            var room = MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 2);
            Require(Vector3.Distance(vadim.transform.position, new Vector3(room.X(3), 0.05f, room.Z(4))) < 0.15f, "Vadim position not restored");
        }
        private const string InitialLine = "Я с дороги, только устроился. Слышал гул, но хочу отдохнуть. Что случилось?";
        private static string DialogueLine(DialogueController window) => (string)typeof(DialogueController).GetField("line", Private).GetValue(window);
        private static DialogueController.Choice[] Choices(DialogueController window) =>
            (DialogueController.Choice[])typeof(DialogueController).GetField("choices", Private).GetValue(window);
        private static void CheckFreshTalk(MotelLoopController loop, VadimEvacuee vadim, DialogueController window)
        {
            Require(MotelLoopController.KnowsEquipmentOverheat && !loop.EquipmentInspectedThisCycle &&
                vadim.CurrentStage == VadimEvacuee.Stage.Skeptical, "Knowledge and cycle state mixed");
            vadim.Interact();
            Require(window.IsOpen && window.Participant == vadim && DialogueLine(window) == InitialLine &&
                Choices(window).All(c => !c.Text.StartsWith("Диагностика показывает")), "Wrong first greeting or stale readings offered");
        }
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static void Resume(MotelLoopController loop) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { false });
        private static void MovePlayer()
        {
            var player = Object.FindAnyObjectByType<FirstPersonController>();
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false; player.transform.position = new Vector3(10, 0.05f, -23); cc.enabled = true;
        }
        private static void SetPhase(int value) { phase = value; since = EditorApplication.timeSinceStartup; }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static string UserHash()
        {
            string path = Path.Combine(Application.persistentDataPath, "observations-v1.json");
            if (!File.Exists(path)) return "ABSENT";
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Finish(string text)
        {
            SessionState.SetBool(Running, false);
            Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
            File.WriteAllText("Logs/Vadim-PlayChecks.txt", text);
            if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text);
            initialized = false;
            EditorApplication.isPlaying = false;
        }
    }
}
