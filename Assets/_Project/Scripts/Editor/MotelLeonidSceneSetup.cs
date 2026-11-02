using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelLeonidSceneSetup
    {
        public static Vector3[] Route(MotelGuestRoomModule.Layout room) => new[]
        {
            At(room.X(1.25f), room.Z(4)),
            At(room.X(1.15f), room.Z(2.9f)),
            At(room.X(1.15f), room.Z(1.7f)),
            At(room.X(0.8f), room.Z(1.7f)), // Beyond the full entrance-door swing plus body clearance.
            At(room.X(0.8f), 0),
            At(MotelBlockoutPlan.WingEnd - 1.2f, 0),
            At(MotelBlockoutPlan.WingEnd + 1.7f, 0),
            At(MotelBlockoutPlan.WingEnd + 1.7f, -10),
            At(25, -18.1f),
            At(18, -18.1f)
        };

        public static void Create(Transform root)
        {
            if (root.Find("Leonid Episode") != null)
                throw new InvalidOperationException("Leonid episode already exists; do not append a duplicate.");
            var room = MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 4);
            var loop = root.GetComponentInChildren<MotelLoopController>(true);
            if (loop == null) throw new InvalidOperationException("Create the loop before Leonid.");
            Transform group = Child(root, "Leonid Episode");
            var dialogue = group.gameObject.AddComponent<DialogueController>();
            dialogue.Configure(loop);
            loop.ConfigureDialogue(dialogue);
            Transform safe = Child(group, "Safe Point");
            safe.position = At(18, -18.1f);

            Transform npc = Child(group, "Leonid");
            npc.position = At(room.X(3), room.Z(4));
            var character = npc.gameObject.AddComponent<CharacterController>();
            character.height = 1.7f;
            character.radius = 0.28f;
            character.center = new Vector3(0, 0.86f, 0);
            character.skinWidth = 0.025f;
            character.stepOffset = 0.15f;
            character.minMoveDistance = 0;
            character.enabled = false;
            var leonid = npc.gameObject.AddComponent<LeonidEvacuee>();
            Transform sleeping = Child(npc, "Sleeping Placeholder");
            sleeping.localPosition = new Vector3(0.5f, 0.85f, 1.55f);
            sleeping.localRotation = Quaternion.Euler(0, 0, -90);
            MotelGuestVisuals.Person(sleeping);
            // Person coordinates are centred on a standing torso; recline along the bed's X axis.
            sleeping.localPosition -= sleeping.localRotation * new Vector3(0, 1.05f, 0);
            var target = npc.gameObject.AddComponent<BoxCollider>();
            target.center = new Vector3(0.5f, 0.87f, 1.55f);
            target.size = new Vector3(1.7f, 0.55f, 0.55f);

            Transform standing = Child(npc, "Standing Placeholder");
            MotelGuestVisuals.Person(standing);
            standing.gameObject.SetActive(false);
            Transform carried = Cane(standing, "Held Cane", new Vector3(0.32f, 0.02f, 0.05f));
            carried.gameObject.SetActive(false);
            leonid.Configure(loop, dialogue, sleeping.gameObject, standing.gameObject, carried.gameObject,
                target, Route(room), safe, root.Find("Doors/Room4").GetComponent<PrototypeDoor>(),
                root.Find("Doors/Emergency Exit").GetComponent<PrototypeDoor>());

            Transform pickup = Cane(group, "Leonid Cane", At(room.X(2.1f), room.Z(4.25f)));
            var collider = pickup.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0.04f, 0.5f, 0);
            collider.size = new Vector3(0.22f, 1.02f, 0.18f);
            pickup.gameObject.AddComponent<CanePickup>().Configure(leonid);
        }

        private static Transform Cane(Transform parent, string name, Vector3 position)
        {
            Transform cane = Child(parent, name);
            cane.localPosition = position;
            Primitive(cane, "Shaft", PrimitiveType.Cylinder, new Vector3(0, 0.46f, 0), new Vector3(0.035f, 0.46f, 0.035f), "Furniture");
            Primitive(cane, "Handle", PrimitiveType.Cube, new Vector3(0.055f, 0.94f, 0), new Vector3(0.16f, 0.045f, 0.055f), "Furniture");
            return cane;
        }

        private static void Primitive(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 size, string material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/BlockoutMaterials/" + material + ".mat");
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static Vector3 At(float x, float z) => new Vector3(x, 0.05f, z);
        private static Transform Child(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }
    }
}
