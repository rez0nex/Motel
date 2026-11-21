using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Motel.Editor
{
    public static partial class MotelTechnicalDetails
    {
        private static void RefineRoom(Transform room)
        {
            FineMaterial("PlasterFine", new Color(.60f,.58f,.53f), false);
            FineMaterial("ConcreteFine", new Color(.40f,.40f,.37f), true);
            Material("Plastic",new Color(.56f,.55f,.46f),"paper");
            Material("BlueCan",new Color(.18f,.31f,.36f),"paint");
            Material("Bulb",new Color(.85f,.83f,.74f),"paper");
            TimberMaterial();
            Surface(room.Find("Concrete Floor"),"ConcreteFine");
            foreach(string name in new[]{"Back Concrete","Left Concrete","Right Concrete"}) Surface(room.Find(name),"PlasterFine");
            var ceiling=room.parent.Find("Ceilings/Technical Concrete Soffit/Worn Concrete Ceiling");
            if(ceiling!=null)Surface(ceiling,"PlasterFine");
            LocalWear(room);
            FilledRack(room.Find("Shallow Entry Shelf"));
            RefinedBench(room.Find("Right Workbench"));
            FixedEmergencyAssembly(room.Find("Olive Generator/Sloped Control Panel/Emergency Cutoff"));
        }

        private static void Surface(Transform t,string material)
        {
            // Cube UVs normally cover 0..1 regardless of its transform scale. Project
            // in room metres instead: one half-metre tile, equally sized on every face.
            var mesh=Object.Instantiate(t.GetComponent<MeshFilter>().sharedMesh);
            var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<uv.Length;i++)
            {
                Vector3 p=t.localPosition+t.localRotation*Vector3.Scale(vertices[i],t.localScale);
                Vector3 n=t.localRotation*normals[i];
                uv[i]=Mathf.Abs(n.y)>.8f?new Vector2(p.x,p.z)*2:
                    Mathf.Abs(n.x)>.8f?new Vector2(p.z,p.y)*2:new Vector2(p.x,p.y)*2;
            }
            mesh.uv=uv;mesh.RecalculateTangents();
            t.GetComponent<MeshFilter>().sharedMesh=Stored("MetreUV_"+t.name,mesh);
            t.GetComponent<Renderer>().sharedMaterial=materials[material];
        }
        private static Mesh Stored(string name,Mesh mesh)
        {
            string path=Folder+"/Meshes/Refined_"+Hash128.Compute(name)+".asset";
            var stored=AssetDatabase.LoadAssetAtPath<Mesh>(path);mesh.name=name;
            if(stored==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,stored);EditorUtility.SetDirty(stored);Object.DestroyImmediate(mesh);return stored;
        }
        private static void FineMaterial(string name,Color tint,bool floor)
        {
            string texPath=Folder+"/Textures/"+name+".png",normalPath=Folder+"/Textures/"+name+"_Normal.png";
            if(!File.Exists(texPath))
            {
                const int size=512;var colors=new Color[size*size];var heights=new float[size*size];var rng=new System.Random(floor?31027:91021);
                for(int i=0;i<colors.Length;i++)
                {
                    float grit=(float)rng.NextDouble(),noise=((float)rng.NextDouble()-.5f)*(floor?.10f:.055f);
                    float pore=grit<(floor?.010f:.004f)?-.16f:0;
                    colors[i]=tint*(1+noise+pore);heights[i]=noise+pore;
                }
                var tex=new Texture2D(size,size,TextureFormat.RGB24,false);tex.SetPixels(colors);tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);
                var ntex=new Texture2D(size,size,TextureFormat.RGB24,false);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float dx=heights[y*size+(x+1)%size]-heights[y*size+(x+size-1)%size];
                    float dy=heights[((y+1)%size)*size+x]-heights[((y+size-1)%size)*size+x];
                    Vector3 n=new Vector3(-dx*1.1f,-dy*1.1f,1).normalized;colors[y*size+x]=new Color(n.x*.5f+.5f,n.y*.5f+.5f,n.z*.5f+.5f);
                }
                ntex.SetPixels(colors);ntex.Apply();File.WriteAllBytes(normalPath,ntex.EncodeToPNG());Object.DestroyImmediate(ntex);
            }
            foreach(string path in new[]{texPath,normalPath})
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.mipmapEnabled=true;importer.maxTextureSize=512;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                if(path==normalPath)importer.textureType=TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            string matPath=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,matPath);}
            m.SetColor("_BaseColor",Color.white);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));m.SetTextureScale("_BaseMap",Vector2.one);
            m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));m.SetFloat("_BumpScale",.25f);m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Metallic",0);m.SetFloat("_Smoothness",floor?.06f:.10f);materials[name]=m;EditorUtility.SetDirty(m);
        }

        private static void LocalWear(Transform room)
        {
            var art=Art(room,"Localized Wear");
            string tp=Folder+"/Textures/LocalDust.png";
            if(!File.Exists(tp))
            {
                var tex=new Texture2D(128,128,TextureFormat.RGBA32,false);var pixels=new Color[128*128];var rng=new System.Random(52);
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {float a=Mathf.Clamp01(1-new Vector2((x-63.5f)/64,(y-63.5f)/64).magnitude);pixels[y*128+x]=new Color(.19f,.17f,.13f,a*a*(.11f+(float)rng.NextDouble()*.09f));}
                tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(tp,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(tp,ImportAssetOptions.ForceSynchronousImport);
            TransparentMaterial("LocalDust",new Color(1,1,1,1));
            var dust=materials["LocalDust"];dust.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            dust.SetFloat("_Smoothness",0);dust.SetFloat("_BlendModePreserveSpecular",0);dust.SetFloat("_SpecularHighlights",0);dust.SetFloat("_EnvironmentReflections",0);
            dust.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");dust.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            Patch(art,"Wear under generator",new Vector3(1.15f,.014f,5.90f),new Vector2(1.8f,1.4f),Quaternion.Euler(90,0,0));
            foreach(float x in new[]{.6f,5.5f})Patch(art,"Back lower wear "+x,new Vector3(x,.17f,6.935f),new Vector2(1.15f,.34f),Quaternion.identity);
            foreach(float z in new[]{.8f,6.1f})Patch(art,"Corner lower wear "+z,new Vector3(.102f,.16f,z),new Vector2(.85f,.32f),Quaternion.Euler(0,-90,0));
        }
        private static void Patch(Transform p,string name,Vector3 at,Vector2 size,Quaternion rotation)
        {var t=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;t.SetParent(p,false);t.name=name;t.localPosition=at;t.localRotation=rotation;t.localScale=new Vector3(size.x,size.y,1);t.GetComponent<Renderer>().sharedMaterial=materials["LocalDust"];Object.DestroyImmediate(t.GetComponent<Collider>());}
        private static void TransparentMaterial(string name,Color color)
        {
            string path=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_Cull",2);
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetFloat("_Smoothness",.15f);materials[name]=m;EditorUtility.SetDirty(m);
        }

        private static Transform FurnitureRoot(Transform furniture,Vector3 center,Vector3 size)
        {
            var art=Art(furniture,"Refinement");
            foreach(var r in furniture.GetComponentsInChildren<Renderer>(true))if(!r.transform.IsChildOf(art))r.enabled=false;
            foreach(var c in furniture.GetComponentsInChildren<Collider>(true))if(!c.transform.IsChildOf(art))c.enabled=false;
            var proxy=art.gameObject.AddComponent<BoxCollider>();proxy.center=center;proxy.size=size;
            return art;
        }
        private static void FilledRack(Transform shelf)
        {
            var art=FurnitureRoot(shelf,new Vector3(0,.88f,0),new Vector3(.50f,1.76f,1.46f));
            foreach(float x in new[]{-.20f,.20f})foreach(float z in new[]{-.67f,.67f})
            {
                Box(art,"Steel angle upright",new Vector3(x,.84f,z),new Vector3(.035f,1.68f,.065f),"Steel");
                Box(art,"Steel angle flange",new Vector3(x+.015f,.84f,z-.016f),new Vector3(.065f,1.68f,.033f),"Steel");
                Box(art,"Foot",new Vector3(x,.025f,z),new Vector3(.10f,.045f,.11f),"Steel");
                for(int i=0;i<12;i++)Box(art,"Rack slot",new Vector3(x+.05f,.20f+i*.12f,z),new Vector3(.004f,.018f,.012f),"Rubber");
            }
            foreach(float y in new[]{.16f,.62f,1.08f,1.54f})
            {
                Bevel(art,"Metal shelf "+y,new Vector3(0,y,0),new Vector3(.47f,.035f,1.42f),.006f,"Steel");
                foreach(float x in new[]{-.235f,.235f})Box(art,"Shelf rolled lip",new Vector3(x,y+.01f,0),new Vector3(.015f,.05f,1.42f),"Steel");
                foreach(float z in new[]{-.71f,.71f})Box(art,"Shelf end lip",new Vector3(0,y+.01f,z),new Vector3(.47f,.05f,.015f),"Steel");
            }
            Rod(art,"Rear X brace A",new Vector3(-.23f,.20f,-.65f),new Vector3(-.23f,1.62f,.65f),.008f,"Steel");
            Rod(art,"Rear X brace B",new Vector3(-.23f,.20f,.65f),new Vector3(-.23f,1.62f,-.65f),.008f,"Steel");
            var toolcase=Node(art,"Service tool case",new Vector3(0,.1775f,-.37f));
            Bevel(toolcase,"Case",new Vector3(0,.095f,0),new Vector3(.34f,.19f,.47f),.024f,"Rubber");
            FrameOnX(toolcase,new Vector3(.176f,.11f,0),.34f,.11f,"PaintEdge");
            Tube(toolcase,"Case handle",.012f,"Steel",new Vector3(.18f,.10f,-.07f),new Vector3(.21f,.10f,-.07f),new Vector3(.21f,.10f,.07f),new Vector3(.18f,.10f,.07f));
            Coil(art,"Rack coiled cable",new Vector3(0,.1905f,.35f),.17f,3);
            for(int i=0;i<3;i++)Bevel(art,"Folded rag "+i,new Vector3(0,.6565f+i*.038f,-.48f),new Vector3(.33f,.038f,.30f),.012f,"Cloth");
            Bevel(art,"Consumables bin",new Vector3(0,.7375f,.02f),new Vector3(.35f,.20f,.38f),.014f,"Plastic");
            Bevel(art,"Bin lid",new Vector3(0,.846f,.02f),new Vector3(.37f,.017f,.40f),.006f,"BlueCan");
            SmallLabelX(art,"КРЕПЁЖ",new Vector3(.181f,.76f,.02f),.22f);
            Carton(art,"Spare bulbs carton",new Vector3(0,.6375f,.51f),new Vector3(.34f,.15f,.28f));
            for(int i=0;i<3;i++) { Ball(art,"Spare bulb",new Vector3(-.09f+i*.09f,.80f,.51f),new Vector3(.075f,.095f,.075f),"Bulb");Rod(art,"Bulb screw",new Vector3(-.09f+i*.09f,.72f,.51f),new Vector3(-.09f+i*.09f,.76f,.51f),.021f,"Steel"); }
            Jerrycan(art,new Vector3(0,1.0975f,-.45f));
            var tin=Node(art,"Paint tin",new Vector3(0,1.0975f,.02f));
            Rod(tin,"Paint body",Vector3.zero,Vector3.up*.27f,.105f,"Paint");
            Rod(tin,"Tin lid",Vector3.up*.27f,Vector3.up*.285f,.112f,"Steel");
            SmallLabelX(tin,"КРАСКА",new Vector3(.107f,.16f,0),.17f);
            Tube(tin,"Tin handle",.008f,"Steel",new Vector3(0,.22f,-.11f),new Vector3(0,.35f,-.08f),new Vector3(0,.35f,.08f),new Vector3(0,.22f,.11f));
            Rod(art,"Small solvent bottle",new Vector3(0,1.0975f,.46f),new Vector3(0,1.3475f,.46f),.064f,"Plastic");
            Rod(art,"Bottle cap",new Vector3(0,1.3475f,.46f),new Vector3(0,1.38f,.46f),.035f,"Rubber");
            Carton(art,"Large parcel",new Vector3(0,1.5575f,-.38f),new Vector3(.34f,.18f,.45f));
            Carton(art,"Small parcel",new Vector3(0,1.5575f,.24f),new Vector3(.32f,.13f,.34f));
        }
        private static void Jerrycan(Transform p,Vector3 bottom)
        {
            var t=Node(p,"Small jerrycan",bottom);
            Bevel(t,"Blue can body",new Vector3(0,.15f,0),new Vector3(.28f,.30f,.25f),.026f,"BlueCan");
            Tube(t,"Moulded carry handle",.018f,"BlueCan",new Vector3(0,.28f,-.055f),new Vector3(0,.37f,-.055f),new Vector3(0,.37f,.06f),new Vector3(0,.28f,.06f));
            Rod(t,"Filler cap",new Vector3(-.07f,.29f,-.07f),new Vector3(-.07f,.33f,-.07f),.035f,"Rubber");
            foreach(float z in new[]{-.065f,.065f})Box(t,"Moulded ridge",new Vector3(.142f,.15f,z),new Vector3(.012f,.20f,.012f),"BlueCan");
        }
        private static void Carton(Transform p,string name,Vector3 bottom,Vector3 size)
        {
            var c=Node(p,name,bottom);
            Bevel(c,"Carton body",new Vector3(0,size.y/2,0),size,.006f,"Cardboard");
            Box(c,"Top seam",new Vector3(0,size.y+.002f,0),new Vector3(size.x,.005f,.012f),"Wood");
            Box(c,"Packing tape",new Vector3(0,size.y+.006f,0),new Vector3(.065f,.006f,size.z),"Enamel");
            Box(c,"Tape front",new Vector3(size.x/2+.003f,size.y*.6f,0),new Vector3(.006f,size.y*.7f,.06f),"Enamel");
            Box(c,"Simple parcel label",new Vector3(size.x/2+.007f,size.y*.55f,size.z*.23f),new Vector3(.004f,size.y*.5f,size.z*.30f),"Bulb");
        }
        private static void Coil(Transform p,string name,Vector3 at,float radius,int count)
        {
            var c=Node(p,name,at);c.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<count;i++)Ring(c,"Cable turn "+i,new Vector3(0,0,-i*.022f),radius-i*.015f,.013f,"Rubber");
            Tube(c,"Loose cable end",.013f,"Rubber",new Vector3(radius,0,0),new Vector3(radius+.012f,.08f,0),new Vector3(radius-.03f,.11f,0));
        }

        private static void RefinedBench(Transform bench)
        {
            var art=FurnitureRoot(bench,new Vector3(-.045f,.51f,0),new Vector3(.80f,1.02f,2.62f));
            for(int i=0;i<3;i++)Bevel(art,"Bench top plank "+i,new Vector3(0,.9275f,(i-1)*.85f),new Vector3(.65f,.095f,.846f),.011f,"BenchWood");
            foreach(float x in new[]{-.245f,.245f})foreach(float z in new[]{-1.09f,1.09f})
            {
                Bevel(art,"Metal leg "+x+" "+z,new Vector3(x,.45f,z),new Vector3(.06f,.88f,.06f),.004f,"Paint");
                Box(art,"Steel foot",new Vector3(x,.025f,z),new Vector3(.13f,.04f,.13f),"Steel");
            }
            foreach(float x in new[]{-.26f,.26f})
            {Box(art,"Top cross rail",new Vector3(x,.84f,0),new Vector3(.045f,.09f,2.35f),"Paint");Box(art,"Lower cross rail",new Vector3(x,.24f,0),new Vector3(.045f,.06f,2.35f),"Paint");}
            foreach(float z in new[]{-1.1f,1.1f})Box(art,"End cross rail",new Vector3(0,.84f,z),new Vector3(.55f,.09f,.045f),"Paint");
            Bevel(art,"Lower wooden shelf",new Vector3(0,.25f,0),new Vector3(.55f,.04f,2.30f),.006f,"BenchWood");
            Bevel(art,"Drawer body",new Vector3(0,.78f,.36f),new Vector3(.53f,.13f,.70f),.007f,"Paint");
            Bevel(art,"Drawer face",new Vector3(-.30f,.78f,.36f),new Vector3(.04f,.145f,.74f),.007f,"PaintEdge");
            Tube(art,"Drawer pull",.012f,"Steel",new Vector3(-.326f,.78f,.20f),new Vector3(-.37f,.78f,.20f),new Vector3(-.37f,.78f,.52f),new Vector3(-.326f,.78f,.52f));
            ToolBox(art,new Vector3(0,.995f,.83f));
            var vice=Node(art,"Edge mounted vice",new Vector3(-.29f,1.025f,-.85f));
            Bevel(vice,"Cast base",Vector3.zero,new Vector3(.34f,.10f,.31f),.015f,"Rust");
            foreach(float z in new[]{-.09f,.09f}) {Bevel(vice,"Vice jaw body "+z,new Vector3(0,.10f,z),new Vector3(.26f,.13f,.075f),.01f,"Rust");Box(vice,"Serrated jaw",new Vector3(0,.175f,z),new Vector3(.29f,.035f,.054f),"Steel");}
            Rod(vice,"Vice screw",new Vector3(0,.09f,-.25f),new Vector3(0,.09f,.22f),.023f,"Steel");
            for(int i=0;i<8;i++)Ring(vice,"Thread "+i,new Vector3(0,.09f,-.15f-i*.012f),.024f,.003f,"Steel");
            Rod(vice,"Crank",new Vector3(-.15f,.09f,-.25f),new Vector3(.15f,.09f,-.25f),.012f,"Steel");
            foreach(float x in new[]{-.15f,.15f})Ball(vice,"Crank end",new Vector3(x,.09f,-.25f),Vector3.one*.035f,"Steel");
            Box(vice,"Under table clamp",new Vector3(0,-.13f,0),new Vector3(.20f,.035f,.12f),"Rust");
            Rod(vice,"Clamp fixing",new Vector3(.06f,-.13f,0),new Vector3(.06f,-.025f,0),.018f,"Steel");
            Wrench(art,new Vector3(.10f,.9875f,-.22f));
            Rod(art,"Hammer wood",new Vector3(-.20f,.993f,-.43f),new Vector3(-.20f,.993f,-.10f),.018f,"Wood");
            Bevel(art,"Hammer head",new Vector3(-.20f,1.0f,-.43f),new Vector3(.12f,.05f,.054f),.006f,"Steel");
            Rod(art,"Screwdriver shaft",new Vector3(-.02f,.982f,-.30f),new Vector3(-.02f,.999f,.02f),.007f,"Steel");
            Rod(art,"Screwdriver grip",new Vector3(-.02f,.999f,.02f),new Vector3(-.02f,.999f,.17f),.024f,"Rust");
            for(int i=0;i<2;i++)Bevel(art,"Folded bench rag "+i,new Vector3(.015f,.993f+i*.036f,.42f),new Vector3(.31f,.036f,.26f),.012f,"Cloth");
            Carton(art,"Lower carton",new Vector3(0,.27f,.72f),new Vector3(.43f,.25f,.52f));
            CableReel(art,new Vector3(0,.44f,-.18f));
            for(int i=0;i<3;i++)Carton(art,"Consumables pack "+i,new Vector3(0,.27f+i*.075f,-.89f),new Vector3(.33f,.075f,.28f));
            ToolBoard(art);
        }
        private static void ToolBox(Transform p,Vector3 at)
        {
            var b=Node(p,"Open tool box",at);
            Box(b,"Box base",Vector3.zero,new Vector3(.46f,.04f,.58f),"Paint");
            foreach(float x in new[]{-.22f,.22f})Bevel(b,"Box side "+x,new Vector3(x,.12f,0),new Vector3(.025f,.25f,.58f),.005f,"Paint");
            foreach(float z in new[]{-.28f,.28f})Box(b,"Box end",new Vector3(0,.12f,z),new Vector3(.46f,.25f,.025f),"Paint");
            Tube(b,"Handle",.017f,"Steel",new Vector3(0,.20f,-.27f),new Vector3(0,.37f,-.16f),new Vector3(0,.37f,.16f),new Vector3(0,.20f,.27f));
            Rod(b,"Tool handle",new Vector3(-.14f,.055f,-.17f),new Vector3(-.06f,.08f,-.06f),.03f,"Wood");
            Rod(b,"Tool shaft",new Vector3(-.06f,.08f,-.06f),new Vector3(.12f,.17f,.19f),.014f,"Steel");
        }
        private static void CableReel(Transform p,Vector3 at)
        {
            var r=Node(p,"Cable reel",at);
            foreach(float x in new[]{-.105f,.105f})Rod(r,"Reel flange",new Vector3(x-.014f,0,0),new Vector3(x+.014f,0,0),.17f,"Plastic");
            Rod(r,"Wound cable",new Vector3(-.09f,0,0),new Vector3(.09f,0,0),.125f,"Rubber");
            Rod(r,"Axle",new Vector3(-.14f,0,0),new Vector3(.14f,0,0),.035f,"Steel");
            Tube(r,"Cable plug lead",.012f,"Rubber",new Vector3(-.11f,0,.12f),new Vector3(-.15f,-.08f,.18f),new Vector3(-.06f,-.14f,.22f));
        }
        private static void ToolBoard(Transform p)
        {
            var board=Node(p,"Wall tool board",new Vector3(.3575f,1.72f,0));board.localRotation=Quaternion.Euler(0,90,0);
            Bevel(board,"Pegboard",Vector3.zero,new Vector3(1.80f,.76f,.025f),.008f,"BenchWood");
            Frame(board,"Board edge",Vector3.back*.018f,1.8f,.76f,.018f,"Steel");
            for(int x=0;x<14;x++)for(int y=0;y<5;y++)Box(board,"Peg hole",new Vector3(-.80f+x*.12f,-.25f+y*.12f,-.014f),new Vector3(.007f,.007f,.003f),"Rubber");
            foreach(float x in new[]{-.62f,-.30f,.05f,.38f})
            {Rod(board,"Peg hook",new Vector3(x,.20f,-.02f),new Vector3(x,.20f,-.065f),.009f,"Steel");}
            Rod(board,"Hanging driver steel",new Vector3(-.62f,-.19f,-.065f),new Vector3(-.62f,.20f,-.065f),.008f,"Steel");
            Rod(board,"Hanging driver grip",new Vector3(-.62f,-.24f,-.065f),new Vector3(-.62f,-.10f,-.065f),.025f,"Rust");
            var wrench=Node(board,"Hanging spanner",new Vector3(-.30f,-.02f,-.065f));wrench.localRotation=Quaternion.Euler(90,0,0);Wrench(wrench,Vector3.zero);
            foreach(int side in new[]{-1,1})Tube(board,"Pliers handle "+side,.016f,"Rust",new Vector3(.05f+side*.06f,-.23f,-.065f),new Vector3(.05f+side*.025f,-.02f,-.065f),new Vector3(.05f-side*.035f,.12f,-.065f));
            Rod(board,"Small hanging wrench",new Vector3(.38f,-.17f,-.065f),new Vector3(.38f,.18f,-.065f),.015f,"Steel");
            Tube(board,"Task lamp arm",.014f,"Steel",new Vector3(.72f,.25f,-.025f),new Vector3(.72f,.39f,-.19f),new Vector3(.58f,.30f,-.40f));
            var lamp=Node(board,"Task lamp",new Vector3(.58f,.28f,-.40f));lamp.localRotation=Quaternion.Euler(20,0,0);
            MeshPart(lamp,"Lamp shade",ShadeMesh(),"Paint");
            Ball(lamp,"Lamp bulb",new Vector3(0,-.045f,0),new Vector3(.11f,.055f,.11f),"Bulb");
            var light=Node(lamp,"Workbench lamp light",new Vector3(0,-.09f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.intensity=.35f;light.range=1.5f;light.color=new Color(1,.91f,.75f);light.shadows=LightShadows.None;
        }

        private static void FixedEmergencyAssembly(Transform red)
        {
            var art=Art(red,"Fixed Housing");
            Bevel(art,"Mounted button housing",new Vector3(0,0,-.008f),new Vector3(.29f,.29f,.070f),.012f,"PaintEdge");
            Frame(art,"Housing seal",new Vector3(0,0,-.052f),.273f,.273f,.015f,"Rubber");
            foreach(float x in new[]{-.12f,.12f})foreach(float y in new[]{-.12f,.12f})Screw(art,new Vector3(x,y,-.049f),.016f);
            var cap=red.Find("Red Cap");cap.localPosition=new Vector3(0,0,-.059f);
            red.Find("Red Cap Bezel").GetComponent<Renderer>().enabled=false;
            var oldArt=red.Find("Art Details");foreach(var r in oldArt.GetComponentsInChildren<Renderer>())if(r.name.Contains("Red metal bezel"))r.enabled=false;
            var label=red.Find("Cutoff Name");var labelAt=label.localPosition;labelAt.y=.255f;label.localPosition=labelAt;
            var plate=oldArt.Find("Enamel name plate");var plateAt=plate.localPosition;plateAt.y=.255f;plate.localPosition=plateAt;
            Ring(art,"Mounted red bezel",new Vector3(0,0,-.049f),.068f,.008f,"Steel");
            foreach(float x in new[]{-.105f,.105f})Box(art,"Fixed hinge bracket",new Vector3(x,.14f,-.069f),new Vector3(.024f,.045f,.065f),"Steel");
            var cover=red.Find("Clear Cover Hinge");var ca=Art(cover,"Lid Details");
            TransparentMaterial("ClearPlastic",new Color(.73f,.83f,.84f,.18f));
            cover.Find("Transparent Cover").GetComponent<Renderer>().sharedMaterial=materials["ClearPlastic"];
            foreach(float x in new[]{-.135f,.135f})Box(ca,"Transparent lid side",new Vector3(x,-.14f,.023f),new Vector3(.010f,.28f,.052f),"ClearPlastic");
            Box(ca,"Transparent bottom edge",new Vector3(0,-.277f,.023f),new Vector3(.27f,.009f,.052f),"ClearPlastic");
            Bevel(ca,"Moving lid latch",new Vector3(0,-.289f,.025f),new Vector3(.065f,.023f,.032f),.005f,"Steel");
            Bevel(art,"Fixed latch catch",new Vector3(0,-.158f,-.056f),new Vector3(.08f,.025f,.026f),.004f,"Steel");
        }

        private static void SmallLabelX(Transform p,string text,Vector3 at,float width)
        {var t=Node(p,"Label "+text,at);t.localRotation=Quaternion.Euler(0,-90,0);Box(t,"Label plate",Vector3.zero,new Vector3(width,.065f,.003f),"Enamel");var ink=Node(t,"Label ink",Vector3.back*.004f);MotelLoopSceneSetup.Text(ink,"Text",text,width*.90f,.045f,Color.black);}
        private static void FrameOnX(Transform p,Vector3 at,float width,float height,string mat)
        {var t=Node(p,"Case lid seam",at);t.localRotation=Quaternion.Euler(0,-90,0);Frame(t,"Seam",Vector3.zero,width,height,.006f,mat);}
        private static void TimberMaterial()
        {
            string path=Folder+"/Textures/BenchWood.png";
            if(!File.Exists(path))
            {
                const int size=512;var tex=new Texture2D(size,size,TextureFormat.RGB24,false);var pixels=new Color[size*size];var rng=new System.Random(7301);
                var tint=new Color(.48f,.36f,.23f);
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {float grain=Mathf.Sin(y*.63f+Mathf.Sin(x*.018f)*1.1f)*.019f+Mathf.Sin(y*.18f)*.012f;
                    pixels[y*size+x]=tint*(1+grain+((float)rng.NextDouble()-.5f)*.025f);}
                tex.SetPixels(pixels);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
            }
            Material("BenchWood",Color.white,"wood");
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=8;importer.wrapMode=TextureWrapMode.Repeat;importer.SaveAndReimport();
        }
        private static void Bevel(Transform p,string name,Vector3 at,Vector3 size,float bevel,string mat)
        {
            var key=FormattableString.Invariant($"Bevel_{size.x:R}_{size.y:R}_{size.z:R}_{bevel:R}");
            var t=Node(p,name,at);t.gameObject.AddComponent<MeshFilter>().sharedMesh=Stored(key,BevelMesh(size,bevel));t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials[mat];
        }
        private static Mesh BevelMesh(Vector3 size,float radius)
        {
            radius=Mathf.Min(radius,size.x/3,size.y/3,size.z/3);var half=size/2;var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            foreach(Vector3 n in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,n),v=Vector3.Cross(n,u);
                float hn=Vector3.Dot(Vector3.Scale(n,n),half),hu=Vector3.Dot(Vector3.Scale(u,u),half),hv=Vector3.Dot(Vector3.Scale(v,v),half);
                float[] us={-hu,-hu+radius,hu-radius,hu},vs={-hv,-hv+radius,hv-radius,hv};int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {var q=n*hn+u*us[x]+v*vs[y];var core=Vector3.Min(Vector3.Max(q,-half+Vector3.one*radius),half-Vector3.one*radius);var dir=(q-core).normalized;vertices.Add(core+dir*radius);normals.Add(dir);uv.Add(new Vector2(us[x]*2,vs[y]*2));}
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;tris.AddRange(new[]{a,a+1,a+5,a,a+5,a+4});}
            }
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
        }
        private static Mesh ShadeMesh()
        {
            const int count=24;var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            for(int i=0;i<=count;i++){float a=i*2*Mathf.PI/count;v.Add(new Vector3(Mathf.Cos(a)*.045f,.05f,Mathf.Sin(a)*.045f));v.Add(new Vector3(Mathf.Cos(a)*.105f,-.045f,Mathf.Sin(a)*.105f));uv.Add(new Vector2(i/(float)count,1));uv.Add(new Vector2(i/(float)count,0));}
            for(int i=0;i<count;i++){int a=i*2;tr.AddRange(new[]{a,a+3,a+1,a,a+2,a+3});}
            var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        }
    }
}
