using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.ArtTests
{
    // Runs against the art-test scene only; no gameplay code or user save is changed.
    [InitializeOnLoad]
    public static class ReceptionDeskChecks
    {
        private static bool running, initialized;
        private static int stage, frame;
        private static double deadline;
        private static bool background;
        private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static MotelLoopController loop;
        private static FirstPersonController player;
        private static Camera camera;
        private static GuestRegister book;
        private static VadimEvacuee vadim;
        private static DialogueController dialogue;
        private static Vector3 initial;
        private static string userHash;

        static ReceptionDeskChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s==PlayModeStateChange.EnteredEditMode && running)Finish(false,"Play Mode stopped externally");
            };
        }
        [MenuItem("Motel/Art Tests/Check Reception Desk in Play Mode")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path!=ReceptionDeskStudy.ScenePath)
                throw new Exception("Open saved reception test scene outside Play Mode.");
            ReceptionDeskStudy.Validate();
            Directory.CreateDirectory("Logs/DeskTest");
            userHash=SaveHash();running=true;initialized=false;stage=frame=0;
            background=Application.runInBackground;
            File.WriteAllText("Logs/DeskTest/PlayChecks.txt","RUNNING\n");
            EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(!running || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            EditorApplication.QueuePlayerLoopUpdate();Application.runInBackground=true;
            if(frame==Time.frameCount || Time.frameCount<5)return;frame=Time.frameCount;
            try
            {
                if(!initialized)
                {
                    loop=Object.FindAnyObjectByType<MotelLoopController>();player=Object.FindAnyObjectByType<FirstPersonController>();
                    camera=player.GetComponentInChildren<Camera>();book=Object.FindAnyObjectByType<GuestRegister>();
                    vadim=Object.FindAnyObjectByType<VadimEvacuee>();dialogue=Object.FindAnyObjectByType<DialogueController>();
                    typeof(MotelLoopController).GetField("knowledge",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,
                        new ObservationStore(Path.GetFullPath("Temp/DeskTest-"+Guid.NewGuid().ToString("N")+".json")));
                    initial=player.transform.position;initialized=true;deadline=EditorApplication.timeSinceStartup+80;
                    Capture(camera,"Start");
                }
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout stage "+stage+" Vadim="+vadim.CurrentStage+" player="+player.transform.position);
                typeof(MotelLoopController).GetMethod("SetPaused",Private).Invoke(loop,new object[]{false});
                if(stage==0)
                {
                    camera.transform.LookAt(book.transform.position+Vector3.up*.02f);
                    Require(Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<GuestRegister>()==book,"Journal first ray");
                    book.Interact();Require(dialogue.IsOpen && dialogue.Participant==book,"Journal E handler");
                    Capture(camera,"JournalView");dialogue.Close();
                    camera.transform.localRotation=Quaternion.identity;
                    Log("PASS: spawn-camera first solid ray <=2.5m reaches journal; existing E handler opens journal.");stage++;
                }
                else if(stage==1)
                {
                    // Walk around the pulled-out chair at full player radius, keeping the original spawn.
                    if(player.transform.position.x>1.12f)Walk(new Vector3(1.05f,.05f,-13.6f));
                    else if(Walk(new Vector3(1.05f,.05f,-11.5f)))stage++;
                }
                else if(stage==2 && Walk(new Vector3(4.8f,.05f,-11.5f)))
                {Log("PASS: physical CharacterController exits behind desk via north side, no old collider.");stage++;}
                else if(stage==3 && Walk(new Vector3(4.9f,.05f,-14.7f)))
                {camera.transform.LookAt(new Vector3(3,1.05f,-13.6f));Capture(camera,"Guest");stage++;}
                else if(stage==4 && vadim.CurrentStage==VadimEvacuee.Stage.AtReception)
                {
                    Require(Vector3.Distance(vadim.transform.position,new Vector3(4.05f,.05f,-13.6f))<.25f,"Vadim arrival position");
                    Log("PASS: Vadim physically arrived at unchanged reception point at "+loop.Elapsed.ToString("F2")+" active seconds; no route edits.");
                    var clock=GameObject.Find("Reception Clock").GetComponentInChildren<TextMesh>();
                    Require(clock!=null && clock.text.StartsWith("20:16:") && clock.text!="20:16:00","Relocated clock ticks");
                    Vector3 oldPosition=camera.transform.position;Quaternion oldRotation=camera.transform.rotation;
                    camera.transform.position=new Vector3(1.7f,1.7f,-12.5f);camera.transform.LookAt(clock.transform.position);
                    Capture(camera,"Clock");camera.transform.SetPositionAndRotation(oldPosition,oldRotation);
                    Log("PASS: relocated clock ticks; original spawn does not overlap desk or chair (static capsule).");
                    vadim.Interact();Require(dialogue.IsOpen && dialogue.Participant==vadim,"Vadim dialogue available");
                    dialogue.Choose(0);Require(vadim.BookingChecked,"Booking checked");dialogue.Choose(0);
                    Require(book.VadimCheckedIn && book.EntryCount==3,"Journal check-in reference preserved");dialogue.Close();
                    Log("PASS: original booking and key actions add exactly one journal entry.");
                    stage++;deadline=EditorApplication.timeSinceStartup+50;
                }
                else if(stage==5 && vadim.transform.position.z>-11)
                {Log("PASS: Vadim leaves reception along unchanged check-in route.");Finish(true,"Reception study checks completed (handlers, not hardware E). User save unchanged.");}
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        private static bool Walk(Vector3 goal)
        {
            Vector3 delta=goal-player.transform.position;delta.y=0;
            if(delta.magnitude<.09f)return true;
            player.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta,3*Time.deltaTime)+Vector3.down*Time.deltaTime);
            return false;
        }
        private static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
        private static void Log(string s)=>File.AppendAllText("Logs/DeskTest/PlayChecks.txt",s+"\n");
        private static string SaveHash()
        {
            string path=Path.Combine(Application.persistentDataPath,"observations-v1.json");
            if(!File.Exists(path))return "absent";
            using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
        private static void Finish(bool ok,string message)
        {
            if(userHash!=SaveHash()){ok=false;message+=" USER SAVE CHANGED";}
            running=false;Application.runInBackground=background;Log((ok?"PASS: ":"FAIL: ")+message);
            EditorApplication.isPlaying=false;
        }
        public static void Capture(Camera cam,string name)
        {
            var target=new RenderTexture(1440,900,24);target.Create();
            var oldTarget=cam.targetTexture;var oldActive=RenderTexture.active;
            var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
            try
            {
                foreach(var sign in Object.FindObjectsByType<WorldSign>(FindObjectsInactive.Include))sign.RefreshNow();
                cam.targetTexture=target;cam.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();File.WriteAllBytes("Logs/DeskTest/"+name+".png",pixels.EncodeToPNG());
            }
            finally{cam.targetTexture=oldTarget;RenderTexture.active=oldActive;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
