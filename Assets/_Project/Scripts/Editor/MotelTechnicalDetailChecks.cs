using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    // One short inspection session: no accident, reload, rescue route, or Player build.
    [InitializeOnLoad]
    public static class MotelTechnicalDetailChecks
    {
        public const string Output = "Logs/TechnicalRefinement";
        private const string Prefix = "Motel.TechnicalDetail.";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Step { public string Name; public Func<bool> Run; }
        private static readonly List<Step> steps = new List<Step>();
        private static bool ready, initialized;
        private static int cursor, frame;
        private static double deadline, stepAt;
        private static MotelLoopController loop;
        private static FirstPersonController player;
        private static CharacterController body;
        private static Dictionary<PrototypeDoor, Quaternion> closed;
        private static string Report => Output+(SessionState.GetBool(Prefix+"PhotosOnly",false)?"/Photos.txt":"/Play.txt");

        static MotelTechnicalDetailChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.EnteredPlayMode) ready = true;
                if (s == PlayModeStateChange.EnteredEditMode)
                { ready = false; if (SessionState.GetBool(Prefix + "Run", false)) Finish(false, "Play stopped externally."); }
            };
        }
        [MenuItem("Motel/Run Short Technical Detail Checks")]
        public static void Begin()=>Start(false);
        public static void BeginPhotos()=>Start(true);
        private static void Start(bool photosOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path != MotelBlockoutSceneCreator.ScenePath)
                throw new InvalidOperationException("Open saved MotelBlockout outside Play Mode.");
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Prefix+"PhotosOnly",photosOnly);
            File.WriteAllText(Report, photosOnly?"CAPTURE ONLY: fixture camera poses in Play Mode, isolated save. No repeated route/collision tests.\n":"RUNNING short local inspection. Capsule 3 m/s, isolated save; handler-driven actions after first-solid rays.\n");
            SessionState.SetString(Prefix + "Hash", UserHash()); SessionState.SetBool(Prefix + "Run", true);SessionState.SetBool(Prefix+"RoomOnly",false);
            initialized = ready = false; cursor = frame = 0; deadline = EditorApplication.timeSinceStartup + 140;
            EditorApplication.isPlaying = true;
        }
        public static void BeginRoomOnly(){Begin();SessionState.SetBool(Prefix+"RoomOnly",true);File.AppendAllText(Output+"/Play.txt","CONTINUATION room only: three-door checks already passed in Initial-Route-Blocked.txt.\n");}
        private static void Tick()
        {
            if (!ready || !EditorApplication.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Prefix + "Run", false)) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            if (frame == Time.frameCount || Time.frameCount < 5) return; frame = Time.frameCount;
            try
            {
                if (!initialized)
                {
                    loop = Object.FindAnyObjectByType<MotelLoopController>(); player = Object.FindAnyObjectByType<FirstPersonController>(); body = player.GetComponent<CharacterController>();
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null,
                        new ObservationStore(Path.GetFullPath("Temp/TechnicalDetail-" + Guid.NewGuid().ToString("N") + ".json")));
                    closed = Object.FindObjectsByType<PrototypeDoor>().ToDictionary(d => d, d => d.transform.localRotation);
                    foreach (var door in closed.Keys) Require(Math.Abs(new SerializedObject(door).FindProperty("openAngle").floatValue) == 95, "Room door angle " + door.name);
                    var panel = Object.FindAnyObjectByType<ModuleBypassPanel>();
                    Require(new SerializedObject(panel.CabinetDoor).FindProperty("openAngle").floatValue == 170, "Cabinet angle changed");
                    Require(new SerializedObject(Object.FindAnyObjectByType<EmergencyCutoff>().Cover).FindProperty("openAngle").floatValue == 105, "Cover angle changed");
                    if(!SessionState.GetBool(Prefix+"RoomOnly",false)&&!SessionState.GetBool(Prefix+"PhotosOnly",false))CheckDoorGeometry(); Build(); initialized = true;
                }
                if (loop.IsPaused) typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { false });
                Require(EditorApplication.timeSinceStartup < deadline, "Overall timeout");
                if (cursor >= steps.Count)
                {
                    Require(UserHash() == SessionState.GetString(Prefix + "Hash", ""), "User save changed");
                    Require(!loop.Transitioning, "Unexpected accident");
                    Finish(true, SessionState.GetBool(Prefix+"PhotosOnly",false)?"Photographs only: final overall/rack/bench/closed button. User save unchanged.":(SessionState.GetBool(Prefix+"RoomOnly",false)?"Technical-room continuation; three-door results are in prior report. ":"Three service doors: fitted frames, closed leaf clearance and 0..95-degree wall/frame sweep; capsule passes and doors close. ")+"Shelf/bench block physical capsule attempts. Metre UVs and fine materials inspected by camera; equipment/button first-solid rays <=2.5m, safe green start, fixed red housing/cap with separately opening 105-degree cover, cabinet/board remain available at 170. No accident, resets, four-guest route or build. Handler input; physical E/mouse acceptance remains manual."); return;
                }
                Require(EditorApplication.timeSinceStartup - stepAt < 20, "Step timeout " + steps[cursor].Name + " P=" + body.transform.position);
                if (steps[cursor].Run()) { File.AppendAllText(Report, "t=" + loop.Elapsed.ToString("F2") + " " + steps[cursor].Name + "\n"); cursor++; stepAt = EditorApplication.timeSinceStartup; }
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        private static void Build()
        {
            steps.Clear(); stepAt = EditorApplication.timeSinceStartup;
            if(SessionState.GetBool(Prefix+"PhotosOnly",false))
            {
                Act("open technical for photography",()=>Door("Technical").Toggle());Wait("door open for photography",()=>Door("Technical").IsFullyOpen);
                Act("final overall",()=>PoseShot(3.12f,.65f,3.0f,1.35f,6.1f,"Technical-Overall.png"));
                Act("final complete rack",()=>PoseShot(1.9f,2.05f,.39f,.90f,1.15f,"Technical-Rack.png"));
                Act("final bench and wall tools",()=>PoseShot(4.2f,4.15f,5.78f,1.03f,2.55f,"Technical-Workbench.png"));
                Act("final closed emergency assembly",()=>PoseShot(1.15f,4.3f,1.15f,1.24f,5.47f,"Emergency-Closed.png"));
                return;
            }
            Walk(1.55f, -11.4f); Walk(4.8f, -11.4f);
            if(!SessionState.GetBool(Prefix+"RoomOnly",false))foreach (var pair in new[] { ("Technical", MotelBlockoutPlan.TechnicalCenterZ), ("Linen", MotelBlockoutPlan.LinenCenterZ), ("Office", MotelBlockoutPlan.OfficeCenterZ) })
            {
                string name = pair.Item1; float z = pair.Item2;
                Walk(4.8f, z);
                if(name=="Technical") { Walk(5.4f,z); Act("fitted door closed render",()=>{Look(new Vector3(3.6f,1.18f,z));Capture("Service-Door-Closed.png");}); }
                Open(name);
                if(name=="Technical") Act("fitted door open render",()=>{Look(new Vector3(3.1f,1.18f,z));Capture("Service-Door-Open.png");});
                Walk(2.9f, z); Walk(4.8f, z);
                Act("close " + name, () => Door(name).Toggle());
                Wait(name + " closed", () => Quaternion.Angle(Door(name).transform.localRotation, closed[Door(name)]) < .1f);
            }
            Walk(4.8f, MotelBlockoutPlan.TechnicalCenterZ); Open("Technical");
            Local(3.12f, .65f);
            Act("overall render", () => { Look(MotelTechnicalRoomSceneSetup.World(3.0f, 1.35f, 6.1f)); Capture("Technical-Overall.png"); });
            Local(3.12f,2.1f);Local(1.25f,2.1f);
            Local(1.25f,1.15f);PushFurniture("shelf",new Vector3(-1,0,0));Local(1.30f,1.15f);
            Local(1.38f,1.80f);
            Act("filled rack close render",()=>{Look(MotelTechnicalRoomSceneSetup.World(.39f,.94f,1.15f));Capture("Technical-Rack.png");});
            Local(.85f,3.3f);
            Act("plaster close render",()=>{Look(MotelTechnicalRoomSceneSetup.World(.10f,1.45f,3.3f));Capture("Plaster-Close.png");});
            Local(4.85f, 3.5f); Local(5.22f, 5.1f);
            Act("independent diagnostic reachable", () => { var d = Object.FindAnyObjectByType<EquipmentInspection>(); Ray(d); d.Interact(); Require(loop.EquipmentInspectedThisCycle, "Diagnostics did not inspect"); Object.FindAnyObjectByType<DialogueController>().Close(); });
            Local(1.15f, 4.3f);
            Act("all three buttons reachable; green safe", () =>
            {
                foreach (var button in Object.FindObjectsByType<GeneratorButton>()) { Ray(button); if (button.name.Contains("Green")) button.Interact(); }
                var cut = Object.FindAnyObjectByType<EmergencyCutoff>(); Ray(cut.Cover);
                redFrom=cut.transform.position;capFrom=cut.transform.Find("Red Cap").position;
                Look(cut.transform.position);Capture("Emergency-Closed.png");
                cut.Cover.Interact(); Require(!loop.Transitioning, "Safe approach triggered accident");
            });
            Wait("red cover reaches own angle", () => Object.FindAnyObjectByType<EmergencyCutoff>().Cover.IsFullyOpen);
            Act("red button reachable after cover", () => { var cut = Object.FindAnyObjectByType<EmergencyCutoff>(); Ray(cut);
                Require(Vector3.Distance(cut.transform.position,redFrom)<.0001f&&Vector3.Distance(cut.transform.Find("Red Cap").position,capFrom)<.0001f,"Red housing or cap moved with cover");
                Look(cut.transform.position);Capture("Emergency-Open.png");cut.Cover.Interact(); });
            Local(3.25f, 5.1f);
            Act("cabinet reachable", () => { var p = Object.FindAnyObjectByType<ModuleBypassPanel>(); Ray(p.CabinetDoor); p.CabinetDoor.Interact(); });
            Wait("cabinet remains 170", () => Object.FindAnyObjectByType<ModuleBypassPanel>().CabinetDoor.IsFullyOpen);
            Act("board reachable", () => { var p = Object.FindAnyObjectByType<ModuleBypassPanel>(); Ray(p); p.Interact(); Require(p.IsOpen, "Board did not open"); p.Close(); });
            Local(3.20f, 3.65f);
            Act("equipment render", () => { Look(MotelTechnicalRoomSceneSetup.World(3.1f, 1.76f, 6.7f)); Capture("Technical-Equipment.png"); });
            Local(4.65f, 2.55f);PushFurniture("bench",new Vector3(1,0,0));Local(4.65f,2.55f);
            Local(4.65f, 3.0f);
            Act("workbench render", () => { Look(MotelTechnicalRoomSceneSetup.World(5.78f, .90f, 2.30f)); Capture("Technical-Workbench.png"); });
            Local(3.12f, 1); Walk(4.8f, MotelBlockoutPlan.TechnicalCenterZ);
        }
        private static Vector3 redFrom,capFrom;
        private static void PoseShot(float x,float z,float tx,float ty,float tz,string name)
        {
            body.enabled=false;player.transform.position=MotelTechnicalRoomSceneSetup.World(x,.03f,z);body.enabled=true;
            Look(MotelTechnicalRoomSceneSetup.World(tx,ty,tz));Capture(name);
        }
        private static void PushFurniture(string name,Vector3 direction)
        {
            int ticks=0;bool hit=false;
            Wait("capsule blocked by "+name,()=>
            {
                var room=GameObject.Find("Motel Blockout/Technical Room").transform;
                hit|=(body.Move(room.TransformDirection(direction)*.045f)&CollisionFlags.Sides)!=0;
                if(++ticks<35)return false;
                var p=room.InverseTransformPoint(body.transform.position);
                Require(hit&&(name=="shelf"?p.x>.88f&&p.x<1.05f:p.x>4.9f&&p.x<5.12f),"Capsule entered "+name+" at "+p);
                return true;
            });
        }
        private static void CheckDoorGeometry()
        {
            var root=GameObject.Find("Motel Blockout").transform;
            var obstacles=root.Find("Walls").GetComponentsInChildren<Collider>().Concat(root.Find("Service Door Frames").GetComponentsInChildren<Collider>()).ToArray();
            foreach(string name in new[]{"Technical","Linen","Office"})
            {
                var d=Door(name);var panel=d.GetComponentsInChildren<BoxCollider>().Single();var pose=d.transform.localRotation;
                try
                {
                    for(int angle=0;angle<=95;angle+=5)
                    {
                        d.transform.localRotation=pose*Quaternion.Euler(0,-angle,0);Physics.SyncTransforms();
                        foreach(var other in obstacles)
                            Require(!Physics.ComputePenetration(panel,panel.transform.position,panel.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out float depth)||depth<.001f,
                                name+" intersects "+other.name+" at "+angle+" depth="+depth);
                    }
                }
                finally{d.transform.localRotation=pose;Physics.SyncTransforms();}
            }
        }
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static void Open(string name)
        {
            Act("open " + name, () => { Ray(Door(name)); Door(name).Toggle(); });
            // IsFullyOpen has a half-degree tolerance; the interpolated rigidbody pose
            // catches up on the next render frame. Wait for that pose before measuring.
            Wait(name + " animated 95", () => { var d = Door(name); return d.IsFullyOpen && Math.Abs(Quaternion.Angle(d.transform.localRotation, closed[d]) - 95) < .1f; });
        }
        private static void Act(string name, Action run) => steps.Add(new Step { Name = name, Run = () => { run(); return true; } });
        private static void Wait(string name, Func<bool> run) => steps.Add(new Step { Name = name, Run = run });
        private static void Local(float x, float z) { var p = MotelTechnicalRoomSceneSetup.World(x, 0, z); Walk(p.x, p.z); }
        private static void Walk(float x, float z) => steps.Add(new Step { Name = "walk " + x + "," + z, Run = () =>
        { var d = new Vector3(x, body.transform.position.y, z) - body.transform.position; if (d.magnitude < .055f) return true; Look(new Vector3(x, body.transform.position.y + 1.55f, z)); body.Move(d.normalized * Mathf.Min(3 * Time.deltaTime, d.magnitude)); return false; } });
        private static void Look(Vector3 target)
        { var cam = player.GetComponentInChildren<Camera>(); var q = Quaternion.LookRotation(target - cam.transform.position); player.transform.rotation = Quaternion.Euler(0, q.eulerAngles.y, 0); cam.transform.rotation = q; typeof(FirstPersonController).GetField("pitch", Private).SetValue(player, cam.transform.localEulerAngles.x); }
        private static void Ray(Component target)
        {
            Physics.SyncTransforms(); var c = target.GetComponentsInChildren<Collider>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            Vector3 from = player.GetComponentInChildren<Camera>().transform.position, d = c.bounds.center - from;
            Require(Physics.Raycast(from, d.normalized, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.transform.IsChildOf(target.transform), "First solid ray to " + target.name + " hits " + (hit.collider == null ? "nothing" : hit.collider.name));
        }
        private static void Capture(string name)
        {
            var cam = player.GetComponentInChildren<Camera>(); var rt = new RenderTexture(1600, 1000, 24); rt.Create(); var tex = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            var old = cam.targetTexture; var active = RenderTexture.active; float aspect = cam.aspect;
            try { cam.targetTexture = rt; cam.aspect = 1.6f; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); tex.Apply(); File.WriteAllBytes(Output + "/" + name, tex.EncodeToPNG()); }
            finally { cam.targetTexture = old; cam.aspect = aspect; RenderTexture.active = active; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); }
        }
        private static string UserHash()
        { string p = Path.Combine(Application.persistentDataPath, "observations-v1.json"); if (!File.Exists(p)) return "absent"; using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Finish(bool pass, string message)
        { SessionState.SetBool(Prefix + "Run", false); File.AppendAllText(Report, (pass ? "PASS " : "FAIL ") + message + "\n"); if (!pass) Debug.LogError(message); EditorApplication.isPlaying = false; }
    }
}
