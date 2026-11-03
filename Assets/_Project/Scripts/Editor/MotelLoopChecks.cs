using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class MotelLoopChecks
    {
        [MenuItem("Motel/Validate Loop and Observations")]
        public static void ValidateCurrent()
        {
            Debug.Log(ValidateState());
            Scene scene = SceneManager.GetActiveScene();
            ValidateScene(scene);
            Debug.Log("PASS: loop scene references, one loop/player/interactor/cutoff, 9 solid windows, initial state.");
        }

        public static string ValidateState()
        {
            var normal = new AttemptClock();
            Require(normal.DisplayTime == "20:16:00", "Start clock");
            normal.Advance(359, false, false);
            Require(!normal.Expired && normal.DisplayTime == "20:21:59", "Normal cycle before end");
            normal.Advance(120, true, false);
            Require(normal.Elapsed == 359, "Paused normal clock moved");
            normal.Advance(1, false, false);
            Require(normal.Expired && normal.Elapsed == 360 && normal.DisplayTime == "20:22:00", "Normal 360s boundary");
            Require(normal.TryRequestAccident() && !normal.TryRequestAccident(), "Timer then cutoff double request");
            var debug = new AttemptClock();
            debug.Advance(30, false, true);
            Require(debug.Elapsed == 180, "Debug x6 speed");
            debug.Advance(500, true, true);
            Require(debug.Elapsed == 180, "Paused debug clock moved");
            debug.Advance(30, false, true);
            Require(debug.Expired && debug.DisplayTime == "20:22:00", "Debug 60s boundary");
            var early = new AttemptClock();
            early.Advance(12, false, false);
            Require(early.TryRequestAccident() && !early.TryRequestAccident(), "Cutoff then timer double request");
            early.Advance(1000, false, false);
            Require(early.Elapsed == 12, "Transition clock changed");
            for (int i = 0; i < 10; i++)
            {
                var reset = new AttemptClock();
                Require(reset.Elapsed == 0 && !reset.AccidentRequested && reset.DisplayTime == "20:16:00", "Reset clock");
            }
            string directory = Path.GetFullPath("Temp/MotelLoopChecks-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "observations-v1.json");
            var store = new ObservationStore(path);
            Require(!store.HasCutoffFact && store.RememberCutoff(), "Learn observation");
            string saved = File.ReadAllText(path);
            for (int i = 0; i < 10; i++)
            {
                store = new ObservationStore(path);
                Require(store.HasCutoffFact && !store.RememberCutoff(), "Fact lost or duplicated after reset");
                Require(saved == File.ReadAllText(path), "Duplicate fact rewrites save");
            }
            string future = Path.Combine(directory, "future.json");
            string experience = Path.Combine(directory, "experience.json");
            File.WriteAllText(experience, saved); // Legacy facts, without the new progress field.
            store = new ObservationStore(experience);
            Require(store.HasCutoffFact && !store.HasExperiencedExplosion, "Legacy observation grants explosion experience");
            int factCount = store.Facts.Length;
            Require(store.RememberExperiencedExplosion(), "Learn explosion experience");
            store = new ObservationStore(experience);
            Require(store.HasExperiencedExplosion && store.Facts.Length == factCount && !store.RememberExperiencedExplosion(), "Independent experience lost/duplicated");
            File.WriteAllText(future, "{\"version\":99}");
            store = new ObservationStore(future);
            store.RememberCutoff();
            store.RememberExperiencedExplosion();
            Require(File.ReadAllText(future) == "{\"version\":99}" && store.Warning != null, "Future save overwritten");
            string corrupt = Path.Combine(directory, "corrupt.json");
            File.WriteAllText(corrupt, "not json");
            store = new ObservationStore(corrupt);
            store.RememberCutoff();
            Require(File.ReadAllText(corrupt) == "not json" && store.HasCutoffFact && store.Warning != null, "Corrupt save handling");
            return "PASS: normal 360s/debug 60s clock, pause/resume, early/double requests, versioned persistence/deduplication; independent explosion experience, legacy observations do not grant it; future save preserved. State test, not Play Mode.";
        }

        public static void ValidateScene(Scene scene)
        {
            var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            ValidatePlan(objects.Single(t => t.name == "Motel Blockout"));
            Require(objects.Count(t => t.GetComponent<MotelLoopController>() != null) == 1, "Loop count");
            Require(objects.Count(t => t.GetComponent<FirstPersonController>() != null) == 1, "Player count");
            Require(objects.Count(t => t.GetComponent<PrototypeInteractor>() != null) == 1, "Interaction UI count");
            Require(objects.Count(t => t.GetComponent<EmergencyCutoff>() != null) == 1, "Cutoff count");
            var loop = objects.Select(t => t.GetComponent<MotelLoopController>()).Single(c => c != null);
            var data = new SerializedObject(loop);
            foreach (string field in new[] { "player", "receptionClock", "equipmentIndicator", "intactEquipment", "damagedEquipment" })
                Require(data.FindProperty(field).objectReferenceValue != null, "Missing loop reference: " + field);
            var display = (TextMesh)data.FindProperty("receptionClock").objectReferenceValue;
            Transform housing = display.transform.parent.Find("Clock Housing");
            Require(housing.localPosition.z - housing.localScale.z / 2 > display.transform.localPosition.z + 0.005f,
                "Clock housing occludes its digits");
            Require(display.GetComponent<MeshRenderer>().sharedMaterial.shader.name == "Motel/World Sign Text",
                "Clock must use depth-tested world text");
            Require(!data.FindProperty("debugSixtySecondCycle").boolValue, "Saved scene must use normal 360s speed");
            Require(!((GameObject)data.FindProperty("damagedEquipment").objectReferenceValue).activeSelf, "Damage starts visible");
            var windows = objects.Where(t => t.name.EndsWith(" Window 0 Glass") || t.name.Contains(" Window ") && t.name.EndsWith(" Glass")).ToArray();
            Require(windows.Length == 9, "Nine windows required");
            foreach (Transform window in windows)
            {
                Require(window.GetComponent<BoxCollider>() != null && !window.GetComponent<BoxCollider>().isTrigger, "Window is an exit");
                Material glass = window.GetComponent<MeshRenderer>().sharedMaterial;
                Require(glass.shader.name == "Universal Render Pipeline/Lit" && glass.GetFloat("_Surface") == 1, "Glass not URP transparent");
                Require(window.GetComponent<PrototypeDoor>() == null, "Operable window");
            }
            foreach (Transform node in objects)
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0, "Missing script: " + node.name);
        }

        private static void ValidatePlan(Transform root)
        {
            MotelBlockoutPlan plan = MotelBlockoutPlan.Create();
            foreach (var box in plan.Boxes)
            {
                Transform node = root.Find(box.Group + "/" + box.Name);
                Require(node != null, "Generated box missing: " + box.Name);
                Require(Vector3.Distance(node.localPosition, new Vector3(box.X, box.Y, box.Z)) < 0.001f &&
                    Vector3.Distance(node.localScale, new Vector3(box.Width, box.Height, box.Depth)) < 0.001f,
                    "Saved scene differs from generator: " + box.Name);
                Require((node.GetComponent<BoxCollider>() != null) == box.Solid, "Collider differs: " + box.Name);
                Require(node.GetComponent<MeshRenderer>().sharedMaterial.name == box.Material, "Material differs: " + box.Name);
            }
            Require(root.Find("Doors").childCount == plan.Doors.Count, "Duplicate/missing doors");
            foreach (var door in plan.Doors)
            {
                Transform node = root.Find("Doors/" + door.Name + (door.Operable ? "" : " (Closed)"));
                Require(node != null, "Generated door missing: " + door.Name);
                Quaternion rotation = Quaternion.Euler(0, door.Yaw, 0);
                Vector3 hinge = new Vector3(door.X, 0, door.Z) - rotation * Vector3.right * (door.Width / 2);
                Require(Vector3.Distance(node.localPosition, hinge) < 0.001f && Quaternion.Angle(node.localRotation, rotation) < 0.01f,
                    "Door pose differs: " + door.Name);
                Require((node.GetComponent<PrototypeDoor>() != null) == door.Operable, "Door availability differs: " + door.Name);
                BoxCollider panel = node.GetComponentsInChildren<BoxCollider>().Single();
                Require(!panel.isTrigger && Math.Abs(panel.transform.localScale.x - (door.Width - 0.02f)) < 0.001f,
                    "Door panel differs: " + door.Name);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
