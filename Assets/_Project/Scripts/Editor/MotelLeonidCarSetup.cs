using UnityEditor;
using UnityEngine;

namespace Motel.Editor
{
    public static class MotelLeonidCarSetup
    {
        public static Vector3 CarPosition => new Vector3(22.5f, 0, -13.45f);
        public static Vector3 RearApproach => new Vector3(22.5f, 0.05f, -16.8f);
        public static Vector3 KeyPosition(MotelGuestRoomModule.Layout room) => new Vector3(room.X(4.68f), 0.56f, room.Z(5.36f));
        public static Vector3 KeyApproach(MotelGuestRoomModule.Layout room) => new Vector3(room.X(4.55f), 0.05f, room.Z(4.1f));

        public static void Create(Transform group, LeonidEvacuee owner, MotelLoopController loop,
            DialogueController dialogue, MotelGuestRoomModule.Layout room)
        {
            var supplies = group.gameObject.AddComponent<LeonidBelongings>();
            Transform broken = Child(group, "Broken Cane"); broken.position = new Vector3(room.X(1.85f), 0.07f, room.Z(5.8f));
            var lower = Part(broken, "Lower Shaft", PrimitiveType.Cylinder, new Vector3(-0.19f, 0.025f, 0), new Vector3(0.035f, 0.2f, 0.035f), "Furniture", false);
            lower.localRotation = Quaternion.Euler(0, 0, 90);
            Transform upper = Part(broken, "Detached Handle", PrimitiveType.Cube, new Vector3(0.19f, 0.025f, 0.08f), new Vector3(0.16f, 0.045f, 0.055f), "Furniture", false);
            upper.localRotation = Quaternion.Euler(0, 35, 0);
            Part(broken, "Split Mount", PrimitiveType.Cube, new Vector3(0.05f, 0.025f, 0), new Vector3(0.065f, 0.05f, 0.065f), "Paint", false);
            var target = broken.gameObject.AddComponent<BoxCollider>(); target.center = new Vector3(-0.05f, 0.025f, 0.02f); target.size = new Vector3(0.7f, 0.12f, 0.3f);
            broken.gameObject.AddComponent<LeonidWorldItem>().Configure(supplies, LeonidWorldItem.ItemKind.BrokenCane);

            Transform keys = Child(group, "Leonid Car Keys"); keys.position = KeyPosition(room);
            Part(keys, "Key Bow", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.1f, 0.012f, 0.1f), "Paint", false);
            Part(keys, "Key Blade", PrimitiveType.Cube, new Vector3(0.065f, 0, 0), new Vector3(0.12f, 0.025f, 0.025f), "Paint", false);
            var keyTarget = keys.gameObject.AddComponent<BoxCollider>(); keyTarget.center = new Vector3(0.03f, 0, 0); keyTarget.size = new Vector3(0.23f, 0.06f, 0.15f);
            keys.gameObject.AddComponent<LeonidWorldItem>().Configure(supplies, LeonidWorldItem.ItemKind.CarKeys);

            const string bluePath = "Assets/_Project/Art/BlockoutMaterials/CarBlue.mat";
            var blue = AssetDatabase.LoadAssetAtPath<Material>(bluePath);
            if (blue == null)
            {
                blue = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CarBlue" };
                blue.SetColor("_BaseColor", new Color(0.035f, 0.18f, 0.65f)); blue.SetFloat("_Smoothness", 0.25f);
                AssetDatabase.CreateAsset(blue, bluePath);
            }
            Transform car = Child(group, "Leonid Blue Car"); car.position = CarPosition;
            Transform front = Part(car, "Blue Front", PrimitiveType.Cube, new Vector3(0, 0.65f, 1.1f), new Vector3(1.8f, 0.65f, 1.9f), "Equipment");
            front.GetComponent<Renderer>().sharedMaterial = blue;
            Transform cabin = Part(car, "Blue Cabin", PrimitiveType.Cube, new Vector3(0, 1.12f, 0.15f), new Vector3(1.6f, 0.65f, 1.8f), "Equipment");
            cabin.GetComponent<Renderer>().sharedMaterial = blue;
            Part(car, "Windshield", PrimitiveType.Cube, new Vector3(0, 1.2f, 1.06f), new Vector3(1.4f, 0.45f, 0.03f), "Glass", false);
            Part(car, "Rear Window", PrimitiveType.Cube, new Vector3(0, 1.2f, -0.76f), new Vector3(1.4f, 0.45f, 0.03f), "Glass", false);
            Part(car, "Trunk Floor", PrimitiveType.Cube, new Vector3(0, 0.7f, -1.42f), new Vector3(1.8f, 0.13f, 1.3f), "Equipment");
            foreach (int side in new[] { -1, 1 })
            {
                Part(car, "Side Window " + side, PrimitiveType.Cube, new Vector3(side * 0.81f, 1.2f, 0.15f), new Vector3(0.03f, 0.4f, 1.45f), "Equipment", false);
                var body = Part(car, "Blue Trunk Side " + side, PrimitiveType.Cube, new Vector3(side * 0.85f, 0.76f, -1.42f), new Vector3(0.1f, 0.4f, 1.3f), "Equipment");
                body.GetComponent<Renderer>().sharedMaterial = blue;
                Part(car, "Front Lamp " + side, PrimitiveType.Cube, new Vector3(side * 0.58f, 0.74f, 2.06f), new Vector3(0.36f, 0.18f, 0.035f), "Paint", false);
                foreach (float z in new[] { -1.25f, 1.25f })
                {
                    var wheel = Part(car, "Wheel " + side + " " + z, PrimitiveType.Cylinder, new Vector3(side * 0.87f, 0.31f, z), new Vector3(0.58f, 0.14f, 0.58f), "Asphalt", false);
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                }
            }
            var rear = Part(car, "Blue Rear Panel", PrimitiveType.Cube, new Vector3(0, 0.65f, -2.06f), new Vector3(1.8f, 0.25f, 0.08f), "Equipment");
            rear.GetComponent<Renderer>().sharedMaterial = blue;
            Transform handle = Part(car, "Trunk Handle", PrimitiveType.Cube, new Vector3(0, 0.86f, -2.1f), new Vector3(0.5f, 0.13f, 0.08f), "Paint");
            handle.gameObject.AddComponent<LeonidWorldItem>().Configure(supplies, LeonidWorldItem.ItemKind.Trunk);
            Transform lid = Child(car, "Trunk Lid Hinge"); lid.localPosition = new Vector3(0, 1.01f, -0.85f);
            Transform panel = Part(lid, "Blue Trunk Lid", PrimitiveType.Cube, new Vector3(0, 0, -0.59f), new Vector3(1.72f, 0.1f, 1.22f), "Equipment");
            panel.GetComponent<Renderer>().sharedMaterial = blue;

            Transform spare = Child(car, "Spare Folding Cane"); spare.localPosition = new Vector3(0, 0.8f, -1.52f);
            foreach (int side in new[] { -1, 1 })
            {
                var shaft = Part(spare, "Folded Shaft " + side, PrimitiveType.Cylinder, new Vector3(0, 0, side * 0.065f), new Vector3(0.035f, 0.3f, 0.035f), "Furniture", false);
                shaft.localRotation = Quaternion.Euler(0, 0, 90);
            }
            Part(spare, "Handle", PrimitiveType.Cube, new Vector3(0.32f, 0, 0.01f), new Vector3(0.08f, 0.06f, 0.22f), "Paint", false);
            var spareTarget = spare.gameObject.AddComponent<BoxCollider>(); spareTarget.size = new Vector3(0.76f, 0.14f, 0.29f);
            spare.gameObject.AddComponent<CanePickup>().Configure(owner, supplies);
            spare.gameObject.SetActive(false);
            supplies.Configure(loop, dialogue, owner, keys.gameObject, spare.gameObject, lid);

            Transform sign = Child(group, "Motel Parking Sign"); sign.position = new Vector3(24.9f, 0, -13.45f);
            Part(sign, "Post", PrimitiveType.Cube, new Vector3(0, 1.2f, 0), new Vector3(0.1f, 2.4f, 0.1f), "Equipment");
            Transform board = Part(sign, "Board", PrimitiveType.Cube, new Vector3(0, 2.45f, 0), new Vector3(1.6f, 0.6f, 0.12f), "Paint");
            board.localRotation = Quaternion.Euler(0, 90, 0);
            Transform label = Child(board, "Label"); label.localPosition = new Vector3(0, 0, -0.55f);
            // Cancel the primitive's scale: text uses real world metres.
            label.localScale = new Vector3(1 / 1.6f, 1 / 0.6f, 1 / 0.12f);
            MotelLoopSceneSetup.Text(label, "Text", "МОТЕЛЬ", 1.4f, 0.32f, new Color(0.08f, 0.1f, 0.12f));
        }
        private static Transform Part(Transform p, string name, PrimitiveType type, Vector3 pos, Vector3 size, string material, bool solid = true)
        {
            Transform t = MotelGuestVisuals.Part(p, name, type, pos, size, material);
            if (solid) t.gameObject.AddComponent<BoxCollider>();
            return t;
        }
        private static Transform Child(Transform parent, string name) { var t = new GameObject(name).transform; t.SetParent(parent, false); return t; }
    }
}
