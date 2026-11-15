using System;
using System.Collections;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using SagittariusA.Gameplay.Ganymede;
using SagittariusA.Gameplay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SagittariusA.EditorTools.Ganymede
{
    [InitializeOnLoad]
    public static class GanymedePlayChecks
    {
        private const string Key="FarSignal.RunGanymedePlayChecks";
        static GanymedePlayChecks()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
                {SessionState.SetBool(Key,false);EditorApplication.delayCall+=StartRunner;}
            };
        }
        [MenuItem("Far Signal/Ganymede/Run Full Ganymede and Cargo Play Checks")]
        public static void Run()
        {
            GanymedeAcceptanceChecks.Run();EarthCargoChecks.Run();
            if(EditorApplication.isPlaying)StartRunner();
            else {SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
        }
        private static void StartRunner()
        {new GameObject("Ganymede and cargo automated play checks").AddComponent<GanymedePlayCheckRunner>();}
    }
    public sealed class GanymedePlayCheckRunner : MonoBehaviour
    {
        private GameObject cargoOwner,chapterOwner;
        private GanymedePlayableExperience experience;
        private bool completed;
        private sealed class MemoryEarthStore : IEarthDraftStore {private string json="";public string Load()=>json;public void Save(string value)=>json=value;}
        private void Start(){StartCoroutine(Guarded());}
        private IEnumerator Guarded()
        {
            var routine=Checks();
            while(true)
            {
                object current=null;bool more=false;
                try{more=routine.MoveNext();if(more)current=routine.Current;}
                catch(Exception error){Debug.LogException(error);Cleanup();if(Application.isBatchMode)EditorApplication.Exit(1);yield break;}
                if(!more)break;yield return current;
            }
            completed=true;Debug.Log("GANYMEDE + CARGO PLAY CHECKS PASSED: UI flow, cargo hold, cancellation, two surveys, comparison retry, release, packets, finale, collision and text layout.");
            Cleanup();
            if(Application.isBatchMode)EditorApplication.Exit(0);
        }
        private IEnumerator Checks()
        {
            Application.runInBackground=true;
            cargoOwner=new GameObject("Isolated cargo checks");var session=cargoOwner.AddComponent<EarthGameplaySession>();session.Initialize(new MemoryEarthStore());
            Check(session.CanRun(EarthActivity.Cargo)&&!session.TryLaunch(),"cargo runnable and launch held");
            session.BeginActivity(EarthActivity.Cargo);yield return null;
            var cargo=cargoOwner.GetComponent<EarthCargoMinigame>();
            // The manifest uses catalog names; selection itself is independent of localization.
            cargo.Packing.Place("panel",0);cargo.Cancel();yield return null;
            Check(!session.Busy&&session.Checks.placements.Count==0&&!session.TryLaunch(),"cancel cannot clear hold");
            session.BeginActivity(EarthActivity.Cargo);yield return null;
            cargo.Packing.Place("panel",0);cargo.Packing.Place("kit",2);cargo.Packing.Lock("panel");cargo.Packing.Lock("kit");
            // Refresh through the actual slot selector before securing the moved item again.
            var button=cargoOwner.GetComponentsInChildren<Button>().First(b=>b.name.StartsWith("RETURN SELECTED"));button.onClick.Invoke();yield return null;
            cargo.Packing.Place("panel",0);cargo.Packing.Lock("panel");
            typeof(EarthCargoMinigame).GetMethod("Render",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(cargo,null);
            CheckText(cargoOwner);yield return new WaitForSecondsRealtime(.4f);Click(cargoOwner,"CONFIRM CARGO");yield return null;
            Check(session.CanLaunchFromEarth&&session.TryLaunch()&&!session.CanRun(EarthActivity.Cargo),"cargo saved then launch available");
            Destroy(cargoOwner);cargoOwner=null;

            chapterOwner=new GameObject("Isolated Ganymede UI checks");experience=chapterOwner.AddComponent<GanymedePlayableExperience>();experience.StartIsolatedTest();
            yield return null;CheckText(chapterOwner);Click(chapterOwner,"RUN ORBITER CHECKOUT");yield return null;
            Check(!experience.Mission.CheckoutRunning,"UI camera overload rejection");Click(chapterOwner,"EXTERIOR CAMERA");yield return null;Click(chapterOwner,"RUN ORBITER CHECKOUT");
            yield return new WaitForSecondsRealtime(3.4f);Click(chapterOwner,"CONFIRM PLAN & LAND");yield return new WaitForSecondsRealtime(2.1f);
            Check(experience.Mission.State.phase==GanymedePhase.Surface,"surface");experience.Interact("case",true);yield return null;
            Check(experience.Mission.State.dosimeterCollected,"right-click pickup routing");
            // Drive the actual CharacterController along the marked path; crossing geometry cannot teleport the player through it.
            var destination=experience.SitePosition(GanymedeMission.SiteA)+new Vector3(0,0,-2.6f);
            for(int i=0;i<240&&Vector3.Distance(new Vector3(experience.Player.transform.position.x,0,experience.Player.transform.position.z),destination)>.3f;i++)
            {var d=destination-experience.Player.transform.position;d.y=0;experience.Player.Move(d.normalized*.12f+Vector3.down*.08f);yield return null;}
            Check(Vector3.Distance(new Vector3(experience.Player.transform.position.x,0,experience.Player.transform.position.z),destination)<.5f,"walkable A route");
            foreach(string site in new[]{GanymedeMission.SiteA,GanymedeMission.SiteB})
            {
                MoveToSite(site);yield return null;experience.Interact(site);yield return null;CheckText(chapterOwner);
                Click(chapterOwner,"FIT TO □");yield return null;Click(chapterOwner,"△ INSTRUMENT");yield return null;Click(chapterOwner,"FIT TO △");yield return null;Click(chapterOwner,"○ POWER");yield return null;Click(chapterOwner,"FIT TO ○");yield return null;Click(chapterOwner,"COMMISSION STATION");yield return null;
                Check(experience.Mission.StationBuilt(site),"station built "+site);experience.Interact(site);yield return null;
                for(int i=0;i<5;i++){Click(chapterOwner,"X −");yield return null;}
                for(int i=0;i<4;i++){Click(chapterOwner,"Y +");yield return null;}
                Click(chapterOwner,"CONFIRM LEVEL");yield return null;Click(chapterOwner,"SWITCH ON / SAMPLE");yield return new WaitForSecondsRealtime(3.4f);
                Check(experience.Mission.Record(site)!=null,"UI sample "+site);CheckText(chapterOwner);Click(chapterOwner,"BACK");yield return null;experience.Interact(site,true);yield return null;
            }
            MoveToSite(GanymedeMission.Relay);yield return null;experience.Interact(GanymedeMission.Relay);yield return null;
            Click(chapterOwner,"FIT TO □");yield return null;Click(chapterOwner,"△ INSTRUMENT");yield return null;Click(chapterOwner,"FIT TO △");yield return null;Click(chapterOwner,"○ POWER");yield return null;Click(chapterOwner,"FIT TO ○");yield return null;Click(chapterOwner,"COMMISSION STATION");yield return null;
            Check(experience.Environment==null||!experience.Environment.Loaded||experience.Environment.ActiveStationCount==3,"three independent construction models active");
            experience.Interact("lander");yield return null;CheckText(chapterOwner);
            Click(chapterOwner,"Site B is always safe.");yield return null;Check(!experience.Mission.State.comparisonSaved,"wrong comparison retries");
            Click(chapterOwner,"Site B had the lower reading");yield return null;Click(chapterOwner,"ASCEND TO ORBIT");yield return new WaitForSecondsRealtime(2.1f);CheckText(chapterOwner);
            Click(chapterOwner,"CONFIRM CREW SYSTEMS");yield return null;Click(chapterOwner,"ACCELERATE TO MARKED POINT");yield return new WaitForSecondsRealtime(2.1f);
            Click(chapterOwner,"RELEASE BOTH CLAMPS");yield return new WaitForSecondsRealtime(4.3f);Check(experience.Mission.State.deploymentCount==1,"single release");CheckText(chapterOwner);
            Click(chapterOwner,"OPEN 10 s CONTACT");yield return new WaitForSecondsRealtime(10.4f);Check(experience.Mission.RequiredPackets,"actual timed packets");
            Click(chapterOwner,"TIME SKIP → EARTH'S REPLY");yield return new WaitForSecondsRealtime(2.1f);CheckText(chapterOwner);Click(chapterOwner,"OPEN CAMPAIGN FLIGHT LOG");yield return null;CheckText(chapterOwner);
            Check(experience.Mission.State.logOpened,"playable ending");
        }
        private void MoveToSite(string id)
        {
            var target=chapterOwner.GetComponentsInChildren<GanymedeWorldTarget>().First(t=>t.targetId==id);var at=target.GetComponent<Collider>().bounds.center;
            var point=at+new Vector3(0,0,-2.6f);point.y=experience.Environment!=null&&experience.Environment.Loaded?experience.Environment.GroundAt(point)+.15f:.15f;
            MovePlayer(point);experience.ViewCamera.transform.LookAt(at);Physics.SyncTransforms();
            Check(Physics.Raycast(experience.ViewCamera.transform.position,experience.ViewCamera.transform.forward,out var hit,4f)&&hit.collider.GetComponent<GanymedeWorldTarget>()?.targetId==id,"site target reachable "+id);
        }
        private void MovePlayer(Vector3 at){experience.Player.enabled=false;experience.Player.transform.position=at;experience.Player.enabled=true;Cursor.lockState=CursorLockMode.None;}
        private static void Click(GameObject owner,string starts)
        {var button=owner.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name.StartsWith(starts,StringComparison.Ordinal)&&b.interactable);Check(button!=null,"button "+starts);button.onClick.Invoke();}
        private static void CheckText(GameObject owner)
        {
            Canvas.ForceUpdateCanvases();foreach(var text in owner.GetComponentsInChildren<Text>())
                if(text.gameObject.activeInHierarchy&&text.preferredHeight>text.rectTransform.rect.height+3)
                    throw new InvalidOperationException("Clipped text: "+text.text+" / needs "+text.preferredHeight+" has "+text.rectTransform.rect.height);
        }
        private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("Ganymede play check: "+message);}
        private void Cleanup(){if(experience!=null)experience.Exit();if(chapterOwner!=null)Destroy(chapterOwner);if(cargoOwner!=null)Destroy(cargoOwner);Destroy(gameObject);}
        private void OnDestroy(){if(!completed&&experience!=null)experience.Exit();}
    }
}
