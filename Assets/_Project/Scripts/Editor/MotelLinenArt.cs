using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Motel.Editor
{
    public static class MotelLinenArt
    {
        public const string Folder="Assets/_Project/Art/LinenRoom";
        private static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private static readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>();
        public static void Prepare()
        {
            foreach(string d in new[]{"Materials","Textures","Meshes"})Directory.CreateDirectory(Folder+"/"+d);
            materials.Clear();meshes.Clear();
            Mat("Plaster",new Color(.75f,.72f,.63f),"plaster");Mat("Sage",new Color(.36f,.41f,.32f),"paint");Mat("Tiles",new Color(.62f,.54f,.42f),"tiles");
            Mat("Steel",new Color(.43f,.44f,.42f),"metal");Mat("Wood",new Color(.57f,.48f,.34f),"wood");Mat("PaleTop",new Color(.78f,.74f,.64f),"wood");Mat("Beige",new Color(.66f,.63f,.53f),"paint");
            Mat("CleanWhite",new Color(.89f,.88f,.81f),"cloth");Mat("Cream",new Color(.82f,.81f,.73f),"cloth");Mat("Blanket",Color.white,"stripes");
            Mat("Canvas",new Color(.58f,.53f,.41f),"canvas");Mat("PlasticWhite",new Color(.74f,.73f,.65f),"plastic");Mat("PlasticBlue",new Color(.25f,.42f,.49f),"plastic");
            Mat("Rubber",new Color(.055f,.06f,.055f));Mat("Chrome",new Color(.55f,.57f,.54f));Mat("Red",new Color(.45f,.13f,.09f));Mat("Cork",new Color(.43f,.30f,.16f),"cork");
            Mat("Paper",new Color(.85f,.82f,.71f));Mat("Cardboard",new Color(.48f,.39f,.26f),"card");Mat("Notebook",new Color(.21f,.105f,.075f));Mat("Straw",new Color(.60f,.48f,.24f));Mat("OldWood",new Color(.32f,.25f,.16f),"wood");
            Mat("Frost",new Color(.66f,.77f,.79f,.92f),"frost");Mat("DoorGlass",new Color(.15f,.19f,.19f,.86f));Mat("Tube",new Color(.93f,.93f,.84f));
            foreach(string n in new[]{"Frost","DoorGlass"}){var m=materials[n];m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_Cull",0);m.SetOverrideTag("RenderType","Transparent");m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
            materials["Sage"].SetColor("_BaseColor",new Color(1.30f,1.25f,1.18f));
            materials["Beige"].SetColor("_BaseColor",new Color(1.08f,1.05f,.96f));
            materials["Canvas"].SetFloat("_Cull",0);materials["Chrome"].SetFloat("_Metallic",.7f);materials["Chrome"].SetFloat("_Smoothness",.55f);
            materials["Tube"].EnableKeyword("_EMISSION");materials["Tube"].SetColor("_EmissionColor",new Color(1.5f,1.5f,1.3f));
            materials["Tiles"].SetTextureScale("_BaseMap",new Vector2(6.08f/1.6f,6.88f/1.6f));
            AssetDatabase.SaveAssets();
        }
        public static Material Material(string name)=>materials[name];
        private static void Mat(string name,Color color,string texture=null)
        {
            string p=Folder+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,p);}
            m.SetColor("_BaseColor",texture==null?color:Color.white);m.SetFloat("_Smoothness",.12f);m.enableInstancing=true;
            if(texture!=null)
            {
                string tp=Folder+"/Textures/"+(name=="Tiles"?"TilesFine":name)+".png";
                if(!File.Exists(tp))
                {
                    int size=texture=="tiles"?512:256;var t=new Texture2D(size,size,TextureFormat.RGBA32,false);var pixels=new Color[size*size];var rng=new System.Random(name.Length*73+521);
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                    {
                        float random=(float)rng.NextDouble(),noise=(random-.5f)*.025f,cloud=Mathf.PerlinNoise(x*.045f,y*.045f);Color c=color;
                        if(texture=="tiles")
                        {
                            int cell=size/4,tx=x%cell,ty=y%cell;float tile=Mathf.PerlinNoise((x/cell)*1.2f,(y/cell)*1.7f)*.09f-.04f;
                            c=color*(1+tile+(cloud-.5f)*.12f+noise);if(tx<1||ty<1)c=new Color(.35f,.32f,.26f)*(1+noise);else if(tx==1||ty==1)c*=1.07f;
                        }
                        else if(texture=="stripes")
                        {int band=(y/15)%6;Color[] colors={new Color(.63f,.56f,.40f),new Color(.78f,.74f,.60f),new Color(.46f,.49f,.35f),new Color(.77f,.71f,.56f),new Color(.56f,.34f,.23f),new Color(.82f,.77f,.65f)};c=colors[band]*(1+noise+(x%3==0?.015f:0));}
                        else if(texture=="cloth"||texture=="canvas")c=color*(1+noise+(x%3==0||y%3==0?-.012f:.006f));
                        else if(texture=="wood")c=color*(1+Mathf.Sin(y*.38f+Mathf.Sin(x*.05f)*.8f)*.05f+noise);
                        else if(texture=="frost")c=new Color(.66f,.77f,.79f,.93f)*(1+(cloud-.5f)*.20f+noise*2);
                        else {c=color*(1+(cloud-.5f)*.09f+noise);if(texture=="paint"||texture=="metal")if((x<3||y<3||x>252||y>252)&&random>.60f)c=Color.Lerp(c,new Color(.39f,.34f,.24f),.35f);}
                        if(texture!="frost")c.a=1;pixels[y*size+x]=c;
                    }
                    t.SetPixels(pixels);t.Apply();File.WriteAllBytes(tp,t.EncodeToPNG());Object.DestroyImmediate(t);
                }
                AssetDatabase.ImportAsset(tp,ImportAssetOptions.ForceSynchronousImport);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            }
            materials[name]=m;EditorUtility.SetDirty(m);
        }
        public static Transform Node(Transform parent,string name,Vector3 at=default)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=at;return t;}
        public static Transform Box(Transform p,string name,Vector3 at,Vector3 size,string mat,bool solid=false)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=materials[mat];if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;}
        public static Transform Soft(Transform p,string name,Vector3 at,Vector3 size,string mat,bool pillow=false)
        {var t=MeshPart(p,name,at,RoundBox(pillow?.24f:.10f),mat);t.localScale=size;return t;}
        public static Transform MeshPart(Transform p,string name,Vector3 at,Mesh mesh,string mat)
        {var t=Node(p,name,at);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=materials[mat];return t;}
        public static void Solid(Transform p,Vector3 center,Vector3 size){var c=p.gameObject.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        public static void Rod(Transform p,string name,Vector3 a,Vector3 b,float r,string mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=(a+b)/2;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);g.transform.localScale=new Vector3(2*r,(b-a).magnitude/2,2*r);g.GetComponent<Renderer>().sharedMaterial=materials[mat];Object.DestroyImmediate(g.GetComponent<Collider>());}
        public static void Ball(Transform p,string name,Vector3 at,Vector3 scale,string mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=materials[mat];Object.DestroyImmediate(g.GetComponent<Collider>());}
        public static void Ring(Transform p,string name,Vector3 at,float radius,float tube,string mat,Quaternion rotation)
        {var t=MeshPart(p,name,at,Torus(radius,tube),mat);t.localRotation=rotation;}
        public static Mesh Store(string name,Func<Mesh> build)
        {if(meshes.TryGetValue(name,out var m))return m;string path=Folder+"/Meshes/"+name+".asset";m=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(m==null){m=build();m.name=name;AssetDatabase.CreateAsset(m,path);}meshes[name]=m;return m;}
        private static Mesh RoundBox(float bevel)=>Store("SoftBox"+(int)(bevel*100),()=>
        {
            const int n=8;var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var axis in axes)
            {
                Vector3 u=Mathf.Abs(axis.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,axis),w=Vector3.Cross(axis,u);int start=v.Count;
                for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
                {Vector3 q=axis*.5f+u*(x/(float)n-.5f)+w*(y/(float)n-.5f),core=Vector3.Min(Vector3.Max(q,Vector3.one*(-.5f+bevel)),Vector3.one*(.5f-bevel)),d=(q-core).normalized;v.Add(core+d*bevel);normals.Add(d);uv.Add(new Vector2(x/(float)n,y/(float)n));}
                for(int y=0;y<n;y++)for(int x=0;x<n;x++){int a=start+y*(n+1)+x;tr.AddRange(new[]{a,a+1,a+n+2,a,a+n+2,a+n+1});}
            }
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(tr,0);mesh.RecalculateBounds();return mesh;
        });
        private static Mesh Torus(float radius,float tube)=>Store("Ring"+Mathf.RoundToInt(radius*1000)+"_"+Mathf.RoundToInt(tube*1000),()=>
        {
            const int n=40,k=10;var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
            for(int i=0;i<=n;i++)for(int j=0;j<=k;j++){float a=i*2*Mathf.PI/n,b=j*2*Mathf.PI/k;var normal=new Vector3(Mathf.Cos(a)*Mathf.Cos(b),Mathf.Sin(a)*Mathf.Cos(b),Mathf.Sin(b));v.Add(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0)+normal*tube);normals.Add(normal);uv.Add(new Vector2(i/(float)n,j/(float)k));}
            for(int i=0;i<n;i++)for(int j=0;j<k;j++){int a=i*(k+1)+j;tr.AddRange(new[]{a,a+k+1,a+k+2,a,a+k+2,a+1});}var m=new Mesh();m.SetVertices(v);m.SetNormals(normals);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateBounds();return m;
        });
        public static Mesh Surface(string name,int nx,int nz,Func<float,float,Vector3> sample)=>Store(name,()=>
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){float u=x/(float)nx,w=z/(float)nz;v.Add(sample(u,w));uv.Add(new Vector2(u,w));}
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int a=z*(nx+1)+x;tr.AddRange(new[]{a,a+nx+1,a+nx+2,a,a+nx+2,a+1});}var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        });
        public static Mesh Revolve(string name,Vector2[] profile,int sides=32)=>Store(name,()=>
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();for(int y=0;y<profile.Length;y++)for(int x=0;x<=sides;x++){float a=x*Mathf.PI*2/sides;v.Add(new Vector3(profile[y].x*Mathf.Cos(a),profile[y].y,profile[y].x*Mathf.Sin(a)));uv.Add(new Vector2(x/(float)sides,y/(float)(profile.Length-1)));}
            for(int y=0;y<profile.Length-1;y++)for(int x=0;x<sides;x++){int a=y*(sides+1)+x;tr.AddRange(new[]{a,a+sides+1,a+sides+2,a,a+sides+2,a+1});}var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();m.RecalculateBounds();return m;
        });
        public static void Label(Transform p,string name,string text,Vector3 at,float width,float height,Quaternion rotation)
        {var t=Node(p,name,at);t.localRotation=rotation;MotelLoopSceneSetup.Text(t,"Ink",text,width,height,new Color(.14f,.13f,.10f));}
    }
}