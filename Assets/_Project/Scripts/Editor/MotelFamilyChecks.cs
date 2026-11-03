using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class MotelFamilyChecks
    {
        [MenuItem("Motel/Validate Family Episode")]
        public static void ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
            Scene scene = SceneManager.GetActiveScene();
            MotelLoopChecks.ValidateScene(scene); MotelLeonidChecks.ValidateScene(scene); MotelVadimChecks.ValidateScene(scene);
            ValidateScene(scene); ValidateKnowledge();
            Debug.Log("PASS: family scene references, one of each actor/keys/lock, independent version-1 knowledge. Play Mode is separate.");
        }

        public static void ValidateScene(Scene scene)
        {
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            T One<T>() where T : Component => nodes.Select(t => t.GetComponent<T>()).Where(c => c != null).Single();
            var family = One<FamilyEpisode>(); var marina = One<MarinaEvacuee>(); var lera = One<LeraEvacuee>();
            var keys = One<RoomKeysPickup>(); var lock8 = One<RoomEightLock>(); var loop = One<MotelLoopController>();
            var dialogue = One<DialogueController>(); var player = One<FirstPersonController>();
            Require(lera.Age == 14, "Lera age must be 14");
            var f = new SerializedObject(family);
            Require(f.FindProperty("loop").objectReferenceValue == loop && f.FindProperty("dialogue").objectReferenceValue == dialogue &&
                f.FindProperty("player").objectReferenceValue == player.transform && f.FindProperty("marina").objectReferenceValue == marina &&
                f.FindProperty("lera").objectReferenceValue == lera && f.FindProperty("duplicateKeys").objectReferenceValue == keys.gameObject, "Family references");
            foreach (Component actor in new Component[] { marina, lera, keys, lock8 })
                Require(new SerializedObject(actor).FindProperty("family").objectReferenceValue == family, "Family reference on " + actor.name);
            Require(lock8.GetComponent<PrototypeDoor>() != null && lock8.name == "Room8", "Wrong locked door");
            Require(nodes.Single(t => t.name == "Connection 7-8").GetComponent<PrototypeDoor>() != null, "Connection lost");
            foreach (Component actor in new Component[] { marina, lera })
            {
                var cc = actor.GetComponent<CharacterController>();
                Require(cc != null && Mathf.Approximately(cc.radius, 0.28f) && Mathf.Approximately(cc.height, 1.7f), "NPC body changed");
            }
            var plan = MotelBlockoutPlan.Create(); var seven = plan.Rooms.Single(r => r.Number == 7); var eight = plan.Rooms.Single(r => r.Number == 8);
            Require(Vector3.Distance(marina.transform.position, new Vector3(seven.X(1.8f), 0.05f, seven.Z(4))) < 0.01f &&
                Vector3.Distance(lera.transform.position, new Vector3(eight.X(1.4f), 0.05f, eight.Z(4))) < 0.01f, "Family spawn");
            var m = new SerializedObject(marina); var route = m.FindProperty("route"); var expected = MotelFamilySceneSetup.MarinaRoute(seven);
            Require(route.arraySize == expected.Length, "Marina route length");
            for (int i = 0; i < expected.Length; i++) Require(Vector3.Distance(route.GetArrayElementAtIndex(i).vector3Value, expected[i]) < 0.01f, "Marina waypoint");
            var interior = new SerializedObject(lock8).FindProperty("inside").boundsValue;
            Require(!interior.Contains(player.transform.position) && interior.Contains(lera.transform.position), "Room8 inside release bounds");
            Require(Vector3.Distance(((Transform)m.FindProperty("safePoint").objectReferenceValue).position, new Vector3(19, 0.05f, -17.25f)) < 0.01f &&
                Vector3.Distance(((Transform)new SerializedObject(lera).FindProperty("safePoint").objectReferenceValue).position,
                    new Vector3(17.5f, 0.05f, -17)) < 0.01f, "Saved family arrival points differ from generator");
        }

        public static void ValidateKnowledge()
        {
            string path = Path.GetFullPath("Temp/FamilyKnowledge-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true,\"equipmentOverheatInspected\":true,\"vadimRescued\":true}");
            var store = new ObservationStore(path);
            Require(store.Facts.Length == 4 && !store.HasFamilyFact && !store.HasMarinaFact && !store.HasLeraFact, "Old save compatibility");
            Require(store.RememberLeraRescue() && !store.HasMarinaFact && store.RememberFamilyPlacement() && store.RememberMarinaRescue(), "Independent family facts");
            for (int i = 0; i < 5; i++)
            {
                store = new ObservationStore(path);
                Require(store.Facts.Length == 7 && !store.RememberLeraRescue() && !store.RememberMarinaRescue() &&
                    !store.RememberFamilyPlacement(), "Knowledge lost/duplicated");
            }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
