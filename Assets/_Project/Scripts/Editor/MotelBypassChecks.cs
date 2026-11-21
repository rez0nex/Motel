using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Motel.Editor
{
    public static class MotelBypassChecks
    {
        [MenuItem("Motel/Validate Module Bypass")]
        public static void ValidateCurrent(){Debug.Log(BypassCircuitChecks.Validate());ValidateScene(SceneManager.GetActiveScene());Debug.Log("PASS: expanded technical room, cabinet, independent controls, world board and loop references.");}
        public static void ValidateScene(Scene scene)
        {
            var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            T One<T>() where T:Component=>all.Select(t=>t.GetComponent<T>()).Where(c=>c!=null).Single();
            var panel=One<ModuleBypassPanel>();var note=One<ModuleBypassNote>();var loop=One<MotelLoopController>();
            Require(panel.CabinetDoor!=null && One<EmergencyCutoff>().Cover!=null,"Missing door/cover");
            Require(new SerializedObject(loop).FindProperty("bypassPanel").objectReferenceValue==panel,"Loop panel reference");
            Require(Vector3.Distance(panel.transform.position,MotelBypassSceneSetup.Position)<.001f,"Generator/scene panel position");
            Require(all.Count(t=>t.GetComponent<GeneratorButton>()!=null)==2 && all.Count(t=>t.GetComponent<TechnicalHatch>()!=null)==2,"Duplicate/missing world controls");
            Require(note.Content.Contains("После работы перезапустить генератор"),"Inside note");
            var floor=all.Single(t=>t.name=="Technical Floor");Require(Mathf.Abs(floor.localScale.x-7.04f)<.001f && Mathf.Abs(floor.localScale.z-6.24f)<.001f,"Technical footprint");
            Require(all.Single(t=>t.name=="Technical Ceiling").parent.name=="Ceilings","Ceiling group");
            var ports=all.Where(t=>t.name=="Left Side Input" || t.name=="Right Side Output").ToArray();
            Require(ports.Length==2 && Mathf.Abs(ports[0].position.y-ports[1].position.y)<.001f && Mathf.Abs(ports[0].position.y-2.275f)<.001f,"Side ports must align with second row");
            foreach(var r in all.Single(t=>t.name=="Technical Room").GetComponentsInChildren<Renderer>(true))
                Require(r.sharedMaterial!=null && r.sharedMaterial.shader.isSupported,"Missing/unsupported technical material "+r.name);
            Require(One<EquipmentInspection>()!=null,"Separate diagnostics missing");
        }
        private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    }
}
