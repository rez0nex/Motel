using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Motel.Editor
{
    public static class MotelServiceBlockUpdater
    {
        [Serializable] public class BoxRecord { public string Name,Group;public Vector3 Position,Size; }
        [Serializable] public class Baseline { public List<BoxRecord> Boxes=new List<BoxRecord>(); }
        public static void CaptureBaseline()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new Exception("Saved edit scene required before snapshot.");
            var data=new Baseline();foreach(var b in MotelBlockoutPlan.Create().Boxes)data.Boxes.Add(new BoxRecord{Name=b.Name,Group=b.Group,Position=new Vector3(b.X,b.Y,b.Z),Size=new Vector3(b.Width,b.Height,b.Depth)});
            if(File.Exists("Logs/ServiceLinen/OldPlan.json"))throw new Exception("Baseline exists; do not replace it.");
            File.WriteAllText("Logs/ServiceLinen/OldPlan.json",JsonUtility.ToJson(data,true));
        }

        private static bool OldResidential(Vector3 p)=>p.x>=3.5f && p.x<=32 && p.z>=-8.2f && p.z<=8.2f;
        [MenuItem("Motel/Update Service Block and Linen")]
        public static void UpdateCurrent()
        {
            var scene=SceneManager.GetActiveScene();
            if(EditorApplication.isPlayingOrWillChangePlaymode || scene.isDirty || scene.path!=MotelBlockoutSceneCreator.ScenePath)
                throw new Exception("Open saved MotelBlockout outside Play Mode.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Motel Blockout").transform;
            var marker=root.GetComponent<MotelSpatialLayout>();bool relocated=marker!=null && Mathf.Abs(marker.ResidentialOffsetZ-MotelBlockoutPlan.ResidentialZ)<.001f;
            var report=new List<string>();
            if(!relocated)
            {
                if(!File.Exists("Logs/ServiceLinen/OldPlan.json"))throw new Exception("Original geometry snapshot required before migration.");
                var all=root.GetComponentsInChildren<Transform>(true);
                var selected=new HashSet<Transform>(all.Where(t=>t!=root && OldResidential(t.position)));
                foreach(var t in selected)
                {
                    bool ancestor=false;for(var p=t.parent;p!=null&&p!=root;p=p.parent)if(selected.Contains(p)){ancestor=true;break;}
                    if(!ancestor){report.Add("MOVE "+t.name+" "+t.position);t.position+=Vector3.forward*MotelBlockoutPlan.ResidentialZ;}
                }
                // These are WORLD coordinates, not local offsets. Preserve every object reference and gate index.
                foreach(var mono in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if(!(mono is LeonidEvacuee || mono is VadimEvacuee || mono is MarinaEvacuee || mono is RoomEightLock))continue;
                    var data=new SerializedObject(mono);var it=data.GetIterator();bool enter=true;
                    while(it.NextVisible(enter))
                    {
                        enter=true;
                        if(it.propertyType==SerializedPropertyType.Vector3)
                        {if(OldResidential(it.vector3Value)){it.vector3Value+=Vector3.forward*MotelBlockoutPlan.ResidentialZ;report.Add("WORLD DATA "+mono.name+"/"+it.propertyPath);}enter=false;}
                        else if(it.propertyType==SerializedPropertyType.Bounds)
                        {var b=it.boundsValue;if(OldResidential(b.center)){b.center+=Vector3.forward*MotelBlockoutPlan.ResidentialZ;it.boundsValue=b;report.Add("BOUNDS "+mono.name);}enter=false;}
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                var technical=root.Find("Technical Room");Vector3 delta=MotelTechnicalRoomSceneSetup.Origin-technical.position;
                technical.position+=delta;var damage=root.Find("Time Loop/Prepared Damage");damage.position+=delta;
                var ceiling=root.Find("Ceilings/Technical Concrete Soffit");if(ceiling!=null)ceiling.position+=delta;
                marker=root.gameObject.AddComponent<MotelSpatialLayout>();marker.Configure(MotelBlockoutPlan.ResidentialZ);
            }
            var old=File.Exists("Logs/ServiceLinen/OldPlan.json") ? JsonUtility.FromJson<Baseline>(File.ReadAllText("Logs/ServiceLinen/OldPlan.json")) : new Baseline();
            var plan=MotelBlockoutPlan.Create();
            var wanted=new HashSet<string>(plan.Boxes.Select(b=>b.Group+"/"+b.Name));
            foreach(var b in old.Boxes)if(!wanted.Contains(b.Group+"/"+b.Name))
            {var t=root.Find(b.Group+"/"+b.Name);if(t!=null){report.Add("REPLACE OLD GEOMETRY "+b.Name);UnityEngine.Object.DestroyImmediate(t.gameObject);}}
            foreach(var b in plan.Boxes)
            {
                var group=root.Find(b.Group);if(group==null){group=new GameObject(b.Group).transform;group.SetParent(root,false);}
                var t=group.Find(b.Name);bool create=t==null;
                if(create){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);t=g.transform;t.name=b.Name;t.SetParent(group,false);}
                t.localPosition=new Vector3(b.X,b.Y,b.Z);t.localScale=new Vector3(b.Width,b.Height,b.Depth);
                if(create){t.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/BlockoutMaterials/"+b.Material+".mat");if(!b.Solid)UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());}
            }
            foreach(var d in plan.Doors)
            {
                var t=root.Find("Doors/"+d.Name+(d.Operable?"":" (Closed)"));if(t==null)throw new Exception("Missing existing door "+d.Name);
                t.localPosition=new Vector3(d.X,0,d.Z)-Quaternion.Euler(0,d.Yaw,0)*Vector3.right*(d.Width/2);
                MotelServiceDoorGeometry.Configure(t,d);
                if(d.Operable){var so=new SerializedObject(t.GetComponent<PrototypeDoor>());so.FindProperty("openAngle").floatValue=d.OpenAngle;so.ApplyModifiedPropertiesWithoutUndo();}
            }
            MotelServiceDoorGeometry.Hardware(root,plan);
            foreach(var sign in plan.Signs){var t=root.Find("Signs/Sign "+sign.Id);if(t!=null)t.localPosition=new Vector3(sign.X,sign.Y,sign.Z);}
            // Retain residential lights (already translated), replace only the old service light positions.
            foreach(Transform t in root.Find("Lighting").Cast<Transform>().Where(t=>t.name.StartsWith("Service Light ")||t.name=="L11 Light").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);
            foreach(var zone in plan.Zones.Where(z=>z.Id=="L04"||z.Id=="L06")){var t=root.Find("Lighting/"+zone.Id+" Light");if(t!=null)t.position=new Vector3((zone.MinX+zone.MaxX)/2,2.6f,(zone.MinZ+zone.MaxZ)/2);}
            foreach(float z in new[]{-13.5f,-9.3f,-5f,-.5f,4.2f,8.2f})
            {var t=new GameObject("Service Light "+z).transform;t.SetParent(root.Find("Lighting"),false);t.localPosition=new Vector3(4.8f,2.6f,z);var light=t.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.93f,.82f);light.intensity=2.5f;light.range=7;}
            var linen=root.Find("Linen Room");if(linen!=null)UnityEngine.Object.DestroyImmediate(linen.gameObject);
            MotelLinenRoomSceneSetup.Create(root);
            RefreshAccidentLights(root);
            Physics.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            File.WriteAllText("Logs/ServiceLinen/Upgrade.txt",string.Join("\n",report)+"\nSAVED: layout="+MotelBlockoutPlan.ResidentialZ+"; all existing non-service objects/references retained.");
        }
        public static void RefreshAccidentLights(Transform root)
        {
            var loop=root.GetComponentInChildren<MotelLoopController>(true);var data=new SerializedObject(loop);var lights=data.FindProperty("accidentLights");
            var actual=root.GetComponentsInChildren<Light>(true);lights.arraySize=actual.Length;
            for(int i=0;i<actual.Length;i++)lights.GetArrayElementAtIndex(i).objectReferenceValue=actual[i];data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
