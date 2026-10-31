using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelBlockoutSceneCreator
    {
        public const string ScenePath = "Assets/Scenes/MotelBlockout.unity";
        private const string PrototypePath = "Assets/Scenes/Prototype.unity";
        private const string MaterialFolder = "Assets/_Project/Art/BlockoutMaterials";

        [MenuItem("Motel/Create Motel Blockout")]
        public static void CreateMotelBlockout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Перезаписать MotelBlockout?",
                    "Черновая локация будет создана заново. Изменения в ней будут потеряны. Prototype и SampleScene сохраняются.",
                    "Перезаписать", "Отмена")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // A saved staging scene also avoids Unity's restriction on additive creation beside an untitled scene.
            // Prototype is opened only as a template and is never saved or modified here.
            EditorSceneManager.OpenScene(PrototypePath, OpenSceneMode.Single);
            try
            {
                BuildAndSave(true);
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Selection.activeGameObject = GameObject.Find("Motel Blockout");
                Debug.Log("Motel blockout saved. Play → Game → click. Start behind reception counter.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Motel Blockout", "Создание остановлено. Подробности в Console.", "OK");
            }
        }

        [MenuItem("Motel/Create Motel Blockout", true)]
        private static bool CanCreate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        // Builds additively and restores the open scene; also usable by an edit-mode automation request.
        public static void BuildAndSave(bool overwrite)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating a blockout.");
            if (File.Exists(ScenePath) && !overwrite)
                throw new IOException("Existing MotelBlockout requires explicit overwrite confirmation.");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded)
                throw new InvalidOperationException("Close the existing blockout before replacing it.");

            MotelBlockoutPlan plan = MotelBlockoutPlan.Create();
            string report = MotelBlockoutValidation.Validate(plan);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP/Lit shader unavailable.");
            Scene previous = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(PrototypePath);
            bool closeSource = !source.isLoaded;
            Scene scene = default;
            try
            {
                if (closeSource) source = EditorSceneManager.OpenScene(PrototypePath, OpenSceneMode.Additive);
                FirstPersonController playerSource = source.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<FirstPersonController>(true)).Single();
                PrototypeDoor doorSource = source.GetRootGameObjects()
                    .SelectMany(g => g.GetComponentsInChildren<PrototypeDoor>(true)).Single();
                ValidateTemplates(playerSource, doorSource);

                EnsureFolder(MaterialFolder);
                var materials = CreateMaterials(shader);
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                Transform root = new GameObject("Motel Blockout").transform;
                var groups = new Dictionary<string, Transform>();
                foreach (var box in plan.Boxes)
                {
                    if (!groups.TryGetValue(box.Group, out Transform group))
                    {
                        group = new GameObject(box.Group).transform;
                        group.SetParent(root, false);
                        groups.Add(box.Group, group);
                    }
                    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.name = box.Name;
                    cube.transform.SetParent(group, false);
                    cube.transform.localPosition = new Vector3(box.X, box.Y, box.Z);
                    cube.transform.localScale = new Vector3(box.Width, box.Height, box.Depth);
                    cube.GetComponent<MeshRenderer>().sharedMaterial = materials[box.Material];
                    if (!box.Solid) Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
                }

                Transform doors = Child(root, "Doors");
                foreach (var door in plan.Doors)
                {
                    GameObject clone = Object.Instantiate(doorSource.gameObject);
                    SceneManager.MoveGameObjectToScene(clone, scene);
                    clone.name = door.Name + (door.Operable ? "" : " (Closed)");
                    clone.transform.SetParent(doors, false);
                    Quaternion rotation = Quaternion.Euler(0, door.Yaw, 0);
                    clone.transform.localPosition = new Vector3(door.X, 0, door.Z) - rotation * Vector3.right * (door.Width / 2);
                    clone.transform.localRotation = rotation;
                    BoxCollider panel = clone.GetComponentsInChildren<BoxCollider>().Single();
                    panel.transform.localPosition = new Vector3(door.Width / 2, 1.14f, 0);
                    panel.transform.localScale = new Vector3(door.Width - 0.02f, 2.24f, 0.12f);
                    foreach (Transform part in clone.transform)
                        if (part.name == "Door Handle" || part.name == "Inside Handle")
                            part.localPosition = new Vector3(door.Width - 0.2f, part.localPosition.y, part.localPosition.z);
                    SerializedObject doorData = new SerializedObject(clone.GetComponent<PrototypeDoor>());
                    doorData.FindProperty("openAngle").floatValue = door.OpenAngle;
                    doorData.ApplyModifiedPropertiesWithoutUndo();
                    if (!door.Operable)
                    {
                        Object.DestroyImmediate(clone.GetComponent<PrototypeDoor>());
                        Object.DestroyImmediate(clone.GetComponent<Rigidbody>());
                    }
                }

                GameObject player = Object.Instantiate(playerSource.gameObject);
                SceneManager.MoveGameObjectToScene(player, scene);
                player.name = "Player";
                player.transform.position = new Vector3(plan.SpawnX, 0.05f, plan.SpawnZ);
                player.transform.rotation = Quaternion.Euler(0, plan.SpawnYaw, 0);
                CheckPlayerReferences(player);
                MotelBlockoutSigns.Rebuild(plan, Child(root, "Signs"));
                CreateLights(plan, Child(root, "Lighting"));
                MotelLoopSceneSetup.Create(root, player.GetComponent<FirstPersonController>(), materials);
                MotelLeonidSceneSetup.Create(root);
                MotelLoopChecks.ValidateScene(scene);
                MotelLeonidChecks.ValidateScene(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Could not save " + ScenePath);
                Debug.Log(report + "\nScene saved: " + ScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (closeSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
            }
        }

        private static void ValidateTemplates(FirstPersonController player, PrototypeDoor door)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            var interaction = new SerializedObject(player.GetComponent<PrototypeInteractor>());
            var doorData = new SerializedObject(door);
            BoxCollider panel = door.GetComponentsInChildren<BoxCollider>().Single();
            if (Mathf.Abs(cc.radius - 0.3f) > 0.001f || Mathf.Abs(cc.skinWidth - 0.03f) > 0.001f ||
                Mathf.Abs(cc.height - 1.8f) > 0.001f || Mathf.Abs(interaction.FindProperty("interactionDistance").floatValue - 2.5f) > 0.001f ||
                Mathf.Abs(doorData.FindProperty("openAngle").floatValue + 95) > 0.001f ||
                Vector3.Distance(panel.transform.localScale, new Vector3(1.48f, 2.24f, 0.12f)) > 0.001f ||
                Vector3.Distance(panel.transform.localPosition, new Vector3(0.75f, 1.14f, 0)) > 0.001f)
                throw new InvalidOperationException("Prototype templates differ from validated dimensions; update the layout check before rebuilding.");
        }

        private static void CheckPlayerReferences(GameObject player)
        {
            var movement = new SerializedObject(player.GetComponent<FirstPersonController>());
            var interaction = new SerializedObject(player.GetComponent<PrototypeInteractor>());
            Camera camera = player.GetComponentInChildren<Camera>();
            if (movement.FindProperty("viewCamera").objectReferenceValue != camera ||
                interaction.FindProperty("viewCamera").objectReferenceValue != camera ||
                interaction.FindProperty("player").objectReferenceValue != player.GetComponent<FirstPersonController>())
                throw new InvalidOperationException("Player references were not preserved during cloning.");
        }

        private static void CreateLights(MotelBlockoutPlan plan, Transform parent)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.4f);
            RenderSettings.fog = false;
            Transform sun = Child(parent, "Sun");
            sun.rotation = Quaternion.Euler(55, -35, 0);
            Light main = sun.gameObject.AddComponent<Light>();
            main.type = LightType.Directional;
            main.intensity = 1;
            main.shadows = LightShadows.Soft;
            RenderSettings.sun = main;
            for (int i = 0; i < 6; i++) PointLight(parent, "Corridor Light " + i, 4.8f + 4 * i, 0);
            foreach (var room in plan.Rooms)
            {
                PointLight(parent, room.Id + " Living Light", room.X(2.5f), room.Z(4.5f));
                PointLight(parent, room.Id + " Bathroom Light", room.X(3.5f), room.Z(1.1f));
            }
            foreach (var zone in plan.Zones.Where(z => z.Id == "L04" || z.Id == "L11" || z.Id == "L06"))
                PointLight(parent, zone.Id + " Light", (zone.MinX + zone.MaxX) / 2, (zone.MinZ + zone.MaxZ) / 2);
            foreach (float z in new[] { -3f, -7f, -10f, -13.5f }) PointLight(parent, "Service Light " + z, 4.8f, z);
            PointLight(parent, "Reception Light", 1.7f, -14.8f);
        }

        private static void PointLight(Transform parent, string name, float x, float z)
        {
            Transform node = Child(parent, name);
            node.localPosition = new Vector3(x, 2.6f, z);
            Light light = node.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1, 0.93f, 0.82f);
            light.intensity = 2.5f;
            light.range = 7;
        }

        private static Dictionary<string, Material> CreateMaterials(Shader shader)
        {
            var colors = new Dictionary<string, Color>
            {
                ["Ground"] = new Color(0.23f, 0.3f, 0.22f), ["Floor"] = new Color(0.4f, 0.38f, 0.33f),
                ["Wall"] = new Color(0.68f, 0.65f, 0.56f), ["Ceiling"] = new Color(0.82f, 0.82f, 0.78f),
                ["Furniture"] = new Color(0.3f, 0.19f, 0.12f), ["Equipment"] = new Color(0.23f, 0.3f, 0.35f),
                ["Marker"] = new Color(0.12f, 0.55f, 0.25f), ["Asphalt"] = new Color(0.16f, 0.17f, 0.18f),
                ["Paving"] = new Color(0.4f, 0.43f, 0.44f), ["Paint"] = new Color(0.95f, 0.85f, 0.5f),
                ["Glass"] = new Color(0.55f, 0.77f, 0.85f, 0.2f)
            };
            var result = new Dictionary<string, Material>();
            foreach (var entry in colors)
            {
                string path = MaterialFolder + "/" + entry.Key + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = entry.Key };
                    material.SetColor("_BaseColor", entry.Value);
                    material.SetFloat("_Smoothness", 0.1f);
                    if (entry.Key == "Glass")
                    {
                        material.SetFloat("_Surface", 1);
                        material.SetFloat("_Blend", 0);
                        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                        material.SetFloat("_ZWrite", 0);
                        material.SetFloat("_Cull", (float)CullMode.Off);
                        material.SetFloat("_Smoothness", 0.65f);
                        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        material.SetOverrideTag("RenderType", "Transparent");
                        material.SetShaderPassEnabled("ShadowCaster", false);
                        material.renderQueue = (int)RenderQueue.Transparent;
                    }
                    AssetDatabase.CreateAsset(material, path);
                    AssetDatabase.SaveAssetIfDirty(material);
                }
                result.Add(entry.Key, material);
            }
            return result;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
