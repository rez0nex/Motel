using System;
using System.Collections;
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
    public static class MotelFamilyDialogueChecks
    {
        private const string Prefix = "Motel.FamilyDialogue.";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static bool ready, preview, previewOnly;
        private static int frame;
        private static IEnumerator scenario;
        static MotelFamilyDialogueChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ready = false;
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (SessionState.GetBool(Prefix + "Running", false))
                {
                    SessionState.SetBool(Prefix + "Running", false);
                    File.WriteAllText("Logs/Family-DialogueChecks.txt", "CANCELLED before completion.");
                }
            };
        }
        [MenuItem("Motel/Run Family Dialogue Checks")]
        public static void Begin() => Begin(false);
        public static void Begin(bool keepPreview, bool onlyPreview = false)
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath) throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            string path = Path.GetFullPath("Temp/FamilyDialogue-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true,\"equipmentOverheatInspected\":true,\"vadimRescued\":true,\"familyPlacementKnown\":true,\"marinaRescued\":true,\"leraRescued\":true}");
            SessionState.SetString(Prefix + "Save", path); SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true); SessionState.SetBool(Prefix + "Running", true);
            preview = keepPreview; previewOnly = onlyPreview; ready = false; frame = -1; scenario = null;
            File.WriteAllText(previewOnly ? "Logs/Family-DialoguePreview.txt" : "Logs/Family-DialogueChecks.txt", "RUNNING");
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (!ready || !SessionState.GetBool(Prefix + "Running", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                var loop = Object.FindAnyObjectByType<MotelLoopController>(); if (loop == null) return;
                if (loop.IsPaused) typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { false });
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                if (scenario == null)
                {
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
                    scenario = previewOnly ? Preview() : Run();
                }
                if (!scenario.MoveNext()) Finish(previewOnly ? "PASS PREVIEW ONLY: isolated save, Lera question with separate speaker/topic."
                    : "PASS: NPC speech vs player replies, separate speaker/topic; four real mother whereabouts; live refresh and stale-choice guard; no unreported/hidden rescue knowledge; visible reunion; key wording, repeat follow dialogue and reset with seven past facts. Dialogue-state fixtures in Play Mode, not a physical-route timing test. User save unchanged.");
            }
            catch (Exception e) { preview = false; Finish("FAIL: " + e); }
        }
        private static IEnumerator Run()
        {
            var family = Object.FindAnyObjectByType<FamilyEpisode>(); var window = family.Dialogue;
            var marina = family.Marina; var lera = family.Lera;
            Move(family.Player, new Vector3(10, 0.05f, -23));
            foreach (FamilyEpisode.MotherWhereabouts where in Enum.GetValues(typeof(FamilyEpisode.MotherWhereabouts)))
            {
                MotherFixture(family, where);
                Field(lera, "heardMotherReport", false);
                lera.Interact(); window.Choose(1); Question(family);
                Require(!lera.HasMotherReport, "Lera learned before player spoke");
                window.Choose(0);
                Require(lera.HasMotherReport && window.Speaker == "Лера" && window.Topic == "Мама" &&
                    !Line(window).Contains("Твоя мама"), "Administrator report attributed to NPC");
                Record(where + ": player=" + family.MotherLocation + " | Lera=" + Line(window));
                window.Choose(1);
            }
            MotherFixture(family, FamilyEpisode.MotherWhereabouts.LeavingBuilding);
            Field(lera, "heardMotherReport", false);
            lera.Interact(); window.Choose(1);
            MotherFixture(family, FamilyEpisode.MotherWhereabouts.Courtyard); yield return null;
            Question(family); // NPC update refreshes an open question when Marina leaves the building.
            MotherFixture(family, FamilyEpisode.MotherWhereabouts.Safe);
            window.Choose(0); // Deliberately click a stale choice before the next frame.
            Require(!lera.HasMotherReport, "Stale report delivered"); Question(family);
            window.Choose(0); Require(lera.HasMotherReport, "Current report not delivered"); window.Choose(1);

            MotherFixture(family, FamilyEpisode.MotherWhereabouts.InRoom);
            Move(family.Player, lera.transform.position + new Vector3(1, 0, 0));
            lera.Interact(); window.Choose(0);
            Require(window.Speaker == "Лера" && window.Topic == "Покажите выход" && !Line(window).Contains("Твоя мама"), "Warn speaker");
            window.Choose(0); lera.Interact();
            Require(lera.CurrentStage == LeraEvacuee.Stage.Following && !Line(window).Contains("Твоя мама"), "Repeat follow dialogue");
            window.Close();

            Move(family.Player, new Vector3(10, 0.05f, -23));
            MotherFixture(family, FamilyEpisode.MotherWhereabouts.LeavingBuilding);
            Move(marina.transform, new Vector3(21, 0.05f, 0));
            Require(!family.CanSee(marina, lera), "Vision passes through residential walls");
            LeraFixture(lera, LeraEvacuee.Stage.Safe, new Vector3(17.5f, 0.05f, -17));
            MotherFixture(family, FamilyEpisode.MotherWhereabouts.InRoom); Field(marina, "knowsLeraSafe", false);
            marina.Interact();
            Require(!marina.KnowsLeraIsSafe && !Line(window).Contains("Лера уже"), "Marina knows an unseen rescue");
            Require(Choices(window)[0].Text.StartsWith("Лера уже"), "Player cannot report actual rescue");
            window.Choose(0); Require(marina.KnowsLeraIsSafe && Line(window).Contains("сказали"), "Marina did not hear player report");
            window.Choose(0); Require(marina.CurrentStage == MarinaEvacuee.Stage.Evacuating, "Reported early rescue blocks evacuation");
            window.Close();

            MotherFixture(family, FamilyEpisode.MotherWhereabouts.InRoom); Field(marina, "knowsLeraSafe", false);
            marina.Interact(); window.Choose(1);
            Require(Line(window).Contains("дочери дали") && !Line(window).Contains("Лера сейчас"), "Placement claims current whereabouts");
            window.Choose(1); Require(family.HasKeys && window.Speaker == "Марина" && window.Topic == "Ключи" &&
                Line(window).StartsWith("Вот мой комплект"), "Key handover isn't Marina speech"); window.Close();

            MotherFixture(family, FamilyEpisode.MotherWhereabouts.Safe); Field(marina, "knowsLeraSafe", false);
            LeraFixture(lera, LeraEvacuee.Stage.InRoom, new Vector3(22.96f, 0.05f, -5.28f));
            marina.Interact(); Require(Line(window).Contains("А Леру") && !marina.KnowsLeraIsSafe, "Past knowledge replaced current search"); window.Close();
            LeraFixture(lera, LeraEvacuee.Stage.Safe, new Vector3(22.96f, 0.05f, -5.28f)); // Hidden behind the building.
            marina.Interact(); Require(Line(window).Contains("не вижу") && !marina.KnowsLeraIsSafe, "Sees through walls");
            window.Choose(0); Require(marina.KnowsLeraIsSafe && Line(window).Contains("сообщили") && !Line(window).Contains("Вижу"), "Hidden rescue report"); window.Close();
            Field(marina, "knowsLeraSafe", false);
            LeraFixture(lera, LeraEvacuee.Stage.Following, new Vector3(17.5f, 0.05f, -17));
            marina.Interact(); Require(Line(window).Contains("идёт за вами") && !marina.KnowsLeraIsSafe, "Seeing follow counts as rescue"); window.Close();
            LeraFixture(lera, LeraEvacuee.Stage.Safe, new Vector3(17.5f, 0.05f, -17));
            marina.Interact(); Require(Line(window).Contains("вижу") && marina.KnowsLeraIsSafe, "Visible reunion missing"); window.Close();

            var oldLoop = family.Loop;
            Require(oldLoop.RequestAccident(false) && !oldLoop.RequestAccident(true), "Duplicate reset");
            while (ReferenceEquals(oldLoop, Object.FindAnyObjectByType<MotelLoopController>())) yield return null;
            family = Object.FindAnyObjectByType<FamilyEpisode>(); lera = family.Lera; marina = family.Marina;
            Require(!lera.HasMotherReport && !marina.KnowsLeraIsSafe && !family.HasKeys &&
                lera.CurrentStage == LeraEvacuee.Stage.InRoom && marina.CurrentStage == MarinaEvacuee.Stage.InRoom,
                "Dialogue memory survived reset");
            Require(new ObservationStore(SessionState.GetString(Prefix + "Save", "")).Facts.Length == 7 &&
                UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "Past knowledge lost or user save changed");
            if (preview)
            {
                Move(family.Player, new Vector3(22.71f, 0.05f, -4.53f));
                var camera = family.Player.GetComponentInChildren<Camera>();
                camera.transform.LookAt(lera.transform.position + Vector3.up * 1.3f);
                lera.Interact(); family.Dialogue.Choose(1); Question(family);
                EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
                yield return null;
            }
        }
        private static void Question(FamilyEpisode family)
        {
            var w = family.Dialogue;
            Require(w.Speaker == "Лера" && w.Topic == "Мама" && Line(w) == "А мама? Она тоже выходит?" &&
                Choices(w)[0].Text == family.MotherLocation, "Question/report ownership or whereabouts");
        }
        private static IEnumerator Preview()
        {
            var family = Object.FindAnyObjectByType<FamilyEpisode>();
            Move(family.Player, new Vector3(22.71f, 0.05f, -4.53f));
            family.Player.GetComponentInChildren<Camera>().transform.LookAt(family.Lera.transform.position + Vector3.up * 1.3f);
            family.Lera.Interact(); family.Dialogue.Choose(1); Question(family);
            EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Show();
            yield return null;
            Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed in preview");
        }
        private static void MotherFixture(FamilyEpisode family, FamilyEpisode.MotherWhereabouts where)
        {
            var stage = where == FamilyEpisode.MotherWhereabouts.InRoom ? MarinaEvacuee.Stage.InRoom :
                where == FamilyEpisode.MotherWhereabouts.Safe ? MarinaEvacuee.Stage.Safe : MarinaEvacuee.Stage.Evacuating;
            Stage(family.Marina, stage);
            Move(family.Marina.transform, where switch
            {
                FamilyEpisode.MotherWhereabouts.InRoom => new Vector3(18.2f, 0.05f, -5.28f),
                FamilyEpisode.MotherWhereabouts.LeavingBuilding => new Vector3(17.2f, 0.05f, 0),
                FamilyEpisode.MotherWhereabouts.Courtyard => new Vector3(28.34f, 0.05f, -10),
                _ => new Vector3(19, 0.05f, -17.25f)
            });
        }
        private static void LeraFixture(LeraEvacuee lera, LeraEvacuee.Stage stage, Vector3 position) { Stage(lera, stage); Move(lera.transform, position); }
        private static void Stage(Component actor, object value)
        {
            actor.GetType().GetProperty("CurrentStage").GetSetMethod(true).Invoke(actor, new[] { value });
            var motor = actor.GetType().GetField("motor", Private).GetValue(actor);
            typeof(EvacuationMotor).GetField("walking", Private).SetValue(motor, false);
        }
        private static void Field(object actor, string name, object value) => actor.GetType().GetField(name, Private).SetValue(actor, value);
        private static void Move(Transform actor, Vector3 position)
        {
            var cc = actor.GetComponent<CharacterController>(); bool enabled = cc.enabled;
            cc.enabled = false; actor.position = position; cc.enabled = enabled; Physics.SyncTransforms();
        }
        private static string Line(DialogueController w) => (string)typeof(DialogueController).GetField("line", Private).GetValue(w);
        private static DialogueController.Choice[] Choices(DialogueController w) => (DialogueController.Choice[])typeof(DialogueController).GetField("choices", Private).GetValue(w);
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Record(string text) => File.AppendAllText("Logs/Family-DialogueExamples.txt", text + "\n");
        private static string UserHash()
        {
            string path = Path.Combine(Application.persistentDataPath, "observations-v1.json");
            if (!File.Exists(path)) return "ABSENT";
            using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Finish(string text)
        {
            SessionState.SetBool(Prefix + "Running", false);
            File.WriteAllText(previewOnly ? "Logs/Family-DialoguePreview.txt" : "Logs/Family-DialogueChecks.txt", text);
            if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text);
            if (!preview)
            {
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
