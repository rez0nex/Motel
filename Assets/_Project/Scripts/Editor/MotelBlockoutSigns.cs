using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelBlockoutSigns
    {
        private const string MaterialFolder = "Assets/_Project/Art/BlockoutMaterials";

        public static void Rebuild(MotelBlockoutPlan plan, Transform parent)
        {
            // Do not remove unknown user objects from the sign group.
            foreach (Transform child in parent)
                if (child.GetComponent<TextMesh>() == null && child.Find("Text")?.GetComponent<WorldSign>() == null)
                    throw new InvalidOperationException("Unknown object in Signs: " + child.name);

            Shader shader = Shader.Find("Motel/World Sign Text");
            if (shader == null) throw new InvalidOperationException("Import WorldSignText.shader before rebuilding signs.");
            Material ink = GetMaterial("SignInk", shader, Color.black);
            Material plate = GetMaterial("SignPlate", Shader.Find("Universal Render Pipeline/Lit"), new Color(0.9f, 0.88f, 0.78f));
            for (int i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var sign in plan.Signs)
            {
                Transform plaque = new GameObject("Sign " + (string.IsNullOrEmpty(sign.Id) ? sign.Text : sign.Id)).transform;
                plaque.SetParent(parent, false);
                plaque.localPosition = new Vector3(sign.X, sign.Y, sign.Z);
                plaque.localRotation = Quaternion.Euler(0, sign.Yaw, 0);
                float width = sign.Text.All(char.IsDigit) ? 0.28f : (sign.Text.Length <= 5 ? 0.42f : 0.86f);
                const float height = 0.22f;
                GameObject backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backing.name = "Plate";
                backing.transform.SetParent(plaque, false);
                backing.transform.localPosition = new Vector3(0, 0, 0.012f);
                backing.transform.localScale = new Vector3(width, height, 0.012f);
                Object.DestroyImmediate(backing.GetComponent<BoxCollider>());
                backing.GetComponent<MeshRenderer>().sharedMaterial = plate;
                Transform letters = new GameObject("Text").transform;
                letters.SetParent(plaque, false);
                TextMesh text = letters.gameObject.AddComponent<TextMesh>();
                text.text = sign.Text;
                text.font = font;
                text.fontSize = 64; // Atlas resolution; measured bounds below determine world size.
                text.characterSize = 1;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(0.03f, 0.04f, 0.04f);
                MeshRenderer renderer = text.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = ink;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                WorldSign label = letters.gameObject.AddComponent<WorldSign>();
                label.Configure(width - 0.06f, height - 0.07f);
                if (!label.RefreshNow()) throw new InvalidOperationException("Text bounds unavailable: " + sign.Text);
            }
            Validate(parent, plan.Signs.Count);
        }

        public static void Validate(Transform parent, int expectedCount)
        {
            TextMesh[] texts = parent.GetComponentsInChildren<TextMesh>(true);
            if (parent.childCount != expectedCount || texts.Length != expectedCount)
                throw new InvalidOperationException("Sign count does not match the plan.");
            foreach (TextMesh text in texts)
            {
                WorldSign label = text.GetComponent<WorldSign>();
                MeshRenderer renderer = text.GetComponent<MeshRenderer>();
                if (label == null || !label.RefreshNow() || renderer.sharedMaterial.shader.name != "Motel/World Sign Text")
                    throw new InvalidOperationException("Invalid sign material or layout: " + text.text);
                Vector3 size = renderer.localBounds.size * text.transform.localScale.x;
                if (size.x > 0.801f || size.y > 0.151f || text.transform.lossyScale.x <= 0 ||
                    text.transform.lossyScale.y <= 0 || text.transform.lossyScale.z <= 0)
                    throw new InvalidOperationException("World sign exceeds physical limits: " + text.text);
                if (text.GetComponentInParent<Collider>() != null || text.GetComponent<Collider>() != null)
                    throw new InvalidOperationException("Sign must not block interaction rays: " + text.text);
            }
        }

        private static Material GetMaterial(string name, Shader shader, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            if (shader == null || !AssetDatabase.IsValidFolder(MaterialFolder))
                throw new InvalidOperationException("Blockout material folder or shader missing.");
            result = new Material(shader) { name = name };
            if (result.HasProperty("_BaseColor")) result.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(result, path);
            AssetDatabase.SaveAssetIfDirty(result);
            return result;
        }
    }
}
