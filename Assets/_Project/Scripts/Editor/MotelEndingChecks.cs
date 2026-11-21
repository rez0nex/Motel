using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    [InitializeOnLoad]
    public static class MotelEndingChecks
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private const string Prefix="Motel.EndingChecks.",Report="Logs/EndingStage/Play.txt";
        private static bool ready,initialized,reloading,chain;
        private static int scenario,step,frame;
        private static double deadline,marked;
        private static MotelLoopController loop,previous;
        private static VadimEvacuee vadim;private static LeonidEvacuee leonid;
        private static MarinaEvacuee marina;private static LeraEvacuee lera;
        private static DialogueController dialogue;private static FirstPersonController player;
        private static string save;private static Keyboard testKeyboard;
        private static double frozenClock;
        private static Vector3 rescuersFrom;
        private static bool captureOnly,corridorShot,rescueShot,handlerHandInput;
        private static double phaseSince;
        private static double rescueDeadline, pausedClock, pausedSince;
        static MotelEndingChecks()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>
            {
                if(state==PlayModeStateChange.EnteredPlayMode)ready=true;
                if(state!=PlayModeStateChange.EnteredEditMode)return;
                ready=false;
                if(SessionState.GetBool(Prefix+"Done",false))
                {
                    SessionState.SetBool(Prefix+"Done",false);
                    if(chain && SessionState.GetInt(Prefix+"ExitCode",1)==0)
                    {chain=false;EditorApplication.delayCall+=()=>{try{MotelBypassPlayChecks.Begin();}catch(Exception e){File.AppendAllText(Report,"\nBypass regression could not start: "+e);if(Application.isBatchMode)EditorApplication.Exit(1);}};}
                    else if(Application.isBatchMode)EditorApplication.Exit(SessionState.GetInt(Prefix+"ExitCode",1));
                }
                else if(SessionState.GetBool(Prefix+"Running",false))Finish(false,"Play stopped externally");
            };
        }
        public static void RunBatch()
        {
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                    throw new Exception("Stop Play Mode and save the current scene first.");
                EditorSceneManager.OpenScene(MotelBlockoutSceneCreator.ScenePath);chain=true;Begin();
            }
            catch(Exception e){Directory.CreateDirectory("Logs/EndingStage");File.WriteAllText(Report,"FAIL "+e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        public static void CaptureSuccess()
        {chain=false;Begin();scenario=1;captureOnly=true;}
        // Background editor input may discard synthetic key events. This mode checks the
        // same public hand command; it does not claim a physical keyboard acceptance test.
        public static void BeginWithHandlerInput()
        { Begin(); handlerHandInput=true; Log("Hand action uses handler for background automation; physical E remains a manual check."); }
        [MenuItem("Motel/Run Ending Play Checks")]
        public static void Begin()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path!=MotelBlockoutSceneCreator.ScenePath)
                throw new Exception("Open saved MotelBlockout in edit mode.");
            Directory.CreateDirectory("Logs/EndingStage");File.WriteAllText(Report,"RUNNING ending checks, real final-metre NPC motors, isolated saves.\n");
            Log(ValidateClockDeadlines());
            SessionState.SetString(Prefix+"UserHash",UserHash());SessionState.SetBool(Prefix+"Running",true);
            SessionState.SetBool(Prefix+"Done",false);
            initialized=ready=reloading=false;scenario=step=frame=0;deadline=EditorApplication.timeSinceStartup+150;
            captureOnly=corridorShot=rescueShot=handlerHandInput=false;phaseSince=0;
            EditorApplication.isPaused=false;EditorApplication.isPlaying=true;
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Prefix+"Running",false)||!ready||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            Application.runInBackground=true;EditorApplication.QueuePlayerLoopUpdate();
            if(frame==Time.frameCount || Time.frameCount<5)return;frame=Time.frameCount;
            try
            {
                Require(EditorApplication.timeSinceStartup<deadline,"Timeout scenario="+scenario+" step="+step);
                loop=Object.FindAnyObjectByType<MotelLoopController>();if(loop==null)return;
                if(reloading)
                {
                    if(ReferenceEquals(loop,previous))return;
                    Require(!loop.Transitioning && loop.Outcome==MotelLoopController.AccidentOutcome.Undecided && loop.CurrentRescueMask==0 && loop.Ending==null,"Reset did not restore attempt");
                    Require(UserHash()==SessionState.GetString(Prefix+"UserHash",""),"User save changed");
                    Log("PASS reset: fresh clock/player/guests, no ending objects");
                    Require(Object.FindObjectsByType<MotelEndingSequence>().Length==0,"Ending survives reset");
                    reloading=false;initialized=false;step=0;
                }
                if(!initialized){Initialize();initialized=true;}
                if(!loop.EndingActive && loop.IsPaused && step!=10)Pause(false);
                if(scenario==0)ThreeSafe();else EndingScenario();
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        private static void Initialize()
        {
            vadim=Object.FindAnyObjectByType<VadimEvacuee>();leonid=Object.FindAnyObjectByType<LeonidEvacuee>();
            marina=Object.FindAnyObjectByType<MarinaEvacuee>();lera=Object.FindAnyObjectByType<LeraEvacuee>();
            player=Object.FindAnyObjectByType<FirstPersonController>();dialogue=Object.FindAnyObjectByType<DialogueController>();
            save=Path.GetFullPath("Temp/Ending-"+Guid.NewGuid().ToString("N")+".json");
            var knowledge=new ObservationStore(save);
            if(scenario==0){knowledge.RememberLeonidRescue();knowledge.RememberVadimRescue();knowledge.RememberMarinaRescue();knowledge.RememberLeraRescue();}
            typeof(MotelLoopController).GetField("knowledge",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,knowledge);
            Move(player,new Vector3(16.65f,.05f,-16.1f));
            Pause(false);
            if (scenario == 3) Clock().Advance(356, false, false); // Normal deadline precedes the arrival delay.
            StartArrival(vadim,VadimEvacuee.Stage.Evacuating,new Vector3(0,0,-.8f));
            StartArrival(leonid,LeonidEvacuee.Stage.Evacuating,new Vector3(0,0,-.8f));
            StartArrival(marina,MarinaEvacuee.Stage.Evacuating,new Vector3(0,0,-.8f));
            // Lera uses her own following/settling logic, not a forced Safe setter.
            var destination=(Transform)typeof(LeraEvacuee).GetField("safePoint",Private).GetValue(lera);
            Move(lera,destination.position+new Vector3(0,0,scenario==0?3.4f:.9f));
            typeof(LeraEvacuee).GetMethod("Follow",Private).Invoke(lera,null);
            marked=EditorApplication.timeSinceStartup;
        }
        private static void StartArrival<T>(T npc,object state,Vector3 offset) where T:MonoBehaviour
        {
            var type=typeof(T);var destination=(Transform)type.GetField("safePoint",Private).GetValue(npc);
            Move(npc,destination.position+offset);
            type.GetProperty("CurrentStage").GetSetMethod(true).Invoke(npc,new[]{state});
            var body=npc.GetComponent<CharacterController>();body.enabled=true;
            var speed=(float)type.GetField("speed",Private).GetValue(npc);
            var motor=new EvacuationMotor(body,loop,dialogue,npc,new[]{destination.position},destination,speed);motor.Begin();
            type.GetField("motor",Private).SetValue(npc,motor);
            if(npc is VadimEvacuee v)type.GetField("openedJourneyDoor",Private).SetValue(v,true);
            if(npc is LeonidEvacuee)
            {
                ((BoxCollider)type.GetField("sleepingTarget",Private).GetValue(npc)).enabled=false;
                ((GameObject)type.GetField("sleepingModel",Private).GetValue(npc)).SetActive(false);
                ((GameObject)type.GetField("standingModel",Private).GetValue(npc)).SetActive(true);
            }
        }
        private static void Move(Component who,Vector3 at)
        {var cc=who.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;who.transform.position=at;if(cc!=null)cc.enabled=true;Physics.SyncTransforms();}
        private static void ThreeSafe()
        {
            if(step==0 && loop.CurrentRescueMask==7)
            {
                Require(lera.CurrentStage==LeraEvacuee.Stage.Following && !FamilyEpisode.InSafeArea(lera.transform.position),"Fourth guest not actually pending");
                Require(new ObservationStore(save).HasLeraFact,"Legacy rescue fact fixture missing");
                Require(!loop.RescueAccidentAt.HasValue,"Incomplete rescue scheduled an early accident");
                NearExpiry();step=1;
            }
            else if(step==1 && loop.Transitioning)
            {
                Require(loop.Outcome==MotelLoopController.AccidentOutcome.Reset && loop.RescueMaskAtAccident==7,"Past knowledge or pending fourth triggered ending");
                Require(!loop.RequestAccident(true) && !loop.RequestBypassAccident(),"Duplicate accident accepted");
                Log("PASS: three physically arrived, fourth still following outside safe area; old rescue knowledge ignored; timer chooses reset once");
                previous=loop;scenario=1;reloading=true;
            }
        }
        private static void EndingScenario()
        {
            if(step==0 && loop.CurrentRescueMask==15)
            {
                Require(loop.RescueAccidentAt.HasValue, "Four arrivals did not schedule a deadline");
                rescueDeadline = loop.RescueAccidentAt.Value;
                if(scenario==1)
                {
                    Require(rescueDeadline - loop.Elapsed > 7.5 && rescueDeadline - loop.Elapsed <= 8,
                        "Arrival delay is not eight game seconds");
                    pausedClock = loop.Elapsed; Pause(true); pausedSince = EditorApplication.timeSinceStartup;
                    step=10; return;
                }
                if(scenario==2)
                {
                    var panel=Object.FindAnyObjectByType<ModuleBypassPanel>();
                    if(panel.CabinetDoor!=null && !panel.CabinetDoor.IsFullyOpen)
                    {if(!panel.CabinetDoor.IsOpen)panel.CabinetDoor.Interact();return;}
                    panel.Interact();
                    int[] cells={0,1,2,6,10,11,7},turns={1,1,2,1,2,2,2};
                    for(int i=0;i<cells.Length;i++)for(int n=0;n<turns[i];n++)Require(panel.Rotate(cells[i]),"Panel rotation refused");
                    Require(!loop.Transitioning && panel.CanLaunch,"Puzzle solving starts explosion or fails");Require(panel.Launch(),"Explicit launch refused");
                }
                if(scenario==3) Require(rescueDeadline > AttemptClock.Duration,"Normal timer should be first in fixture");
                if(scenario==4)
                {
                    // Exact common boundary, independent of variable frame duration / NPC arrival frame.
                    var tie = new AttemptClock(); tie.Advance(352,false,false); tie.TryScheduleRescueAccident(true);
                    typeof(MotelLoopController).GetField("clock",Private).SetValue(loop,tie);
                    rescueDeadline = 360;
                }
                step=1;
            }
            else if(step==10)
            {
                Require(loop.IsPaused && Time.timeScale==0 && Math.Abs(loop.Elapsed-pausedClock)<.000001,
                    "Pause advanced the rescue countdown");
                Require(!loop.Transitioning,"Accident ran during pause");
                if(EditorApplication.timeSinceStartup-pausedSince<.65)return;
                loop.RememberVadimRescue();loop.RememberLeonidRescue();loop.RememberMarinaRescue();loop.RememberLeraRescue();
                Require(loop.RescueAccidentAt==rescueDeadline,"Repeated arrival restarted countdown");
                Pause(false);step=11;
            }
            else if(step==11)
            {
                Require(!(bool)typeof(FirstPersonController).GetField("controlBlocked",Private).GetValue(player),
                    "Player control blocked during arrival delay");
                Require(!loop.SuppressInteractionUI && !loop.Transitioning,"World interactions blocked before the deadline");
                loop.RememberLeraRescue();Require(loop.RescueAccidentAt==rescueDeadline,"Repeat arrival postponed countdown");
                if(loop.Elapsed >= rescueDeadline-3)step=1;
            }
            else if(step==1 && loop.Transitioning)
            {
                Require(loop.Outcome==MotelLoopController.AccidentOutcome.Ending && loop.RescueMaskAtAccident==15,"Four arrivals did not select ending");
                if(scenario==1)Require(Math.Abs(loop.Elapsed-rescueDeadline)<.000001,"Automatic ending missed its deadline");
                if(scenario==3)Require(Clock().Expired && !Clock().RescueAccidentDue,"Normal timer did not win");
                if(scenario==4)Require(Clock().Expired && Clock().RescueAccidentDue,"Exact timer tie fixture failed");
                frozenClock=loop.Elapsed;previous=loop;step=2;
            }
            else if(step==2)
            {
                Require(ReferenceEquals(loop,previous),"Ending reloaded scene");
                Require(!loop.RequestAccident(false)&&!loop.RequestAccident(true)&&!loop.RequestBypassAccident(),"Repeat signal accepted during ending");
                Require(Math.Abs(loop.Elapsed-frozenClock)<.00001 && loop.SuppressInteractionUI,"Ending clock/UI not locked");
                var ending=loop.Ending;if(ending==null)return;
                Require(ending.StartCount==1 && Object.FindObjectsByType<MotelEndingSequence>().Length==1,"Duplicate ending");
                Require((bool)typeof(FirstPersonController).GetField("controlBlocked",Private).GetValue(player),"World movement allowed");
                if(ending.CurrentPhase==MotelEndingSequence.Phase.Rescuers)
                {Require(!ending.PressHand(),"Early hand accepted");rescuersFrom=ending.RescuerStart;}
                if(ending.CurrentPhase==MotelEndingSequence.Phase.Corridor && !corridorShot)
                {if(phaseSince==0)phaseSince=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-phaseSince>1){ScreenCapture.CaptureScreenshot("Logs/EndingStage/Corridor.png");corridorShot=true;phaseSince=0;}}
                if(ending.CurrentPhase==MotelEndingSequence.Phase.Rescuers && !rescueShot)
                {if(phaseSince==0)phaseSince=EditorApplication.timeSinceStartup;if(EditorApplication.timeSinceStartup-phaseSince>1){ScreenCapture.CaptureScreenshot("Logs/EndingStage/Rescuers.png");rescueShot=true;phaseSince=0;}}
                if(ending.CurrentPhase!=MotelEndingSequence.Phase.Hospital)return;
                Require(Vector3.Distance(ending.FirstRescuer.position,rescuersFrom)>2,"Rescuer never approached");
                Require(ending.EndingCamera.transform.position.y < -40,"Hospital camera not placed");
                Require(Object.FindObjectsByType<AudioListener>().Count(l=>l.enabled)==1,"Audio listener duplicate");
                if(scenario==2)Require(new ObservationStore(save).HasBypassFact,"Final panel explosion did not preserve observation");
                ScreenCapture.CaptureScreenshot("Logs/EndingStage/Hospital-"+scenario+".png");
                marked=EditorApplication.timeSinceStartup;step=21;
            }
            else if(step==21 && EditorApplication.timeSinceStartup-marked>.4)
            {
                if(handlerHandInput)Require(loop.Ending.PressHand(),"Hospital hand command refused");
                else {testKeyboard=InputSystem.AddDevice<Keyboard>("Ending test keyboard");InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.E));}
                step=3;
            }
            else if(step==3)
            {
                var ending=loop.Ending;if(ending.CurrentPhase==MotelEndingSequence.Phase.Hospital)return;
                Require(ending.HandPressCount==1 && !ending.PressHand(),"Hand action duplicated");
                if(testKeyboard!=null){InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;}
                step=4;
            }
            else if(step==4 && loop.Ending.CurrentPhase==MotelEndingSequence.Phase.Complete)
            {
                Require(ReferenceEquals(loop,previous)&&loop.Ending.HandPressCount==1&&loop.Ending.StartCount==1,"Ending reset/repeated after completion");
                ScreenCapture.CaptureScreenshot("Logs/EndingStage/Complete-"+scenario+".png");
                Log("PASS ending "+(scenario==1?"automatic eight game seconds + pause/repeated arrivals/control":scenario==2?"explicit bypass launch before arrival deadline":scenario==3?"normal timer before arrival deadline":"exact tie of both timers")+": all four actual arrivals; corridor/rescuers/hospital/E/complete; no reload, duplicate request, moving clock or duplicate hand action");
                if(scenario==4 || captureOnly){Finish(true,"Ending/countdown scenarios passed; user save unchanged. Arrival fixtures are not full motel routes; countdown manual acceptance remains required.");return;}
                previous=loop;scenario++;reloading=true;
                EditorSceneManager.LoadSceneAsyncInPlayMode(MotelBlockoutSceneCreator.ScenePath,new LoadSceneParameters(LoadSceneMode.Single));
            }
        }
        private static void NearExpiry(){var c=(AttemptClock)typeof(MotelLoopController).GetField("clock",Private).GetValue(loop);c.Advance(AttemptClock.Duration-c.Elapsed-.04,false,false);}
        private static AttemptClock Clock() => (AttemptClock)typeof(MotelLoopController).GetField("clock",Private).GetValue(loop);
        private static string ValidateClockDeadlines()
        {
            var c = new AttemptClock(); c.Advance(12,false,false);
            Require(!c.TryScheduleRescueAccident(false) && !c.RescueAccidentAt.HasValue,"Partial rescue clock scheduled");
            Require(c.TryScheduleRescueAccident(true) && c.RescueAccidentAt==20,"Clock did not use shared elapsed+8");
            c.Advance(50,true,false);Require(c.Elapsed==12,"Paused deadline advanced");
            c.Advance(1,false,true);Require(c.Elapsed==18,"Debug delay not measured in game seconds");
            Require(!c.TryScheduleRescueAccident(true) && c.RescueAccidentAt==20,"Clock rescheduled");
            c.Advance(3,false,false);Require(c.RescueAccidentDue && !c.Expired && c.Elapsed==20,"Rescue deadline lost");
            Require(c.TryRequestAccident() && !c.TryRequestAccident(),"Deadline latch duplicated");
            c.Advance(20,false,false);Require(c.Elapsed==20,"Latched clock moved");
            foreach(double arrival in new[]{352d,355d})
            {
                c=new AttemptClock();c.Advance(arrival,false,false);c.TryScheduleRescueAccident(true);c.Advance(20,false,false);
                Require(c.Elapsed==360 && c.Expired && c.RescueAccidentDue==(arrival==352),"Normal/tied deadline incorrect");
                Require(c.TryRequestAccident() && !c.TryRequestAccident(),"Timer race duplicated request");
            }
            Require(!new AttemptClock().RescueAccidentAt.HasValue,"New attempt retained deadline");
            return "PASS clock: partial rescue, shared eight-second deadline, pause, debug speed, repeated arrival, early/normal/exact-tie deadlines, one request and fresh attempt.";
        }
        private static void Pause(bool value)=>typeof(MotelLoopController).GetMethod("SetPaused",Private).Invoke(loop,new object[]{value});
        private static string UserHash(){string path=Path.Combine(Application.persistentDataPath,"observations-v1.json");if(!File.Exists(path))return "missing";using var sha=SHA256.Create();return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));}
        private static void Require(bool yes,string text){if(!yes)throw new Exception(text);}
        private static void Log(string text)=>File.AppendAllText(Report,text+"\n");
        private static void Finish(bool success,string text)
        {
            if(UserHash()!=SessionState.GetString(Prefix+"UserHash","")){success=false;text+=" USER SAVE CHANGED";}
            if(testKeyboard!=null){InputSystem.RemoveDevice(testKeyboard);testKeyboard=null;}
            Log((success?"PASS ":"FAIL ")+text);
            SessionState.SetBool(Prefix+"Running",false);SessionState.SetBool(Prefix+"Done",true);SessionState.SetInt(Prefix+"ExitCode",success?0:1);
            EditorApplication.isPlaying=false;
        }
    }
}
