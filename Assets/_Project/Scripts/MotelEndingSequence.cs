using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Motel
{
    // Disposable scene-local presentation. No new scene, save migration or gameplay prerequisite.
    [DisallowMultipleComponent]
    public sealed class MotelEndingSequence : MonoBehaviour
    {
        public enum Phase { NotStarted, Corridor, Rescuers, HospitalTransition, Hospital, HandAccepted, Complete }
        public Phase CurrentPhase { get; private set; }
        public int StartCount { get; private set; }
        public int HandPressCount { get; private set; }
        public Camera EndingCamera { get; private set; }
        public Vector3 RescuerStart { get; private set; }
        public Transform FirstRescuer { get; private set; }
        private readonly List<Object> resources = new List<Object>();
        private Transform presentation, hospital;
        private AudioSource audioSource;
        private float fade;
        private string caption;
        private GUIStyle heading, body, action;

        public IEnumerator Play(Camera original)
        {
            if (CurrentPhase != Phase.NotStarted) yield break;
            StartCount++;
            presentation = new GameObject("Ending Presentation").transform;
            presentation.SetParent(transform, false);
            EndingCamera = Child(presentation, "Ending Camera", Vector3.zero).gameObject.AddComponent<Camera>();
            EndingCamera.CopyFrom(original); EndingCamera.targetTexture = null;
            EndingCamera.enabled = true; EndingCamera.tag = "MainCamera";
            original.enabled = false;
            var previousListener = original.GetComponent<AudioListener>();
            if (previousListener != null) previousListener.enabled = false;
            EndingCamera.gameObject.AddComponent<AudioListener>();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            audioSource = presentation.gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0; audioSource.volume = 0.18f;
            audioSource.PlayOneShot(Tone("Explosion ringing", 880, 0.85f, false));

            var dark = Material("Debris", new Color(.13f,.11f,.09f));
            var dust = Material("Damaged plaster", new Color(.34f,.29f,.23f));
            Vector3 corridorOffset = Vector3.forward * MotelSpatialLayout.OffsetFor(this);
            var corridor = Child(presentation, "Damaged Corridor", corridorOffset);
            // Existing corridor walls/floor/ceiling remain visible. Only damage props are temporary.
            Box(corridor, "Fallen ceiling slab", new Vector3(10.9f,.13f,.65f), new Vector3(.9f,.22f,.43f), dust).localRotation=Quaternion.Euler(0,17,0);
            for(int i=0;i<5;i++)
                Box(corridor,"Small debris "+i,new Vector3(10.1f+i*.36f,.09f,-.65f+(i%2)*.26f),new Vector3(.23f,.16f,.18f),dark).localRotation=Quaternion.Euler(i*11,23*i,5);
            Box(corridor,"Scorched wall",new Vector3(11.2f,1.35f,1.115f),new Vector3(1.5f,1.7f,.015f),dark);
            Light(corridor,"Rescue light",new Vector3(11,2.2f,-.35f),new Color(1,.76f,.44f),2.5f,7);
            EndingCamera.transform.SetPositionAndRotation(new Vector3(12.7f,.62f,0)+corridorOffset,Quaternion.LookRotation(new Vector3(-1,.11f,0))*Quaternion.Euler(0,0,13));
            EndingCamera.fieldOfView = 62;
            CurrentPhase=Phase.Corridor;caption="Марина снаружи: «Он остался внутри!»";
            fade=1;
            for(float t=0;t<1.7f;t+=Time.unscaledDeltaTime){fade=Mathf.Clamp01(1-t/.6f);yield return null;}
            fade=0;
            var uniform=Material("Rescuer uniform",new Color(.18f,.25f,.27f));
            var helmet=Material("Rescuer helmet",new Color(.88f,.65f,.13f));
            FirstRescuer=Person(presentation,"Rescuer",new Vector3(6.8f,0,.38f)+corridorOffset,uniform,helmet);
            Transform second=Person(presentation,"Second rescuer",new Vector3(6.3f,0,-.48f)+corridorOffset,uniform,helmet);
            RescuerStart=FirstRescuer.position;
            CurrentPhase=Phase.Rescuers;caption="Спасатель: «Здесь человек! Живой! Слышите меня? Не закрывайте глаза».";
            for(float t=0;t<4.2f;t+=Time.unscaledDeltaTime)
            {
                float u=Mathf.Clamp01(t/4.2f);
                FirstRescuer.position=Vector3.Lerp(RescuerStart,new Vector3(11.5f,0,.38f)+corridorOffset,u);
                second.position=Vector3.Lerp(new Vector3(6.3f,0,-.48f)+corridorOffset,new Vector3(10.5f,0,-.48f)+corridorOffset,u);
                yield return null;
            }
            CurrentPhase=Phase.HospitalTransition;caption="";
            for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){fade=Mathf.Clamp01(t/.8f);yield return null;}
            fade=1;
            CreateHospital();
            corridor.gameObject.SetActive(false);FirstRescuer.gameObject.SetActive(false);second.gameObject.SetActive(false);
            audioSource.clip=Tone("Hospital monitor",620,1.4f,true);audioSource.loop=true;audioSource.Play();
            EndingCamera.transform.position=new Vector3(0,-48.95f,-.6f);
            EndingCamera.transform.LookAt(new Vector3(.95f,-48.55f,.55f));
            EndingCamera.fieldOfView=58;
            caption="Мама: «Ты меня слышишь? Ты в больнице после взрыва. Пожалуйста, сожми мою руку».";
            for(float t=0;t<.8f;t+=Time.unscaledDeltaTime){fade=1-Mathf.Clamp01(t/.8f);yield return null;}
            fade=0;CurrentPhase=Phase.Hospital;
            // No deadline, failure state, inventory or extra conversation in this prototype.
            while(CurrentPhase==Phase.Hospital)yield return null;
            caption="Мама: «Я почувствовала. Ты вернулся. Спасибо».";
            for(float t=0;t<1.2f;t+=Time.unscaledDeltaTime){fade=Mathf.Clamp01((t-.4f)/.8f);yield return null;}
            fade=1;audioSource.Stop();caption="";CurrentPhase=Phase.Complete;
        }

        public bool PressHand()
        {
            if(CurrentPhase!=Phase.Hospital)return false;
            HandPressCount++;CurrentPhase=Phase.HandAccepted;return true;
        }
        private void Update()
        {
            if(CurrentPhase==Phase.Hospital && Keyboard.current!=null && Keyboard.current.eKey.wasPressedThisFrame)PressHand();
        }
        private void CreateHospital()
        {
            hospital=Child(presentation,"Hospital placeholder",new Vector3(0,-50,0));
            var white=Material("Hospital white",new Color(.81f,.84f,.82f));
            var blue=Material("Hospital bedding",new Color(.32f,.48f,.57f));
            var skin=Material("Mother placeholder",new Color(.61f,.43f,.32f));
            var clothes=Material("Mother clothes",new Color(.43f,.28f,.26f));
            Box(hospital,"Floor",new Vector3(0,-.08f,0),new Vector3(5,.16f,5),white);
            Box(hospital,"Ceiling",new Vector3(0,2.85f,0),new Vector3(5,.15f,5),white);
            Box(hospital,"Back wall",new Vector3(0,1.4f,2.5f),new Vector3(5,2.8f,.15f),white);
            Box(hospital,"Front wall",new Vector3(0,1.4f,-2.5f),new Vector3(5,2.8f,.15f),white);
            Box(hospital,"Left wall",new Vector3(-2.5f,1.4f,0),new Vector3(.15f,2.8f,5),white);
            Box(hospital,"Right wall",new Vector3(2.5f,1.4f,0),new Vector3(.15f,2.8f,5),white);
            Box(hospital,"Bed",new Vector3(0,.53f,.3f),new Vector3(.95f,.3f,2.2f),white);
            Box(hospital,"Blanket",new Vector3(0,.72f,.7f),new Vector3(.94f,.10f,1.2f),blue);
            Person(hospital,"Mother",new Vector3(1.0f,0,.55f),clothes,skin);
            Box(hospital,"Mother arm",new Vector3(.65f,.88f,.12f),new Vector3(.6f,.12f,.14f),skin).localRotation=Quaternion.Euler(0,25,-12);
            Box(hospital,"Offered hand",new Vector3(.4f,.82f,-.08f),new Vector3(.18f,.09f,.18f),skin);
            Box(hospital,"Monitor",new Vector3(-1.15f,1,.55f),new Vector3(.47f,.34f,.14f),blue);
            Light(hospital,"Hospital light",new Vector3(0,2.45f,0),new Color(.85f,.94f,1),3.0f,7);
        }
        private Material Material(string name,Color color)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.1f);resources.Add(m);return m;}
        private static Transform Child(Transform p,string name,Vector3 at)
        {var t=new GameObject(name).transform;t.SetParent(p,false);t.localPosition=at;return t;}
        private static Transform Box(Transform p,string name,Vector3 at,Vector3 size,Material mat)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;Destroy(g.GetComponent<Collider>());return g.transform;}
        private static Transform Person(Transform parent,string name,Vector3 at,Material body,Material head)
        {
            var root=Child(parent,name,at);
            var torso=GameObject.CreatePrimitive(PrimitiveType.Capsule);torso.name="Body";torso.transform.SetParent(root,false);torso.transform.localPosition=new Vector3(0,.83f,0);torso.transform.localScale=new Vector3(.42f,.65f,.42f);torso.GetComponent<Renderer>().sharedMaterial=body;Destroy(torso.GetComponent<Collider>());
            var face=GameObject.CreatePrimitive(PrimitiveType.Sphere);face.name="Head";face.transform.SetParent(root,false);face.transform.localPosition=new Vector3(0,1.55f,0);face.transform.localScale=Vector3.one*.33f;face.GetComponent<Renderer>().sharedMaterial=head;Destroy(face.GetComponent<Collider>());return root;
        }
        private static void Light(Transform parent,string name,Vector3 at,Color color,float intensity,float range)
        {var light=Child(parent,name,at).gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;}
        private AudioClip Tone(string name,float frequency,float length,bool pulse)
        {
            const int rate=22050;var data=new float[Mathf.CeilToInt(rate*length)];
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;float envelope=pulse?(t<.10f?Mathf.Sin(Mathf.PI*t/.10f):0):Mathf.Exp(-t*5);data[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*.35f;}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);resources.Add(clip);return clip;
        }
        private void OnGUI()
        {
            if(CurrentPhase==Phase.NotStarted)return;
            if(heading==null)
            {heading=new GUIStyle(GUI.skin.label){fontSize=30,alignment=TextAnchor.MiddleCenter};body=new GUIStyle(GUI.skin.label){fontSize=21,alignment=TextAnchor.MiddleCenter,wordWrap=true};action=new GUIStyle(body){fontStyle=FontStyle.Bold};heading.normal.textColor=body.normal.textColor=action.normal.textColor=Color.white;}
            Color before=GUI.color;GUI.color=new Color(0,0,0,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=before;
            if(CurrentPhase==Phase.Complete){GUI.Label(new Rect(12,Screen.height*.34f,Screen.width-24,70),"MOTEL · КОНЕЦ",heading);GUI.Label(new Rect(20,Screen.height*.5f,Screen.width-40,80),"Все четверо добрались до места сбора.\nСпасибо за игру.",body);return;}
            if(!string.IsNullOrEmpty(caption))
            {GUI.color=new Color(0,0,0,.78f);GUI.DrawTexture(new Rect(12,Screen.height-150,Screen.width-24,138),Texture2D.whiteTexture);GUI.color=before;GUI.Label(new Rect(28,Screen.height-140,Screen.width-56,85),caption,body);}
            if(CurrentPhase==Phase.Hospital)GUI.Label(new Rect(20,Screen.height-55,Screen.width-40,40),"E — сжать руку",action);
        }
        private void OnDestroy(){foreach(var resource in resources)if(resource!=null)Destroy(resource);}
    }
}
