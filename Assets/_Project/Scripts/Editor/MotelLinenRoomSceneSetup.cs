using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
using static Motel.Editor.MotelLinenArt;
namespace Motel.Editor
{
    public static class MotelLinenRoomSceneSetup
    {
        public static readonly Vector3 Origin=new Vector3(3.6f,0,-4.56f);
        public static Vector3 World(float x,float y,float z)=>Origin+Quaternion.Euler(0,-90,0)*new Vector3(x,y,z);
        public static void Create(Transform root)
        {
            if(root.Find("Linen Room")!=null)throw new InvalidOperationException("Linen room already exists.");
            Prepare();var room=Node(root,"Linen Room");room.position=Origin;room.rotation=Quaternion.Euler(0,-90,0);
            Box(room,"Beige Tile Floor",new Vector3(3.12f,.009f,3.52f),new Vector3(6.07f,.018f,6.87f),"Tiles");
            Box(room,"Left Plaster",new Vector3(.09f,1.4f,3.52f),new Vector3(.015f,2.79f,6.87f),"Plaster");
            Box(room,"Right Plaster",new Vector3(6.15f,1.4f,3.52f),new Vector3(.015f,2.79f,6.87f),"Plaster");
            // Actual high window opening in both structural wall and interior finish.
            BackPanel(room,.08f,.875f,0,2.79f);BackPanel(room,2.125f,6.16f,0,2.79f);BackPanel(room,.875f,2.125f,0,1.85f);BackPanel(room,.875f,2.125f,2.45f,2.79f);
            Box(room,"Left Green Wainscot",new Vector3(.102f,.55f,3.52f),new Vector3(.018f,1.08f,6.86f),"Sage");
            Box(room,"Right Green Wainscot",new Vector3(6.138f,.55f,3.52f),new Vector3(.018f,1.08f,6.86f),"Sage");
            Box(room,"Back Green Wainscot",new Vector3(3.12f,.55f,6.938f),new Vector3(6.07f,1.08f,.018f),"Sage");
            Box(room,"Left Skirting",new Vector3(.125f,.07f,3.52f),new Vector3(.045f,.12f,6.86f),"OldWood");Box(room,"Right Skirting",new Vector3(6.115f,.07f,3.52f),new Vector3(.045f,.12f,6.86f),"OldWood");Box(room,"Back Skirting",new Vector3(3.12f,.07f,6.917f),new Vector3(6.07f,.12f,.045f),"OldWood");
            var old=root.Find("Ceilings/Linen Soffit");if(old!=null)Object.DestroyImmediate(old.gameObject);var ceiling=Node(root.Find("Ceilings"),"Linen Soffit");ceiling.position=Origin;ceiling.rotation=room.rotation;
            Box(ceiling,"Aged Ceiling",new Vector3(3.12f,2.782f,3.52f),new Vector3(6.08f,.025f,6.88f),"Plaster");
            var glass=root.Find("Windows/Linen Exterior Window 0 Glass");glass.GetComponent<Renderer>().sharedMaterial=Material("Frost");
            foreach(Transform t in root.Find("Windows"))if(t.name.StartsWith("Linen Exterior")&&!t.name.EndsWith("Glass"))t.GetComponent<Renderer>().sharedMaterial=Material("Steel");
            Box(room,"Window Sill",new Vector3(1.5f,1.825f,6.91f),new Vector3(1.40f,.055f,.22f),"Wood");
            Rack(room,"Near Towel Rack",1.82f,false);Rack(room,"Far Sheet Rack",4.43f,true);
            FoldingTable(room);UtilityCabinet(room);WashingMachine(room);UtilitySink(room);ShelfAndTools(room);Cart(room,"Dirty Linen Hamper",new Vector3(4.72f,0,1.42f),true);Cart(room,"Housekeeping Cart",new Vector3(5.70f,0,1.37f),false);
            for(int i=0;i<2;i++)
            {
                var fixture=Node(room,"Fluorescent Fixture "+i,new Vector3(3.12f,2.71f,2.1f+i*3.0f));
                Box(fixture,"Old Light Housing",Vector3.zero,new Vector3(1.65f,.07f,.22f),"Beige");
                foreach(float z in new[]{-.045f,.045f})Rod(fixture,"Tube",new Vector3(-.75f,-.055f,z),new Vector3(.75f,-.055f,z),.023f,"Tube");
                var light=Node(fixture,"Fluorescent Light",new Vector3(0,-.18f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=7;light.intensity=2.1f;light.color=new Color(1,.97f,.87f);light.shadows=LightShadows.Soft;light.shadowResolution=UnityEngine.Rendering.LightShadowResolution.Low;
                light.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            }
            AssetDatabase.SaveAssets();
        }
        private static void BackPanel(Transform p,float left,float right,float bottom,float top)=>Box(p,"Back Plaster",new Vector3((left+right)/2,(bottom+top)/2,6.95f),new Vector3(right-left,top-bottom,.015f),"Plaster");
        private static void Rack(Transform room,string name,float z,bool sheets)
        {
            var rack=Node(room,name,new Vector3(.51f,0,z));rack.localRotation=Quaternion.Euler(0,-90,0);const float width=2.25f,depth=.70f;
            Solid(rack,new Vector3(0,1.12f,0),new Vector3(width,2.24f,depth));
            foreach(float x in new[]{-1.09f,1.09f})foreach(float d in new[]{-.32f,.32f})
            {
                Box(rack,"Perforated Steel Upright",new Vector3(x,1.16f,d),new Vector3(.045f,2.32f,.045f),"Steel");
                for(int i=0;i<9;i++)Box(rack,"Upright Slot",new Vector3(x-.024f,.25f+i*.22f,d),new Vector3(.008f,.018f,.012f),"Rubber");
            }
            for(int level=0;level<5;level++)
            {
                float y=.20f+level*.46f;Box(rack,"Metal Shelf "+level,new Vector3(0,y,0),new Vector3(width,.035f,depth),"Steel");
                Box(rack,"Shelf Front Lip",new Vector3(0,y+.023f,-.34f),new Vector3(width,.045f,.02f),"Steel");
                if(level==0)
                {
                    foreach(float x in new[]{-.63f,.55f}){Box(rack,"Cardboard Box",new Vector3(x,.40f,0),new Vector3(.72f,.36f,.56f),"Cardboard");Box(rack,"Box Tape",new Vector3(x,.40f,-.286f),new Vector3(.075f,.35f,.008f),"Paper");}continue;
                }
                if(level==4)
                {foreach(float x in new[]{-.64f,.26f}){var pillow=Soft(rack,"Voluminous Pillow",new Vector3(x,y+.14f,0),new Vector3(.82f,.22f,.54f),"CleanWhite",true);pillow.localRotation=Quaternion.Euler(0,0,x<0?3:-2);}continue;}
                foreach(float x in new[]{-.66f,.12f,.78f})
                {
                    if(level==1&&!sheets){Stack(rack,"Folded Striped Blanket",new Vector3(x,y+.02f,0),3,new Vector3(.62f,.10f,.55f),"Blanket");}
                    else Stack(rack,sheets?"Folded Sheet":"Layered Towel",new Vector3(x,y+.02f,0),sheets?5:5,new Vector3(sheets?.65f:.58f,.065f,.53f),level==2?"CleanWhite":"Cream");
                }
            }
            var plate=Node(rack,"Shelf Label",new Vector3(-.2f,1.22f,-.365f));Box(plate,"Paper Strip",Vector3.zero,new Vector3(1.18f,.145f,.012f),"Paper");Label(plate,"Contents",sheets?"ПРОСТЫНИ":"ПОЛОТЕНЦА",new Vector3(0,0,-.01f),1.10f,.10f,Quaternion.identity);
        }
        private static void Stack(Transform p,string name,Vector3 at,int count,Vector3 size,string mat)
        {
            var pile=Node(p,name,at);
            for(int i=0;i<count;i++)
            {var layer=Soft(pile,"Fabric Layer "+i,new Vector3(Mathf.Sin(i*2.1f)*.012f,(i+.5f)*size.y,Mathf.Cos(i*1.7f)*.008f),new Vector3(size.x*(1+Mathf.Sin(i)*.025f),size.y,size.z),mat);layer.localRotation=Quaternion.Euler(0,Mathf.Sin(i)*1.8f,Mathf.Cos(i)*.45f);}
        }
        private static void FoldingTable(Transform room)
        {
            var table=Node(room,"Folding Table",new Vector3(3.25f,0,6.40f));Solid(table,new Vector3(0,.43f,0),new Vector3(2.6f,.86f,.85f));
            Soft(table,"Pale Worn Tabletop",new Vector3(0,.865f,0),new Vector3(2.6f,.065f,.85f),"PaleTop");
            foreach(float x in new[]{-1.15f,1.15f})foreach(float z in new[]{-.33f,.33f})Box(table,"Simple Leg",new Vector3(x,.425f,z),new Vector3(.055f,.85f,.055f),"OldWood");
            foreach(float x in new[]{-1.15f,1.15f})Rod(table,"Diagonal Brace",new Vector3(x,.40f,-.33f),new Vector3(x-.30f*Mathf.Sign(x),.82f,-.33f),.018f,"OldWood");
            var sheet=Surface("PartlyFoldedSheet",16,12,(u,v)=>new Vector3((u-.5f)*1.12f,.015f+Mathf.Sin(u*17+v*9)*.007f+Mathf.Max(0,u-.68f)*.17f,(v-.5f)*.58f));
            MeshPart(table,"Partially Folded Clean Sheet",new Vector3(-.50f,.905f,-.04f),sheet,"CleanWhite");
            MeshPart(table,"Folded Back Sheet Flap",new Vector3(-.50f,.93f,-.04f),Surface("SheetFold",10,10,(u,v)=>new Vector3(.48f-u*.38f,.025f+Mathf.Sin(u*Mathf.PI)*.06f,(v-.5f)*.57f)),"CleanWhite");
            Stack(table,"Folded Towels on Table",new Vector3(.43f,.9f,.02f),4,new Vector3(.48f,.06f,.41f),"CleanWhite");
            var book=Node(table,"Closed Notebook",new Vector3(1.0f,.918f,-.15f));Box(book,"Paper Block",Vector3.zero,new Vector3(.30f,.026f,.22f),"Paper");foreach(float y in new[]{-.018f,.018f})Box(book,"Notebook Cover",new Vector3(0,y,0),new Vector3(.32f,.012f,.24f),"Notebook");book.localRotation=Quaternion.Euler(0,-8,0);
            var board=Node(room,"Notice Board",new Vector3(3.5f,1.70f,6.918f));Box(board,"Cork",Vector3.zero,new Vector3(1.1f,.68f,.035f),"Cork");
            foreach(float x in new[]{-.55f,.55f})Box(board,"Wood Side",new Vector3(x,0,-.02f),new Vector3(.035f,.72f,.035f),"Wood");foreach(float y in new[]{-.34f,.34f})Box(board,"Wood Edge",new Vector3(0,y,-.02f),new Vector3(1.13f,.035f,.035f),"Wood");
            foreach(var item in new[]{new Vector3(-.29f,.08f,-.025f),new Vector3(.32f,-.13f,-.025f)})
            {var note=Node(board,"Pinned Paper",item);note.localRotation=Quaternion.Euler(0,0,item.x<0?-3:4);Box(note,"Sheet",Vector3.zero,new Vector3(.23f,.30f,.007f),"Paper");Ball(note,"Pin",new Vector3(0,.12f,-.009f),Vector3.one*.014f,"Red");for(int i=0;i<5;i++)Box(note,"Writing",new Vector3(0,.06f-i*.035f,-.007f),new Vector3(.16f,.003f,.003f),"Wood");}
        }
        private static void UtilityCabinet(Transform room)
        {
            var c=Node(room,"Beige Utility Cabinet",new Vector3(5.54f,0,6.58f));Solid(c,new Vector3(0,1.07f,0),new Vector3(1.13f,2.14f,.64f));
            Soft(c,"Cabinet Body",new Vector3(0,1.08f,0),new Vector3(1.13f,2.08f,.64f),"Beige");Box(c,"Recessed Plinth",new Vector3(0,.07f,0),new Vector3(1.04f,.14f,.57f),"OldWood");
            foreach(int side in new[]{-1,1})
            {Soft(c,"Separate Door "+side,new Vector3(side*.275f,1.09f,-.337f),new Vector3(.537f,1.98f,.033f),"Beige");Box(c,"Long Handle",new Vector3(side*.045f,1.02f,-.37f),new Vector3(.018f,.20f,.035f),"Steel");foreach(float y in new[]{.22f,1.96f})Box(c,"Door Hinge",new Vector3(side*.53f,y,-.35f),new Vector3(.018f,.08f,.025f),"Steel");}
        }
        private static void WashingMachine(Transform room)
        {
            var wm=Node(room,"Old Front Loading Washer",new Vector3(5.79f,0,4.77f));wm.localRotation=Quaternion.Euler(0,90,0);Solid(wm,new Vector3(0,.455f,0),new Vector3(.72f,.91f,.69f));
            Soft(wm,"Aged Plastic Housing",new Vector3(0,.46f,0),new Vector3(.70f,.85f,.67f),"PlasticWhite");Soft(wm,"Thick Top",new Vector3(0,.90f,0),new Vector3(.73f,.045f,.70f),"Beige");
            Box(wm,"Inset Control Panel",new Vector3(0,.79f,-.341f),new Vector3(.62f,.13f,.014f),"Beige");
            Ring(wm,"Selector Dial Ring",new Vector3(.20f,.79f,-.36f),.045f,.007f,"Steel",Quaternion.identity);Ball(wm,"Selector Dial",new Vector3(.20f,.79f,-.368f),new Vector3(.075f,.075f,.035f),"PlasticWhite");Box(wm,"Dial Pointer",new Vector3(.20f,.817f,-.39f),new Vector3(.006f,.018f,.008f),"Rubber");
            for(int i=0;i<2;i++)Box(wm,"Mechanical Button",new Vector3(-.19f+i*.11f,.79f,-.36f),new Vector3(.065f,.04f,.02f),"PlasticWhite");
            Box(wm,"Detergent Drawer",new Vector3(-.17f,.80f,-.349f),new Vector3(.21f,.10f,.02f),"PlasticWhite");Box(wm,"Drawer Finger Recess",new Vector3(-.17f,.775f,-.364f),new Vector3(.13f,.02f,.008f),"Steel");
            Ring(wm,"Recessed Black Door Gasket",new Vector3(0,.43f,-.345f),.214f,.025f,"Rubber",Quaternion.identity);Ring(wm,"Circular Hatch Rim",new Vector3(0,.43f,-.369f),.226f,.026f,"Steel",Quaternion.identity);
            Ball(wm,"Dark Drum Interior",new Vector3(0,.43f,-.348f),new Vector3(.39f,.39f,.018f),"Rubber");Ball(wm,"Convex Hatch Glass",new Vector3(0,.43f,-.375f),new Vector3(.37f,.37f,.045f),"DoorGlass");
            Box(wm,"Hatch Handle",new Vector3(.23f,.43f,-.405f),new Vector3(.033f,.11f,.035f),"PlasticWhite");Box(wm,"Bottom Service Strip",new Vector3(0,.10f,-.345f),new Vector3(.62f,.08f,.02f),"Beige");
            foreach(float x in new[]{-.27f,.27f})foreach(float z in new[]{-.25f,.25f})Box(wm,"Rubber Foot",new Vector3(x,.025f,z),new Vector3(.065f,.05f,.065f),"Rubber");
        }
        private static void UtilitySink(Transform room)
        {
            var sink=Node(room,"Deep Utility Sink",new Vector3(5.73f,0,3.77f));sink.localRotation=Quaternion.Euler(0,90,0);Solid(sink,new Vector3(0,.76f,0),new Vector3(.82f,.39f,.66f));
            var mesh=Store("RoundedDeepBasin",()=>
            {
                var vertices=new List<Vector3>();var triangles=new List<int>();var uv=new List<Vector2>();
                Vector3[] loop(float width,float depth,float y,float radius)
                {
                    var points=new List<Vector3>();Vector2[] c={new Vector2(-width/2+radius,-depth/2+radius),new Vector2(width/2-radius,-depth/2+radius),new Vector2(width/2-radius,depth/2-radius),new Vector2(-width/2+radius,depth/2-radius)};
                    for(int corner=0;corner<4;corner++)for(int j=0;j<=3;j++){float a=(180+corner*90+j*30)*Mathf.Deg2Rad;points.Add(new Vector3(c[corner].x+radius*Mathf.Cos(a),y,c[corner].y+radius*Mathf.Sin(a)));}return points.ToArray();
                }
                var outer=loop(.82f,.66f,.96f,.07f);var inner=loop(.71f,.55f,.945f,.055f);var bottom=loop(.56f,.40f,.70f,.055f);var low=loop(.69f,.53f,.65f,.06f);
                Action<Vector3,Vector3,Vector3,Vector3> q=(a,b,c,d)=>{int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});uv.AddRange(new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up});triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});};
                for(int i=0;i<outer.Length;i++){int j=(i+1)%outer.Length;q(outer[i],inner[i],inner[j],outer[j]);q(inner[i],bottom[i],bottom[j],inner[j]);q(outer[j],low[j],low[i],outer[i]);int n=vertices.Count;vertices.AddRange(new[]{new Vector3(0,.70f,0),bottom[j],bottom[i]});uv.AddRange(new[]{new Vector2(.5f,.5f),Vector2.zero,Vector2.one});triangles.AddRange(new[]{n,n+1,n+2});}
                var m=new Mesh();m.SetVertices(vertices);m.SetUVs(0,uv);m.SetTriangles(triangles,0);m.RecalculateNormals();m.RecalculateBounds();return m;
            });
            MeshPart(sink,"Visible Recessed Basin",Vector3.zero,mesh,"PlasticWhite");Ring(sink,"Drain Flange",new Vector3(0,.704f,0),.032f,.006f,"Steel",Quaternion.Euler(90,0,0));Ball(sink,"Dark Drain",new Vector3(0,.703f,0),new Vector3(.050f,.003f,.050f),"Rubber");
            foreach(float x in new[]{-.30f,.30f}){Rod(sink,"Wall Bracket",new Vector3(x,.61f,.32f),new Vector3(x,.82f,-.13f),.016f,"Steel");}
            Rod(sink,"Drain Drop",new Vector3(0,.65f,0),new Vector3(0,.38f,0),.033f,"Steel");Rod(sink,"Trap Lower",new Vector3(0,.38f,0),new Vector3(0,.30f,.10f),.033f,"Steel");Rod(sink,"Trap Return",new Vector3(0,.30f,.10f),new Vector3(0,.42f,.19f),.033f,"Steel");Rod(sink,"Drain to Wall",new Vector3(0,.42f,.19f),new Vector3(0,.42f,.42f),.033f,"Steel");
            Rod(sink,"Mixer Body",new Vector3(-.13f,1.20f,.38f),new Vector3(.13f,1.20f,.38f),.032f,"Chrome");Rod(sink,"Spout Out",new Vector3(0,1.20f,.38f),new Vector3(0,1.20f,.13f),.023f,"Chrome");Rod(sink,"Spout Down",new Vector3(0,1.20f,.13f),new Vector3(0,1.12f,.08f),.023f,"Chrome");
            foreach(int side in new[]{-1,1}){Rod(sink,"Wall Water Feed",new Vector3(side*.14f,1.20f,.44f),new Vector3(side*.14f,1.20f,.33f),.023f,"Chrome");Ring(sink,"Tap Wheel",new Vector3(side*.14f,1.26f,.33f),.035f,.009f,side<0?"Red":"PlasticBlue",Quaternion.Euler(90,0,0));}
        }
        private static void ShelfAndTools(Transform room)
        {
            var shelf=Node(room,"Detergent Wall Shelf",new Vector3(5.99f,1.82f,4.17f));Box(shelf,"Shelf Board",Vector3.zero,new Vector3(.30f,.055f,1.90f),"Wood");
            foreach(float z in new[]{-.73f,.73f}){Rod(shelf,"Steel Bracket",new Vector3(.11f,-.30f,z),new Vector3(-.13f,-.025f,z),.012f,"Steel");}
            for(int i=0;i<3;i++)
            {var bottle=Node(shelf,"Detergent Bottle "+i,new Vector3(-.025f,.045f,-.53f+i*.47f));Soft(bottle,"Plastic Jug",new Vector3(0,.14f,0),new Vector3(.13f,.27f,.17f),i==1?"PlasticBlue":"PlasticWhite");Rod(bottle,"Neck",new Vector3(0,.26f,0),new Vector3(0,.31f,0),.025f,"PlasticWhite");Rod(bottle,"Colored Cap",new Vector3(0,.31f,0),new Vector3(0,.345f,0),.031f,i==2?"Red":"PlasticBlue");Box(bottle,"Bottle Label",new Vector3(-.070f,.15f,0),new Vector3(.008f,.13f,.12f),"Paper");}
            var tools=Node(room,"Mop and Broom",new Vector3(6.08f,0,2.61f));Box(tools,"Wood Hook Rail",new Vector3(0,1.94f,0),new Vector3(.08f,.15f,.78f),"OldWood");
            for(int i=0;i<3;i++){Rod(tools,"Hook Stem",new Vector3(-.05f,1.96f,-.26f+i*.26f),new Vector3(-.12f,2.0f,-.26f+i*.26f),.011f,"Steel");}
            Rod(tools,"Mop Handle",new Vector3(-.09f,.72f,-.20f),new Vector3(-.09f,1.96f,-.20f),.016f,"OldWood");
            for(int i=0;i<14;i++){float a=i*2*Mathf.PI/14;Vector3 start=new Vector3(-.09f,.72f,-.20f);Vector3 middle=start+new Vector3(Mathf.Cos(a)*.075f,-.20f,Mathf.Sin(a)*.075f);Rod(tools,"Mop Cotton String",start,middle,.010f,"Cream");Rod(tools,"Mop Cotton End",middle,middle+new Vector3(.02f*Mathf.Cos(a),-.14f,.02f*Mathf.Sin(a)),.009f,"Cream");}
            Rod(tools,"Broom Handle",new Vector3(-.09f,.65f,.22f),new Vector3(-.09f,1.96f,.22f),.015f,"OldWood");
            for(int i=0;i<13;i++){float f=(i-6)/6f;Rod(tools,"Broom Bristles",new Vector3(-.09f,.67f,.22f+f*.04f),new Vector3(-.10f,.39f,.22f+f*.12f),.011f,"Straw");}Box(tools,"Broom Binding",new Vector3(-.09f,.61f,.22f),new Vector3(.055f,.035f,.13f),"OldWood");
        }
        private static void Cart(Transform room,string name,Vector3 at,bool hamper)
        {
            var cart=Node(room,name,at);float width=hamper?.83f:.67f,depth=hamper?.82f:1.02f;Solid(cart,new Vector3(0,.52f,0),new Vector3(width,1.04f,depth));
            foreach(float x in new[]{-width/2+.04f,width/2-.04f})foreach(float z in new[]{-depth/2+.04f,depth/2-.04f})
            {
                Rod(cart,"Frame Upright",new Vector3(x,.14f,z),new Vector3(x,.98f,z),.015f,"Steel");
                Box(cart,"Caster Fork",new Vector3(x,.105f,z),new Vector3(.045f,.10f,.04f),"Steel");Ring(cart,"Rubber Caster",new Vector3(x,.066f,z),.052f,.018f,"Rubber",Quaternion.Euler(0,90,0));
            }
            foreach(float y in new[]{.18f,.98f})foreach(int side in new[]{-1,1}){Rod(cart,"Frame Cross Rail",new Vector3(-width/2,y,side*depth/2),new Vector3(width/2,y,side*depth/2),.016f,"Steel");Rod(cart,"Frame Side Rail",new Vector3(side*width/2,y,-depth/2),new Vector3(side*width/2,y,depth/2),.016f,"Steel");}
            foreach(float x in new[]{-width/2,width/2})Rod(cart,"Handle Rise",new Vector3(x,.93f,depth/2),new Vector3(x,1.17f,depth/2-.05f),.017f,"Steel");Rod(cart,"Push Handle",new Vector3(-width/2,1.17f,depth/2-.05f),new Vector3(width/2,1.17f,depth/2-.05f),.020f,"Steel");
            if(hamper)
            {
                for(int side=0;side<4;side++)
                {
                    int captured=side;var mesh=Surface("SaggingCanvasSide"+side,18,12,(u,v)=>{float sag=.12f*Mathf.Sin(u*Mathf.PI),height=.18f+v*(.80f-sag),wrinkle=.012f*Mathf.Sin(u*30+v*9)*Mathf.Sin(v*Mathf.PI);float q=(u-.5f),w=.75f*(.88f+.12f*v);return captured==0?new Vector3(q*w,height,-.375f+wrinkle):captured==1?new Vector3(.375f+wrinkle,height,q*w):captured==2?new Vector3(-q*w,height,.375f+wrinkle):new Vector3(-.375f+wrinkle,height,-q*w);});MeshPart(cart,"Hanging Canvas Bag "+side,Vector3.zero,mesh,"Canvas");
                }
                MeshPart(cart,"Crumpled Loose Sheet",new Vector3(0,.85f,0),Surface("HamperCrumpledSheet",22,22,(u,v)=>new Vector3((u-.5f)*.58f,.035f*Mathf.Sin(u*18+v*7)+.028f*Mathf.Sin(v*23-u*6),(v-.5f)*.58f)),"CleanWhite");
                MeshPart(cart,"Turned Sheet Fold",new Vector3(0,.91f,.05f),Surface("HamperTurnedFold",14,12,(u,v)=>new Vector3((u-.5f)*.48f,.025f*Mathf.Sin(u*17)+.04f*Mathf.Sin(v*Mathf.PI),(v-.5f)*.38f)),"Cream");
                foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})Rod(cart,"Canvas Attachment",new Vector3(sx*.375f,.98f,sz*.375f),new Vector3(sx*width/2,.98f,sz*depth/2),.012f,"Canvas");
            }
            else
            {
                foreach(float y in new[]{.20f,.93f})Box(cart,"Metal Tray",new Vector3(0,y,0),new Vector3(width,.035f,depth),"Steel");Stack(cart,"Cart Towels",new Vector3(0,.955f,-.13f),3,new Vector3(.56f,.065f,.63f),"CleanWhite");
                var bucket=Node(cart,"Deep Cleaning Bucket",new Vector3(0,.22f,.15f));MeshPart(bucket,"Hollow Bucket",Vector3.zero,Revolve("Bucket",new[]{new Vector2(0,0),new Vector2(.13f,0),new Vector2(.17f,.29f),new Vector2(.155f,.29f),new Vector2(.12f,.025f),new Vector2(0,.025f)}),"PlasticBlue");Ring(bucket,"Bucket Rim",new Vector3(0,.29f,0),.166f,.012f,"Steel",Quaternion.Euler(90,0,0));
            }
        }
    }
}