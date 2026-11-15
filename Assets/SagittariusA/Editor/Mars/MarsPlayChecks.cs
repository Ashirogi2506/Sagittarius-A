using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using SagittariusA.Gameplay.Mars;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SagittariusA.EditorTools.Mars
{
    [InitializeOnLoad]
    public static class MarsPlayChecks
    {
        private const string Pending="FarSignal.Mars.AuthoredPlayChecks";
        static MarsPlayChecks(){EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);EditorApplication.delayCall+=StartRunner;}};}
        [MenuItem("Far Signal/Mars/Run Authored Mars Play Checks")]
        public static void Run()
        {
            MarsAcceptanceChecks.RunAll();
            if(EditorApplication.isPlaying)StartRunner();else{SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
        }
        private static void StartRunner(){new GameObject("Mars authored play checks").AddComponent<MarsPlayCheckRunner>();}
    }
    public sealed class MarsPlayCheckRunner:MonoBehaviour
    {
        private MarsPlayableExperience experience;
        private GameObject owner;
        private void Start(){StartCoroutine(Guarded());}
        private IEnumerator Guarded()
        {
            var stack=new System.Collections.Generic.Stack<IEnumerator>();stack.Push(Checks());
            while(stack.Count>0)
            {
                object current=null;bool more;
                try{var check=stack.Peek();more=check.MoveNext();if(more)current=check.Current;}
                catch(Exception e){Debug.LogException(e);Cleanup();if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(1);yield break;}
                if(!more){stack.Pop();continue;}
                if(current is IEnumerator child)stack.Push(child);else yield return current;
            }
            Debug.Log("MARS AUTHORED PLAY CHECKS PASSED: imported environment, walking route, real pickup and snap targets, mismatch, live mass, missing-data recovery, hardware invalidation, loader transport, both clamps, Ganymede handoff, optional skips, text layout and restart.");Cleanup();if(Application.isBatchMode)UnityEditor.EditorApplication.Exit(0);
        }
        private IEnumerator Checks()
        {
            Application.runInBackground=true;
            owner=new GameObject("Isolated authored Mars check");experience=owner.AddComponent<MarsPlayableExperience>();experience.StartIsolatedTest();yield return null;
            var stage=experience.Stage;
            for(int i=0;i<180&&stage.Phase==MarsPhase.AntennaRecovery;i++)stage.TickManualAlignment(stage.Context.antennaAngle<stage.Context.antennaTarget-.2f?1:0,1f/60);
            Check(stage.Phase==MarsPhase.ArrivalPackets,"manual antenna lock");yield return null;
            Click("ARRIVAL FIRST");yield return null;Click("SEND NEXT");yield return new WaitForSecondsRealtime(3.4f);
            Check(stage.Context.completedPackets.Contains("arrival-update"),"timed arrival packet");Click("LAND ON MARS");yield return null;
            Check(experience.Environment!=null&&experience.Environment.Loaded,"authored model loaded");
            Check(owner.GetComponentsInChildren<MeshRenderer>(true).Length>1000,"authored meshes used");
            Check(Mathf.Abs(experience.Player.transform.position.y-experience.Environment.GroundAt(experience.Player.transform.position))<.4f,"spawn grounded");
            yield return Walk(-10,-9);yield return Walk(-4,-8);yield return Walk(4,-8);yield return Walk(5.5f,-4.2f);
            Use(MarsWorldTarget.PanelKind.Airlock);yield return new WaitForSecondsRealtime(1.6f);
            yield return Walk(4,-1);yield return Walk(1,2.0f);
            foreach(var pair in new[]{(MarsWorldTarget.PanelKind.PickSensor,MarsWorldTarget.PanelKind.MountSensor),
                (MarsWorldTarget.PanelKind.PickAntenna,MarsWorldTarget.PanelKind.MountAntenna),(MarsWorldTarget.PanelKind.PickShield,MarsWorldTarget.PanelKind.MountShield)})
            {
                yield return Walk(1,2.0f);Use(pair.Item1);yield return null;
                yield return Walk(7,2.0f);
                if(pair.Item1==MarsWorldTarget.PanelKind.PickSensor){Use(MarsWorldTarget.PanelKind.MountAntenna);Check(!stage.Context.antennaInstalled,"wrong mount rejected");}
                Use(pair.Item2);yield return null;
            }
            Check(stage.Context.sensorInstalled&&stage.Context.antennaInstalled&&stage.Context.shieldInstalled,"three actual modules installed");
            Use(MarsWorldTarget.PanelKind.PowerPort);yield return null;
            yield return Walk(0,2.2f);Use(MarsWorldTarget.PanelKind.Bench);yield return null;CheckText();
            Click("1 · POWER ON");yield return null;Click("2 · TAKE SAMPLE");yield return null;Click("3 · TRANSMIT");yield return null;
            Check(!stage.Context.testPassed&&stage.Context.benchGraph.samples.Length==0,"missing data blank graph");
            Click("DATA · SQUARE");yield return null;Click("1 · POWER ON");yield return null;Click("2 · TAKE SAMPLE");yield return null;Click("3 · TRANSMIT");yield return null;
            Check(stage.Context.testPassed&&stage.Context.massKg==250,"basic tested build");Click("X");yield return null;
            Check(stage.Act(MarsActionKind.SelectDirectionalAntenna).Accepted&&!stage.Context.testPassed,"hardware invalidates bench test");
            stage.Act(MarsActionKind.SelectReinforcedShield);Check(stage.Context.massKg==305&&!stage.Act(MarsActionKind.SeatOrbiter).Accepted,"305 kg blocked");
            stage.Act(MarsActionKind.SelectBasicShield);stage.Act(MarsActionKind.SetFrequentSensor);
            stage.Act(MarsActionKind.BenchPowerOn);stage.Act(MarsActionKind.BenchSample);stage.Act(MarsActionKind.BenchTransmit);
            Check(stage.Context.massKg==275&&stage.Context.benchGraph.samples.Length==12,"corrected frequent directional build");
            yield return Walk(8,1);Use(MarsWorldTarget.PanelKind.Loader);yield return null;CheckText();Click("MOVE LOADER");yield return null;
            Check(experience.LoaderMoving&&!stage.Context.orbiterSeated&&!stage.Context.canDepart,"transport cannot complete before arrival");
            float deadline=Time.realtimeSinceStartup+30;
            while(experience.LoaderMoving&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!experience.LoaderMoving&&stage.Context.orbiterSeated,"loader reached ship");
            Check(Vector3.Distance(experience.Orbiter.position,experience.Environment.Point(-15,-8,1.95f))<.1f,"actual orbiter transported");
            stage.Act(MarsActionKind.CloseLeftClamp);stage.Act(MarsActionKind.ReceiveDeploymentPlan);Check(!stage.Context.canDepart,"one clamp held");
            stage.Act(MarsActionKind.CloseRightClamp);Check(stage.Context.canDepart,"two clamps ready");stage.Act(MarsActionKind.CompleteMars);yield return null;CheckText();
            var handoff=stage.GetGanymedeHandoff();Check(handoff!=null&&handoff.antenna=="directional"&&handoff.sensorMode=="frequent"&&handoff.sampleCount==12&&!handoff.dustDiaryComplete&&!handoff.practicePassed,"exact optional-free handoff");
            experience.Exit();yield return null;experience.StartIsolatedTest();yield return null;Check(experience.Stage.Phase==MarsPhase.AntennaRecovery,"fresh second run");
        }
        private IEnumerator Walk(float x,float y)
        {
            var target=experience.Environment.Point(x,y,0);float deadline=Time.realtimeSinceStartup+12;
            while(Vector2.Distance(new Vector2(experience.Player.transform.position.x,experience.Player.transform.position.z),new Vector2(target.x,target.z))>.25f&&Time.realtimeSinceStartup<deadline)
            {
                Vector3 direction=target-experience.Player.transform.position;direction.y=0;
                experience.Player.Move(direction.normalized*.14f+Vector3.down*.08f);yield return null;
            }
            Check(Vector2.Distance(new Vector2(experience.Player.transform.position.x,experience.Player.transform.position.z),new Vector2(target.x,target.z))<.4f,"walkable route to "+x+", "+y+" from "+experience.Player.transform.position);
        }
        private void Use(MarsWorldTarget.PanelKind kind)
        {
            var target=owner.GetComponentsInChildren<MarsWorldTarget>().Where(t=>t.Panel==kind).OrderBy(t=>Vector3.Distance(t.transform.position,experience.Player.transform.position)).First();
            var collider=target.GetComponent<Collider>();var point=collider.bounds.center;
            experience.ViewCamera.transform.LookAt(point);Physics.SyncTransforms();
            Check(Physics.Raycast(experience.ViewCamera.transform.position,experience.ViewCamera.transform.forward,out var hit,3.5f,~0,QueryTriggerInteraction.Collide)&&hit.collider.GetComponentInParent<MarsWorldTarget>()==target,"reachable target "+kind+"; hit "+(hit.collider==null?"none":hit.collider.name));
            typeof(MarsPlayableExperience).GetMethod("OpenTarget",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(experience,new object[]{target});
        }
        private void Click(string prefix)
        {var button=owner.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.name.StartsWith(prefix,StringComparison.Ordinal)&&b.interactable);Check(button!=null,"button "+prefix);button.onClick.Invoke();}
        private void CheckText()
        {Canvas.ForceUpdateCanvases();foreach(var text in owner.GetComponentsInChildren<Text>())if(text.gameObject.activeInHierarchy&&text.preferredHeight>text.rectTransform.rect.height+4)throw new InvalidOperationException("Mars clipped text: "+text.text);}
        private static void Check(bool ok,string description){if(!ok)throw new InvalidOperationException("Mars play check: "+description);}
        private void Cleanup(){if(experience!=null)experience.Exit();if(owner!=null)Destroy(owner);Destroy(gameObject);}
    }
}
