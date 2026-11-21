using System;
using System.Linq;
using UnityEngine;

namespace Motel.Editor
{
    public static class MotelFamilySceneSetup
    {
        public static Vector3[] MarinaRoute(MotelGuestRoomModule.Layout room) => new[]
        {
            At(room.X(1.25f), room.Z(4)), At(room.X(1.15f), room.Z(2.9f)),
            At(room.X(1.15f), room.Z(1.7f)), At(room.X(0.8f), room.Z(1.7f)),
            At(room.X(0.8f), MotelBlockoutPlan.ResidentialZ), At(MotelBlockoutPlan.WingEnd - 1.2f, MotelBlockoutPlan.ResidentialZ),
            At(MotelBlockoutPlan.WingEnd + 1.7f, MotelBlockoutPlan.ResidentialZ), At(MotelBlockoutPlan.WingEnd + 1.7f, -12),
            At(25, -16.8f), At(19, -17.25f)
        };

        public static void Create(Transform root)
        {
            if (root.Find("Family Episode") != null) throw new InvalidOperationException("Family episode already exists.");
            var plan = MotelBlockoutPlan.Create();
            var seven = plan.Rooms.Single(r => r.Number == 7); var eight = plan.Rooms.Single(r => r.Number == 8);
            var loop = root.GetComponentInChildren<MotelLoopController>(true);
            var window = root.GetComponentInChildren<DialogueController>(true);
            var player = UnityEngine.Object.FindObjectsByType<FirstPersonController>().Single(p => p.gameObject.scene == root.gameObject.scene);
            Transform group = Child(root, "Family Episode");
            var family = group.gameObject.AddComponent<FamilyEpisode>();
            Transform mother = Person(group, "Marina", At(seven.X(1.8f), seven.Z(4)), "Marker");
            Transform daughter = Person(group, "Lera", At(eight.X(1.4f), eight.Z(4)), "Ceiling");
            var marina = mother.gameObject.AddComponent<MarinaEvacuee>();
            var lera = daughter.gameObject.AddComponent<LeraEvacuee>();
            Transform marinaSafe = Child(group, "Marina Safe Point"); marinaSafe.position = At(19, -17.25f);
            Transform leraSafe = Child(group, "Lera Safe Point"); leraSafe.position = At(17.5f, -17);

            Transform keys = Child(group, "Reception Duplicate Keys"); keys.position = new Vector3(3, 1.1f, -14.3f);
            var ring = MotelGuestVisuals.Part(keys, "Keys 7-8", PrimitiveType.Cube, Vector3.zero, new Vector3(0.22f, 0.07f, 0.12f), "Paint");
            ring.gameObject.AddComponent<BoxCollider>();
            keys.gameObject.AddComponent<RoomKeysPickup>().Configure(family);
            family.Configure(loop, window, player.transform, marina, lera, keys.gameObject);
            marina.Configure(family, MarinaRoute(seven), marinaSafe,
                root.Find("Doors/Room7").GetComponent<PrototypeDoor>(), root.Find("Doors/Emergency Exit").GetComponent<PrototypeDoor>());
            lera.Configure(family, leraSafe);
            var lock8 = root.Find("Doors/Room8").gameObject.AddComponent<RoomEightLock>();
            lock8.Configure(family, new Bounds(new Vector3(eight.X(2.5f), 1.4f, eight.Z(3.25f)), new Vector3(5, 3, 6.5f)));

            Transform belongings = Child(group, "Room7 Shared Stay");
            MotelConnectionSceneSetup.Create(root, belongings, seven, loop, window);
            for (int i = 0; i < 2; i++)
            {
                MotelGuestVisuals.Part(belongings, "Mug " + i, PrimitiveType.Cylinder,
                    new Vector3(seven.X(0.4f), 0.82f, seven.Z(5.4f + i * 0.5f)), new Vector3(0.11f, 0.07f, 0.11f), "Ceiling");
                MotelGuestVisuals.Part(belongings, "Travel Bag " + i, PrimitiveType.Cube,
                    new Vector3(seven.X(2.8f + i * 0.6f), 0.78f, seven.Z(5.5f)), new Vector3(0.4f, 0.3f, 0.3f), "Equipment");
            }
        }

        private static Transform Person(Transform parent, string name, Vector3 position, string shirt)
        {
            Transform person = Child(parent, name); person.position = position;
            var cc = person.gameObject.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.86f, 0);
            cc.skinWidth = 0.025f; cc.stepOffset = 0.15f; cc.minMoveDistance = 0;
            MotelGuestVisuals.Person(Child(person, "Placeholder"), shirt);
            return person;
        }
        private static Vector3 At(float x, float z) => new Vector3(x, 0.05f, z);
        private static Transform Child(Transform parent, string name)
        { var value = new GameObject(name).transform; value.SetParent(parent, false); return value; }
    }
}
