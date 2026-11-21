using UnityEngine;

namespace Motel.Editor
{
    public static class MotelConnectionSceneSetup
    {
        public static readonly Vector3 WardrobeStart = new Vector3(21.08f, 0, -5.53f+MotelBlockoutPlan.ResidentialZ);
        public static readonly Vector3 WardrobeEnd = new Vector3(21.08f, 0, -4.30f+MotelBlockoutPlan.ResidentialZ);

        public static void Create(Transform root, Transform belongings, MotelGuestRoomModule.Layout room,
            MotelLoopController loop, DialogueController window)
        {
            CreateCot(belongings, room);
            Transform wardrobe = Child(belongings, "Movable Wardrobe"); wardrobe.position = WardrobeStart;
            var box = wardrobe.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 0.95f, 0); box.size = new Vector3(0.48f, 1.7f, 0.90f);
            var body = wardrobe.gameObject.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            var slide = wardrobe.gameObject.AddComponent<SlidingWardrobe>(); slide.Configure(loop);
            Part(wardrobe, "Wooden Cabinet", new Vector3(0, 0.95f, 0), box.size, "Furniture");
            for (int side = -1; side <= 1; side += 2)
            {
                Part(wardrobe, "Cabinet Door " + side, new Vector3(-0.25f, 0.95f, side * 0.22f), new Vector3(0.025f, 1.56f, 0.42f), "Equipment");
                Part(wardrobe, "Handle " + side, new Vector3(-0.28f, 1, side * 0.055f), new Vector3(0.035f, 0.18f, 0.025f), "Paint");
            }
            var door = root.Find("Doors/Connection 7-8").GetComponent<PrototypeDoor>();
            door.ConfigureObstruction(slide);
            // Visible trim above/beside the smaller cabinet lets the player notice the hidden door.
            Transform trim = Child(belongings, "Connection Door Frame");
            foreach (float z in new[] { -4.84f, -6.22f })
                Part(trim, "Upright " + z, new Vector3(21.38f, 1.17f, z+MotelBlockoutPlan.ResidentialZ), new Vector3(0.07f, 2.34f, 0.06f), "Paint");
            Part(trim, "Lintel", new Vector3(21.38f, 2.34f, -5.53f+MotelBlockoutPlan.ResidentialZ), new Vector3(0.07f, 0.07f, 1.44f), "Paint");
            CreatePlan(root, window);
        }

        private static void CreateCot(Transform parent, MotelGuestRoomModule.Layout room)
        {
            Transform cot = Child(parent, "Broken Folding Cot"); cot.position = new Vector3(room.X(1.3f), 0, room.Z(5.37f));
            cot.localRotation = Quaternion.Euler(0, 90, 0);
            var collider = cot.gameObject.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 0.31f, 0); collider.size = new Vector3(1.96f, 0.6f, 0.82f);
            Transform bed = Child(cot, "Sagging Frame"); bed.localPosition = new Vector3(0, 0.46f, 0); bed.localRotation = Quaternion.Euler(0, 0, -5);
            Part(bed, "Thin Fabric Mattress", Vector3.zero, new Vector3(1.83f, 0.10f, 0.70f), "Ceiling");
            foreach (int side in new[] { -1, 1 })
            {
                Part(bed, "Tubular Side " + side, new Vector3(0, -0.03f, side * 0.38f), new Vector3(1.93f, 0.045f, 0.045f), "Paint");
                Part(bed, "Tubular End " + side, new Vector3(side * 0.95f, -0.03f, 0), new Vector3(0.045f, 0.045f, 0.8f), "Paint");
                var leg = Part(cot, side < 0 ? "Intact Folding Support" : "Bent Folding Support",
                    new Vector3(side * 0.7f, side < 0 ? 0.25f : 0.18f, 0), new Vector3(0.045f, side < 0 ? 0.46f : 0.38f, 0.045f), "Paint");
                leg.localRotation = Quaternion.Euler(0, 0, side < 0 ? -22 : 65);
                Part(cot, "Support Foot " + side, new Vector3(side * 0.8f, 0.045f, 0), new Vector3(0.06f, 0.055f, 0.75f), "Paint");
            }
            Part(cot, "Detached Leg Segment", new Vector3(0.8f, 0.07f, 0.22f), new Vector3(0.3f, 0.045f, 0.045f), "Paint");
        }

        private static void CreatePlan(Transform root, DialogueController window)
        {
            Transform map = Child(root, "Reception Room Plan"); map.position = new Vector3(0.14f, 1.52f, -12.2f); map.rotation = Quaternion.Euler(0, -90, 0);
            Part(map, "Plan Board", Vector3.zero, new Vector3(1.15f, 0.8f, 0.04f), "Ceiling");
            var box = map.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(1.15f, 0.8f, 0.04f);
            map.gameObject.AddComponent<MotelFloorPlan>().Configure(window);
            // Local -Z is the readable front of WorldSign. Dark lines are physical, depth-tested URP primitives.
            foreach (float y in new[] { -0.16f, 0.29f }) Part(map, "Plan Wall", new Vector3(0, y, -0.026f), new Vector3(0.92f, 0.015f, 0.012f), "Equipment");
            foreach (float x in new[] { -0.46f, 0.46f }) Part(map, "Plan Outer Wall", new Vector3(x, 0.065f, -0.026f), new Vector3(0.015f, 0.45f, 0.012f), "Equipment");
            foreach (float y in new[] { -0.10f, 0.23f }) Part(map, "Plan Divider", new Vector3(0, y, -0.026f), new Vector3(0.015f, 0.12f, 0.012f), "Equipment");
            Part(map, "Marked Connecting Door", new Vector3(0.045f, 0.065f, -0.033f), new Vector3(0.09f, 0.11f, 0.014f), "Marker");
            Label(map, "Room 7", "7", new Vector3(-0.24f, 0.09f, -0.045f), 0.16f, 0.18f);
            Label(map, "Room 8", "8", new Vector3(0.24f, 0.09f, -0.045f), 0.16f, 0.18f);
            Label(map, "Corridor", "КОРИДОР", new Vector3(0, -0.25f, -0.045f), 0.72f, 0.07f);
            Label(map, "Connection Caption", "Соединительная дверь", new Vector3(0, 0.35f, -0.045f), 0.95f, 0.055f);
        }
        private static void Label(Transform parent, string name, string text, Vector3 position, float width, float height)
        {
            Transform holder = Child(parent, name); holder.localPosition = position;
            MotelLoopSceneSetup.Text(holder, "Text", text, width, height, Color.black);
        }
        private static Transform Part(Transform parent, string name, Vector3 pos, Vector3 size, string mat)
            => MotelGuestVisuals.Part(parent, name, PrimitiveType.Cube, pos, size, mat);
        private static Transform Child(Transform parent, string name)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    }
}
