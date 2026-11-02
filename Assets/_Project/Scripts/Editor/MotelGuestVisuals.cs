using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelGuestVisuals
    {
        public static void Person(Transform parent, string shirt = "Equipment")
        {
            Part(parent, "Torso", PrimitiveType.Capsule, new Vector3(0, 1.05f, 0), new Vector3(0.4f, 0.36f, 0.3f), shirt);
            Part(parent, "Head", PrimitiveType.Sphere, new Vector3(0, 1.54f, 0), new Vector3(0.28f, 0.32f, 0.28f), "Ceiling");
            Part(parent, "Nose", PrimitiveType.Cube, new Vector3(0, 1.54f, 0.15f), new Vector3(0.06f, 0.06f, 0.06f), "Ceiling");
            foreach (int side in new[] { -1, 1 })
            {
                Part(parent, "Leg " + side, PrimitiveType.Cube, new Vector3(side * 0.11f, 0.4f, 0), new Vector3(0.15f, 0.8f, 0.18f), "Furniture");
                Part(parent, "Arm " + side, PrimitiveType.Cube, new Vector3(side * 0.26f, 1.01f, 0), new Vector3(0.11f, 0.55f, 0.13f), shirt);
            }
        }

        public static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, string material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/BlockoutMaterials/" + material + ".mat");
            Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }
    }
}
