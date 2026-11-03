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
    [InitializeOnLoad]
    public static class MotelLeonidPlayChecks
    {
        private const string Prefix = "Motel.CaneChecks.";
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class Step { public string Name; public Func<bool> Tick; }
        private static readonly List<Step> steps = new List<Step>();
        private static bool ready, initialized, reloading, intentionalPause;
        private static int cycle, cursor, frame;
        private static double stepStart, lastReport, realTime, sampledTime;
        private static MotelLoopController loop, oldLoop;
        private static LeonidEvacuee npc;
        private static LeonidBelongings supplies;
        private static DialogueController window;
        private static CharacterController player;
        private static CanePickup cane;

        static MotelLeonidPlayChecks()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) ready = true;
                if (state != PlayModeStateChange.EnteredEditMode) return;
                ready = false;
                if (SessionState.GetBool(Prefix + "Restore", false))
                {
                    PlayerSettings.runInBackground = SessionState.GetBool(Prefix + "ProjectBackground", false);
                    SessionState.SetBool(Prefix + "Restore", false);
                }
                if (SessionState.GetBool(Prefix + "Running", false)) Finish("CANCELLED: Play Mode stopped.");
            };
        }
        [MenuItem("Motel/Run Automated Leonid Play Checks")]
        public static void Begin()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || scene.isDirty ||
                scene.path != MotelBlockoutSceneCreator.ScenePath) throw new Exception("Open saved MotelBlockout in edit mode.");
            string save = Path.GetFullPath("Temp/CanePlay-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(save, "{\"version\":1,\"cutoffCausedExplosion\":true}");
            SessionState.SetString(Prefix + "Save", save); SessionState.SetString(Prefix + "UserHash", UserHash());
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "ProjectBackground", PlayerSettings.runInBackground);
            SessionState.SetBool(Prefix + "Restore", true); SessionState.SetBool(Prefix + "Running", true);
            cycle = cursor = frame = 0; lastReport = 0; ready = initialized = reloading = intentionalPause = false;
            File.WriteAllText("Logs/Leonid-PlayChecks.txt", "RUNNING: spare cane in car.");
            File.WriteAllText("Logs/Leonid-PlayChecks-progress.txt", "Player 3 m/s, Leonid 1.2 m/s, 360-second setting. Real CC walk and first-hit checks; handler-driven dialogue without human reading.\n");
            EditorApplication.isPaused = false; EditorApplication.isPlaying = true;
        }
        private static void Tick()
        {
            if (!ready || !SessionState.GetBool(Prefix + "Running", false) || EditorApplication.isCompiling || !EditorApplication.isPlaying) return;
            Application.runInBackground = true; EditorApplication.QueuePlayerLoopUpdate();
            try
            {
                loop = Object.FindAnyObjectByType<MotelLoopController>(); npc = Object.FindAnyObjectByType<LeonidEvacuee>();
                supplies = Object.FindAnyObjectByType<LeonidBelongings>(); window = Object.FindAnyObjectByType<DialogueController>();
                cane = Object.FindObjectsByType<CanePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
                if (loop == null || npc == null || supplies == null || window == null || Time.frameCount < 4) return;
                player = Object.FindAnyObjectByType<FirstPersonController>().GetComponent<CharacterController>();
                if (!initialized)
                {
                    typeof(MotelLoopController).GetField("knowledge", BindingFlags.Static | BindingFlags.NonPublic)
                        .SetValue(null, new ObservationStore(SessionState.GetString(Prefix + "Save", "")));
                    initialized = true; Build();
                }
                if (!intentionalPause && loop.IsPaused) Pause(false);
                if (reloading)
                {
                    if (ReferenceEquals(loop, oldLoop)) { Require(EditorApplication.timeSinceStartup - realTime < 15, "Reload timeout"); return; }
                    cycle++; reloading = false; ResetState();
                    if (cycle == 3)
                    {
                        Require(UserHash() == SessionState.GetString(Prefix + "UserHash", ""), "User save changed");
                        Finish("PASS: physical normal quest and early retrieval, broken cane not usable, keys on nightstand, locked trunk feedback, real lid and spare-only pickup, dialogue clock/pause, handover once, blocked room/exit and physical rescue, reset during evacuation/dialogue/open trunk, independent persistent facts without copies. User save unchanged. Handler-driven input, manual acceptance still required."); return;
                    }
                    Build();
                }
                if (frame == Time.frameCount) return; frame = Time.frameCount;
                if (cursor >= steps.Count) return;
                Require(loop.Elapsed < 350 && EditorApplication.timeSinceStartup - stepStart < 55, "Step timeout: " + steps[cursor].Name);
                if (EditorApplication.timeSinceStartup > lastReport)
                {
                    lastReport = EditorApplication.timeSinceStartup + 2;
                    File.AppendAllText("Logs/Leonid-PlayChecks-progress.txt", "cycle=" + cycle + " t=" + loop.Elapsed.ToString("F2") + " step=" + cursor + "/" + steps[cursor].Name +
                        " P=" + player.transform.position + " NPC=" + npc.CurrentStage + "/" + npc.Waypoint + "/" + npc.BlockingObject + "\n");
                }
                if (steps[cursor].Tick()) { cursor++; stepStart = EditorApplication.timeSinceStartup; }
            }
            catch (Exception e) { Finish("FAIL cycle=" + cycle + " step=" + cursor + ": " + e); }
        }
        private static void Build()
        {
            steps.Clear(); cursor = 0; stepStart = EditorApplication.timeSinceStartup;
            Act("reset state", ResetState);
            if (cycle == 2)
            {
                // Short state fixture: both real physical acquisition orders have already been walked.
                Act("open trunk fixture", () => { Item(LeonidWorldItem.ItemKind.CarKeys).Interact(); supplies.ToggleTrunk(); });
                Wait("fully open fixture", () => supplies.CanTakeSpare);
                Act("reset open trunk and dialogue", () => { npc.Interact(); Reload(); });
                return;
            }
            if (cycle == 1)
            {
                Walk(1.55f, -11.4f); Walk(4.8f, -11.4f); Walk(4.8f, -13.6f); Open("Main Entrance");
                Walk(8, -13.6f); Walk(10, -16); Walk(22.5f, -16.8f);
                Act("locked trunk without keys", () =>
                {
                    var trunk = Item(LeonidWorldItem.ItemKind.Trunk); Near(trunk); trunk.Interact();
                    Require(window.IsOpen && Line().Contains("заперт") && !supplies.IsTrunkOpen && !npc.CarryingCane, "Locked trunk bypassed"); window.Close();
                    cane.Interact(); Require(!npc.CarryingCane && !supplies.SpareTaken, "Closed trunk yielded spare");
                });
                ReturnFromCar();
            }
            else { Walk(1.55f, -11.4f); Walk(4.8f, -11.4f); Walk(4.8f, 0); }
            EnterRoom();
            if (cycle == 0)
            {
                Walk(R().X(1.7f), R().Z(4.4f));
                Act("inspect broken cane", () =>
                {
                    var broken = Item(LeonidWorldItem.ItemKind.BrokenCane); Near(broken); broken.Interact();
                    Require(Line().Contains("Крепление") && Line().Contains("пользоваться") && !npc.CarryingCane && broken.GetComponent<CanePickup>() == null, "Broken cane usable"); window.Close();
                });
                Walk(R().X(1.75f), R().Z(4.9f));
                Act("wake and ask for spare", () =>
                {
                    Near(npc); npc.Interact(); window.Choose(0);
                    Require(npc.CurrentStage == LeonidEvacuee.Stage.NeedsCane && Line() == LeonidBelongings.Directions && window.Speaker == "Леонид", "Wrong car directions");
                    sampledTime = loop.Elapsed;
                });
                Wait("dialogue time continues", () => loop.Elapsed - sampledTime > 0.6);
                Act("pause dialogue clock", () => { sampledTime = loop.Elapsed; realTime = EditorApplication.timeSinceStartup; intentionalPause = true; Pause(true); });
                Wait("paused clock stable", () =>
                {
                    if (EditorApplication.timeSinceStartup - realTime < 0.6) return false;
                    Require(Math.Abs(loop.Elapsed - sampledTime) < 0.001, "Clock moved on pause"); intentionalPause = false; Pause(false); window.Close(); return true;
                });
            }
            Walk(R().X(1.75f), R().Z(3.25f)); Walk(R().X(4.55f), R().Z(3.25f)); Walk(R().X(4.55f), R().Z(4.1f));
            Act("keys on nightstand", () =>
            {
                var keys = Item(LeonidWorldItem.ItemKind.CarKeys); Near(keys); keys.Interact(); keys.Interact();
                Require(supplies.HasCarKeys && !keys.gameObject.activeSelf && !npc.CarryingCane && !supplies.IsTrunkOpen, "Keys state");
                if (cycle == 1) Require(npc.CurrentStage == LeonidEvacuee.Stage.Sleeping, "Early keys woke Leonid");
            });
            Walk(R().X(4.55f), R().Z(3.25f)); Walk(R().X(1.15f), R().Z(3.25f)); ExitRoom();
            Walk(25.4f, 0); Open("Emergency Exit"); Walk(28.34f, 0); Walk(28.34f, -12); Walk(25, -16.8f); Walk(22.5f, -16.8f);
            Act("open trunk with keys", () => { Near(Item(LeonidWorldItem.ItemKind.Trunk)); supplies.ToggleTrunk(); });
            Wait("lid opens and spare appears", () => supplies.CanTakeSpare && cane.gameObject.activeSelf);
            Act("take spare exactly once", () =>
            {
                Near(cane); cane.Interact(); cane.Interact(); Require(npc.CarryingCane && supplies.SpareTaken && !cane.gameObject.activeSelf, "Spare pickup");
                supplies.ToggleTrunk(); supplies.ToggleTrunk(); Require(!cane.gameObject.activeSelf, "Collected spare respawned");
                if (cycle == 1) Require(npc.CurrentStage == LeonidEvacuee.Stage.Sleeping, "Early retrieval requires conversation");
            });
            ReturnFromCar(); EnterRoom(); Walk(R().X(1.75f), R().Z(4.9f));
            Act("give working spare and depart", () =>
            {
                Near(npc); npc.Interact(); if (cycle == 1) window.Choose(0);
                Require(Line().Contains("уже принесли") && !Line().Contains("Ключи на тумбочке"), "Repeated errand despite early spare");
                window.Choose(0); Require(npc.CurrentStage == LeonidEvacuee.Stage.Evacuating && !npc.CarryingCane && !window.IsOpen, "Handover failed");
                npc.Interact(); window.Close(); Require(!npc.TakeCane(), "Duplicate cane accepted while evacuating");
            });
            if (cycle == 1)
            {
                Wait("moving evacuation", () => npc.Waypoint > 0);
                Act("reset during evacuation", Reload); return;
            }
            Walk(R().X(1.15f), R().Z(3.25f)); ExitRoom(); Close("Room4"); Walk(24.5f, 0.65f); Close("Emergency Exit");
            Wait("closed room door", () => npc.WaitingForPath && npc.Waypoint == 4);
            Open("Room4"); Wait("closed emergency exit", () => npc.WaitingForPath && npc.Waypoint == 6); Open("Emergency Exit");
            Walk(28.34f, 0); Walk(29.2f, -10); Walk(27, -19.4f);
            Wait("physical safe arrival", () => npc.CurrentStage == LeonidEvacuee.Stage.Safe);
            Act("rescue fact and ordinary reset", () =>
            {
                Require(Vector3.Distance(npc.transform.position, new Vector3(18, 0.05f, -18.1f)) < 0.5f && MotelLoopController.KnowsLeonidRescue, "False rescue");
                var saved = new ObservationStore(SessionState.GetString(Prefix + "Save", ""));
                Require(saved.Facts.Length == 2 && !saved.RememberLeonidRescue(), "Fact lost/duplicated");
                File.AppendAllText("Logs/Leonid-PlayChecks-progress.txt", "NORMAL QUEST COMPLETED activeSeconds=" + loop.Elapsed.ToString("F2") + "\n"); Reload();
            });
        }
        private static void ResetState()
        {
            Require(npc.CurrentStage == LeonidEvacuee.Stage.Sleeping && !npc.CarryingCane && !supplies.HasCarKeys && !supplies.IsTrunkOpen && !supplies.SpareTaken && !cane.gameObject.activeSelf,
                "NPC/belongings did not reset");
            Require(Item(LeonidWorldItem.ItemKind.CarKeys).gameObject.activeSelf && Quaternion.Angle(GameObject.Find("Motel Blockout/Leonid Episode/Leonid Blue Car/Trunk Lid Hinge").transform.localRotation, Quaternion.identity) < 0.1f,
                "Keys/lid not restored");
            Require(Object.FindObjectsByType<LeonidBelongings>().Length == 1 && Object.FindObjectsByType<LeonidWorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 3 &&
                Object.FindObjectsByType<CanePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1 && !window.IsOpen && loop.Elapsed < 2, "Duplicates or time/dialogue not reset");
            if (cycle > 0) Require(MotelLoopController.KnowsLeonidRescue && MotelLoopController.KnowsCutoff, "Knowledge lost");
        }
        private static void EnterRoom()
        { Walk(R().X(0.8f), 0); Open("Room4"); Walk(R().X(0.8f), R().Z(1.7f)); Walk(R().X(1.15f), R().Z(1.7f)); Walk(R().X(1.15f), R().Z(3.25f)); }
        private static void ExitRoom() { Walk(R().X(1.15f), R().Z(1.7f)); Walk(R().X(0.8f), R().Z(1.7f)); Walk(R().X(0.8f), 0); }
        private static void ReturnFromCar() { Walk(25, -16.8f); Walk(28.34f, -12); Walk(28.34f, 0); Open("Emergency Exit"); Walk(25.4f, 0); }
        private static MotelGuestRoomModule.Layout R() => MotelBlockoutPlan.Create().Rooms.Single(r => r.Number == 4);
        private static LeonidWorldItem Item(LeonidWorldItem.ItemKind kind) => Object.FindObjectsByType<LeonidWorldItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(i => i.Kind == kind);
        private static void Near(Component item)
        {
            var target = item.GetComponentsInChildren<Collider>().First(c => c.enabled && c.gameObject.activeInHierarchy);
            Vector3 from = player.transform.position + Vector3.up * 1.55f, delta = target.bounds.center - from;
            Require(Physics.Raycast(from, delta.normalized, out var hit, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent(item.GetType()) == item, "First-hit/range failed: " + item.name + "; hit=" + (hit.collider == null ? "none" : hit.collider.name) + "; player=" + player.transform.position);
        }
        private static void Walk(float x, float z)
        {
            var destination = new Vector3(x, 0, z);
            steps.Add(new Step { Name = "walk " + x + "," + z, Tick = () =>
            {
                Vector3 d = destination - player.transform.position; d.y = 0; if (d.magnitude < 0.07f) return true;
                player.Move(d.normalized * Mathf.Min(3 * Time.deltaTime, d.magnitude)); return false;
            }});
        }
        private static void Open(string name) { Act("open " + name, () => { if (!Door(name).IsOpenRequested) Door(name).Toggle(); }); Wait("swing " + name, () => Door(name).IsFullyOpen); }
        private static void Close(string name) => Act("close " + name, () => { if (Door(name).IsOpenRequested) Door(name).Toggle(); });
        private static PrototypeDoor Door(string name) => GameObject.Find("Motel Blockout/Doors/" + name).GetComponent<PrototypeDoor>();
        private static void Act(string name, Action action) => steps.Add(new Step { Name = name, Tick = () => { action(); return true; } });
        private static void Wait(string name, Func<bool> test) => steps.Add(new Step { Name = name, Tick = test });
        private static string Line() => (string)typeof(DialogueController).GetField("line", Private).GetValue(window);
        private static void Pause(bool value) => typeof(MotelLoopController).GetMethod("SetPaused", Private).Invoke(loop, new object[] { value });
        private static void Reload() { oldLoop = loop; Require(loop.RequestAccident(false) && !loop.RequestAccident(true) && !window.IsOpen, "Repeated accident"); reloading = true; realTime = EditorApplication.timeSinceStartup; }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static string UserHash()
        { string p = Path.Combine(Application.persistentDataPath, "observations-v1.json"); if (!File.Exists(p)) return "ABSENT"; using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(p))); }
        private static void Finish(string text)
        {
            SessionState.SetBool(Prefix + "Running", false); Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
            File.WriteAllText("Logs/Leonid-PlayChecks.txt", text); if (text.StartsWith("PASS")) Debug.Log(text); else Debug.LogError(text); EditorApplication.isPlaying = false;
        }
    }
}
