using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelLoopSceneSetup
    {
        public static void Create(Transform root, FirstPersonController player, Dictionary<string, Material> materials)
        {
            if (root.Find("Time Loop") != null) throw new InvalidOperationException("A loop already exists in this blockout.");
            Transform group = Child(root, "Time Loop");
            MotelLoopController loop = group.gameObject.AddComponent<MotelLoopController>();
            Transform clock = Child(group, "Reception Clock");
            clock.localPosition = new Vector3(2.93f, 1.4f, -12.8f);
            clock.localRotation = Quaternion.Euler(0, 90, 0); // Face the administrator west of the desk.
            Box(clock, "Clock Housing", new Vector3(0, 0, 0.06f), new Vector3(0.7f, 0.23f, 0.08f), materials["Equipment"], false);
            Box(clock, "Clock Stand", new Vector3(0, -0.23f, 0.06f), new Vector3(0.09f, 0.23f, 0.08f), materials["Equipment"], false);
            TextMesh display = Text(clock, "Digital Time", "20:16:00", 0.63f, 0.16f, new Color(0.35f, 1, 0.55f));

            // The technical-room builder assigns the final equipment after the shared loop exists.
            GameObject equipment = Child(group, "Equipment Placeholder").gameObject;
            GameObject indicator = Child(group, "Indicator Placeholder").gameObject;
            Transform button = Child(group, "Emergency Cutoff");
            // East face of the equipment, reached from the technical room's clear central aisle.
            button.localPosition = new Vector3(1.92f, 1.1f, -8.96f);
            Box(button, "Cutoff Button", Vector3.zero, new Vector3(0.11f, 0.22f, 0.22f), materials["Furniture"], true);
            button.gameObject.AddComponent<EmergencyCutoff>().Configure(loop);
            Transform sign = Child(group, "Emergency Cutoff Label");
            sign.localPosition = new Vector3(1.94f, 1.6f, -9.3f);
            sign.localRotation = Quaternion.Euler(0, -90, 0);
            Text(sign, "Text", "Аварийное отключение", 0.8f, 0.1f, Color.white);

            Transform damage = Child(group, "Prepared Damage");
            Transform cabinet = Box(damage, "Damaged Equipment", new Vector3(1.1f, 0.7f, -9.3f),
                new Vector3(1.5f, 1.4f, 1.2f), materials["Equipment"], true);
            cabinet.localRotation = Quaternion.Euler(0, 0, -11);
            Transform fallen = Box(damage, "Fallen Panel", new Vector3(2.2f, 0.12f, -9.6f),
                new Vector3(0.7f, 0.08f, 0.55f), materials["Furniture"], true);
            fallen.localRotation = Quaternion.Euler(0, 24, 7);
            for (int i = 0; i < 5; i++)
            {
                Transform debris = Box(damage, "Debris " + i, new Vector3(1.9f + i * 0.18f, 0.07f, -8.6f - (i % 2) * 0.4f),
                    new Vector3(0.22f, 0.12f, 0.16f), materials["Equipment"], true);
                debris.localRotation = Quaternion.Euler(0, i * 37, 0);
            }
            damage.gameObject.SetActive(false);
            Light[] lights = root.Find("Lighting").GetComponentsInChildren<Light>();
            loop.Configure(player, display, indicator, equipment, damage.gameObject, lights);
        }

        internal static TextMesh Text(Transform parent, string name, string content, float width, float height, Color color)
        {
            Transform node = Child(parent, name);
            TextMesh text = node.gameObject.AddComponent<TextMesh>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = 1;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/BlockoutMaterials/SignInk.mat");
            node.gameObject.AddComponent<WorldSign>().Configure(width, height);
            return text;
        }

        private static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            return cube.transform;
        }

        private static Transform Child(Transform parent, string name)
        {
            Transform node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }
    }
}
