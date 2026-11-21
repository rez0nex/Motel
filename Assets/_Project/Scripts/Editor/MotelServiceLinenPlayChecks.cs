using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Motel.Editor
{
    [InitializeOnLoad]public static class MotelServiceLinenPlayChecks
    {
        private const string Prefix="Motel.ServiceLinen.",Report="Logs/ServiceLinen/Play.txt";
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class Step{public string Name;public Func<bool> Run;}
        private static readonly List<Step> steps=new List<Step>();private static int cursor,frame;private static bool ready,initialized;
        private static double deadline,stepAt;private static FirstPersonController player;private static CharacterController body;private static MotelLoopController loop;
        static MotelServiceLinenPlayChecks(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode)ready=true;if(s==PlayModeStateChange.EnteredEditMode){ready=false;if(SessionState.GetBool(Prefix+"Run",false))Finish(false,"Play stopped externally");}};}
        [MenuItem("Motel/Run Service Block and Linen Checks")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||SceneManager.GetActiveScene().path!=MotelBlockoutSceneCreator.ScenePath)throw new Exception("Open saved MotelBlockout outside Play Mode.");
            File.WriteAllText(Report,"RUNNING: original capsule and 3 m/s; isolated save; commands after first-solid checks.\n");SessionState.SetBool(Prefix+"Run",true);SessionState.SetString(Prefix+"Hash",Hash());ready=initialized=false;cursor=frame=0;deadline=EditorApplication.timeSinceStartup+180;EditorApplication.isPlaying=true;EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        private static void Tick()
        {
            if(!ready||!SessionState.GetBool(Prefix+"Run",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount||Time.frameCount<5)return;frame=Time.frameCount;
            try
            {
                if(!initialized){loop=Object.FindAnyObjectByType<MotelLoopController>();player=Object.FindAnyObjectByType<FirstPersonController>();body=player.GetComponent<CharacterController>();typeof(MotelLoopController).GetField("knowledge",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new ObservationStore(Path.GetFullPath("Temp/Linen-"+Guid.NewGuid().ToString("N")+".json")));Build();initialized=true;}
                if(loop.IsPaused)typeof(MotelLoopController).GetMethod("SetPaused",Private).Invoke(loop,new object[]{false});
                Require(EditorApplication.timeSinceStartup<deadline,"Overall timeout");if(cursor>=steps.Count){Require(Hash()==SessionState.GetString(Prefix+"Hash",""),"User save changed");Finish(true,"Capsule: direct entrances, linen aisles/cart fronts/table/sink, technical diagnostics/buttons/open cabinet, office, shifted residential corridor and emergency exterior to parking. Four renders from Play Mode. No human keyboard/mouse or manual acceptance implied.");return;}
                Require(EditorApplication.timeSinceStartup-stepAt<25,"Step timeout "+steps[cursor].Name+" P="+body.transform.position);
                if(steps[cursor].Run()){File.AppendAllText(Report,"t="+loop.Elapsed.ToString("F2")+" "+steps[cursor].Name+"\n");cursor++;stepAt=EditorApplication.timeSinceStartup;}
            }catch(Exception e){Finish(false,"step="+cursor+" "+e);}
        }
        private static void Build()
        {
            steps.Clear();stepAt=EditorApplication.timeSinceStartup;Walk(1.55f,-11.4f);Walk(4.8f,-11.4f);Walk(4.8f,MotelBlockoutPlan.TechnicalCenterZ);Open("Technical");
            WalkPoint(MotelTechnicalRoomSceneSetup.World(3.12f,.05f,.6f));WalkPoint(MotelTechnicalRoomSceneSetup.World(5.22f,.05f,5.1f));
            Act("independent technical diagnostics",()=>{var d=Object.FindAnyObjectByType<EquipmentInspection>();Ray(d);d.Interact();Require(loop.EquipmentInspectedThisCycle,"Fresh diagnostics");Object.FindAnyObjectByType<DialogueController>().Close();});
            WalkPoint(MotelTechnicalRoomSceneSetup.World(1.15f,.05f,4.3f));Act("technical buttons reachable",()=>{foreach(var b in Object.FindObjectsByType<GeneratorButton>())Ray(b);Ray(Object.FindAnyObjectByType<EmergencyCutoff>());});
            WalkPoint(MotelTechnicalRoomSceneSetup.World(3.25f,.05f,5.1f));Act("technical cabinet",()=>{var p=Object.FindAnyObjectByType<ModuleBypassPanel>();Ray(p.CabinetDoor);p.CabinetDoor.Interact();});Wait("cabinet open",()=>Object.FindAnyObjectByType<ModuleBypassPanel>().CabinetDoor.IsFullyOpen);
            Act("same technical board",()=>{var p=Object.FindAnyObjectByType<ModuleBypassPanel>();Ray(p);p.Interact();Require(p.IsOpen&&p.Rotate(0),"Board interaction");p.Close();});
            WalkPoint(MotelTechnicalRoomSceneSetup.World(3.12f,.05f,.8f));Walk(4.8f,MotelBlockoutPlan.TechnicalCenterZ);
            Walk(4.8f,MotelBlockoutPlan.LinenCenterZ);Open("Linen");WalkLinen(3.12f,.30f);
            Act("linen threshold render",()=>{Look(MotelLinenRoomSceneSetup.World(3.12f,1.48f,6.6f));Capture(player.GetComponentInChildren<Camera>(),"Linen-Threshold.png");Top(false);Top(true);});
            WalkLinen(3.12f,2.3f);WalkLinen(1.35f,2.3f);WalkLinen(1.35f,4.8f);WalkLinen(2.9f,5.35f);
            Act("linen furniture close render",()=>{Look(MotelLinenRoomSceneSetup.World(5.3f,1.0f,3.65f));Capture(player.GetComponentInChildren<Camera>(),"Linen-Furniture.png");});
            WalkLinen(4.5f,5.2f);WalkLinen(4.9f,3.6f);WalkLinen(3.4f,2.4f);WalkLinen(3.4f,.60f);Walk(4.8f,MotelBlockoutPlan.LinenCenterZ);
            Walk(4.8f,MotelBlockoutPlan.OfficeCenterZ);Open("Office");Walk(2.4f,MotelBlockoutPlan.OfficeCenterZ);Walk(2.3f,6.0f);Walk(-1,6.0f);Walk(2.3f,6.0f);Walk(2.4f,MotelBlockoutPlan.OfficeCenterZ);Walk(4.8f,MotelBlockoutPlan.OfficeCenterZ);
            Walk(4.8f,MotelBlockoutPlan.ResidentialZ);Walk(19.5f,MotelBlockoutPlan.ResidentialZ);Walk(MotelBlockoutPlan.WingEnd-1.2f,MotelBlockoutPlan.ResidentialZ);Open("Emergency Exit");Walk(MotelBlockoutPlan.WingEnd+1.7f,MotelBlockoutPlan.ResidentialZ);Walk(MotelBlockoutPlan.WingEnd+1.7f,-12);Walk(25,-18);Walk(18,-18);
        }
        private static void Open(string name){Act("open "+name,()=>{var d=GameObject.Find("Motel Blockout/Doors/"+name).GetComponent<PrototypeDoor>();Ray(d);if(!d.IsOpenRequested)d.Toggle();});Wait(name+" fully open",()=>GameObject.Find("Motel Blockout/Doors/"+name).GetComponent<PrototypeDoor>().IsFullyOpen);}
        private static void Act(string name,Action action)=>steps.Add(new Step{Name=name,Run=()=>{action();return true;}});private static void Wait(string name,Func<bool> run)=>steps.Add(new Step{Name=name,Run=run});
        private static void WalkLinen(float x,float z)=>WalkPoint(MotelLinenRoomSceneSetup.World(x,.05f,z));private static void WalkPoint(Vector3 p)=>Walk(p.x,p.z);
        private static void Walk(float x,float z)=>steps.Add(new Step{Name="walk "+x+","+z,Run=()=>{var d=new Vector3(x,body.transform.position.y,z)-body.transform.position;if(d.magnitude<.055f)return true;Look(new Vector3(x,body.transform.position.y+1.55f,z));body.Move(d.normalized*Mathf.Min(3*Time.deltaTime,d.magnitude));return false;}});
        private static void Look(Vector3 target){var cam=player.GetComponentInChildren<Camera>();var q=Quaternion.LookRotation(target-cam.transform.position);player.transform.rotation=Quaternion.Euler(0,q.eulerAngles.y,0);cam.transform.rotation=q;typeof(FirstPersonController).GetField("pitch",Private).SetValue(player,cam.transform.localEulerAngles.x);}
        private static void Ray(Component actor){Physics.SyncTransforms();var c=actor.GetComponentsInChildren<Collider>().First(c=>c.enabled&&c.gameObject.activeInHierarchy);Vector3 from=player.GetComponentInChildren<Camera>().transform.position,d=c.bounds.center-from;Require(Physics.Raycast(from,d.normalized,out var hit,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(actor.transform),"First-solid ray misses "+actor.name+" from "+from+" hit "+(hit.collider==null?"none":hit.collider.name));}
        public static void Capture(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,1100,24);rt.Create();var pixels=new Texture2D(1600,1100,TextureFormat.RGB24,false);var previous=RenderTexture.active;var old=camera.targetTexture;float aspect=camera.aspect;
            try{camera.targetTexture=rt;camera.aspect=1600f/1100;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1600,1100),0,0);pixels.Apply();File.WriteAllBytes("Logs/ServiceLinen/"+name,pixels.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=previous;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
        }
        private static void Top(bool all)
        {
            var ceilings=GameObject.Find("Motel Blockout/Ceilings");var hidden=GameObject.Find("Motel Blockout").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Fluorescent Fixture")).Select(t=>t.gameObject).ToArray();
            var g=new GameObject("Temporary top camera");var c=g.AddComponent<Camera>();c.CopyFrom(player.GetComponentInChildren<Camera>());c.orthographic=true;
            if(all){c.transform.SetPositionAndRotation(new Vector3(1.2f,24,-1.44f),Quaternion.Euler(90,-90,0));c.orthographicSize=6.8f;}
            else{c.transform.SetPositionAndRotation(MotelLinenRoomSceneSetup.World(3.12f,12,3.52f),Quaternion.Euler(90,-90,0));c.orthographicSize=4.1f;}
            try{ceilings.SetActive(false);foreach(var h in hidden)h.SetActive(false);Capture(c,all?"Service-Top.png":"Linen-Top.png");}finally{ceilings.SetActive(true);foreach(var h in hidden)h.SetActive(true);Object.DestroyImmediate(g);}
        }
        private static string Hash(){var p=Path.Combine(Application.persistentDataPath,"observations-v1.json");if(!File.Exists(p))return "absent";using var sha=SHA256.Create();return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        private static void Require(bool value,string text){if(!value)throw new Exception(text);}private static void Finish(bool pass,string text){SessionState.SetBool(Prefix+"Run",false);File.AppendAllText(Report,(pass?"PASS ":"FAIL ")+text+"\n");if(!pass)Debug.LogError(text);EditorApplication.isPlaying=false;}
    }
}