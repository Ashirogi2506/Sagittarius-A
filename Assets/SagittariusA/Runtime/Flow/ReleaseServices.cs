using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using SagittariusA.Gameplay.Ganymede;
using SagittariusA.Gameplay.Mars;

namespace SagittariusA.Gameplay.Flow
{
    // One owner for presentation settings, original audio and skippable transition films.
    public sealed class ReleaseServices : MonoBehaviour
    {
        public static ReleaseServices Instance { get; private set; }
        public static bool Modal => Instance != null && Instance.overlay != null;
        public static float Sensitivity => PlayerPrefs.GetFloat("FarSignal.Look",1);
        private AudioSource music,sfx;
        private AudioClip click,step,jump,success;
        private GameObject overlay;
        private float oldTime, nextScan, nextStep;
        private CursorLockMode oldLock;
        private bool oldVisible;
        private readonly HashSet<Button> bound=new HashSet<Button>();
        private static readonly Color Ink=new Color(.025f,.04f,.065f,.99f),Orange=new Color(1,.43f,.04f),Muted=new Color(.68f,.75f,.82f);
        private static readonly string[] Ids={"earth","moon","mars","ganymede","dust","practice","photo","graph"};
        private static readonly string[] Titles={"LIFTOFF","POLE-7 RESTORED","ORBITER ENGINEER","FIRST SIGNAL HOME","DUST DIARY","PRIORITIES","SURFACE PHOTOGRAPHER","FULL SCIENCE RETURN"};
        private static readonly string[] Descriptions={"Launch your packed ship from Earth.","Restore Pole-7 and return to the lander.","Test and secure the Mars orbiter.","Receive Earth acknowledgement and open the final log.","Complete the optional Mars dust comparison.","Complete Mars packet-priority practice.","Save the Ganymede surface photograph.","Deliver the full science graph to Earth."};
        private void Awake()
        {
            Instance=this;music=gameObject.AddComponent<AudioSource>();sfx=gameObject.AddComponent<AudioSource>();music.loop=true;music.spatialBlend=sfx.spatialBlend=0;
            click=Tone("UI select",.08f,580,.16f);step=Tone("Suit step",.12f,72,.35f);jump=Tone("Suit movement",.22f,110,.17f);success=Tone("Receipt",.5f,440,.14f);
            music.clip=Theme();music.Play();ApplySettings();
        }
        private void Start(){Screen.fullScreen=PlayerPrefs.GetInt("FarSignal.Fullscreen",1)==1;}
        private void Update()
        {
            if(Keyboard.current?.f1Key.wasPressedThisFrame==true&&overlay==null)Open("SETTINGS");
            if(Time.unscaledTime>nextScan){nextScan=Time.unscaledTime+1;BindButtons();CheckAchievements();}
            if(overlay!=null||Cursor.lockState!=CursorLockMode.Locked)return;
            var k=Keyboard.current;if(k==null)return;
            if(k.spaceKey.wasPressedThisFrame)sfx.PlayOneShot(jump);
            var cc=FindObjectsByType<CharacterController>(FindObjectsInactive.Exclude).FirstOrDefault(c=>c.enabled&&c.isGrounded&&c.velocity.sqrMagnitude>.3f);
            if(cc!=null&&Time.unscaledTime>nextStep){nextStep=Time.unscaledTime+.48f;sfx.pitch=UnityEngine.Random.Range(.92f,1.08f);sfx.PlayOneShot(step);}
        }
        private void BindButtons(){bound.RemoveWhere(b=>b==null);foreach(var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude))if(bound.Add(b))b.onClick.AddListener(()=>{if(sfx!=null){sfx.pitch=1;sfx.PlayOneShot(click);}});}
        private AudioClip Tone(string name,float seconds,float hz,float volume)
        {
            int rate=22050;var data=new float[(int)(rate*seconds)];var random=new System.Random(73);
            for(int i=0;i<data.Length;i++){float t=i/(float)rate,e=Mathf.Sin(Mathf.PI*i/data.Length)*Mathf.Exp(-t*12);data[i]=volume*e*(.75f*Mathf.Sin(t*hz*2*Mathf.PI)+.25f*((float)random.NextDouble()*2-1));}
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        private AudioClip Theme()
        {
            const int rate=22050,seconds=24;var data=new float[rate*seconds];
            for(int i=0;i<data.Length;i++){float t=i/(float)rate;float swell=.65f+.35f*Mathf.Sin(2*Mathf.PI*t/24);data[i]=swell*(.055f*Mathf.Sin(2*Mathf.PI*55*t)+.03f*Mathf.Sin(2*Mathf.PI*82.5f*t)+.017f*Mathf.Sin(2*Mathf.PI*116.625f*t))+.009f*Mathf.Sin(2*Mathf.PI*220*t)*Mathf.Pow(.5f+.5f*Mathf.Sin(2*Mathf.PI*t/8),8);}
            var clip=AudioClip.Create("Far Signal / Quiet Distance (original)",data.Length,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void ApplySettings()
        {
            AudioListener.volume=PlayerPrefs.GetFloat("FarSignal.Master",.8f);music.volume=PlayerPrefs.GetFloat("FarSignal.Music",.35f);sfx.volume=PlayerPrefs.GetFloat("FarSignal.Sfx",.65f);
            SagittariusA.Minigames.MinigameAudio.Volume=sfx.volume;
            QualitySettings.vSyncCount=PlayerPrefs.GetInt("FarSignal.Vsync",1);Application.targetFrameRate=60;
        }
        public static RectTransform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(x+w,y+h);r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return r;}
        public static Text Text(Transform parent,string value,float x,float y,float w,float h,int size=24)
        {var go=new GameObject("Text",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(x,y);r.anchorMax=new Vector2(x+w,y+h);r.offsetMin=r.offsetMax=Vector2.zero;var t=go.GetComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.fontSize=size;t.color=Color.white;t.raycastTarget=false;t.alignment=TextAnchor.MiddleLeft;return t;}
        public static Button Button(Transform parent,string title,float x,float y,float w,float h,Action action)
        {var r=Box(parent,title,x,y,w,h,new Color(.14f,.19f,.24f));var b=r.gameObject.AddComponent<Button>();var t=Text(r,title,.04f,.04f,.92f,.92f,22);t.alignment=TextAnchor.MiddleCenter;b.onClick.AddListener(()=>action());return b;}
        public static GameObject Canvas(string name,int order=800)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;go.GetComponent<Canvas>().sortingOrder=order;var s=go.GetComponent<CanvasScaler>();s.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;s.referenceResolution=new Vector2(1920,1080);s.matchWidthOrHeight=.5f;return go;}
        private RectTransform BeginOverlay(string title)
        {oldTime=Time.timeScale;oldLock=Cursor.lockState;oldVisible=Cursor.visible;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;overlay=Canvas(title);Box(overlay.transform,"Input shield",0,0,1,1,new Color(0,0,0,.7f));return Box(overlay.transform,title,.08f,.06f,.84f,.88f,Ink);}
        public void Close()
        {if(overlay==null)return;overlay.SetActive(false);Destroy(overlay);overlay=null;Time.timeScale=oldTime;Cursor.lockState=oldLock;Cursor.visible=oldVisible;PlayerPrefs.Save();}
        public void Open(string title)
        {
            if(overlay!=null)return;var panel=BeginOverlay(title);Text(panel,title,.05f,.86f,.9f,.1f,38);Button(panel,"BACK",.77f,.035f,.18f,.075f,Close);
            if(title=="SETTINGS")
            {
                Setting(panel,"MASTER VOLUME","FarSignal.Master",.8f,.73f,0,1);Setting(panel,"MUSIC · QUIET DISTANCE","FarSignal.Music",.35f,.60f,0,1);Setting(panel,"SOUND EFFECTS","FarSignal.Sfx",.65f,.47f,0,1);Setting(panel,"MOUSE SENSITIVITY","FarSignal.Look",1,.34f,.3f,2.5f);
                Button(panel,"TOGGLE FULLSCREEN",.05f,.18f,.37f,.08f,()=>{bool next=!Screen.fullScreen;Screen.fullScreen=next;PlayerPrefs.SetInt("FarSignal.Fullscreen",next?1:0);});
                Button(panel,"TOGGLE V-SYNC",.48f,.18f,.37f,.08f,()=>{PlayerPrefs.SetInt("FarSignal.Vsync",1-PlayerPrefs.GetInt("FarSignal.Vsync",1));ApplySettings();});
                Text(panel,"Changes save automatically.  F1 settings · WASD move · Space jump · Left use · Right pick up",.05f,.06f,.68f,.08f,18);
            }
            else if(title=="ACHIEVEMENTS"||title=="MISSION ARCHIVE")
            {
                CheckAchievements();int count=Ids.Count(id=>PlayerPrefs.GetInt("FarSignal.Achievement."+id)==1);
                Text(panel,$"ACHIEVEMENTS COLLECTED   {count*100/Ids.Length}%"+(count==Ids.Length?"   ·   COMPLETIONIST UNLOCKED":""),.05f,.76f,.9f,.08f,26).color=Orange;
                for(int i=0;i<Ids.Length;i++){bool got=PlayerPrefs.GetInt("FarSignal.Achievement."+Ids[i])==1;float y=.67f-i*.072f;Text(panel,(got?"[ COMPLETE ]  ":"[ LOCKED ]  ")+Titles[i],.05f,y,.37f,.065f,20).color=got?Orange:Muted;Text(panel,Descriptions[i],.43f,y,.52f,.065f,19);}
            }
            else if(title=="CREDITS")Text(panel,"FAR SIGNAL\nA Sagittarius-A team expedition\n\nGameplay, UI, mission simulation, scene models, dialogue and films by the Far Signal team.\nCommander models: space_pilot_male / space_pilot_female from the team repository.\nOriginal procedural score and interface / suit sounds: Quiet Distance.\n\nMission quantities are game design values.\nBuilt with Unity 6.6.\n\nEarth → Moon → Mars → Ganymede",.05f,.15f,.90f,.66f,25);
            else if(title=="NEW EXPEDITION")
            {
                Text(panel,"Start again from Earth?\nThis replaces the current expedition save. Your commander profile, settings and earned achievements stay saved.",.05f,.43f,.9f,.32f,30);
                Button(panel,"START NEW EXPEDITION",.25f,.25f,.5f,.1f,()=>{
                    foreach(var key in new[]{"FarSignal.EarthGarageDraft.v1","FarSignal.MoonDraft.v1","FarSignal.MarsDraft.v1","FarSignal.GanymedeDraft.v1"})PlayerPrefs.DeleteKey(key);
                    Close();UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                });
            }
            else if(title=="EXIT") {Text(panel,"Your completed actions are saved automatically.",.05f,.50f,.9f,.15f,30);Button(panel,"QUIT GAME",.3f,.3f,.4f,.1f,()=>Application.Quit());}
        }
        private void Setting(Transform p,string title,string key,float fallback,float y,float min,float max)
        {
            var label=Text(p,"",.05f,y,.49f,.09f,23);Action refresh=()=>label.text=title+"   "+PlayerPrefs.GetFloat(key,fallback).ToString("0.00");refresh();
            Button(p,"−",.60f,y,.12f,.085f,()=>{PlayerPrefs.SetFloat(key,Mathf.Clamp(PlayerPrefs.GetFloat(key,fallback)-.1f,min,max));ApplySettings();refresh();});
            Button(p,"+",.76f,y,.12f,.085f,()=>{PlayerPrefs.SetFloat(key,Mathf.Clamp(PlayerPrefs.GetFloat(key,fallback)+.1f,min,max));ApplySettings();refresh();});
        }
        [Serializable] private sealed class Progress { public bool launched,dustDiaryComplete,practicePassed,photoSaved;public int phase;public List<string> received; }
        private Progress Load(string key){try{return JsonUtility.FromJson<Progress>(PlayerPrefs.GetString(key,""))??new Progress();}catch{return new Progress();}}
        private void CheckAchievements()
        {
            var earth=Load("FarSignal.EarthGarageDraft.v1");var moon=Load("FarSignal.MoonDraft.v1");var mars=Load("FarSignal.MarsDraft.v1");var gany=Load("FarSignal.GanymedeDraft.v1");
            bool[] checks={earth.launched,moon.phase==(int)MoonStagePhase.Complete,mars.phase==(int)MarsPhase.Complete,gany.phase==(int)GanymedePhase.Complete,mars.dustDiaryComplete,mars.practicePassed,gany.photoSaved,gany.received!=null&&gany.received.Contains("full-graph")};
            bool changed=false;for(int i=0;i<checks.Length;i++)if(checks[i]&&PlayerPrefs.GetInt("FarSignal.Achievement."+Ids[i])==0){PlayerPrefs.SetInt("FarSignal.Achievement."+Ids[i],1);changed=true;}
            if(changed){PlayerPrefs.Save();sfx.PlayOneShot(success,.5f);}
        }
        public static void Film(string id,Action finished)
        {if(Instance==null||Application.isBatchMode){finished?.Invoke();return;}Instance.StartCoroutine(Instance.PlayFilm(id,finished));}
        private IEnumerator PlayFilm(string id,Action finished)
        {
            if(overlay!=null){finished?.Invoke();yield break;}string path=Path.Combine(Application.streamingAssetsPath,"Cutscenes",id+".mp4");if(!File.Exists(path)){finished?.Invoke();yield break;}
            var p=BeginOverlay("Flight film");p.anchorMin=Vector2.zero;p.anchorMax=Vector2.one;
            var img=new GameObject("Film",typeof(RectTransform),typeof(RawImage));img.transform.SetParent(p,false);
            var rect=(RectTransform)img.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var raw=img.GetComponent<RawImage>();raw.raycastTarget=false;
            var fit=img.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=16f/9;
            var filmAudio=overlay.AddComponent<AudioSource>();filmAudio.volume=sfx.volume;filmAudio.spatialBlend=0;
            var video=overlay.AddComponent<VideoPlayer>();video.playOnAwake=false;video.timeUpdateMode=VideoTimeUpdateMode.UnscaledGameTime;video.source=VideoSource.Url;video.url=path;video.renderMode=VideoRenderMode.APIOnly;video.audioOutputMode=VideoAudioOutputMode.AudioSource;video.controlledAudioTrackCount=1;video.SetTargetAudioSource(0,filmAudio);video.isLooping=false;
            bool done=false;video.loopPointReached+=v=>done=true;video.errorReceived+=(v,e)=>{Debug.LogWarning("Cutscene unavailable: "+id);done=true;};
            Button(p,"SKIP  →",.83f,.04f,.13f,.06f,()=>done=true);video.Prepare();float until=Time.realtimeSinceStartup+8;
            while(!video.isPrepared&&!done&&Time.realtimeSinceStartup<until)yield return null;
            if(video.isPrepared&&!done){video.Play();until=Time.realtimeSinceStartup+Mathf.Max(15,(float)video.length+5);while(!done&&Time.realtimeSinceStartup<until){raw.texture=video.texture;yield return null;}}
            video.Stop();Close();finished?.Invoke();
        }
        private void OnDestroy(){if(Instance==this)Instance=null;foreach(var clip in new[]{click,step,jump,success,music==null?null:music.clip})if(clip!=null)Destroy(clip);}
    }
}
