using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Motel.ArtTests
{
    // Standalone art study. Never calls or modifies the main blockout generator.
    public static class ReceptionDeskStudy
    {
        public const string Folder = "Assets/_Project/ArtTests/ReceptionDesk";
        public const string ScenePath = "Assets/Scenes/MotelBlockout_ReceptionTest.unity";
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [MenuItem("Motel/Art Tests/Create Reception Desk Test")]
        public static void CreateMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Тестовая стойка", "Пересоздать только тестовую сцену и ассеты стойки?", "Пересоздать", "Отмена")) return;
            Create();
        }

        public static void Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Сначала остановите Play Mode и сохраните текущую сцену.");
            foreach (string name in new[] { "Materials", "Meshes", "Textures", "Prefabs" }) Directory.CreateDirectory(Folder + "/" + name);
            AssetDatabase.Refresh();
            materials.Clear();
            Mat("Laminate", Color.white, .23f, Grain());
            Mat("LaminateVertical", Color.white, .23f, VerticalGrain());
            Mat("Edge", new Color(.46f,.30f,.15f), .12f);
            Mat("Chipboard", new Color(.52f,.38f,.22f), .05f);
            Mat("Dark", new Color(.055f,.05f,.045f), .15f);
            Mat("Fabric", Color.white, .08f, Fabric());
            Mat("Metal", new Color(.30f,.29f,.26f), .45f, null, .65f);
            Mat("Bell", new Color(.54f,.53f,.47f), .65f, null, .8f);
            Mat("Beige", new Color(.67f,.56f,.36f), .21f);
            Mat("Paper", new Color(.88f,.81f,.65f), .04f);
            Mat("LedgerPage", Color.white, .04f, RuledPaper());
            Mat("Ink", new Color(.52f,.49f,.39f), .03f);
            Mat("Pen", new Color(.035f,.085f,.20f), .3f);
            Mat("Pot", new Color(.32f,.145f,.065f), .05f);
            Mat("Leaf", new Color(.24f,.32f,.095f), .1f);
            Mat("LeafLight", new Color(.34f,.40f,.13f), .08f);
            Mat("Stain", new Color(.40f,.26f,.13f), .04f);

            var desk = new GameObject("Reception Desk - worn laminate");
            try
            {
                Cabinet(desk.transform);
                Book(desk.transform);
                Fan(desk.transform);
                Plant(desk.transform);
                Bell(desk.transform);
                Paper(desk.transform);
                PrefabUtility.SaveAsPrefabAsset(desk, Folder + "/Prefabs/ReceptionDesk.prefab");
            }
            finally { Object.DestroyImmediate(desk); }
            var chair = new GameObject("Office Chair - separate");
            try { Chair(chair.transform); PrefabUtility.SaveAsPrefabAsset(chair, Folder + "/Prefabs/OfficeChair.prefab"); }
            finally { Object.DestroyImmediate(chair); }
            AssetDatabase.SaveAssets();

            // Copy to disk before opening. No save of the source scene, no generator invocation.
            if (!File.Exists(ScenePath))
            {
                if (!AssetDatabase.CopyAsset("Assets/Scenes/MotelBlockout.unity", ScenePath)) throw new Exception("Scene copy failed.");
            }
            // Existing test-room edits survive: replace only this study's desk and chair.
            if (SceneManager.GetActiveScene().path != ScenePath) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform root = GameObject.Find("Motel Blockout").transform;
            foreach (string name in new[] { "ReceptionDesk", "OfficeChair" })
            {
                var oldPrefab = root.Find("Furniture/" + name);
                if (oldPrefab != null) Object.DestroyImmediate(oldPrefab.gameObject);
            }
            foreach (string name in new[] { "Reception Counter", "Reception Countertop", "Reception Front Panel", "Reception Monitor" })
            {
                Transform old = root.Find("Furniture/" + name);
                if (old != null) Object.DestroyImmediate(old.gameObject); // Test copy only, including old colliders.
            }
            var placed = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/ReceptionDesk.prefab"));
            placed.transform.SetParent(root.Find("Furniture"), false);
            placed.transform.SetPositionAndRotation(new Vector3(3,0,-13.6f), Quaternion.Euler(0,90,0));
            var seat = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Prefabs/OfficeChair.prefab"));
            seat.transform.SetParent(root.Find("Furniture"), false);
            seat.transform.SetPositionAndRotation(new Vector3(2.10f,0,-13.20f), Quaternion.Euler(0,65,0));
            GuestRegister register = Object.FindAnyObjectByType<GuestRegister>();
            foreach (Transform child in register.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            Transform book = placed.transform.Find("Open Guest Register");
            register.transform.SetPositionAndRotation(book.position, book.rotation);
            var hit = register.GetComponent<BoxCollider>() ?? register.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0,.027f,0); hit.size = new Vector3(.76f,.055f,.46f);
            // Working clock moves to the west wall in the test scene, outside the desk composition.
            var clock = root.Find("Time Loop/Reception Clock");
            clock.SetPositionAndRotation(new Vector3(.19f,2.30f,-12.25f), Quaternion.Euler(0,270,0));
            clock.Find("Clock Stand").gameObject.SetActive(false);
            Physics.SyncTransforms();
            Validate();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("Reception test created. Main MotelBlockout untouched.");
        }

        private static Material Mat(string name, Color color, float smooth, Texture tex = null, float metal = 0)
        {
            string path = Folder + "/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",smooth); m.SetFloat("_Metallic",metal);
            m.SetTexture("_BaseMap",tex); EditorUtility.SetDirty(m); materials[name]=m; return m;
        }
        private static Texture2D Grain()
        {
            string path = Folder + "/Textures/LaminateGrain.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool fresh = tex == null;
            if (fresh) tex = new Texture2D(512,512,TextureFormat.RGB24,true);
            else tex.Reinitialize(512,512,TextureFormat.RGB24,true);
            tex.name="Warm walnut laminate with grain and patchy wear"; tex.wrapMode=TextureWrapMode.Repeat;
            for(int y=0;y<512;y++) for(int x=0;x<512;x++)
            {
                float bend = Mathf.PerlinNoise(x*.015f,y*.007f)*9;
                float streak = Mathf.PerlinNoise(x*.007f,(y+bend)*.17f);
                float fine = Mathf.PerlinNoise(x*.05f,y*.8f);
                float patch = Mathf.PerlinNoise(x*.045f,y*.05f);
                float grain = .45f+streak*.22f+fine*.06f+patch*.10f;
                Color c=Color.Lerp(new Color(.19f,.075f,.024f),new Color(.52f,.29f,.13f),grain);
                c *= .86f + Mathf.PerlinNoise(x*.022f,y*.024f)*.28f;
                if(fine<.32f)c*=.87f;
                tex.SetPixel(x,y,c);
            }
            tex.Apply(); if(fresh) AssetDatabase.CreateAsset(tex,path);else EditorUtility.SetDirty(tex); return tex;
        }
        private static Texture2D VerticalGrain()
        {
            string path=Folder+"/Textures/VerticalLaminate.asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);bool fresh=tex==null;
            if(fresh)tex=new Texture2D(512,512,TextureFormat.RGB24,true){name="Vertical walnut laminate"};
            var grain=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/LaminateGrain.asset");
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)tex.SetPixel(x,y,grain.GetPixel(y,x));
            tex.Apply();if(fresh)AssetDatabase.CreateAsset(tex,path);else EditorUtility.SetDirty(tex);return tex;
        }
        private static Texture2D Fabric()
        {
            string path=Folder+"/Textures/WornFabric.asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(tex!=null)return tex;
            tex=new Texture2D(128,128,TextureFormat.RGB24,true){name="Charcoal fabric",wrapMode=TextureWrapMode.Repeat};
            for(int y=0;y<128;y++)for(int x=0;x<128;x++)
            {float a=.12f+Mathf.PerlinNoise(x*.07f,y*.07f)*.11f+Mathf.PerlinNoise(x*.8f,y*.8f)*.06f;tex.SetPixel(x,y,new Color(a,a*.96f,a*.89f));}
            tex.Apply();AssetDatabase.CreateAsset(tex,path);return tex;
        }
        private static Texture2D RuledPaper()
        {
            string path=Folder+"/Textures/LedgerPage.asset";
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(tex!=null)return tex;
            tex=new Texture2D(512,512,TextureFormat.RGB24,true){name="Cream ruled guest register",wrapMode=TextureWrapMode.Clamp,anisoLevel=4};
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                float grain=Mathf.PerlinNoise(x*.31f,y*.31f)*.025f;
                var color=new Color(.88f+grain,.81f+grain,.66f+grain);
                bool ruled=x>70 && x<490 && y>25 && y<480 && ((y-25)%32<2 || x==125 || x==126 || x==345 || x==346);
                if(ruled)color=new Color(.57f,.55f,.47f);
                // Faint handwritten strokes: decoration, not an alternative source of guest data.
                if(x>155 && x<285 && y>290 && y<450 && (y-30)%32<9 && ((x+(y%32)*3)%24<3))color=new Color(.40f,.39f,.34f);
                tex.SetPixel(x,y,color);
            }
            tex.Apply();AssetDatabase.CreateAsset(tex,path);return tex;
        }
        private static void PageUV(Mesh mesh)
        {
            mesh.uv=mesh.vertices.Select(v=>new Vector2(Mathf.Abs(v.x)/.38f,v.z/.46f+.5f)).ToArray();
            EditorUtility.SetDirty(mesh);
        }
        // Update saved assets in place without recreating the prefab or the scene.
        [MenuItem("Motel/Art Tests/Refine Saved Journal and Clock")]
        public static void RefineSavedDetails()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty || SceneManager.GetActiveScene().path!=ScenePath)
                throw new Exception("Open the saved reception test in edit mode.");
            var material=Mat("LedgerPage",Color.white,.04f,RuledPaper());
            string path=Folder+"/Prefabs/ReceptionDesk.prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var book=prefab.transform.Find("Open Guest Register");
                foreach(Transform child in book.Cast<Transform>().ToArray())
                {
                    if(child.name.StartsWith("Ledger ruling ") || child.name.StartsWith("Column ") || child.name.StartsWith("Handwritten mark "))
                        Object.DestroyImmediate(child.gameObject);
                    else if(child.name.StartsWith("Curved cream page "))
                    {child.GetComponent<Renderer>().sharedMaterial=material;PageUV(child.GetComponent<MeshFilter>().sharedMesh);}
                    else if(child.name.StartsWith("Paper block "))
                    {child.localPosition=new Vector3(child.localPosition.x,.015f,0);child.localScale=new Vector3(.38f,.018f,.46f);}
                }
                PrefabUtility.SaveAsPrefabAsset(prefab,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var clock=GameObject.Find("Reception Clock").transform;
            clock.position=new Vector3(.19f,2.30f,-12.25f);
            clock.Find("Clock Stand").gameObject.SetActive(false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        }
        private static Transform Group(Transform parent,string name, Vector3 position)
        { var t=new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t; }
        private static Transform Box(Transform parent,string name, Vector3 pos,Vector3 size,string mat,bool solid=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false);
            g.transform.localPosition=pos; g.transform.localScale=size;
            if(mat=="Laminate" && (name.Contains("carcass") || name.Contains("Drawer ") || name.Contains("Cupboard door") || name.Contains("solid front")))mat="LaminateVertical";
            g.GetComponent<Renderer>().sharedMaterial=materials[mat];
            if(!solid) Object.DestroyImmediate(g.GetComponent<Collider>()); return g.transform;
        }
        private static void Beam(Transform p,string n,Vector3 a,Vector3 b,float width,string mat)
        { var t=Box(p,n,(a+b)*.5f,new Vector3(width,(b-a).magnitude,width),mat); t.localRotation=Quaternion.FromToRotation(Vector3.up,b-a); }
        private static Transform MeshPart(Transform p,string name,List<Vector3> verts,List<int> triangles,string mat)
        {
            string path=Folder+"/Meshes/"+name+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);} else mesh.Clear();
            // Split triangle vertices for intentionally flat low-poly normals; explicit UVs on custom wood.
            var flat=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            foreach(int index in triangles){Vector3 a=verts[index];indices.Add(flat.Count);flat.Add(a);uv.Add(new Vector2(a.x*.65f,a.z*.65f+a.y*.65f));}
            mesh.SetVertices(flat);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var g=Group(p,name,Vector3.zero);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials[mat];return g;
        }
        private static Transform Lathe(Transform p,string name,Vector3 pos,float[] radii,float[] heights,string mat,int sides=12)
        {
            var v=new List<Vector3>();var tri=new List<int>();
            for(int j=0;j<radii.Length;j++)for(int i=0;i<sides;i++)
            {float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*radii[j],heights[j],Mathf.Sin(a)*radii[j]));}
            for(int j=0;j<radii.Length-1;j++)for(int i=0;i<sides;i++)
            {int a=j*sides+i,b=j*sides+(i+1)%sides,c=b+sides,d=a+sides;tri.AddRange(new[]{a,c,b,a,d,c});}
            var t=MeshPart(p,name,v,tri,mat);t.localPosition=pos;return t;
        }
        private static void Cabinet(Transform p)
        {
            // Width 2.9m, depth 1m, top 1.05m. Open central knee recess.
            Box(p,"Guest solid front",new Vector3(0,.49f,.39f),new Vector3(2.72f,.98f,.08f),"Laminate",true);
            Box(p,"Left drawer carcass",new Vector3(-1.04f,.48f,0),new Vector3(.64f,.96f,.82f),"Laminate",true);
            Box(p,"Right cupboard carcass",new Vector3(1.04f,.48f,0),new Vector3(.64f,.96f,.82f),"Laminate",true);
            for(int i=0;i<2;i++)
            {
                float y=.28f+i*.43f;
                Box(p,"Drawer "+(i+1),new Vector3(-1.04f,y,-.429f),new Vector3(.60f,.405f,.035f),"Laminate");
                Box(p,"Drawer handle wear "+i,new Vector3(-1.04f,y+.07f,-.448f),new Vector3(.25f,.055f,.002f),"Edge");
                Box(p,"Drawer pull "+(i+1),new Vector3(-1.04f,y+.07f,-.47f),new Vector3(.18f,.032f,.035f),"Metal");
            }
            Box(p,"Cupboard door",new Vector3(1.04f,.5f,-.429f),new Vector3(.60f,.86f,.035f),"Laminate");
            Box(p,"Cupboard handle wear",new Vector3(.83f,.61f,-.448f),new Vector3(.055f,.22f,.002f),"Edge");
            Box(p,"Cupboard pull",new Vector3(.83f,.61f,-.47f),new Vector3(.028f,.18f,.035f),"Metal");
            // Chamfered outline is the actual missing corner, not a painted rectangle.
            var outline=new[]{new Vector2(-1.45f,-.5f),new Vector2(1.36f,-.5f),new Vector2(1.45f,-.41f),new Vector2(1.45f,.5f),new Vector2(-1.45f,.5f)};
            var v=new List<Vector3>();var tri=new List<int>();
            foreach(float y in new[]{.98f,1.05f})foreach(var a in outline)v.Add(new Vector3(a.x,y,a.y));
            for(int i=1;i<4;i++){tri.AddRange(new[]{5,5+i+1,5+i,0,i,i+1});}
            for(int i=0;i<5;i++){int n=(i+1)%5;tri.AddRange(new[]{i,5+i,5+n,i,5+n,n});}
            var top=MeshPart(p,"Chipped countertop",v,tri,"Laminate");var collider=top.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,1.015f,0);collider.size=new Vector3(2.9f,.07f,1);
            Beam(p,"Exposed chipped corner",new Vector3(1.36f,1.032f,-.498f),new Vector3(1.448f,1.032f,-.41f),.018f,"Chipboard");
            Box(p,"Guest lip",new Vector3(0,1.0825f,.395f),new Vector3(2.83f,.065f,.18f),"Laminate");
            for(int i=0;i<21;i++)
            {float x=-1.39f+i*.133f;Box(p,"Worn edge "+i,new Vector3(x,1.045f-(i%3)*.002f,-.5006f),new Vector3(.027f+(i%4)*.016f,.003f+(i%3)*.0015f,.001f),"Edge");}
            var wearVerts=new List<Vector3>();var wearTris=new List<int>();var random=new System.Random(143);
            for(int i=0;i<65;i++)
            {
                float x=-1.41f+(float)random.NextDouble()*2.72f,w=.008f+(float)random.NextDouble()*.045f,z=-.499f+(float)random.NextDouble()*.016f;
                int n=wearVerts.Count;wearVerts.AddRange(new[]{new Vector3(x,1.0506f,z),new Vector3(x+w,1.0506f,z+.003f),new Vector3(x+w*.65f,1.0506f,z+.006f)});wearTris.AddRange(new[]{n,n+2,n+1});
            }
            MeshPart(p,"Irregular top edge wear",wearVerts,wearTris,"Edge");
            Lathe(p,"Mug ring",new Vector3(-.65f,1.051f,-.29f),new[]{.076f,.068f},new[]{0f,0f},"Stain",24);
        }
        private static void Book(Transform p)
        {
            var b=Group(p,"Open Guest Register",new Vector3(0,1.055f,-.20f));
            Box(b,"Cloth cover",Vector3.zero,new Vector3(.80f,.015f,.49f),"Laminate");
            for(int side=-1;side<=1;side+=2)
            {
                Box(b,"Paper block "+side,new Vector3(side*.20f,.015f,0),new Vector3(.38f,.018f,.46f),"Paper");
                // Curved paper profile rises gently toward the spine, then drops into the visible fold.
                float[] xs={.008f,.025f,.065f,.14f,.38f};float[] ys={.032f,.044f,.042f,.035f,.030f};
                var v=new List<Vector3>();var t=new List<int>();
                for(int i=0;i<xs.Length;i++){v.Add(new Vector3(side*xs[i],ys[i],-.23f));v.Add(new Vector3(side*xs[i],ys[i],.23f));}
                for(int i=0;i<4;i++){int a=i*2;if(side==1)t.AddRange(new[]{a,a+1,a+2,a+2,a+1,a+3});else t.AddRange(new[]{a,a+2,a+1,a+2,a+3,a+1});}
                var page=MeshPart(b,"Curved cream page "+side,v,t,"LedgerPage");PageUV(page.GetComponent<MeshFilter>().sharedMesh);
                for(int i=0;i<3;i++)Box(b,"Page block edge "+side+" "+i,new Vector3(side*.20f,.012f+i*.005f,-.2305f),new Vector3(.37f,.0006f,.0006f),"Ink");
            }
            Beam(p,"Blue pen",new Vector3(.49f,1.065f,-.35f),new Vector3(.54f,1.065f,-.12f),.012f,"Pen");
            Beam(p,"Pen tip",new Vector3(.49f,1.065f,-.35f),new Vector3(.484f,1.065f,-.375f),.006f,"Metal");
        }
        private static void Fan(Transform p)
        {
            var f=Group(p,"Beige desk fan",new Vector3(-1.04f,1.05f,.04f));
            var bv=new List<Vector3>{new Vector3(-.18f,0,-.15f),new Vector3(.18f,0,-.15f),new Vector3(.18f,0,.14f),new Vector3(-.18f,0,.14f),new Vector3(-.15f,.07f,-.12f),new Vector3(.15f,.07f,-.12f),new Vector3(.15f,.07f,.11f),new Vector3(-.15f,.07f,.11f)};
            MeshPart(f,"Fan sloping base",bv,new List<int>{4,6,5,4,7,6,0,4,5,0,5,1,1,5,6,1,6,2,2,6,7,2,7,3,3,7,4,3,4,0,0,1,2,0,2,3},"Beige");
            Box(f,"Fan neck",new Vector3(0,.18f,.05f),new Vector3(.065f,.29f,.07f),"Beige");
            for(int i=0;i<3;i++)Box(f,"Speed button "+i,new Vector3(-.07f+i*.07f,.077f,-.065f),new Vector3(.045f,.02f,.04f),"Dark");
            var head=Group(f,"Fan guard",new Vector3(0,.46f,0));
            var motor=Lathe(head,"Fan rear motor",new Vector3(0,0,.065f),new[]{0f,.07f,.06f,.04f,0f},new[]{-.02f,-.02f,.04f,.07f,.07f},"Beige");motor.localRotation=Quaternion.Euler(90,0,0);
            var ring=Lathe(head,"Fan outer ring",Vector3.zero,new[]{.238f,.25f,.25f,.238f},new[]{-.035f,-.035f,.035f,.035f},"Beige",16);ring.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<5;i++)
            {
                var v=new List<Vector3>{new Vector3(-.025f,.025f,-.004f),new Vector3(-.072f,.15f,-.008f),new Vector3(-.055f,.22f,-.006f),new Vector3(.035f,.235f,.01f),new Vector3(.073f,.19f,.016f),new Vector3(.025f,.025f,.01f)};
                var t=new List<int>{0,1,2,0,2,3,0,3,4,0,4,5,0,2,1,0,3,2,0,4,3,0,5,4};
                var blade=MeshPart(head,"Fan blade "+i,v,t,"Beige");blade.localRotation=Quaternion.Euler(0,0,-i*72+18);
            }
            for(int i=0;i<4;i++)
            {float a=i*Mathf.PI/4;Vector3 d=new Vector3(Mathf.Cos(a)*.236f,Mathf.Sin(a)*.236f,0);Beam(head,"Front guard bar "+i,d+Vector3.back*.052f,-d+Vector3.back*.052f,.009f,"Metal");}
            var hub=Lathe(head,"Fan hub",new Vector3(0,0,-.07f),new[]{0f,.055f,.055f,0f},new[]{-.01f,-.01f,.02f,.02f},"Beige");hub.localRotation=Quaternion.Euler(90,0,0);
        }
        private static void Plant(Transform p)
        {
            var plant=Group(p,"Aloe in cheap pot",new Vector3(1.10f,1.05f,.17f));
            Lathe(plant,"Terracotta pot",Vector3.zero,new[]{0f,.095f,.135f,.145f,.145f,.12f,.12f},new[]{0f,0f,.20f,.20f,.235f,.235f,.205f},"Pot",10);
            Lathe(plant,"Soil",new Vector3(0,.207f,0),new[]{.12f,0f},new[]{0f,0f},"Dark",10);
            for(int i=0;i<9;i++)
            {
                float a=i*2.399f;float len=i<4?.36f:.25f;
                Vector3 side=new Vector3(Mathf.Cos(a),0,-Mathf.Sin(a))*.035f;
                Vector3 mid=new Vector3(Mathf.Sin(a)*len*.45f,.22f+len*.55f,Mathf.Cos(a)*len*.45f);
                Vector3 tip=new Vector3(Mathf.Sin(a)*len,.22f+len*.65f,Mathf.Cos(a)*len);
                var v=new List<Vector3>{new Vector3(0,.20f,0),mid-side,mid+Vector3.up*.025f,mid+side,tip};
                var t=new List<int>{0,2,1,0,3,2,1,2,4,2,3,4,0,1,3,1,4,3};
                MeshPart(plant,"Aloe angular leaf "+i,v,t,i%2==0?"Leaf":"LeafLight");
            }
        }
        private static void Bell(Transform p)
        {
            var b=Group(p,"Guest service bell",new Vector3(-.24f,1.115f,.395f));
            Lathe(b,"Bell rubber base",Vector3.zero,new[]{0f,.095f,.095f,0f},new[]{0f,0f,.025f,.025f},"Dark",16);
            Lathe(b,"Bell dome",Vector3.zero,new[]{.09f,.09f,.074f,.044f,0f},new[]{.025f,.043f,.075f,.10f,.108f},"Bell",16);
            Lathe(b,"Bell button",Vector3.zero,new[]{.01f,.01f,.027f,.027f,0f},new[]{.10f,.13f,.13f,.14f,.14f},"Metal",12);
        }
        private static void Paper(Transform p)
        {
            var v=new List<Vector3>();var t=new List<int>();
            for(int z=0;z<4;z++)for(int x=0;x<4;x++)v.Add(new Vector3(.18f+x*.055f,1.117f+((x*7+z*3)%5)*.004f,.31f+z*.05f));
            for(int z=0;z<3;z++)for(int x=0;x<3;x++){int a=z*4+x;t.AddRange(new[]{a,a+4,a+1,a+1,a+4,a+5});}
            MeshPart(p,"Crumpled guest note",v,t,"Paper");
        }
        private static void Chair(Transform p)
        {
            Cushion(p,"Worn seat",new Vector3(0,.48f,0),new Vector3(.55f,.11f,.50f),Quaternion.identity);
            Cushion(p,"Upholstered back",new Vector3(0,.83f,.23f),new Vector3(.51f,.095f,.56f),Quaternion.Euler(82,0,0));
            Box(p,"Back support",new Vector3(0,.59f,.23f),new Vector3(.055f,.40f,.035f),"Dark");
            Lathe(p,"Chair gas lift",Vector3.zero,new[]{.035f,.035f},new[]{.09f,.46f},"Metal",10);
            for(int i=0;i<5;i++)
            {float a=i*Mathf.PI*2/5;var end=new Vector3(Mathf.Sin(a)*.30f,.08f,Mathf.Cos(a)*.30f);Beam(p,"Star leg "+i,new Vector3(0,.12f,0),end,.035f,"Dark");Box(p,"Castor "+i,end+Vector3.down*.035f,new Vector3(.065f,.07f,.055f),"Dark");}
            Box(p,"Seat seam",new Vector3(0,.485f,-.251f),new Vector3(.42f,.002f,.002f),"Dark");
            var body=p.gameObject.AddComponent<BoxCollider>();body.center=new Vector3(0,.26f,0);body.size=new Vector3(.62f,.48f,.62f);
        }
        private static void Cushion(Transform p,string name,Vector3 pos,Vector3 size,Quaternion rotation)
        {
            float x=size.x*.5f,z=size.z*.5f,c=.045f;
            var rim=new[]{new Vector2(-x+c,-z),new Vector2(x-c,-z),new Vector2(x,-z+c),new Vector2(x,z-c),new Vector2(x-c,z),new Vector2(-x+c,z),new Vector2(-x,z-c),new Vector2(-x,-z+c)};
            var v=new List<Vector3>();var t=new List<int>();
            foreach(float y in new[]{-size.y*.5f,size.y*.5f})foreach(var a in rim)v.Add(new Vector3(a.x,y,a.y));
            for(int i=1;i<7;i++)t.AddRange(new[]{8,8+i+1,8+i,0,i,i+1});
            for(int i=0;i<8;i++){int n=(i+1)%8;t.AddRange(new[]{i,8+i,8+n,i,8+n,n});}
            var part=MeshPart(p,name,v,t,"Fabric");part.localPosition=pos;part.localRotation=rotation;part.gameObject.AddComponent<BoxCollider>().size=size;
        }

        public static void Validate()
        {
            if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Wrong test scene");
            if(Object.FindObjectsByType<GuestRegister>(FindObjectsInactive.Include).Length!=1)throw new Exception("Register duplicate");
            var register=Object.FindAnyObjectByType<GuestRegister>();
            var player=Object.FindAnyObjectByType<FirstPersonController>();
            var body=player.GetComponent<CharacterController>();
            Vector3 center=player.transform.TransformPoint(body.center);
            float half=Mathf.Max(0,body.height*.5f-body.radius);
            foreach(var c in Physics.OverlapCapsule(center+Vector3.up*half,center-Vector3.up*half,body.radius,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                if(c.transform.IsChildOf(GameObject.Find("ReceptionDesk").transform) || c.transform.IsChildOf(GameObject.Find("OfficeChair").transform))
                    throw new Exception("Furniture overlaps original player spawn: "+c.name);
            var eye=player.GetComponentInChildren<Camera>().transform.position;
            if(!Physics.Raycast(eye,(register.transform.position-eye).normalized,out var hit,2.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<GuestRegister>()!=register)
                throw new Exception("Register not first solid hit from spawn");
            foreach(var r in GameObject.Find("ReceptionDesk").GetComponentsInChildren<Renderer>())
                if(r.sharedMaterial==null || r.sharedMaterial.shader.name!="Universal Render Pipeline/Lit")throw new Exception("Invalid desk material");
            foreach(string old in new[]{"Reception Counter","Reception Countertop","Reception Front Panel","Reception Monitor"})
                if(GameObject.Find(old)!=null)throw new Exception("Old geometry survives: "+old);
            Debug.Log("Reception test: one register, first ray <=2.5m, URP, old desk and colliders removed.");
        }
    }
}
