using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static class MotelTechnicalRoomSceneSetup
    {
        public const string Folder = "Assets/_Project/Art/TechnicalRoom";
        public static readonly Vector3 Origin = new Vector3(3.6f,0,-10.8f);
        public static Vector3 World(float x,float y,float z) => Origin + Quaternion.Euler(0,-90,0)*new Vector3(x,y,z);
        public static Vector3 Approach => World(3.25f,.05f,5.1f);
        private static readonly Dictionary<string,Material> mats = new Dictionary<string,Material>();
        private static Mesh hull;

        [MenuItem("Motel/Update Technical Room")]
        public static void UpdateCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Stop Play Mode and save the current scene first.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!=MotelBlockoutSceneCreator.ScenePath)throw new InvalidOperationException("Open MotelBlockout.");
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Motel Blockout").transform;
            var old=root.Find("Technical Room");if(old!=null)Object.DestroyImmediate(old.gameObject);
            Create(root);MotelBypassChecks.ValidateScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }

        public static void Create(Transform root)
        {
            if(root.Find("Technical Room")!=null)throw new InvalidOperationException("Technical room already exists.");
            PrepareAssets();
            var loop=root.GetComponentInChildren<MotelLoopController>(true);var dialogue=root.GetComponentInChildren<DialogueController>(true);
            foreach(string path in new[]{"Furniture/Technical Equipment","Furniture/Equipment Panel","Module Bypass","Time Loop/Emergency Cutoff","Time Loop/Emergency Cutoff Label","Time Loop/Prepared Damage","Time Loop/Equipment Placeholder","Time Loop/Indicator Placeholder"})
            {var old=root.Find(path);if(old!=null)Object.DestroyImmediate(old.gameObject);}
            foreach(var inspection in root.GetComponentsInChildren<EquipmentInspection>(true))Object.DestroyImmediate(inspection.gameObject);
            Transform room=Node(root,"Technical Room");room.position=Origin;room.rotation=Quaternion.Euler(0,-90,0);
            Box(room,"Concrete Floor",new Vector3(3.12f,.006f,3.52f),new Vector3(6.07f,.012f,6.87f),"Concrete");
            Box(room,"Back Concrete",new Vector3(3.12f,1.4f,6.95f),new Vector3(6.08f,2.79f,.015f),"Concrete");
            Box(room,"Left Concrete",new Vector3(.09f,1.4f,3.52f),new Vector3(.015f,2.79f,6.87f),"Concrete");
            Box(room,"Right Concrete",new Vector3(6.15f,1.4f,3.52f),new Vector3(.015f,2.79f,6.87f),"Concrete");
            var oldSoffit=root.Find("Ceilings/Technical Concrete Soffit");if(oldSoffit!=null)Object.DestroyImmediate(oldSoffit.gameObject);
            var soffit=Node(root.Find("Ceilings"),"Technical Concrete Soffit");soffit.position=Origin;soffit.rotation=room.rotation;
            Box(soffit,"Worn Concrete Ceiling",new Vector3(3.12f,2.786f,3.52f),new Vector3(6.08f,.015f,6.88f),"Concrete");
            Transform gen=Node(room,"Olive Generator");gen.localPosition=new Vector3(1.15f,0,5.92f);
            var shell=new GameObject("Chamfered Housing");shell.transform.SetParent(gen,false);shell.AddComponent<MeshFilter>().sharedMesh=hull;shell.AddComponent<MeshRenderer>().sharedMaterial=mats["Olive"];
            var body=shell.AddComponent<MeshCollider>();body.sharedMesh=hull;body.convex=true;
            foreach(float x in new[]{-.52f,.52f})foreach(float z in new[]{-.43f,.43f})Box(gen,"Foot",new Vector3(x,.10f,z),new Vector3(.17f,.20f,.17f),"Dark",true);
            foreach(float x in new[]{-.32f,.32f})
            {
                Box(gen,"Recessed Vent",new Vector3(x,.59f,-.605f),new Vector3(.44f,.54f,.015f),"Dark");
                for(int i=0;i<6;i++)Box(gen,"Louver",new Vector3(x,.36f+i*.086f,-.62f),new Vector3(.42f,.028f,.03f),"Olive");
            }
            Box(gen,"Case Seam",new Vector3(0,.25f,-.61f),new Vector3(1.30f,.012f,.014f),"Dark");
            for(int i=0;i<6;i++)Box(gen,"Worn Front Edge",new Vector3(-.55f+i*.22f,1.005f,-.605f),new Vector3(.07f,.009f,.014f),"Wear");
            Transform fascia=Node(gen,"Sloped Control Panel");fascia.localPosition=new Vector3(0,1.27f,-.443f);fascia.localRotation=Quaternion.Euler(30,0,0);
            Box(fascia,"Panel Plate",Vector3.zero,new Vector3(1.26f,.56f,.028f),"OliveLight");
            Transform live=Box(gen,"Equipment Live Indicator",new Vector3(.5f,1.56f,.10f),new Vector3(.13f,.018f,.08f),"Green");

            Transform cabinet=Node(room,"Control Cabinet");cabinet.localPosition=new Vector3(3.25f,2.15f,6.74f);
            Box(cabinet,"Cabinet Back",Vector3.zero,new Vector3(1.34f,1.10f,.22f),"Olive",true);
            foreach(float x in new[]{-.65f,.65f})Box(cabinet,"Side Frame",new Vector3(x,0,-.065f),new Vector3(.05f,1.12f,.25f),"OliveLight");
            foreach(float y in new[]{-.54f,.54f})Box(cabinet,"Frame Lip",new Vector3(0,y,-.065f),new Vector3(1.34f,.045f,.25f),"OliveLight");
            Transform hinge=Node(cabinet,"Left Hinged Door");hinge.localPosition=new Vector3(-.68f,0,-.23f);
            // A small hinge-side cable relief keeps the left gland visible with the door folded back.
            Box(hinge,"Thick Door Upper",new Vector3(.67f,.4075f,0),new Vector3(1.34f,.285f,.04f),"Olive",true);
            Box(hinge,"Thick Door Lower",new Vector3(.67f,-.2825f,0),new Vector3(1.34f,.535f,.04f),"Olive",true);
            Box(hinge,"Thick Door Middle",new Vector3(.785f,.125f,0),new Vector3(1.11f,.28f,.04f),"Olive",true);
            Box(hinge,"Door Inset Upper",new Vector3(.67f,.375f,-.025f),new Vector3(1.21f,.22f,.014f),"OliveLight");
            Box(hinge,"Door Inset Lower",new Vector3(.67f,-.25f,-.025f),new Vector3(1.21f,.47f,.014f),"OliveLight");
            Box(hinge,"Door Inset Middle",new Vector3(.785f,.125f,-.025f),new Vector3(1.11f,.28f,.014f),"OliveLight");
            Box(hinge,"Door Handle",new Vector3(1.19f,-.05f,-.065f),new Vector3(.035f,.19f,.07f),"Wear");
            Label(hinge,"Cabinet Name","ШКАФ УПРАВЛЕНИЯ",new Vector3(.67f,.26f,-.038f),1.0f,.09f,Color.white);
            var hatch=hinge.gameObject.AddComponent<TechnicalHatch>();hatch.Configure(loop,hinge,Vector3.up,170,"шкаф управления");Kinematic(hinge);
            foreach(float y in new[]{-.39f,.39f})Pipe(cabinet,"Hinge Barrel",new Vector3(-.69f,y-.07f,-.23f),new Vector3(-.69f,y+.07f,-.23f),.027f,"Wear");
            Transform note=Node(hinge,"Shift Worker Note");note.localPosition=new Vector3(.67f,0,.028f);note.localRotation=Quaternion.Euler(0,180,0);
            Box(note,"Old Paper",Vector3.zero,new Vector3(.85f,.72f,.012f),"Paper",true);
            Label(note,"Restart Note","После работы\nперезапустить\nгенератор",new Vector3(0,.06f,-.009f),.77f,.33f,Color.black);
            Label(note,"Bypass Hint","Средний модуль сгорел.\nПуск можно пустить в обход",new Vector3(0,-.24f,-.009f),.77f,.11f,Color.black);
            note.gameObject.AddComponent<ModuleBypassNote>().Configure(dialogue,"После работы перезапустить генератор.\n\n"+ModuleBypassNote.Text);
            foreach(float x in new[]{-.35f,.35f})Box(note,"Tape",new Vector3(x,.35f,-.014f),new Vector3(.14f,.055f,.009f),"Paper");
            Transform board=Node(cabinet,"Module Bypass Board");board.localPosition=new Vector3(0,0,-.14f);
            board.gameObject.AddComponent<BoxCollider>().size=new Vector3(1.13f,1.03f,.028f);
            var panel=board.gameObject.AddComponent<ModuleBypassPanel>();panel.Configure(loop);
            var cells=new Transform[16];var initial=new BypassCircuit();
            for(int i=0;i<16;i++)
            {
                Vector3 at=new Vector3(-.375f+(i%4)*.25f,.375f-(i/4)*.25f,-.026f);
                Box(board,"Section "+i,at,new Vector3(.23f,.23f,.03f),i==5?"Burned":"Tile");
                Transform wire=Node(board,"Wire "+i);wire.localPosition=at+new Vector3(0,0,-.022f);cells[i]=wire;
                for(int side=0;side<4;side++)
                {
                    Vector3 v=side==0?Vector3.up:side==1?Vector3.right:side==2?Vector3.down:Vector3.left;
                    Box(wire,"NESW"[side].ToString(),v*.055f,side%2==0?new Vector3(.025f,.112f,.008f):new Vector3(.112f,.025f,.008f),initial.IsPowered(i)?"Green":"Wire").gameObject.SetActive((initial.Ports(i)&(1<<side))!=0);
                }
                Box(wire,"Junction",Vector3.zero,new Vector3(.035f,.035f,.011f),initial.IsPowered(i)?"Green":"Wire").gameObject.SetActive(initial.Ports(i)!=0);
                if(i==5)Label(board,"Burned Label","СГОРЕЛ",at+new Vector3(0,0,-.025f),.20f,.065f,Color.white);
            }
            Transform start=Node(room,"Start Block - not interactive");start.localPosition=new Vector3(4.30f,2.15f,6.70f);
            Box(start,"Block Housing",Vector3.zero,new Vector3(.37f,.45f,.25f),"OliveLight");
            Label(start,"Block Name","БЛОК\nПУСКА",new Vector3(0,.08f,-.137f),.31f,.13f,Color.white);
            Transform lamp=Sphere(start,"Start Indicator",new Vector3(.06f,-.13f,-.14f),new Vector3(.07f,.07f,.03f),"Green");lamp.gameObject.SetActive(false);
            panel.ConfigureWorld(hatch,cells,mats["Green"],mats["Wire"],lamp.gameObject);
            MakeButton(fascia,"Green Start",new Vector3(.035f,-.035f,-.04f),"Green","Пуск",panel,GeneratorButton.Command.Start);
            MakeButton(fascia,"Yellow Restart",new Vector3(.39f,-.035f,-.04f),"Yellow","Перезапуск",panel,GeneratorButton.Command.Restart);
            Transform red=Node(fascia,"Emergency Cutoff");red.localPosition=new Vector3(-.39f,-.025f,-.04f);
            ButtonCap(red,"Red Cap",.11f,"Red");Label(red,"Cutoff Name","АВАРИЙНОЕ\nОТКЛЮЧЕНИЕ",new Vector3(0,.19f,-.01f),.34f,.10f,Color.white);
            Transform shield=Node(red,"Clear Cover Hinge");shield.localPosition=new Vector3(0,.14f,-.10f);
            Box(shield,"Transparent Cover",new Vector3(0,-.14f,0),new Vector3(.27f,.28f,.018f),"Glass",true);
            foreach(float x in new[]{-.135f,.135f})Box(shield,"Cover Rim",new Vector3(x,-.14f,0),new Vector3(.012f,.29f,.025f),"Wear");
            Pipe(shield,"Shield Hinge",new Vector3(-.14f,0,0),new Vector3(.14f,0,0),.016f,"Wear");
            var cover=shield.gameObject.AddComponent<TechnicalHatch>();cover.Configure(loop,shield,Vector3.right,105,"защитную крышку");Kinematic(shield);
            var cutoff=red.gameObject.AddComponent<EmergencyCutoff>();cutoff.Configure(loop);cutoff.ConfigureCover(cover);
            Transform diag=Node(room,"Independent Diagnostics");diag.localPosition=new Vector3(5.22f,1.98f,6.65f);
            Box(diag,"Diagnostic Housing",Vector3.zero,new Vector3(.68f,.82f,.30f),"OliveLight",true);
            Label(diag,"Diagnostic Name","ДИАГНОСТИКА",new Vector3(0,.29f,-.16f),.59f,.09f,Color.white);
            Box(diag,"Diagnostic Screen",new Vector3(0,0,-.16f),new Vector3(.55f,.34f,.012f),"Dark");
            Label(diag,"Overheat Readout","КРИТИЧЕСКИЙ\nПЕРЕГРЕВ",new Vector3(0,0,-.17f),.49f,.17f,new Color(1,.29f,.05f));
            for(int i=0;i<3;i++)Box(diag,"Diagnostic Vent",new Vector3(0,-.30f+i*.028f,-.16f),new Vector3(.31f,.009f,.008f),"Dark");
            diag.gameObject.AddComponent<EquipmentInspection>().Configure(loop,dialogue);
            // Second-row height = 2.15 + .125; both cables terminate on side faces, never the bottom.
            Pipe(room,"Input Cable",new Vector3(1.83f,1.44f,6.4f),new Vector3(2.28f,1.44f,6.74f),.035f,"Dark");
            Pipe(room,"Input Cable Riser",new Vector3(2.28f,1.44f,6.74f),new Vector3(2.28f,2.275f,6.74f),.035f,"Dark");
            Pipe(room,"Left Side Input",new Vector3(2.28f,2.275f,6.74f),new Vector3(2.58f,2.275f,6.74f),.044f,"Wear");
            Pipe(room,"Right Side Output",new Vector3(3.92f,2.275f,6.74f),new Vector3(4.30f,2.275f,6.70f),.035f,"Dark");
            Pipe(room,"Diagnostic Cable - separate",new Vector3(5.22f,2.39f,6.70f),new Vector3(5.22f,2.76f,6.70f),.028f,"Dark");
            for(int i=0;i<3;i++)Pipe(room,"Floor Bundle "+i,new Vector3(1.15f,.11f,6.53f+i*.075f),new Vector3(6.16f,.11f,6.53f+i*.075f),.03f,"Dark");
            foreach(float x in new[]{2.0f,3.5f,5.0f})Box(room,"Bundle Strap",new Vector3(x,.11f,6.605f),new Vector3(.04f,.18f,.28f),"Wear");
            Furniture(room);
            foreach(float x in new[]{-.62f,.62f})foreach(float y in new[]{-.51f,.51f})Sphere(cabinet,"Frame Screw",new Vector3(x,y,-.201f),Vector3.one*.025f,"Wear");
            Transform fixture=Node(room,"Fluorescent Fixture");fixture.localPosition=new Vector3(3.12f,2.72f,3.6f);
            Box(fixture,"Steel Light Housing",Vector3.zero,new Vector3(2.05f,.07f,.25f),"Dark");
            foreach(float z in new[]{-.06f,.06f})Pipe(fixture,"Tube",new Vector3(-.94f,-.045f,z),new Vector3(.94f,-.045f,z),.03f,"Light");
            var light=Node(room,"Technical Ceiling Light").gameObject.AddComponent<Light>();light.transform.localPosition=new Vector3(3.12f,2.50f,3.6f);light.type=LightType.Point;light.color=new Color(1,.91f,.75f);light.intensity=2.1f;light.range=9;light.shadows=LightShadows.Soft;light.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Low;
            if(light.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>()==null)
                light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            Transform damage=Node(root.Find("Time Loop"),"Prepared Damage");damage.position=gen.position;damage.rotation=room.rotation;
            var broken=new GameObject("Damaged Generator Shell");broken.transform.SetParent(damage,false);broken.transform.localRotation=Quaternion.Euler(0,0,-10);broken.AddComponent<MeshFilter>().sharedMesh=hull;broken.AddComponent<MeshRenderer>().sharedMaterial=mats["Burned"];
            for(int i=0;i<6;i++)Box(damage,"Prepared Debris "+i,new Vector3(-.4f+i*.2f,.08f,-.74f-(i%2)*.25f),new Vector3(.18f,.12f,.23f),"Dark",true);
            damage.gameObject.SetActive(false);
            var data=new SerializedObject(loop);data.FindProperty("equipmentIndicator").objectReferenceValue=live.gameObject;data.FindProperty("intactEquipment").objectReferenceValue=gen.gameObject;data.FindProperty("damagedEquipment").objectReferenceValue=damage.gameObject;
            var lights=data.FindProperty("accidentLights");var kept=new List<Light>();
            for(int i=0;i<lights.arraySize;i++){var l=lights.GetArrayElementAtIndex(i).objectReferenceValue as Light;if(l!=null && !kept.Contains(l))kept.Add(l);}
            kept.Add(light);lights.arraySize=kept.Count;for(int i=0;i<kept.Count;i++)lights.GetArrayElementAtIndex(i).objectReferenceValue=kept[i];data.ApplyModifiedPropertiesWithoutUndo();
            MotelTechnicalDetails.Apply(room);
        }

        private static void Furniture(Transform room)
        {
            Transform shelf=Node(room,"Shallow Entry Shelf");shelf.localPosition=new Vector3(.39f,0,1.15f);
            foreach(float x in new[]{-.20f,.20f})foreach(float z in new[]{-.66f,.66f})Box(shelf,"Upright",new Vector3(x,.83f,z),new Vector3(.04f,1.66f,.04f),"Wear");
            for(int i=0;i<3;i++){Box(shelf,"Shelf",new Vector3(0,.18f+i*.59f,0),new Vector3(.45f,.04f,1.4f),"Wood",i==0);Box(shelf,"Cardboard Box",new Vector3(0,.38f+i*.59f,.21f),new Vector3(.36f,.35f,.55f),"Cardboard",true);Box(shelf,"Packing Tape",new Vector3(.185f,.38f+i*.59f,.21f),new Vector3(.008f,.34f,.07f),"Paper");}
            Transform bench=Node(room,"Right Workbench");bench.localPosition=new Vector3(5.78f,0,2.55f);
            Box(bench,"Old Wood Top",new Vector3(0,.94f,0),new Vector3(.65f,.07f,2.55f),"Wood",true);
            foreach(float x in new[]{-.24f,.24f})foreach(float z in new[]{-1.1f,1.1f})Box(bench,"Steel Leg",new Vector3(x,.46f,z),new Vector3(.07f,.92f,.07f),"Olive",true);
            Box(bench,"Tool Box",new Vector3(0,1.12f,.83f),new Vector3(.47f,.30f,.59f),"Olive",true);Pipe(bench,"Tool Box Handle",new Vector3(0,1.30f,.68f),new Vector3(0,1.30f,.96f),.02f,"Dark");
            Box(bench,"Vice Base",new Vector3(-.22f,1.04f,-.85f),new Vector3(.34f,.13f,.25f),"Rust");foreach(float z in new[]{-.94f,-.77f})Box(bench,"Vice Jaw",new Vector3(-.22f,1.16f,z),new Vector3(.30f,.13f,.05f),"Wear");
            Pipe(bench,"Screwdriver Shaft",new Vector3(-.05f,1.0f,-.3f),new Vector3(-.05f,1.0f,.03f),.01f,"Wear");Pipe(bench,"Screwdriver Handle",new Vector3(-.05f,1.0f,.03f),new Vector3(-.05f,1.0f,.19f),.028f,"Rust");
            Pipe(bench,"Spanner",new Vector3(.13f,1.0f,-.18f),new Vector3(.13f,1.0f,.16f),.018f,"Wear");Box(bench,"Spanner Head",new Vector3(.13f,1,-.2f),new Vector3(.07f,.025f,.08f),"Wear");
        }

        private static void MakeButton(Transform p,string name,Vector3 at,string mat,string text,ModuleBypassPanel panel,GeneratorButton.Command action)
        {var t=Node(p,name);t.localPosition=at;ButtonCap(t,"Button Cap",.105f,mat);t.gameObject.AddComponent<GeneratorButton>().Configure(panel,action);Label(t,"Command Label",text,new Vector3(0,.13f,-.025f),action==GeneratorButton.Command.Start?.19f:.34f,.065f,Color.white);}
        private static void ButtonCap(Transform p,string name,float diameter,string mat)
        {Sphere(p,name+" Bezel",Vector3.zero,new Vector3(diameter+.04f,diameter+.04f,.05f),"Dark");Sphere(p,name,new Vector3(0,0,-.03f),new Vector3(diameter,diameter,.05f),mat).gameObject.AddComponent<BoxCollider>();}
        private static void Kinematic(Transform t){var r=t.gameObject.AddComponent<Rigidbody>();r.isKinematic=true;r.useGravity=false;}
        private static Transform Node(Transform p,string name){var t=new GameObject(name).transform;t.SetParent(p,false);return t;}
        private static Transform Box(Transform p,string name,Vector3 at,Vector3 size,string mat,bool solid=false)
        {var t=GameObject.CreatePrimitive(PrimitiveType.Cube);t.name=name;t.transform.SetParent(p,false);t.transform.localPosition=at;t.transform.localScale=size;t.GetComponent<Renderer>().sharedMaterial=mats[mat];if(!solid)Object.DestroyImmediate(t.GetComponent<Collider>());return t.transform;}
        private static Transform Sphere(Transform p,string name,Vector3 at,Vector3 size,string mat)
        {var t=GameObject.CreatePrimitive(PrimitiveType.Sphere);t.name=name;t.transform.SetParent(p,false);t.transform.localPosition=at;t.transform.localScale=size;t.GetComponent<Renderer>().sharedMaterial=mats[mat];Object.DestroyImmediate(t.GetComponent<Collider>());return t.transform;}
        private static void Pipe(Transform p,string name,Vector3 a,Vector3 b,float radius,string mat)
        {var t=GameObject.CreatePrimitive(PrimitiveType.Cylinder);t.name=name;t.transform.SetParent(p,false);t.transform.localPosition=(a+b)/2;t.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);t.transform.localScale=new Vector3(radius*2,(b-a).magnitude/2,radius*2);t.GetComponent<Renderer>().sharedMaterial=mats[mat];Object.DestroyImmediate(t.GetComponent<Collider>());}
        private static void Label(Transform p,string name,string text,Vector3 at,float width,float height,Color color)
        {var t=Node(p,name);t.localPosition=at;MotelLoopSceneSetup.Text(t,"Text",text,width,height,color);}

        private static void PrepareAssets()
        {
            foreach(string d in new[]{"Materials","Textures","Meshes"})Directory.CreateDirectory(Folder+"/"+d);mats.Clear();
            Mat("Olive",new Color(.28f,.30f,.18f),true);Mat("OliveLight",new Color(.36f,.37f,.23f),true);Mat("Wood",new Color(.33f,.22f,.12f),true);Mat("Concrete",new Color(.51f,.48f,.40f),true);
            Mat("Dark",new Color(.045f,.043f,.035f));Mat("Wear",new Color(.43f,.39f,.28f));Mat("Paper",new Color(.80f,.72f,.51f));Mat("Tile",new Color(.10f,.105f,.095f));Mat("Burned",new Color(.25f,.075f,.037f));Mat("Wire",new Color(.60f,.61f,.51f));
            Mat("Green",new Color(.15f,.72f,.15f),false,true);Mat("Yellow",new Color(.82f,.63f,.03f));Mat("Red",new Color(.68f,.08f,.04f));Mat("Rust",new Color(.40f,.13f,.065f));Mat("Cardboard",new Color(.43f,.32f,.18f));Mat("Light",new Color(.9f,.87f,.68f),false,true);Mat("Glass",new Color(.55f,.76f,.75f,.20f));
            var glass=mats["Glass"];glass.SetFloat("_Surface",1);glass.SetFloat("_ZWrite",0);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_Cull",0);glass.SetOverrideTag("RenderType","Transparent");glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
            string path=Folder+"/Meshes/GeneratorHull.asset";hull=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(hull==null)
            {
                hull=new Mesh{name="Generator chamfered housing"};var v=new List<Vector3>();var tris=new List<int>();Vector2[] yz={new Vector2(.18f,-.60f),new Vector2(1.0f,-.60f),new Vector2(1.55f,-.2825f),new Vector2(1.55f,.55f),new Vector2(.18f,.55f)};
                Action<Vector3,Vector3,Vector3,Vector3> quad=(a,b,c,d)=>{int n=v.Count;v.AddRange(new[]{a,b,c,d});tris.AddRange(new[]{n,n+2,n+1,n,n+3,n+2});};
                for(int i=0;i<5;i++){int j=(i+1)%5;quad(new Vector3(-.68f,yz[i].x,yz[i].y),new Vector3(.68f,yz[i].x,yz[i].y),new Vector3(.68f,yz[j].x,yz[j].y),new Vector3(-.68f,yz[j].x,yz[j].y));}
                foreach(float x in new[]{-.68f,.68f})for(int i=1;i<4;i++){int n=v.Count;v.AddRange(new[]{new Vector3(x,yz[0].x,yz[0].y),new Vector3(x,yz[i].x,yz[i].y),new Vector3(x,yz[i+1].x,yz[i+1].y)});tris.AddRange(x<0?new[]{n,n+2,n+1}:new[]{n,n+1,n+2});}
                hull.SetVertices(v);hull.SetTriangles(tris,0);hull.SetUVs(0,v.Select(a=>new Vector2(a.x+a.z,a.y)).ToList());hull.RecalculateNormals();hull.RecalculateBounds();AssetDatabase.CreateAsset(hull,path);
            }
            AssetDatabase.SaveAssets();
        }
        private static void Mat(string name,Color color,bool texture=false,bool emission=false)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",texture?Color.white:color);m.SetFloat("_Smoothness",name=="Glass"?.45f:.12f);m.SetFloat("_Metallic",0);
            if(texture)
            {
                string tp=Folder+"/Textures/"+name+".png";if(!File.Exists(tp)){var t=new Texture2D(128,128,TextureFormat.RGBA32,false);var pixels=new Color[16384];var rng=new System.Random(134+name.Length);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){float grain=name=="Wood"?Mathf.Sin(y*.34f+Mathf.Sin(x*.12f)*.8f)*.055f:Mathf.PerlinNoise(x*.09f,y*.09f)*.10f-.05f;pixels[y*128+x]=color*(1+grain+(float)rng.NextDouble()*.07f-.035f);}t.SetPixels(pixels);t.Apply();File.WriteAllBytes(tp,t.EncodeToPNG());Object.DestroyImmediate(t);}
                AssetDatabase.ImportAsset(tp,ImportAssetOptions.ForceSynchronousImport);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            }
            if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*1.5f);}mats[name]=m;EditorUtility.SetDirty(m);
        }
    }
}
