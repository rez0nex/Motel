using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class MotelConnectionChecks
    {
        [MenuItem("Motel/Validate Connection 7-8")]
        public static void ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit mode required.");
            ValidateScene(SceneManager.GetActiveScene());
            Debug.Log("PASS: one cot/cabinet/plan, first-hit interactions, solid closed door/cabinet, open two-way player/NPC capsule clearance. Play Mode is separate.");
        }
        public static void ValidateScene(Scene scene)
        {
            var nodes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            T One<T>() where T : Component => nodes.Select(t => t.GetComponent<T>()).Where(c => c != null).Single();
            var wardrobe = One<SlidingWardrobe>(); var map = One<MotelFloorPlan>();
            var loop = One<MotelLoopController>(); var window = One<DialogueController>();
            foreach (var renderer in wardrobe.GetComponentsInChildren<Renderer>().Concat(map.GetComponentsInChildren<Renderer>())
                         .Concat(nodes.Single(t => t.name == "Broken Folding Cot").GetComponentsInChildren<Renderer>()))
                Require(renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null && renderer.sharedMaterial.shader.isSupported,
                    "Missing/unsupported prop material: " + renderer.name);
            var door = nodes.Single(t => t.name == "Connection 7-8").GetComponent<PrototypeDoor>();
            Require(nodes.Count(t => t.name == "Broken Folding Cot") == 1 && !nodes.Any(t => t.name == "Broken Folded Cot"), "Cot not replaced or duplicated");
            Require(new SerializedObject(wardrobe).FindProperty("loop").objectReferenceValue == loop &&
                new SerializedObject(map).FindProperty("dialogue").objectReferenceValue == window &&
                new SerializedObject(door).FindProperty("obstruction").objectReferenceValue == wardrobe, "Connection references");
            Require(Vector3.Distance(wardrobe.transform.position, MotelConnectionSceneSetup.WardrobeStart) < 0.001f, "Wardrobe initial pose");
            Require(new SerializedObject(door).FindProperty("openAngle").floatValue == 95, "Connection swing regressed");
            Vector3 start = wardrobe.transform.position; Quaternion closed = door.transform.localRotation;
            try
            {
                wardrobe.transform.position = MotelConnectionSceneSetup.WardrobeEnd; Physics.SyncTransforms();
                Require(Blocked(new Vector3(19.8f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), new Vector3(22.35f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), 0.33f) == "Door Panel", "Closed door does not block");
                door.transform.localRotation = closed * Quaternion.Euler(0, 95, 0); Physics.SyncTransforms();
                foreach (float radius in new[] { 0.33f, 0.315f })
                {
                    Require(Blocked(new Vector3(19.8f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), new Vector3(22.35f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), radius) == null, "Open 7→8 passage blocked");
                    Require(Blocked(new Vector3(22.35f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), new Vector3(19.8f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), radius) == null, "Open 8→7 passage blocked");
                }
                Require(Blocked(new Vector3(22.96f, 0.05f, -5.28f+MotelBlockoutPlan.ResidentialZ), new Vector3(22.71f, 0.05f, -4.53f+MotelBlockoutPlan.ResidentialZ), 0.315f, One<LeraEvacuee>().transform) == null, "Lera initial step blocked");
                wardrobe.transform.position = start; Physics.SyncTransforms();
                Require(Blocked(new Vector3(19.8f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), new Vector3(22.35f, 0.05f, -5.53f+MotelBlockoutPlan.ResidentialZ), 0.33f) == wardrobe.name, "Closed wardrobe does not block");
                FirstHit(wardrobe, new Vector3(19.85f, 1.65f, -5.53f+MotelBlockoutPlan.ResidentialZ));
                FirstHit(map, new Vector3(1.55f, 1.65f, -12.2f));
            }
            finally { wardrobe.transform.position = start; door.transform.localRotation = closed; Physics.SyncTransforms(); }
        }
        private static string Blocked(Vector3 from, Vector3 to, float radius, Transform ignore = null)
        {
            Vector3 center = from + Vector3.up * 0.95f;
            foreach (var hit in Physics.CapsuleCastAll(center - Vector3.up * 0.5f, center + Vector3.up * 0.5f, radius,
                         (to - from).normalized, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                if (ignore == null || !hit.transform.IsChildOf(ignore)) return hit.collider.name;
            return null;
        }
        private static void FirstHit(Component target, Vector3 origin)
        {
            Vector3 direction = target.GetComponent<Collider>().bounds.center - origin;
            Require(Physics.Raycast(origin, direction.normalized, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.transform.IsChildOf(target.transform), "First ray misses " + target.name);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
