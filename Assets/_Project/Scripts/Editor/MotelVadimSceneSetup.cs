using System;
using System.Linq;
using UnityEngine;

namespace Motel.Editor
{
    public static class MotelVadimSceneSetup
    {
        public static Vector3[] Route(MotelGuestRoomModule.Layout room) => new[]
        {
            At(room.X(1.25f), room.Z(4)),
            At(room.X(1.15f), room.Z(2.9f)),
            At(room.X(1.15f), room.Z(1.7f)),
            At(room.X(0.8f), room.Z(1.7f)),
            At(room.X(0.8f), 0), // Gate 4: wait beyond the room door's inward swing.
            At(4.8f, 0),
            At(4.8f, -11.8f),
            At(4.8f, -13.6f),
            At(8, -13.6f), // Gate 8: main entrance, opens outside.
            At(10, -16),
            At(17, -18.1f) // Separate slot on the same assembly marker, west of Leonid.
        };

        public static void Create(Transform root)
        {
            if (root.Find("Vadim Episode") != null) throw new InvalidOperationException("Vadim episode already exists.");
            var room = MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 2);
            var loop = root.GetComponentInChildren<MotelLoopController>(true);
            var dialogue = root.GetComponentInChildren<DialogueController>(true);
            if (loop == null || dialogue == null) throw new InvalidOperationException("Create loop and shared dialogue first.");
            Transform group = Child(root, "Vadim Episode");
            Transform safe = Child(group, "Vadim Safe Point");
            safe.position = At(17, -18.1f);
            Transform npc = Child(group, "Vadim");
            npc.position = At(room.X(3), room.Z(4));
            npc.rotation = Quaternion.Euler(0, 180, 0);
            var cc = npc.gameObject.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.28f; cc.center = new Vector3(0, 0.86f, 0);
            cc.skinWidth = 0.025f; cc.stepOffset = 0.15f; cc.minMoveDistance = 0;
            MotelGuestVisuals.Person(Child(npc, "Vadim Placeholder"), "Paint");
            npc.gameObject.AddComponent<VadimEvacuee>().Configure(loop, dialogue, Route(room), safe,
                root.Find("Doors/Room2").GetComponent<PrototypeDoor>(), root.Find("Doors/Main Entrance").GetComponent<PrototypeDoor>());

            Transform panel = Child(group, "Equipment Diagnostics");
            panel.position = new Vector3(2.55f, 1.45f, -10.67f);
            Transform board = MotelGuestVisuals.Part(panel, "Diagnostic Panel", PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.76f, 0.5f, 0.055f), "Equipment");
            board.gameObject.AddComponent<BoxCollider>();
            panel.gameObject.AddComponent<EquipmentInspection>().Configure(loop, dialogue);
            Transform text = Child(panel, "Diagnostic Label");
            text.localPosition = new Vector3(0, 0.08f, 0.033f);
            text.localRotation = Quaternion.Euler(0, 180, 0);
            MotelLoopSceneSetup.Text(text, "Text", "Диагностика", 0.66f, 0.1f, Color.white);
            MotelGuestVisuals.Part(panel, "Overheat Signal", PrimitiveType.Cube,
                new Vector3(0, -0.12f, 0.035f), new Vector3(0.46f, 0.055f, 0.025f), "Paint");
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
