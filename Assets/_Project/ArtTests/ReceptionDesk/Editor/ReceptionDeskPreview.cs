using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace Motel.ArtTests
{
    public static class ReceptionDeskPreview
    {
        [MenuItem("Motel/Art Tests/Render Reception Reference Views")]
        public static void Render()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Exit Play Mode before rendering reference views.");
            string output="Docs/Images/ReceptionDesk";Directory.CreateDirectory(output);
            Scene preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var desk=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ReceptionDeskStudy.Folder+"/Prefabs/ReceptionDesk.prefab"),preview);
                var chair=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ReceptionDeskStudy.Folder+"/Prefabs/OfficeChair.prefab"),preview);
                chair.transform.SetPositionAndRotation(new Vector3(-.40f,0,-.90f),Quaternion.Euler(0,-25,0));
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,preview);
                floor.transform.position=new Vector3(0,-.07f,0);floor.transform.localScale=new Vector3(200,.1f,200);
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.28f,.245f,.21f));mat.SetFloat("_Smoothness",0);
                floor.GetComponent<Renderer>().sharedMaterial=mat;
                Light(preview,"Preview key",new Vector3(50,-35,0),1.3f,new Color(1,.92f,.81f),true);
                Light(preview,"Preview fill",new Vector3(35,145,0),.65f,new Color(.85f,.90f,1),false);
                var node=new GameObject("Preview camera");SceneManager.MoveGameObjectToScene(node,preview);
                var cam=node.AddComponent<Camera>();cam.scene=preview;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.22f,.20f,.18f);
                cam.nearClipPlane=.05f;cam.farClipPlane=250;cam.fieldOfView=34;
                View(cam,new Vector3(-1.55f,2.15f,-4.9f),new Vector3(0,.86f,-.10f),output+"/Administrator.png");
                View(cam,new Vector3(.65f,1.9f,5.0f),new Vector3(0,.88f,-.05f),output+"/Guest.png");
                View(cam,new Vector3(3.15f,3.1f,-4.05f),new Vector3(0,.78f,-.12f),output+"/SideAbove.png");
                Object.DestroyImmediate(mat);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
        }
        private static void Light(Scene scene,string name,Vector3 rotation,float intensity,Color color,bool shadows)
        {
            var g=new GameObject(name);SceneManager.MoveGameObjectToScene(g,scene);g.transform.rotation=Quaternion.Euler(rotation);
            var l=g.AddComponent<Light>();l.type=LightType.Directional;l.intensity=intensity;l.color=color;l.shadows=shadows?LightShadows.Soft:LightShadows.None;
        }
        private static void View(Camera camera,Vector3 from,Vector3 to,string path)
        {
            camera.transform.position=from;camera.transform.LookAt(to);
            var target=new RenderTexture(1600,1200,24){antiAliasing=4};target.Create();
            var pixels=new Texture2D(1600,1200,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,1200),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(pixels);}
        }
    }
}
