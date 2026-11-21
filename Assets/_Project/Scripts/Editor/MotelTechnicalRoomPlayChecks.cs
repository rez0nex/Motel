using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace Motel.Editor
{
    [InitializeOnLoad] public static class MotelTechnicalRoomPlayChecks
    {
        private const string Prefix="Motel.TechnicalChecks.", Report="Logs/TechnicalRoom/Play.txt";
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private sealed class Step { public string Name; public Func<bool> Run; }
        private static readonly List<Step> steps=new List<Step>();
        private static MotelLoopController loop,oldLoop;private static ModuleBypassPanel panel;
        private static FirstPersonController player;private static CharacterController body;private static DialogueController dialogue;
        private static int cycle,cursor,frame;private static bool ready,initialized,reloading,transitionSeen;private static double deadline,stepAt,clockMark;
        static MotelTechnicalRoomPlayChecks()
        {
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode)ready=true;
                if(state==PlayModeStateChange.EnteredEditMode){ready=false;if(SessionState.GetBool(Prefix+"Running",false))Finish(false,"Play stopped externally");}
            };
        }
        [MenuItem("Motel/Run Technical Room Play Checks")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||SceneManager.GetActiveScene().path!=MotelBlockoutSceneCreator.ScenePath)throw new Exception("Open saved MotelBlockout in edit mode.");
            Directory.CreateDirectory("Logs/TechnicalRoom");File.WriteAllText(Report,"RUNNING: real CharacterController routes, first-solid rays, isolated save.\n");
            SessionState.SetString(Prefix+"Hash",UserHash());SessionState.SetString(Prefix+"Save",Path.GetFullPath("Temp/Technical-"+Guid.NewGuid().ToString("N")+".json"));SessionState.SetBool(Prefix+"Running",true);
            initialized=reloading=ready=false;cycle=cursor=frame=0;deadline=EditorApplication.timeSinceStartup+220;
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;EditorApplication.ExecuteMenuItem("Window/General/Game");
        }
        private static void Tick()
        {
            if(!ready||!SessionState.GetBool(Prefix+"Running",false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();if(frame==Time.frameCount||Time.frameCount<5)return;frame=Time.frameCount;
            try
            {
                Require(EditorApplication.timeSinceStartup<deadline,"Overall timeout");loop=Object.FindAnyObjectByType<MotelLoopController>();if(loop==null)return;
                if(!initialized)
                {typeof(MotelLoopController).GetField("knowledge",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,new ObservationStore(SessionState.GetString(Prefix+"Save","")));initialized=true;LoadSceneObjects();Build();}
                if(reloading)
                {
                    if(ReferenceEquals(loop,oldLoop)){if(loop.IsPaused)Pause(false);if(loop.Transitioning){Require(!loop.RequestAccident(false)&&!loop.RequestBypassAccident(),"Repeated accident accepted");transitionSeen=true;}return;}LoadSceneObjects();CheckReset();Log("PASS reset cycle "+cycle+": world board, door, shield, indicators/player and pending state restored");reloading=false;cycle++;
                    if(cycle==5){Require(UserHash()==SessionState.GetString(Prefix+"Hash",""),"User save changed");Finish(true,"Five scenarios passed; no user-save modification. Routes use real capsule, 3 m/s and player-camera renders; commands invoked after first-solid range checks. Human controls/visual acceptance remain manual.");return;}Build();
                }
                if(loop.IsPaused)Pause(false);
                if(loop.Transitioning)return;
                if(cursor>=steps.Count)return;
                Require(EditorApplication.timeSinceStartup-stepAt<30,"Step timeout "+steps[cursor].Name+" P="+body.transform.position);
                if(steps[cursor].Run()){Log("cycle="+cycle+" t="+loop.Elapsed.ToString("F2")+" "+steps[cursor].Name);cursor++;stepAt=EditorApplication.timeSinceStartup;}
            }
            catch(Exception e){Finish(false,"cycle="+cycle+" step="+(cursor<steps.Count?steps[cursor].Name:"end")+" "+e);}
        }
        private static void LoadSceneObjects(){panel=Object.FindAnyObjectByType<ModuleBypassPanel>();player=Object.FindAnyObjectByType<FirstPersonController>();body=player.GetComponent<CharacterController>();dialogue=Object.FindAnyObjectByType<DialogueController>();}
        private static void Build()
        {
            steps.Clear();cursor=0;stepAt=EditorApplication.timeSinceStartup;Pause(false);
            if(cycle==0)
            {
                Walk(new Vector3(1.55f,.05f,-11.4f));Walk(new Vector3(4.8f,.05f,-11.4f));Walk(new Vector3(4.8f,.05f,MotelBlockoutPlan.TechnicalCenterZ));Walk(new Vector3(4.8f,.05f,MotelBlockoutPlan.TechnicalCenterZ));
                var door=GameObject.Find("Motel Blockout/Doors/Technical").GetComponent<PrototypeDoor>();Act("open actual relocated entrance",()=>{FirstRay(door);door.Toggle();});Wait("entrance fully open",()=>door.IsFullyOpen);
                Walk(At(3.12f,.25f));Act("threshold player-camera capture",()=>{Look(new Vector3(3.12f,1.65f,5));Capture(player.GetComponentInChildren<Camera>(),"Threshold.png");Top();});
                Walk(At(1.6f,2));Walk(At(4.6f,2));Walk(At(4.6f,4.2f));Walk(At(5.22f,5.1f));
                Act("diagnostics before solving",()=>{var d=Object.FindAnyObjectByType<EquipmentInspection>();FirstRay(d);d.Interact();Require(loop.EquipmentInspectedThisCycle&&!panel.Circuit.IsComplete&&!loop.Transitioning,"Independent diagnostics");dialogue.Close();});
                Walk(At(1.15f,4.3f));Act("all three physical buttons accessible",()=>{FirstRay(Button(GeneratorButton.Command.Start));FirstRay(Button(GeneratorButton.Command.Restart));FirstRay(Object.FindAnyObjectByType<EmergencyCutoff>());});
                Act("green unsolved does not launch",()=>{var b=Button(GeneratorButton.Command.Start);FirstRay(b);b.Interact();Require(panel.Feedback.Contains("Не работает")&&!panel.LaunchPending&&!loop.Transitioning&&!panel.Started,"Unsolved Start");});
                Walk(At(3.25f,5.1f));Act("closed cabinet blocks board",()=>{panel.Interact();Require(!panel.IsOpen,"Closed door bypassed");FirstRay(panel.CabinetDoor);panel.CabinetDoor.Interact();});
                Wait("cabinet physically reaches 170 degrees",()=>panel.CabinetDoor.IsFullyOpen);
                Act("door sweep has no equipment overlap",CheckSweep);
                Act("inside note and player-camera close-up",()=>{Look(new Vector3(2.8f,2.15f,6.6f));Capture(player.GetComponentInChildren<Camera>(),"CabinetOpen.png");var note=Object.FindAnyObjectByType<ModuleBypassNote>();FirstRay(note);note.Interact();Require(dialogue.IsOpen&&note.Content.Contains("После работы"),"Inside note unreadable");dialogue.Close();});
                Act("open, rotate, reopen world matches UI",()=>{FirstRay(panel);panel.Interact();Require(panel.IsOpen,"Open cabinet board inaccessible");panel.Rotate(0);Require(panel.Circuit.Ports(0)==6,"Clockwise rotation");CheckWorld();panel.Close();panel.Interact();Require(panel.Circuit.Ports(0)==6,"Reopen loses state");CheckWorld();clockMark=loop.Elapsed;});
                Wait("board does not pause active time",()=>loop.Elapsed-clockMark>.3);
                Act("close board and solve reference",()=>{panel.Close();panel.Interact();for(int n=0;n<3;n++)panel.Rotate(0);Solve();panel.Close();});
                Walk(At(1.15f,4.3f));Act("green solved lights passive block only",()=>{var b=Button(GeneratorButton.Command.Start);FirstRay(b);b.Interact();Require(panel.Started&&StartLamp().activeSelf&&!panel.LaunchPending&&!loop.Transitioning&&panel.Feedback.Contains("Перегрев сохраняется"),"Green command caused accident");});
                Walk(At(2.3f,4.5f));Walk(At(4.5f,4.5f));Walk(At(4.5f,2.0f));Walk(At(2.0f,2.0f));Walk(At(3.12f,.3f));Walk(new Vector3(4.8f,.05f,MotelBlockoutPlan.TechnicalCenterZ));
                Act("manual test reload after physical round trip",()=>Reload(true));return;
            }
            if(cycle==1)
            {
                Act("unsolved yellow restart",()=>{Require(!panel.Circuit.IsComplete,"Expected initial board");Require(panel.RestartGenerator()&&!panel.RestartGenerator(),"Unsolved restart/idempotence");clockMark=loop.Elapsed;});
                Act("await unsolved accident",()=>Reload(false));return;
            }
            if(cycle==2)
            {
                Act("open test fixture cabinet",()=>{panel.CabinetDoor.Interact();});Wait("real door open for solved restart",()=>panel.CabinetDoor.IsFullyOpen);
                Act("solve and restart yellow",()=>{panel.Interact();Solve();CheckWorld();panel.Close();Require(panel.StartGenerator()&&panel.Started,"Solved green");clockMark=loop.Elapsed;Require(panel.RestartGenerator()&&!panel.RestartGenerator(),"Solved restart");});Act("await solved accident",()=>Reload(false));return;
            }
            if(cycle==3)
            {
                var cutoff=Object.FindAnyObjectByType<EmergencyCutoff>();Act("first red interaction only opens shield",()=>{cutoff.Interact();Require(cutoff.Cover.IsOpen&&!loop.Transitioning,"Red cover skipped");});Wait("shield fully lifted",()=>cutoff.Cover.IsFullyOpen);
                Act("second red activates existing accident",()=>{cutoff.Interact();Require(loop.Transitioning&&!loop.RequestAccident(false)&&!loop.RequestBypassAccident(),"Cutoff/common latch");Reload(false);});return;
            }
            Act("cabinet open for timer fixture",()=>panel.CabinetDoor.Interact());Wait("cabinet open",()=>panel.CabinetDoor.IsFullyOpen);
            Act("timer with board open",()=>{panel.Interact();Require(panel.IsOpen,"Board absent");var clock=(AttemptClock)typeof(MotelLoopController).GetField("clock",Private).GetValue(loop);clock.Advance(359.8-clock.Elapsed,false,false);Reload(false);});
        }
        private static Vector3 At(float x,float z)=>MotelTechnicalRoomSceneSetup.World(x,.05f,z);
        private static void Act(string name,Action action)=>steps.Add(new Step{Name=name,Run=()=>{action();return true;}});
        private static void Wait(string name,Func<bool> run)=>steps.Add(new Step{Name=name,Run=run});
        private static void Walk(Vector3 destination)=>steps.Add(new Step{Name="walk "+destination,Run=()=>{Vector3 d=destination-body.transform.position;d.y=0;if(d.magnitude<.06f)return true;LookWorld(destination+Vector3.up*1.55f);body.Move(d.normalized*Mathf.Min(3*Time.deltaTime,d.magnitude));return false;}});
        private static GeneratorButton Button(GeneratorButton.Command command)=>Object.FindObjectsByType<GeneratorButton>().Single(b=>b.Action==command);
        private static void Look(Vector3 local)=>LookWorld(MotelTechnicalRoomSceneSetup.World(local.x,local.y,local.z));
        private static void LookWorld(Vector3 target){var cam=player.GetComponentInChildren<Camera>();var d=target-cam.transform.position;if(d.sqrMagnitude<.001)return;var q=Quaternion.LookRotation(d);player.transform.rotation=Quaternion.Euler(0,q.eulerAngles.y,0);cam.transform.rotation=q;typeof(FirstPersonController).GetField("pitch",Private).SetValue(player,cam.transform.localEulerAngles.x);}
        private static void FirstRay(Component target)
        {
            Physics.SyncTransforms();var collider=target.GetComponentsInChildren<Collider>().First(c=>c.enabled&&c.gameObject.activeInHierarchy);Vector3 origin=player.GetComponentInChildren<Camera>().transform.position,d=collider.bounds.center-origin;
            Require(Physics.Raycast(origin,d.normalized,out var hit,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.transform.IsChildOf(target.transform),"First solid ray misses "+target.name+"; from "+origin+" target "+collider.bounds.center+" hit "+(hit.collider==null?"none":hit.collider.name));
        }
        private static void CheckWorld()
        {
            var cells=(Transform[])typeof(ModuleBypassPanel).GetField("physicalCells",Private).GetValue(panel);var on=(Material)typeof(ModuleBypassPanel).GetField("wireOn",Private).GetValue(panel);
            for(int i=0;i<16;i++)for(int side=0;side<4;side++){var wire=cells[i].GetChild(side);Require(wire.gameObject.activeSelf==((panel.Circuit.Ports(i)&(1<<side))!=0),"3D/UI ports mismatch");if(wire.gameObject.activeSelf)Require((wire.GetComponent<Renderer>().sharedMaterial==on)==panel.Circuit.IsPowered(i),"3D/UI signal mismatch");}
        }
        private static void CheckSweep()
        {
            var hatch=panel.CabinetDoor;var old=hatch.Leaf.localRotation;var leaves=hatch.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Thick Door")).ToArray();
            var equipment=GameObject.Find("Motel Blockout/Technical Room/Olive Generator").GetComponentsInChildren<Collider>();
            for(int angle=0;angle<=170;angle+=5){hatch.Leaf.localRotation=Quaternion.Euler(0,angle,0);Physics.SyncTransforms();foreach(var leaf in leaves)foreach(var other in equipment)Require(!Physics.ComputePenetration(leaf,leaf.transform.position,leaf.transform.rotation,other,other.transform.position,other.transform.rotation,out _,out _),"Cabinet door hits "+other.name+" at "+angle);}
            hatch.Leaf.localRotation=old;Physics.SyncTransforms();
        }
        private static void Solve(){int[] cells={0,1,2,6,10,11,7},turns={1,1,2,1,2,2,2};for(int i=0;i<cells.Length;i++)for(int n=0;n<turns[i];n++)Require(panel.Rotate(cells[i]),"Rotate rejected");Require(panel.Circuit.IsComplete,"Reference circuit");}
        private static void Reload(bool manual){oldLoop=loop;reloading=true;transitionSeen=false;if(manual)SceneManager.LoadScene(loop.gameObject.scene.path);else Log("waiting for automatic accident/reload");}
        private static void CheckReset()
        {
            Require(!panel.IsOpen&&!panel.LaunchPending&&!panel.HasLaunched&&!panel.Started&&!panel.CabinetDoor.IsOpen&&!Object.FindAnyObjectByType<EmergencyCutoff>().Cover.IsOpen,"State survived reset");
            Require(!StartLamp().activeSelf,"Start indicator did not reset");
            var initial=new BypassCircuit();for(int i=0;i<16;i++)Require(panel.Circuit.Ports(i)==initial.Ports(i),"Board not reset");CheckWorld();
            Require(!loop.EquipmentInspectedThisCycle&&!loop.RescueAccidentAt.HasValue&&Vector3.Distance(player.transform.position,new Vector3(1.55f,.05f,-13.6f))<.2f,"Player/clock/cycle not restored");
            Require(Object.FindObjectsByType<GeneratorButton>().Length==2&&Object.FindObjectsByType<ModuleBypassPanel>().Length==1&&Object.FindObjectsByType<TechnicalHatch>().Length==2,"Duplicate controls");
            if(cycle>0){Require(transitionSeen&&oldLoop.Outcome==MotelLoopController.AccidentOutcome.Reset,"Incomplete rescue did not reset once");}
            if(cycle==1||cycle==2)Require(oldLoop.Elapsed-clockMark>=ModuleBypassPanel.LaunchDelay,"Restart fired before its shared delay");
            var store=new ObservationStore(SessionState.GetString(Prefix+"Save",""));
            Require(store.HasBypassFact==(cycle>=2),"Unsolved restart invented a bypass fact or solved restart lost it");
            if(cycle>=2)Require(!store.RememberBypassExplosion(),"Duplicate bypass fact");
        }
        private static GameObject StartLamp()=>(GameObject)typeof(ModuleBypassPanel).GetField("startIndicator",Private).GetValue(panel);
        private static void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(1600,1000,24);target.Create();var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;var prev=camera.targetTexture;float aspect=camera.aspect;
            try{camera.aspect=1.6f;camera.targetTexture=target;camera.Render();RenderTexture.active=target;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("Logs/TechnicalRoom/"+filename,tex.EncodeToPNG());}
            finally{camera.targetTexture=prev;camera.aspect=aspect;RenderTexture.active=old;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(tex);}
        }
        private static void Top()
        {
            var ceilings=GameObject.Find("Motel Blockout/Ceilings");var go=new GameObject("Temporary top capture");var cam=go.AddComponent<Camera>();cam.CopyFrom(player.GetComponentInChildren<Camera>());cam.transform.SetPositionAndRotation(MotelTechnicalRoomSceneSetup.World(3.12f,12,3.52f),Quaternion.Euler(90,-90,0));cam.orthographic=true;cam.orthographicSize=4.1f;
            var fixture=GameObject.Find("Motel Blockout/Technical Room/Fluorescent Fixture");bool active=ceilings.activeSelf;bool lit=fixture.activeSelf;try{ceilings.SetActive(false);fixture.SetActive(false);Capture(cam,"Top.png");}finally{ceilings.SetActive(active);fixture.SetActive(lit);Object.DestroyImmediate(go);}
        }
        private static void Pause(bool value)=>typeof(MotelLoopController).GetMethod("SetPaused",Private).Invoke(loop,new object[]{value});
        private static void Require(bool value,string text){if(!value)throw new Exception(text);}
        private static void Log(string text)=>File.AppendAllText(Report,text+"\n");
        private static string UserHash(){string p=Path.Combine(Application.persistentDataPath,"observations-v1.json");if(!File.Exists(p))return "missing";using var sha=SHA256.Create();return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p)));}
        private static void Finish(bool pass,string text){if(UserHash()!=SessionState.GetString(Prefix+"Hash","")){pass=false;text+=" USER SAVE CHANGED";}Log((pass?"PASS ":"FAIL ")+text);SessionState.SetBool(Prefix+"Running",false);if(!pass)Debug.LogError(text);EditorApplication.isPlaying=false;}
    }
}
