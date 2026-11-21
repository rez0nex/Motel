using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class MotelVadimChecks
    {
        [MenuItem("Motel/Validate Vadim Episode")]
        public static void ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
            var scene = SceneManager.GetActiveScene();
            MotelLoopChecks.ValidateScene(scene);
            MotelLeonidChecks.ValidateScene(scene);
            ValidateScene(scene);
            ValidateKnowledge();
            Debug.Log("PASS: Vadim/shared dialogue/diagnostics references, physical route and four independent knowledge flags. Play Mode checks are separate.");
        }

        public static void ValidateScene(Scene scene)
        {
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var vadim = nodes.Select(n => n.GetComponent<VadimEvacuee>()).Where(c => c != null).Single();
            var leonid = nodes.Select(n => n.GetComponent<LeonidEvacuee>()).Where(c => c != null).Single();
            var inspection = nodes.Select(n => n.GetComponent<EquipmentInspection>()).Where(c => c != null).Single();
            var dialogue = nodes.Select(n => n.GetComponent<DialogueController>()).Where(c => c != null).Single();
            var loop = nodes.Select(n => n.GetComponent<MotelLoopController>()).Where(c => c != null).Single();
            var actorData = new SerializedObject(vadim);
            Require(actorData.FindProperty("dialogue").objectReferenceValue == dialogue &&
                actorData.FindProperty("loop").objectReferenceValue == loop, "Vadim references");
            var evidenceData = new SerializedObject(inspection);
            Require(evidenceData.FindProperty("loop").objectReferenceValue == loop &&
                evidenceData.FindProperty("dialogue").objectReferenceValue == dialogue, "Inspection references");
            Transform safe = (Transform)actorData.FindProperty("safePoint").objectReferenceValue;
            Transform otherSafe = (Transform)new SerializedObject(leonid).FindProperty("safePoint").objectReferenceValue;
            Require(safe != null && Vector3.Distance(safe.position, otherSafe.position) >= 0.95f, "Shared arrival slot blocks other guest");
            Require(Vector3.Distance(safe.position, new Vector3(17, 0.05f, -18.1f)) < 0.001f, "Vadim safe slot differs");
            var room = MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 2);
            var expected = MotelVadimSceneSetup.Route(room);
            var route = actorData.FindProperty("route");
            Require(route.arraySize == expected.Length, "Vadim route point count");
            for (int i = 0; i < expected.Length; i++)
                Require(Vector3.Distance(route.GetArrayElementAtIndex(i).vector3Value, expected[i]) < 0.001f, "Route differs from generator");
            Require(Vector3.Distance(vadim.transform.position, MotelVadimSceneSetup.Spawn) < 0.001f, "Vadim arrival spawn");
            foreach (var item in new[] {
                ("arrivalRoute", MotelVadimSceneSetup.ArrivalRoute()),
                ("checkInRoute", MotelVadimSceneSetup.CheckInRoute(room)) })
            {
                var saved = actorData.FindProperty(item.Item1);
                Require(saved.arraySize == item.Item2.Length, item.Item1 + " length");
                for (int i = 0; i < saved.arraySize; i++)
                    Require(Vector3.Distance(saved.GetArrayElementAtIndex(i).vector3Value, item.Item2[i]) < 0.001f, item.Item1 + " point");
            }
            var register = nodes.Select(n => n.GetComponent<GuestRegister>()).Where(c => c != null).Single();
            Require(actorData.FindProperty("register").objectReferenceValue == register &&
                Mathf.Approximately(actorData.FindProperty("arrivalDelay").floatValue, 10), "Register/arrival defaults");
            Require(new SerializedObject(register).FindProperty("dialogue").objectReferenceValue == dialogue &&
                new SerializedObject(register).FindProperty("loop").objectReferenceValue == loop, "Register references");
            var cc = vadim.GetComponent<CharacterController>();
            Require(cc.enabled && Math.Abs(cc.radius - 0.28f) < 0.001f && Math.Abs(cc.height - 1.7f) < 0.001f, "Vadim body size");
            var entrance = (PrototypeDoor)actorData.FindProperty("entranceDoor").objectReferenceValue;
            var exit = (PrototypeDoor)actorData.FindProperty("mainExit").objectReferenceValue;
            Require(entrance != null && entrance.name == "Room2" && exit != null && exit.name == "Main Entrance", "Wrong route doors");
            Physics.SyncTransforms();
            Require(Blocked(expected[3], expected[4], cc), "Closed room door not blocking Vadim");
            Require(Blocked(expected[7], expected[8], cc), "Closed main door not blocking Vadim");
            var doors = new[] { entrance, exit };
            Quaternion[] poses = doors.Select(d => d.transform.localRotation).ToArray();
            try
            {
                foreach (var door in doors)
                    door.transform.localRotation *= Quaternion.Euler(0, new SerializedObject(door).FindProperty("openAngle").floatValue, 0);
                Physics.SyncTransforms();
                Vector3 previous = MotelVadimSceneSetup.RoomPosition(room);
                foreach (Vector3 point in expected)
                {
                    Require(!Blocked(previous, point, cc), "Vadim open route obstructed: " + previous + " -> " + point);
                    previous = point;
                }
                foreach (var trip in new[] { MotelVadimSceneSetup.ArrivalRoute(), MotelVadimSceneSetup.CheckInRoute(room) })
                {
                    previous = trip == null ? Vector3.zero : trip[0];
                    foreach (Vector3 point in trip)
                    {
                        Require(!Blocked(previous, point, cc), "Arrival/check-in path obstructed: " + previous + " -> " + point);
                        previous = point;
                    }
                }
            }
            finally
            {
                for (int i = 0; i < doors.Length; i++) doors[i].transform.localRotation = poses[i];
                Physics.SyncTransforms();
            }
            Vector3 origin = new Vector3(room.X(3), 1.5f, MotelBlockoutPlan.ResidentialZ);
            Require(Physics.Raycast(origin, (vadim.transform.position + Vector3.up - origin).normalized, out RaycastHit hit,
                10, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<VadimEvacuee>() == null,
                "World interaction crosses room wall");
            origin = inspection.transform.TransformPoint(new Vector3(0, -.35f, -1.5f));
            Require(Physics.Raycast(origin, (inspection.transform.position-origin).normalized, out hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<EquipmentInspection>() == inspection, "Diagnostic panel inaccessible");
            Require(inspection.GetComponentInChildren<WorldSign>().GetComponent<MeshRenderer>().sharedMaterial.shader.name == "Motel/World Sign Text",
                "Diagnostic sign lost depth-tested shader");
        }

        private static bool Blocked(Vector3 from, Vector3 to, CharacterController cc)
        {
            Vector3 center = from + cc.center, delta = to - from;
            delta.y = 0;
            float end = cc.height / 2 - cc.radius - 0.08f;
            return Physics.CapsuleCastAll(center - Vector3.up * end, center + Vector3.up * end, cc.radius + 0.035f,
                delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => h.collider.gameObject.scene == cc.gameObject.scene && !h.collider.transform.IsChildOf(cc.transform));
        }

        public static void ValidateKnowledge()
        {
            string folder = Path.GetFullPath("Temp/VadimChecks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "observations-v1.json");
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true,\"leonidRescued\":true}");
            var store = new ObservationStore(path);
            Require(store.HasCutoffFact && store.HasLeonidFact && !store.HasEquipmentFact && !store.HasVadimFact, "Legacy knowledge migration");
            Require(store.RememberEquipmentOverheat() && !store.HasVadimFact, "Evidence must not rescue Vadim");
            Require(store.RememberVadimRescue(), "Rescue knowledge");
            string json = File.ReadAllText(path);
            for (int i = 0; i < 10; i++)
            {
                store = new ObservationStore(path);
                Require(store.Facts.Length == 4 && store.HasCutoffFact && store.HasLeonidFact && store.HasEquipmentFact && store.HasVadimFact,
                    "Independent facts lost");
                Require(!store.RememberCutoff() && !store.RememberLeonidRescue() && !store.RememberEquipmentOverheat() && !store.RememberVadimRescue(), "Fact duplicated");
                Require(json == File.ReadAllText(path), "Duplicate save write");
            }
            path = Path.Combine(folder, "separate.json");
            store = new ObservationStore(path); store.RememberVadimRescue();
            Require(!store.HasLeonidFact && !store.HasEquipmentFact && !store.HasCutoffFact && store.Facts.Length == 1, "Vadim flag contaminates other knowledge");
        }

        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
