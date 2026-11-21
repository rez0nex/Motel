using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    // Decorative additions share one builder between the saved scene and scene generator.
    // Existing interactive objects, colliders and references are retained.
    public static partial class MotelTechnicalDetails
    {
        public const string Folder = MotelTechnicalRoomSceneSetup.Folder + "/Details";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [MenuItem("Motel/Update Technical Details and Service Doors")]
        public static void UpdateCurrent()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path != MotelBlockoutSceneCreator.ScenePath)
                throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            var root = scene.GetRootGameObjects().Single(g => g.name == "Motel Blockout").transform;
            foreach (var door in root.GetComponentsInChildren<PrototypeDoor>(true))
            {
                var data = new SerializedObject(door);
                var angle = data.FindProperty("openAngle");
                angle.floatValue = angle.floatValue < 0 ? -95 : 95;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            MotelServiceDoorGeometry.Update(root);
            Apply(root.Find("Technical Room"));
            MotelServiceBlockUpdater.RefreshAccidentLights(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void Apply(Transform room)
        {
            if (room == null) throw new InvalidOperationException("Technical Room is missing.");
            Prepare();
            Generator(room.Find("Olive Generator"));
            Cabinet(room.Find("Control Cabinet"));
            Controls(room);
            Cables(room);
            Furniture(room);
            RefineRoom(room);
            AssetDatabase.SaveAssets();
        }

        private static void Generator(Transform gen)
        {
            var art = Art(gen);
            gen.Find("Chamfered Housing").GetComponent<Renderer>().sharedMaterial = materials["Paint"];
            foreach (var r in gen.GetComponentsInChildren<Renderer>())
                if (r.name == "Louver") r.sharedMaterial = materials["Paint"];
            // Raised vent frames, deep slots and angled slats, separate lower access panel.
            foreach (float x in new[] { -.32f, .32f })
            {
                Frame(art, "Vent frame", new Vector3(x, .59f, -.633f), .48f, .58f, .024f, "PaintEdge");
                foreach (float dx in new[] { -.216f, .216f })
                    foreach (float y in new[] { .335f, .845f }) Screw(art, new Vector3(x + dx, y, -.65f), .018f);
                for (int i = 0; i < 6; i++)
                {
                    var slat = Box(art, "Angled vent slat", new Vector3(x, .36f + i * .086f, -.649f), new Vector3(.407f, .043f, .030f), "Paint");
                    slat.localRotation = Quaternion.Euler(-22, 0, 0);
                }
            }
            Box(art, "Lower service panel", new Vector3(0, .224f, -.62f), new Vector3(1.27f, .075f, .025f), "PaintEdge");
            foreach (float x in new[] { -.60f, .60f }) Screw(art, new Vector3(x, .225f, -.639f), .02f);
            foreach (float x in new[] { -.69f, .69f })
            {
                Box(art, "Separate side panel", new Vector3(x, .73f, .06f), new Vector3(.017f, .97f, .83f), "Paint");
                foreach (float y in new[] { .28f, 1.18f })
                    foreach (float z in new[] { -.30f, .42f })
                        Bolt(art, new Vector3(x * 1.015f, y, z), Vector3.right, .022f);
                Tube(art, "Service handle " + x, .025f, "Steel", new Vector3(x, .82f, -.05f), new Vector3(x * 1.12f, .82f, -.05f), new Vector3(x * 1.12f, .82f, .25f), new Vector3(x, .82f, .25f));
                foreach (float y in new[] { .31f, .55f, .79f })
                    Box(art, "Side seam", new Vector3(x * 1.014f, y, .07f), new Vector3(.009f, .008f, .67f), "Rubber");
            }
            Box(art, "Top removable lid", new Vector3(0, 1.558f, .13f), new Vector3(1.31f, .018f, .78f), "Paint");
            foreach (float x in new[] { -.60f, .60f }) foreach (float z in new[] { -.18f, .44f }) Bolt(art, new Vector3(x, 1.575f, z), Vector3.up, .022f);
            for (int i = 0; i < 5; i++) Box(art, "Top vent", new Vector3(.31f, 1.57f, -.12f + .07f * i), new Vector3(.35f, .006f, .02f), "Rubber");
            foreach (float x in new[] { -.52f, .52f }) foreach (float z in new[] { -.43f, .43f })
            {
                Box(art, "Foot mounting flange", new Vector3(x, .026f, z), new Vector3(.23f, .038f, .23f), "Steel");
                Box(art, "Anti vibration pad", new Vector3(x, .015f, z), new Vector3(.23f, .025f, .23f), "Rubber");
            }
            var fascia = gen.Find("Sloped Control Panel");
            var fa = Art(fascia);
            Frame(fa, "Fascia gasket", Vector3.back * .022f, 1.29f, .59f, .012f, "Rubber");
            foreach (float x in new[] { -.59f, .59f }) foreach (float y in new[] { -.24f, .24f }) Screw(fa, new Vector3(x, y, -.034f), .02f);
            foreach (var button in fascia.GetComponentsInChildren<GeneratorButton>(true))
            {
                var ba = Art(button.transform);
                Ring(ba, "Button metal rim", new Vector3(0, 0, -.035f), .066f, .008f, "Steel");
                Nameplate(button.transform.Find("Command Label"), ba, button.name.Contains("Green") ? .22f : .34f, .088f);
            }
            var red = fascia.Find("Emergency Cutoff");
            var ra = Art(red);
            Ring(ra, "Red metal bezel", new Vector3(0, 0, -.035f), .070f, .008f, "Steel");
            Nameplate(red.Find("Cutoff Name"), ra, .35f, .115f);
            // The cover and all three interactive caps keep their existing colliders/angles.
        }

        private static void Cabinet(Transform cabinet)
        {
            var art = Art(cabinet);
            foreach (string part in new[] { "Cabinet Back", "Side Frame", "Frame Lip" })
                foreach (var t in cabinet.Cast<Transform>().Where(t => t.name == part)) t.GetComponent<Renderer>().sharedMaterial = materials["Paint"];
            Frame(art, "Recess gasket", new Vector3(0, 0, -.135f), 1.22f, 1.02f, .016f, "Rubber");
            foreach (float x in new[] { -.56f, .56f }) foreach (float y in new[] { -.49f, .49f })
            {
                Box(art, "Wall mounting ear", new Vector3(x, y, .14f), new Vector3(.13f, .13f, .04f), "Steel");
                Screw(art, new Vector3(x, y, -.197f), .022f);
            }
            foreach (float y in new[] { -.39f, .39f })
            {
                Box(art, "Hinge back plate", new Vector3(-.64f, y, -.205f), new Vector3(.14f, .19f, .035f), "PaintEdge");
                for (int n = 0; n < 3; n++) Rod(art, "Hinge knuckle", new Vector3(-.69f, y - .07f + n * .048f, -.23f), new Vector3(-.69f, y - .027f + n * .048f, -.23f), .029f, "Steel");
                foreach (float dy in new[] { -.064f, .064f }) Screw(art, new Vector3(-.60f, y + dy, -.228f), .016f);
            }
            var leaf = cabinet.Find("Left Hinged Door");
            var la = Art(leaf);
            foreach (float y in new[] { -.519f, .519f }) Box(la, "Inside folded edge", new Vector3(.67f, y, .028f), new Vector3(1.27f, .026f, .026f), "PaintEdge");
            Box(la, "Outer folded edge", new Vector3(1.31f, 0, .028f), new Vector3(.026f, 1.02f, .026f), "PaintEdge");
            foreach (float y in new[] { -.47f, .47f }) foreach (float x in new[] { .12f, 1.23f }) Screw(la, new Vector3(x, y, -.033f), .018f);
            Box(la, "Latch base", new Vector3(1.19f, -.05f, -.038f), new Vector3(.11f, .25f, .016f), "PaintEdge");
            Tube(la, "Raised handle", .014f, "Steel", new Vector3(1.19f, -.13f, -.045f), new Vector3(1.19f, -.13f, -.094f), new Vector3(1.19f, .03f, -.094f), new Vector3(1.19f, .03f, -.045f));
            foreach (float y in new[] { -.17f, .07f }) Screw(la, new Vector3(1.19f, y, -.05f), .014f);
        }

        private static void Controls(Transform room)
        {
            var diag = room.Find("Independent Diagnostics");
            var art = Art(diag);
            diag.Find("Diagnostic Housing").GetComponent<Renderer>().sharedMaterial = materials["Paint"];
            Frame(art, "Display surround", new Vector3(0, 0, -.175f), .59f, .39f, .025f, "Steel");
            Frame(art, "Display rubber seal", new Vector3(0, 0, -.181f), .55f, .35f, .010f, "Rubber");
            foreach (float x in new[] { -.30f, .30f }) foreach (float y in new[] { -.37f, .37f }) Screw(art, new Vector3(x, y, -.17f), .018f);
            foreach (float x in new[] { -.23f, .23f })
            {
                Box(art, "Diagnostic mounting bracket", new Vector3(x, 0, .16f), new Vector3(.10f, .9f, .035f), "Steel");
                foreach (float y in new[] { -.43f, .43f }) Screw(art, new Vector3(x, y, .13f), .020f);
            }
            Nameplate(diag.Find("Diagnostic Name"), art, .61f, .11f);
            var start = room.Find("Start Block - not interactive");
            var sa = Art(start);
            Frame(sa, "Relay bezel", new Vector3(0, 0, -.14f), .35f, .43f, .018f, "PaintEdge");
            foreach (float x in new[] { -.145f, .145f }) foreach (float y in new[] { -.18f, .18f }) Screw(sa, new Vector3(x, y, -.155f), .014f);
            Nameplate(start.Find("Block Name"), sa, .28f, .14f);
            // Bezel belongs to the lamp, so it appears only with the existing live indicator.
            var indicator = start.Find("Start Indicator");
            var ia = Art(indicator);
            Ring(ia, "Signal surround", Vector3.zero, .54f, .085f, "Steel");
        }

        private static void Cables(Transform room)
        {
            var art = Art(room, "Cable Details");
            foreach (var t in room.Cast<Transform>().Where(t => t.name.Contains("Cable") || t.name == "Left Side Input" || t.name == "Right Side Output" || t.name.StartsWith("Floor Bundle") || t.name == "Bundle Strap"))
                if (t != art && t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false;
            Tube(art, "Rounded generator supply", .037f, "Rubber", new Vector3(1.83f, 1.44f, 6.4f), new Vector3(2.28f, 1.44f, 6.74f), new Vector3(2.28f, 2.275f, 6.74f), new Vector3(2.58f, 2.275f, 6.74f));
            Tube(art, "Rounded start output", .034f, "Rubber", new Vector3(3.92f, 2.275f, 6.74f), new Vector3(4.10f, 2.275f, 6.74f), new Vector3(4.10f, 2.275f, 6.70f), new Vector3(4.30f, 2.275f, 6.70f));
            foreach (float x in new[] { 2.58f, 3.92f })
            {
                Rod(art, "Cabinet side gland", new Vector3(x - .050f, 2.275f, 6.74f), new Vector3(x + .050f, 2.275f, 6.74f), .063f, "Steel");
                for (int i = 0; i < 3; i++) Rod(art, "Gland grip", new Vector3(x - .034f + i * .025f, 2.275f, 6.74f), new Vector3(x - .022f + i * .025f, 2.275f, 6.74f), .067f, "PaintEdge");
            }
            foreach (float y in new[] { 1.67f, 2.02f })
            {
                Box(art, "Input wall saddle", new Vector3(2.28f, y, 6.86f), new Vector3(.14f, .06f, .18f), "Steel");
                Rod(art, "Saddle band", new Vector3(2.28f, y - .027f, 6.74f), new Vector3(2.28f, y + .027f, 6.74f), .044f, "PaintEdge");
            }
            Tube(art, "Independent diagnostic lead", .028f, "Rubber", new Vector3(5.22f, 2.39f, 6.70f), new Vector3(5.22f, 2.69f, 6.70f), new Vector3(5.22f, 2.76f, 6.85f));
            Rod(art, "Diagnostic top gland", new Vector3(5.22f, 2.39f, 6.70f), new Vector3(5.22f, 2.46f, 6.70f), .048f, "Steel");
            for (int i = 0; i < 3; i++)
            {
                float z = 6.53f + i * .075f;
                Tube(art, "Floor harness " + i, .030f, "Rubber", new Vector3(1.15f, .19f, z - .05f), new Vector3(1.5f, .11f, z), new Vector3(5.92f, .11f, z), new Vector3(6.08f, .11f, z), new Vector3(6.20f, .32f, z));
            }
            Box(art, "Sleeve at linen wall", new Vector3(6.135f, .32f, 6.605f), new Vector3(.04f, .25f, .36f), "PaintEdge");
            foreach (float x in new[] { 2f, 3.5f, 5f })
            {
                Tube(art, "Floor band " + x, .012f, "Steel", new Vector3(x, .027f, 6.47f), new Vector3(x, .17f, 6.47f), new Vector3(x, .17f, 6.74f), new Vector3(x, .027f, 6.74f));
                foreach (float z in new[] { 6.44f, 6.77f }) { Box(art, "Anchor foot", new Vector3(x, .026f, z), new Vector3(.11f, .025f, .07f), "Steel"); Bolt(art, new Vector3(x, .045f, z), Vector3.up, .018f); }
            }
        }

        private static void Furniture(Transform room)
        {
            var bench = room.Find("Right Workbench");
            var art = Art(bench);
            bench.Find("Old Wood Top").GetComponent<Renderer>().sharedMaterial = materials["Wood"];
            Box(art, "Lower shelf", new Vector3(0, .24f, 0), new Vector3(.58f, .045f, 2.4f), "Wood");
            foreach (float x in new[] { -.26f, .26f })
            {
                Box(art, "Top apron", new Vector3(x, .83f, 0), new Vector3(.045f, .14f, 2.35f), "Paint");
                Rod(art, "Bench diagonal brace", new Vector3(x, .30f, -1.08f), new Vector3(x, .79f, 1.08f), .018f, "Steel");
                foreach (float z in new[] { -1.08f, 1.08f }) Box(art, "Foot plate", new Vector3(x, .025f, z), new Vector3(.13f, .04f, .13f), "Steel");
            }
            foreach (float z in new[] { -1.13f, 1.13f }) foreach (float x in new[] { -.25f, .25f }) Bolt(art, new Vector3(x, .982f, z), Vector3.up, .016f);
            Hide(bench, "Tool Box", "Tool Box Handle", "Vice Base", "Vice Jaw", "Spanner", "Spanner Head");
            var box = Node(art, "Open tool box", new Vector3(0, 1.0f, .83f));
            Box(box, "Toolbox floor", Vector3.zero, new Vector3(.46f, .04f, .58f), "Paint");
            foreach (float x in new[] { -.22f, .22f }) Box(box, "Toolbox side", new Vector3(x, .12f, 0), new Vector3(.025f, .25f, .58f), "Paint");
            foreach (float z in new[] { -.28f, .28f }) Box(box, "Toolbox end", new Vector3(0, .12f, z), new Vector3(.46f, .25f, .025f), "Paint");
            Tube(box, "Carry handle", .017f, "Steel", new Vector3(0, .20f, -.27f), new Vector3(0, .37f, -.16f), new Vector3(0, .37f, .16f), new Vector3(0, .20f, .27f));
            Rod(box, "Screwdriver in box", new Vector3(-.14f, .09f, -.17f), new Vector3(.12f, .20f, .19f), .018f, "Steel");
            Rod(box, "Tool wooden grip", new Vector3(-.14f, .09f, -.17f), new Vector3(-.06f, .12f, -.06f), .035f, "Wood");
            var vice = Node(art, "Workshop vice", new Vector3(-.22f, 1.03f, -.85f));
            Box(vice, "Cast base", Vector3.zero, new Vector3(.35f, .10f, .32f), "Rust");
            foreach (float z in new[] { -.09f, .09f })
            { Box(vice, "Vice body", new Vector3(0, .10f, z), new Vector3(.26f, .13f, .075f), "Rust"); Box(vice, "Steel jaw", new Vector3(0, .17f, z), new Vector3(.29f, .035f, .055f), "Steel"); }
            Rod(vice, "Lead screw", new Vector3(0, .09f, -.25f), new Vector3(0, .09f, .22f), .023f, "Steel");
            for (int i = 0; i < 8; i++) Ring(vice, "Screw thread " + i, new Vector3(0, .09f, -.14f - i * .013f), .024f, .004f, "Steel");
            Rod(vice, "Sliding crank", new Vector3(-.15f, .09f, -.25f), new Vector3(.15f, .09f, -.25f), .012f, "Steel");
            foreach (float x in new[] { -.15f, .15f }) Ball(vice, "Crank knob", new Vector3(x, .09f, -.25f), Vector3.one * .04f, "Steel");
            Wrench(art, new Vector3(.13f, .998f, -.22f));
            Rod(art, "Hammer grip", new Vector3(-.09f, .998f, .30f), new Vector3(.12f, .998f, .10f), .019f, "Wood");
            Box(art, "Hammer steel head", new Vector3(-.09f, 1.014f, .30f), new Vector3(.13f, .05f, .055f), "Steel");
            Box(art, "Lower supplies carton", new Vector3(0, .40f, .35f), new Vector3(.43f, .28f, .65f), "Cardboard");
            var shelf = room.Find("Shallow Entry Shelf"); var sa = Art(shelf);
            foreach (float x in new[] { -.20f, .20f }) foreach (float z in new[] { -.66f, .66f })
            {
                Box(sa, "Rack upright flange", new Vector3(x, .83f, z + .018f), new Vector3(.065f, 1.66f, .012f), "Steel");
                for (int i = 0; i < 12; i++) Box(sa, "Rack adjustment hole", new Vector3(x + .034f, .19f + i * .12f, z), new Vector3(.006f, .014f, .015f), "Rubber");
                Box(sa, "Rack foot", new Vector3(x, .028f, z), new Vector3(.10f, .04f, .10f), "Steel");
            }
            Rod(sa, "Rack diagonal", new Vector3(-.22f, .2f, -.63f), new Vector3(-.22f, 1.59f, .63f), .009f, "Steel");
            Rod(sa, "Rack diagonal", new Vector3(-.22f, .2f, .63f), new Vector3(-.22f, 1.59f, -.63f), .009f, "Steel");
            for (int i = 0; i < 3; i++)
            {
                float y = .18f + i * .59f;
                Box(sa, "Shelf front lip", new Vector3(.225f, y + .015f, 0), new Vector3(.018f, .06f, 1.40f), "Steel");
                Box(sa, "Carton top fold", new Vector3(0, y + .385f, .21f), new Vector3(.35f, .012f, .54f), "Cardboard");
                Box(sa, "Parcel tape", new Vector3(0, y + .393f, .21f), new Vector3(.075f, .006f, .54f), "Enamel");
            }
            foreach (float z in new[] { -.48f, -.28f })
            { Rod(sa, "Oil can", new Vector3(0, .81f, z), new Vector3(0, 1.03f, z), .075f, "Paint"); Rod(sa, "Can cap", new Vector3(0, 1.03f, z), new Vector3(0, 1.06f, z), .037f, "Rubber"); }
            for (int i = 0; i < 3; i++) Box(sa, "Folded service rag", new Vector3(0, 1.43f + i * .045f, -.35f), new Vector3(.32f, .045f, .42f), "Cloth");
        }

        private static void Wrench(Transform p, Vector3 at)
        {
            var w = Node(p, "Open ended spanner", at); w.localRotation = Quaternion.Euler(0, -18, 0);
            Box(w, "Wrench shaft", Vector3.zero, new Vector3(.032f, .025f, .30f), "Steel");
            foreach (float z in new[] { -.17f, .17f })
            {
                Box(w, "Jaw heel", new Vector3(0, 0, z), new Vector3(.085f, .025f, .045f), "Steel");
                foreach (float x in new[] { -.036f, .036f }) Box(w, "Open jaw", new Vector3(x, 0, z + Mathf.Sign(z) * .025f), new Vector3(.017f, .025f, .050f), "Steel");
            }
        }

        private static Transform Art(Transform owner, string name = "Art Details")
        { var old = owner.Find(name); if (old != null) Object.DestroyImmediate(old.gameObject); return Node(owner, name, Vector3.zero); }
        private static Transform Node(Transform p, string name, Vector3 at)
        { var t = new GameObject(name).transform; t.SetParent(p, false); t.localPosition = at; return t; }
        private static Transform Box(Transform p, string name, Vector3 at, Vector3 size, string mat)
        { var t = GameObject.CreatePrimitive(PrimitiveType.Cube).transform; t.name = name; t.SetParent(p, false); t.localPosition = at; t.localScale = size; t.GetComponent<Renderer>().sharedMaterial = materials[mat]; Object.DestroyImmediate(t.GetComponent<Collider>()); return t; }
        private static void Ball(Transform p, string name, Vector3 at, Vector3 size, string mat)
        { var t = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; t.name = name; t.SetParent(p, false); t.localPosition = at; t.localScale = size; t.GetComponent<Renderer>().sharedMaterial = materials[mat]; Object.DestroyImmediate(t.GetComponent<Collider>()); }
        private static void Rod(Transform p, string name, Vector3 a, Vector3 b, float r, string mat)
        { var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform; t.name = name; t.SetParent(p, false); t.localPosition = (a + b) / 2; t.localRotation = Quaternion.FromToRotation(Vector3.up, b - a); t.localScale = new Vector3(2 * r, (b - a).magnitude / 2, 2 * r); t.GetComponent<Renderer>().sharedMaterial = materials[mat]; Object.DestroyImmediate(t.GetComponent<Collider>()); }
        private static void Frame(Transform p, string name, Vector3 at, float width, float height, float rim, string mat)
        {
            foreach (float x in new[] { -width / 2, width / 2 }) Box(p, name, at + Vector3.right * x, new Vector3(rim, height, .02f), mat);
            foreach (float y in new[] { -height / 2, height / 2 }) Box(p, name, at + Vector3.up * y, new Vector3(width, rim, .02f), mat);
        }
        private static void Screw(Transform p, Vector3 at, float size)
        { Rod(p, "Screw head", at - Vector3.forward * .004f, at + Vector3.forward * .004f, size / 2, "Steel"); Box(p, "Screw slot", at - Vector3.forward * .005f, new Vector3(size * .65f, .003f, .002f), "Rubber"); }
        private static void Bolt(Transform p, Vector3 at, Vector3 axis, float size)
        { Rod(p, "Fixing bolt", at - axis * .004f, at + axis * .004f, size / 2, "Steel"); }
        private static void Hide(Transform parent, params string[] names)
        { foreach (var t in parent.Cast<Transform>().Where(t => names.Contains(t.name))) if (t.GetComponent<Renderer>() != null) t.GetComponent<Renderer>().enabled = false; }
        private static void Nameplate(Transform label, Transform art, float width, float height)
        {
            if (label == null) return;
            var at = label.localPosition;
            float originalZ = label.name == "Cutoff Name" ? -.01f : label.name == "Diagnostic Name" ? -.16f : label.name == "Block Name" ? -.137f : -.025f;
            at.z = originalZ - .021f; label.localPosition = at;
            Box(art, "Enamel name plate", at + Vector3.forward * .008f, new Vector3(width, height, .009f), "Enamel");
            foreach (var text in label.GetComponentsInChildren<TextMesh>(true)) text.color = new Color(.09f, .08f, .055f);
        }
        private static void Ring(Transform p, string name, Vector3 at, float r, float tube, string mat)
        {
            const int count = 20;
            var points = Enumerable.Range(0, count + 1).Select(i => at + new Vector3(Mathf.Cos(i * 2 * Mathf.PI / count), Mathf.Sin(i * 2 * Mathf.PI / count), 0) * r).ToArray();
            MeshPart(p, name, TubeMesh(points, tube), mat);
        }
        private static void Tube(Transform p, string name, float radius, string mat, params Vector3[] corners)
        {
            var points = new List<Vector3> { corners[0] };
            for (int i = 1; i < corners.Length - 1; i++)
            {
                float cut = Mathf.Min(.12f, Vector3.Distance(corners[i - 1], corners[i]) * .35f, Vector3.Distance(corners[i], corners[i + 1]) * .35f);
                Vector3 a = Vector3.MoveTowards(corners[i], corners[i - 1], cut), b = Vector3.MoveTowards(corners[i], corners[i + 1], cut);
                points.Add(a);
                for (int n = 1; n <= 6; n++) { float t = n / 6f; points.Add((1-t)*(1-t)*a + 2*t*(1-t)*corners[i] + t*t*b); }
            }
            points.Add(corners[corners.Length - 1]); MeshPart(p, name, TubeMesh(points.ToArray(), radius), mat);
        }
        private static Mesh TubeMesh(Vector3[] points, float radius)
        {
            const int sides = 10; var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 d = (points[Math.Min(i + 1, points.Length - 1)] - points[Math.Max(i - 1, 0)]).normalized;
                Vector3 u = Vector3.Cross(Mathf.Abs(Vector3.Dot(d, Vector3.up)) > .9f ? Vector3.right : Vector3.up, d).normalized, v = Vector3.Cross(d, u);
                for (int n = 0; n <= sides; n++) { float a = n * 2 * Mathf.PI / sides; Vector3 normal = u * Mathf.Cos(a) + v * Mathf.Sin(a); vertices.Add(points[i] + normal * radius); normals.Add(normal); uv.Add(new Vector2(n/(float)sides, i)); }
            }
            for (int i = 0; i < points.Length - 1; i++) for (int n = 0; n < sides; n++) { int a = i * (sides + 1) + n, b = a + sides + 1; triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b }); }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); return mesh;
        }
        private static void MeshPart(Transform p, string name, Mesh mesh, string mat)
        {
            string key = PathOf(p) + "/" + name; string hash = Hash128.Compute(key).ToString(); string path = Folder + "/Meshes/" + hash + ".asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored == null) { mesh.name = name; AssetDatabase.CreateAsset(mesh, path); stored = mesh; }
            else { EditorUtility.CopySerialized(mesh, stored); EditorUtility.SetDirty(stored); Object.DestroyImmediate(mesh); }
            var t = Node(p, name, Vector3.zero); t.gameObject.AddComponent<MeshFilter>().sharedMesh = stored; t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = materials[mat];
        }
        private static string PathOf(Transform t) => t.parent == null || t.name == "Technical Room" ? t.name : PathOf(t.parent) + "/" + t.name;

        private static void Prepare()
        {
            foreach (string path in new[] { Folder, Folder + "/Materials", Folder + "/Textures", Folder + "/Meshes" })
                if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            materials.Clear();
            Material("Paint", new Color(.31f, .33f, .21f), "paint"); Material("PaintEdge", new Color(.36f, .36f, .25f), "paint");
            Material("Steel", new Color(.42f, .43f, .39f), "steel"); Material("Wood", new Color(.48f, .35f, .22f), "wood");
            Material("Concrete", new Color(.56f, .53f, .46f), "concrete"); Material("Rubber", new Color(.052f, .051f, .045f), "rubber");
            Material("Rust", new Color(.37f, .16f, .09f), "paint"); Material("Enamel", new Color(.76f, .73f, .61f), "paper");
            Material("Cardboard", new Color(.48f, .36f, .24f), "paper"); Material("Cloth", new Color(.48f, .49f, .42f), "cloth");
        }
        private static void Material(string name, Color color, string kind)
        {
            string texPath = Folder + "/Textures/" + name + ".png";
            if (!File.Exists(texPath))
            {
                const int size = 256; var tex = new Texture2D(size, size, TextureFormat.RGB24, false); var colors = new Color[size * size]; var rng = new System.Random(name.Length * 151 + 97);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float grain = ((float)rng.NextDouble() - .5f) * .04f, cloud = Mathf.PerlinNoise(x * .055f, y * .055f) - .5f;
                    Color c = color * (1 + grain + cloud * (kind == "concrete" ? .22f : .10f));
                    if (kind == "wood") c *= 1 + Mathf.Sin(y * .31f + Mathf.Sin(x * .037f) * 1.4f) * .075f;
                    if ((kind == "paint" || kind == "steel") && (x < 4 || y < 4 || x > 251 || y > 251) && rng.NextDouble() > .6) c = Color.Lerp(c, new Color(.42f, .34f, .24f), .4f);
                    if (kind == "cloth" && (x % 3 == 0 || y % 3 == 0)) c *= .96f;
                    colors[y * size + x] = c;
                }
                tex.SetPixels(colors); tex.Apply(); File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            string path = Folder + "/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            material.SetFloat("_Smoothness", kind == "steel" ? .25f : .12f); material.SetFloat("_Metallic", kind == "steel" ? .55f : 0);
            materials[name] = material; EditorUtility.SetDirty(material);
        }
    }
}
