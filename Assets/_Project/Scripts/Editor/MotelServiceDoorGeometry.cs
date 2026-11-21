using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Motel.Editor
{
    public static class MotelServiceDoorGeometry
    {
        public static void Configure(Transform hinge, MotelBlockoutPlan.Door d)
        {
            if (d.HingeInset == 0) return;
            var rotation = Quaternion.Euler(0, d.Yaw, 0);
            hinge.localPosition = new Vector3(d.X, 0, d.Z) + rotation * new Vector3(-d.Width / 2 + d.HingeInset, 0, d.HingeNormalOffset);
            hinge.localRotation = rotation;
            var panel = hinge.GetComponentsInChildren<BoxCollider>().Single();
            panel.transform.localPosition = new Vector3(d.Width / 2 - d.HingeInset, 1.14f, d.PanelNormalOffset);
            panel.transform.localScale = new Vector3(d.PanelWidth, 2.24f, d.Thickness);
            foreach (Transform part in hinge)
                if (part.name == "Door Handle" || part.name == "Inside Handle")
                    part.localPosition = new Vector3(d.Width - 2*d.HingeInset - .18f, part.localPosition.y,
                        d.PanelNormalOffset + (part.localPosition.z < 0 ? -1 : 1) * (d.Thickness/2 + .025f));
        }
        public static void Update(Transform root)
        {
            var plan = MotelBlockoutPlan.Create();
            var frames = root.Find("Service Door Frames");
            if (frames == null) { frames = new GameObject("Service Door Frames").transform; frames.SetParent(root, false); }
            foreach (var box in plan.Boxes.Where(b => b.Group == "Service Door Frames"))
            {
                var t = frames.Find(box.Name);
                if (t == null) { t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; t.name = box.Name; t.SetParent(frames, false); }
                t.localPosition = new Vector3(box.X, box.Y, box.Z); t.localScale = new Vector3(box.Width, box.Height, box.Depth);
                t.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/BlockoutMaterials/Furniture.mat");
            }
            foreach (var d in plan.Doors.Where(d => d.HingeInset != 0)) Configure(root.Find("Doors/" + d.Name), d);
            Hardware(root, plan);
        }
        public static void Hardware(Transform root, MotelBlockoutPlan plan)
        {
            var old = root.Find("Service Door Hardware"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Service Door Hardware").transform; group.SetParent(root, false);
            foreach (var d in plan.Doors.Where(d => d.HingeInset != 0))
            {
                var hinge = root.Find("Doors/" + d.Name);
                foreach (float y in new[] { .35f, 1.90f })
                {
                    var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform; t.name = d.Name + " Hinge"; t.SetParent(group, false);
                    t.localPosition = hinge.localPosition + Vector3.up*y; t.localScale = new Vector3(.032f,.065f,.032f);
                    t.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/BlockoutMaterials/Equipment.mat");
                    Object.DestroyImmediate(t.GetComponent<Collider>());
                }
            }
        }
    }
}
