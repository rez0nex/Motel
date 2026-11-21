using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class MotelLeonidChecks
    {
        [MenuItem("Motel/Validate Leonid Episode")]
        public static void ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run scene validation outside Play Mode.");
            ValidateScene(SceneManager.GetActiveScene());
            ValidateKnowledge();
            Debug.Log("PASS: Leonid scene references, physical route, blocked doors, wall occlusion, compatible knowledge persistence. Not a Play Mode acceptance.");
        }

        public static void ValidateScene(Scene scene)
        {
            var nodes = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            var leonid = nodes.Select(t => t.GetComponent<LeonidEvacuee>()).Where(c => c != null).Single();
            var cane = nodes.Select(t => t.GetComponent<CanePickup>()).Where(c => c != null).Single();
            var dialogue = nodes.Select(t => t.GetComponent<DialogueController>()).Where(c => c != null).Single();
            var loop = nodes.Select(t => t.GetComponent<MotelLoopController>()).Where(c => c != null).Single();
            var data = new SerializedObject(leonid);
            Require(data.FindProperty("loop").objectReferenceValue == loop &&
                data.FindProperty("dialogue").objectReferenceValue == dialogue, "Episode references");
            Require(new SerializedObject(loop).FindProperty("dialogue").objectReferenceValue == dialogue &&
                new SerializedObject(dialogue).FindProperty("loop").objectReferenceValue == loop &&
                new SerializedObject(cane).FindProperty("owner").objectReferenceValue == leonid, "UI/cane references");
            foreach (string field in new[] { "sleepingModel", "standingModel", "heldCane", "sleepingTarget", "safePoint", "entranceDoor", "emergencyExit" })
                Require(data.FindProperty(field).objectReferenceValue != null, "Missing Leonid field: " + field);
            var room = MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 4);
            var expected = MotelLeonidSceneSetup.Route(room);
            var route = data.FindProperty("route");
            Require(route.arraySize == expected.Length, "Route point count");
            for (int i = 0; i < expected.Length; i++)
                Require(Vector3.Distance(route.GetArrayElementAtIndex(i).vector3Value, expected[i]) < 0.001f, "Route differs from generator");
            Require(Vector3.Distance(leonid.transform.position, new Vector3(room.X(3), 0.05f, room.Z(4))) < 0.001f,
                "Leonid spawn differs");
            Require(!leonid.GetComponent<CharacterController>().enabled && !cane.gameObject.activeSelf, "Saved initial state");
            var supplies = nodes.Select(t => t.GetComponent<LeonidBelongings>()).Where(c => c != null).Single();
            var items = nodes.Select(t => t.GetComponent<LeonidWorldItem>()).Where(c => c != null).ToArray();
            Require(items.Length == 3 && items.Select(i => i.Kind).Distinct().Count() == 3, "Belongings item duplicates");
            Require(items.Single(i => i.Kind == LeonidWorldItem.ItemKind.BrokenCane).GetComponent<CanePickup>() == null, "Broken cane is usable");
            Require(Vector3.Distance(items.Single(i => i.Kind == LeonidWorldItem.ItemKind.CarKeys).transform.position,
                MotelLeonidCarSetup.KeyPosition(room)) < 0.001f, "Keys not on nightstand");
            var sd = new SerializedObject(supplies);
            foreach (string field in new[] { "loop", "dialogue", "leonid", "keys", "spareCane", "trunkLid" })
                Require(sd.FindProperty(field).objectReferenceValue != null, "Missing belongings reference: " + field);
            Require(new SerializedObject(cane).FindProperty("belongings").objectReferenceValue == supplies, "Spare supplies reference");
            foreach (var item in items) Require(new SerializedObject(item).FindProperty("belongings").objectReferenceValue == supplies, "World item supplies reference");
            Require(Vector3.Distance(nodes.Single(n => n.name == "Leonid Blue Car").position, MotelLeonidCarSetup.CarPosition) < 0.001f,
                "Car position differs from generator");
            Require(nodes.Single(n => n.name == "Blue Front").GetComponent<Renderer>().sharedMaterial.shader.name == "Universal Render Pipeline/Lit", "Car material not URP");
            Physics.SyncTransforms();
            Vector3 keyOrigin = MotelLeonidCarSetup.KeyApproach(room) + Vector3.up * 1.55f;
            Vector3 keyTarget = items.Single(i => i.Kind == LeonidWorldItem.ItemKind.CarKeys).GetComponent<Collider>().bounds.center;
            Require(Physics.Raycast(keyOrigin, (keyTarget - keyOrigin).normalized, out var keyHit, 2.5f, Physics.DefaultRaycastLayers) &&
                keyHit.collider.GetComponent<LeonidWorldItem>()?.Kind == LeonidWorldItem.ItemKind.CarKeys, "Nightstand keys occluded");
            var lid = (Transform)sd.FindProperty("trunkLid").objectReferenceValue;
            Quaternion lidPose = lid.localRotation;
            try
            {
                lid.localRotation = Quaternion.Euler(75, 0, 0); cane.gameObject.SetActive(true); Physics.SyncTransforms();
                Vector3 from = new Vector3(MotelLeonidCarSetup.RearApproach.x, 1.5f, MotelLeonidCarSetup.RearApproach.z);
                Vector3 delta = cane.GetComponent<Collider>().bounds.center - from;
                Require(Physics.Raycast(from, delta.normalized, out var spareHit, 2.5f, Physics.DefaultRaycastLayers) &&
                    spareHit.collider.GetComponent<CanePickup>() == cane, "Open trunk spare occluded");
            }
            finally { lid.localRotation = lidPose; cane.gameObject.SetActive(false); Physics.SyncTransforms(); }
            Require(cane.GetComponent<BoxCollider>() != null && !cane.GetComponent<BoxCollider>().isTrigger, "Cane ray target");
            Transform safe = (Transform)data.FindProperty("safePoint").objectReferenceValue;
            Require(Vector3.Distance(safe.position, expected.Last()) < 0.001f, "Safe point outside assembly marker");

            Physics.SyncTransforms();
            Transform entrance = nodes.Single(t => t.name == "Room4" && t.GetComponent<PrototypeDoor>() != null);
            Transform exit = nodes.Single(t => t.name == "Emergency Exit");
            // The generated path intentionally requires these doors, without bypassing either closed leaf.
            Require(SegmentBlocked(expected[3], expected[4], leonid, cane), "Closed room door not blocking route");
            Require(SegmentBlocked(expected[5], expected[6], leonid, cane), "Closed emergency exit not blocking route");
            var poses = new[] { entrance, exit }.Select(t => t.localRotation).ToArray();
            try
            {
                foreach (Transform door in new[] { entrance, exit })
                {
                    float angle = new SerializedObject(door.GetComponent<PrototypeDoor>()).FindProperty("openAngle").floatValue;
                    door.localRotation *= Quaternion.Euler(0, angle, 0);
                }
                Physics.SyncTransforms();
                Vector3 previous = leonid.transform.position;
                foreach (Vector3 point in expected)
                {
                    Require(!SegmentBlocked(previous, point, leonid, cane), "Obstructed open route: " + previous + " -> " + point);
                    previous = point;
                }
            }
            finally
            {
                entrance.localRotation = poses[0]; exit.localRotation = poses[1];
                Physics.SyncTransforms();
            }
            // A ray from the corridor to the sleeping body must encounter the solid room first.
            BoxCollider target = (BoxCollider)data.FindProperty("sleepingTarget").objectReferenceValue;
            Vector3 origin = new Vector3(room.X(3), 1.6f, MotelBlockoutPlan.ResidentialZ);
            Vector3 destination = target.bounds.center;
            Require(Physics.Raycast(origin, (destination - origin).normalized, out RaycastHit hit,
                Vector3.Distance(origin, destination), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<LeonidEvacuee>() == null, "Corridor interaction crosses wall");
        }

        private static bool SegmentBlocked(Vector3 from, Vector3 to, LeonidEvacuee owner, CanePickup cane)
        {
            var character = owner.GetComponent<CharacterController>();
            Vector3 center = from + character.center;
            float end = character.height / 2 - character.radius - 0.08f;
            Vector3 delta = to - from;
            delta.y = 0;
            return Physics.CapsuleCastAll(center - Vector3.up * end, center + Vector3.up * end,
                    character.radius + 0.035f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !h.collider.transform.IsChildOf(owner.transform) && !h.collider.transform.IsChildOf(cane.transform));
        }

        public static void ValidateKnowledge()
        {
            string directory = Path.GetFullPath("Temp/LeonidChecks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "observations-v1.json");
            File.WriteAllText(path, "{\"version\":1,\"cutoffCausedExplosion\":true}");
            var store = new ObservationStore(path);
            Require(store.HasCutoffFact && !store.HasLeonidFact, "Legacy save compatibility");
            Require(store.RememberLeonidRescue(), "Learn rescue");
            string json = File.ReadAllText(path);
            for (int i = 0; i < 10; i++)
            {
                store = new ObservationStore(path);
                Require(store.HasCutoffFact && store.HasLeonidFact && !store.RememberLeonidRescue() && !store.RememberCutoff(), "Knowledge lost or duplicated");
                Require(json == File.ReadAllText(path), "Duplicate save write");
            }
            path = Path.Combine(directory, "reverse.json");
            store = new ObservationStore(path);
            store.RememberLeonidRescue(); store.RememberCutoff();
            store = new ObservationStore(path);
            Require(store.HasLeonidFact && store.HasCutoffFact, "Fact order dependency");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
