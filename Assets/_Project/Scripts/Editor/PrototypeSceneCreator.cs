using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Motel.Editor
{
    public static class PrototypeSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/Prototype.unity";
        private const string MaterialFolder = "Assets/_Project/Art/PrototypeMaterials";

        [MenuItem("Motel/Create Prototype Scene")]
        public static void CreatePrototypeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Motel", "Сначала остановите Play Mode.", "OK");
                return;
            }

            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Перезаписать Prototype?",
                    "Сцена Assets/Scenes/Prototype.unity будет создана заново. Изменения в ней будут потеряны.",
                    "Перезаписать", "Отмена"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Cannot create prototype: Universal Render Pipeline/Lit shader is unavailable.");
                return;
            }

            EnsureFolder("Assets/Scenes");
            EnsureFolder(MaterialFolder);
            Material floor = GetMaterial("Floor", new Color(0.27f, 0.29f, 0.3f), shader);
            Material wall = GetMaterial("Wall", new Color(0.65f, 0.62f, 0.53f), shader);
            Material doorMaterial = GetMaterial("Door", new Color(0.24f, 0.12f, 0.07f), shader);
            Material trim = GetMaterial("Trim", new Color(0.13f, 0.15f, 0.17f), shader);

            // A new single scene replaces the open scene, rather than appending generated objects.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Transform environment = new GameObject("Prototype Environment").transform;
            Cube("Floor", new Vector3(0f, -0.15f, 1.5f), new Vector3(8.4f, 0.3f, 11.4f), floor, environment);
            Cube("Left Wall", new Vector3(-4.1f, 1.5f, 1.5f), new Vector3(0.2f, 3f, 11.4f), wall, environment);
            Cube("Right Wall", new Vector3(4.1f, 1.5f, 1.5f), new Vector3(0.2f, 3f, 11.4f), wall, environment);
            Cube("Front Wall", new Vector3(0f, 1.5f, -4.1f), new Vector3(8f, 3f, 0.2f), wall, environment);
            Cube("Back Wall", new Vector3(0f, 1.5f, 7.1f), new Vector3(8f, 3f, 0.2f), wall, environment);

            // Opening: x = [-0.75, 0.75], y = [0, 2.3], at z = 1.
            Cube("Doorway Left", new Vector3(-2.375f, 1.5f, 1f), new Vector3(3.25f, 3f, 0.2f), wall, environment);
            Cube("Doorway Right", new Vector3(2.375f, 1.5f, 1f), new Vector3(3.25f, 3f, 0.2f), wall, environment);
            Cube("Doorway Header", new Vector3(0f, 2.65f, 1f), new Vector3(1.5f, 0.7f, 0.2f), wall, environment);
            Cube("Left Frame", new Vector3(-0.81f, 1.15f, 0.96f), new Vector3(0.12f, 2.3f, 0.3f), trim, environment);
            Cube("Right Frame", new Vector3(0.81f, 1.15f, 0.96f), new Vector3(0.12f, 2.3f, 0.3f), trim, environment);
            Cube("Top Frame", new Vector3(0f, 2.36f, 0.96f), new Vector3(1.74f, 0.12f, 0.3f), trim, environment);
            Cube("Room Block", new Vector3(2.7f, 0.4f, 5.7f), new Vector3(1.2f, 0.8f, 1.4f), trim, environment);

            CreateDoor(environment, doorMaterial, trim);
            CreatePlayer();
            CreateLighting();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("Prototype scene could not be saved. The generated scene remains open.");
                return;
            }

            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("Motel prototype saved to " + ScenePath + ". Press Play, then click the Game view.");
        }

        [MenuItem("Motel/Create Prototype Scene", true)]
        private static bool CanCreatePrototypeScene()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void CreateDoor(Transform parent, Material panelMaterial, Material handleMaterial)
        {
            GameObject hinge = new GameObject("Door Hinge");
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = new Vector3(-0.75f, 0f, 1f);
            Rigidbody body = hinge.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            hinge.AddComponent<PrototypeDoor>();
            Cube("Door Panel", new Vector3(0.75f, 1.14f, 0f), new Vector3(1.48f, 2.24f, 0.12f), panelMaterial, hinge.transform);
            GameObject handle = Cube("Door Handle", new Vector3(1.3f, 1.05f, -0.1f),
                new Vector3(0.18f, 0.06f, 0.14f), handleMaterial, hinge.transform);
            Object.DestroyImmediate(handle.GetComponent<BoxCollider>());
            GameObject backHandle = Cube("Inside Handle", new Vector3(1.3f, 1.05f, 0.1f),
                new Vector3(0.18f, 0.06f, 0.14f), handleMaterial, hinge.transform);
            Object.DestroyImmediate(backHandle.GetComponent<BoxCollider>());
        }

        private static void CreatePlayer()
        {
            GameObject player = new GameObject("Player");
            player.layer = 2; // Built-in Ignore Raycast layer; physical collisions still work.
            player.transform.position = new Vector3(0f, 0.05f, -1.35f);
            CharacterController character = player.AddComponent<CharacterController>();
            character.height = 1.8f;
            character.radius = 0.3f;
            character.center = new Vector3(0f, 0.9f, 0f);
            character.skinWidth = 0.03f;
            character.stepOffset = 0.25f;
            character.slopeLimit = 45f;
            character.minMoveDistance = 0f;

            GameObject cameraObject = new GameObject("Player Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.fieldOfView = 70f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            cameraObject.AddComponent<AudioListener>();

            FirstPersonController controller = player.AddComponent<FirstPersonController>();
            SetReference(controller, "viewCamera", camera);
            PrototypeInteractor interactor = player.AddComponent<PrototypeInteractor>();
            SetReference(interactor, "player", controller);
            SetReference(interactor, "viewCamera", camera);
        }

        private static void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.35f, 0.37f, 0.4f);
            RenderSettings.fog = false;
            GameObject sunObject = new GameObject("Directional Light");
            sunObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            GameObject roomLight = new GameObject("Room Light");
            roomLight.transform.position = new Vector3(0f, 2.6f, 4.5f);
            Light light = roomLight.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.7f);
            light.intensity = 3f;
            light.range = 7f;
        }

        private static GameObject Cube(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        private static Material GetMaterial(string name, Color color, Shader shader)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            Material material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void SetReference(Object component, string property, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
