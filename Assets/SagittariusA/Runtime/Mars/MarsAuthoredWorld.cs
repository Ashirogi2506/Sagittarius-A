using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SagittariusA.Gameplay.Mars
{
    public sealed partial class MarsPlayableExperience
    {
        private MarsEnvironmentView environment;
        private GameObject sensorPickup,antennaPickup,shieldPickup;
        private Vector3 standPosition,cradlePosition,doorSlide;
        private Quaternion standRotation;
        private bool loaderMoving;
        private int authoredRevision=-1;
        private Vector3[] loaderRoute;
        private GameObject heldPart;
        private string heldPartId;
        private GameObject transitWorld;

        private void BuildMarsTransitCabin()
        {
            transitWorld=new GameObject("Shared expedition cabin / Mars approach");transitWorld.transform.SetParent(transform,false);transitWorld.transform.position=new Vector3(0,1000,0);
            var origin=transitWorld.transform;
            CreateCube("Cabin deck",new Vector3(0,-.2f,2),new Vector3(9,.35f,12),new Color(.10f,.13f,.16f),origin);
            CreateCube("Cabin ceiling",new Vector3(0,3.6f,2),new Vector3(9,.25f,12),new Color(.14f,.18f,.22f),origin);
            for(int side=-1;side<=1;side+=2)
            {
                CreateCube("Cabin wall",new Vector3(side*4.2f,1.7f,2),new Vector3(.25f,3.8f,12),new Color(.15f,.2f,.25f),origin);
                CreateCube("Window surround",new Vector3(side*2.7f,1.7f,6),new Vector3(.2f,3.1f,.2f),new Color(.65f,.68f,.69f),origin);
            }
            CreateCube("Flight console",new Vector3(0,.55f,4),new Vector3(4.4f,1.1f,1.3f),new Color(.08f,.12f,.16f),origin);
            CreatePrimitive("Mars approach globe",PrimitiveType.Sphere,new Vector3(12,10,65),Vector3.one*28,new Color(.62f,.24f,.10f),origin);
            var light=new GameObject("Cabin reading light",typeof(Light));light.transform.SetParent(origin,false);light.transform.localPosition=new Vector3(0,2.7f,0);
            var point=light.GetComponent<Light>();point.type=LightType.Point;point.range=18;point.intensity=3;point.color=new Color(.78f,.87f,1);
            transitCamera.transform.position=origin.position+new Vector3(0,1.65f,-2.6f);transitCamera.GetComponent<Camera>().cullingMask=~0;
        }

        private void ShowMarsHelp()
        {
            ClosePanel(false);if(player!=null)player.Paused=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            var p=Box(canvasObject.transform,"Mars help",.17f,.15f,.66f,.7f,Navy);activePanel=p.gameObject;
            Label(p,"FIRST TEST SIGNAL",.06f,.83f,.88f,.10f,34,Color.white);
            Label(p,"1  Open the bay doors and reach the workbench.\n2  Choose sensor mode, antenna and shielding. Stay within 300 kg.\n3  RIGHT CLICK each tabletop module. LEFT CLICK its keyed frame mount.\n4  Connect POWER and DATA, then power → sample → transmit at the receiver.\n5  Dispatch the loader. At the ship, lock both clamps and receive the plan.\n\nDust Diary and packet practice are optional.\nWASD move · Space jump · Left use · Right pick up · H help · Tab menu",.06f,.22f,.88f,.57f,23,Color.white);
            ActionButton(p,"BACK TO MARS",.35f,.07f,.30f,.10f,()=>ClosePanel(true),Orange);
        }
        public MarsStageController Stage=>stage;
        public MarsEnvironmentView Environment=>environment;
        public CharacterController Player=>playerObject==null?null:playerObject.GetComponent<CharacterController>();
        public Camera ViewCamera=>viewCamera;
        public bool LoaderMoving=>loaderMoving;
        public Transform Orbiter=>orbiterAssembly==null?null:orbiterAssembly.transform;

        private bool BuildAuthoredMarsWorld()
        {
            environment=world.AddComponent<MarsEnvironmentView>();if(!environment.Initialize())return false;
            world.name="Mars / First Test Signal";
            RenderSettings.ambientLight=new Color(.43f,.37f,.31f);RenderSettings.fogDensity=.0035f;
            var taskLight=new GameObject("Assembly task light",typeof(Light));taskLight.transform.SetParent(world.transform,false);
            taskLight.transform.position=environment.Point(3,3,4.7f);var fill=taskLight.GetComponent<Light>();fill.type=LightType.Point;fill.range=16;fill.intensity=5;fill.color=new Color(.76f,.86f,1);
            BuildAuthoredDoors();BuildAuthoredOrbiter();BuildAuthoredLoader();
            BindMesh("Bench | Test receiver",MarsWorldTarget.PanelKind.Bench,"BENCH TEST / power, sample, transmit");
            BindMesh("Bench | Work surface",MarsWorldTarget.PanelKind.Assembly,"CONFIGURE ORBITER / live mass and module choices");
            var weather=CreateCube("Weather readout",environment.Point(-8,-4.3f,1.4f),new Vector3(.65f,.55f,.22f),Card);
            Bind(weather,MarsWorldTarget.PanelKind.Weather,"DUST DIARY / optional comparison");
            var practice=CreateCube("Communications practice",environment.Point(-1.7f,5.4f,2.1f),new Vector3(.7f,1.15f,.6f),Card);
            Bind(practice,MarsWorldTarget.PanelKind.Practice,"WHAT GOES FIRST? / optional packet practice");
            WorldLabel("MARS ASSEMBLY\nFollow the amber guides",environment.Point(-10,-9,1.8f),24,Color.white);
            WorldLabel("ORBITER FRAME\nLeft click: fit held module",environment.Point(7,4,3.7f),18,Color.white);
            return true;
        }

        private void BuildAuthoredDoors()
        {
            airlockLeft=CreateCube("Operating bay door L",environment.Point(1.4f,-2.35f,3.2f),new Vector3(5.15f,3.45f,.18f),new Color(.18f,.24f,.28f));
            airlockRight=CreateCube("Operating bay door R",environment.Point(6.6f,-2.35f,3.2f),new Vector3(5.15f,3.45f,.18f),new Color(.18f,.24f,.28f));
            airlockLeftClosed=airlockLeft.transform.position;airlockRightClosed=airlockRight.transform.position;
            doorSlide=(environment.Point(1,0,0)-environment.Point(0,0,0))*5.25f;
            var outside=CreateCube("Airlock exterior control",environment.Point(7.35f,-3.12f,2.25f),new Vector3(.45f,.65f,.22f),Orange);
            Bind(outside,MarsWorldTarget.PanelKind.Airlock,"OPERATE BAY DOORS");
            var inside=CreateCube("Airlock interior control",environment.Point(7.35f,-1.7f,2.25f),new Vector3(.45f,.65f,.22f),Orange);
            Bind(inside,MarsWorldTarget.PanelKind.Airlock,"OPERATE BAY DOORS");
        }

        private GameObject BindMesh(string name,MarsWorldTarget.PanelKind kind,string text)
        {
            var mesh=environment.FindMesh(name);if(mesh==null)throw new InvalidOperationException("Missing authored Mars mesh: "+name);
            Bind(mesh.gameObject,kind,text);return mesh.gameObject;
        }
        private void Bind(GameObject go,MarsWorldTarget.PanelKind kind,string text)
        {var target=go.GetComponent<MarsWorldTarget>()??go.AddComponent<MarsWorldTarget>();target.Configure(kind,text);targets.Add(target);}
        private GameObject Zone(string name,Vector3 position,Vector3 size,MarsWorldTarget.PanelKind kind)
        {
            var go=new GameObject(name,typeof(BoxCollider));go.transform.SetParent(world.transform,false);go.transform.position=position;
            var collider=go.GetComponent<BoxCollider>();collider.size=size;collider.isTrigger=true;go.layer=TargetLayer;Bind(go,kind,name);return go;
        }
        private GameObject FittedCopy(GameObject source,string name,Vector3 position)
        {
            var copy=Instantiate(source,orbiterAssembly.transform,true);copy.name=name;copy.transform.position=position;
            foreach(var target in copy.GetComponentsInChildren<MarsWorldTarget>())Destroy(target);
            foreach(var collider in copy.GetComponentsInChildren<Collider>())Destroy(collider);
            return copy;
        }
        private void BuildAuthoredOrbiter()
        {
            sensorPickup=BindMesh("Particle sensor | separate",MarsWorldTarget.PanelKind.PickSensor,"PICK UP PARTICLE SENSOR");
            antennaPickup=BindMesh("Antenna | separate",MarsWorldTarget.PanelKind.PickAntenna,"PICK UP ANTENNA");
            shieldPickup=BindMesh("Shield | separate",MarsWorldTarget.PanelKind.PickShield,"PICK UP SHIELD");
            var frame=environment.FindMesh("Frame | separate");if(frame==null)throw new InvalidOperationException("Mars frame missing.");
            orbiterAssembly=new GameObject("Jupiter orbiter / exact selected build");orbiterAssembly.transform.SetParent(world.transform,false);
            orbiterAssembly.transform.position=frame.transform.position;
            frame.transform.SetParent(orbiterAssembly.transform,true);
            standPosition=environment.Point(7,4,2.3f);standRotation=orbiterAssembly.transform.rotation;orbiterAssembly.transform.position=standPosition;
            sensorVisual=FittedCopy(sensorPickup,"Installed particle sensor",environment.Point(6.65f,4,2.7f));
            directionalAntennaVisual=FittedCopy(antennaPickup,"Installed directional antenna",environment.Point(7.55f,4,2.8f));
            basicShieldVisual=FittedCopy(shieldPickup,"Installed basic shield",environment.Point(7,4,1.9f));
            basicShieldVisual.transform.rotation=Quaternion.AngleAxis(90,(environment.Point(0,1,0)-environment.Point(0,0,0)).normalized)*basicShieldVisual.transform.rotation;
            reinforcedShieldVisual=Instantiate(basicShieldVisual,orbiterAssembly.transform,true);reinforcedShieldVisual.name="Installed reinforced shield";
            reinforcedShieldVisual.transform.localScale=Vector3.Scale(basicShieldVisual.transform.localScale,new Vector3(1.9f,1.08f,1.08f));
            lightAntennaVisual=new GameObject("Installed light antenna");lightAntennaVisual.transform.SetParent(orbiterAssembly.transform,false);lightAntennaVisual.transform.position=environment.Point(7.55f,4,2.65f);
            CreatePrimitive("Light antenna mast",PrimitiveType.Cylinder,new Vector3(0,.36f,0),new Vector3(.045f,.36f,.045f),new Color(.75f,.8f,.84f),lightAntennaVisual.transform);
            CreatePrimitive("Light antenna tip",PrimitiveType.Sphere,new Vector3(0,.73f,0),Vector3.one*.10f,Orange,lightAntennaVisual.transform);
            powerCable=CreateCube("Connected POWER lead",new Vector3(-.6f,-.13f,-.55f),new Vector3(.62f,.045f,.045f),Orange,orbiterAssembly.transform);
            dataCable=CreateCube("Connected DATA lead",new Vector3(.6f,-.13f,-.55f),new Vector3(.62f,.045f,.045f),Cyan,orbiterAssembly.transform);
            Zone("SENSOR / keyed mount",environment.Point(6.55f,3.5f,2.55f),new Vector3(.45f,.42f,.45f),MarsWorldTarget.PanelKind.MountSensor);
            Zone("ANTENNA / keyed mount",environment.Point(7.2f,3.5f,2.55f),new Vector3(.4f,.42f,.45f),MarsWorldTarget.PanelKind.MountAntenna);
            Zone("SHIELD / keyed mount",environment.Point(7.85f,3.5f,2.3f),new Vector3(.4f,.45f,.45f),MarsWorldTarget.PanelKind.MountShield);
            var power=CreatePrimitive("POWER / round port",PrimitiveType.Cylinder,environment.Point(6.1f,3.15f,2.0f),new Vector3(.21f,.10f,.21f),Orange);
            power.transform.rotation=Quaternion.Euler(90,0,0);Bind(power,MarsWorldTarget.PanelKind.PowerPort,"CONNECT / DISCONNECT POWER");
            var data=CreateCube("DATA / square port",environment.Point(7.8f,3.15f,2.0f),new Vector3(.23f,.23f,.16f),Cyan);Bind(data,MarsWorldTarget.PanelKind.DataPort,"CONNECT / DISCONNECT DATA");
        }

        private void BuildAuthoredLoader()
        {
            cradlePosition=environment.Point(-15,-8,1.95f);
            loaderRoute=new[]{environment.Point(7,-.7f,0),environment.Point(4,0,0),environment.Point(4,-3,0),environment.Point(4,-7.7f,0),environment.Point(-5,-8,0),environment.Point(-15,-8,0)};
            for(int i=0;i<loaderRoute.Length;i++)loaderRoute[i].y=environment.GroundAt(loaderRoute[i])+.01f;
            loader=new GameObject("Orbiter transport loader");loader.transform.SetParent(world.transform,false);loader.transform.position=loaderRoute[0];
            CreateCube("Loader chassis",new Vector3(0,.32f,0),new Vector3(2.0f,.3f,1.8f),new Color(.3f,.34f,.37f),loader.transform);
            CreateCube("Padded equipment deck",new Vector3(0,.51f,0),new Vector3(1.65f,.10f,1.55f),Card,loader.transform);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {var wheel=CreatePrimitive("Loader wheel",PrimitiveType.Cylinder,new Vector3(x*.98f,.24f,z*.60f),new Vector3(.48f,.13f,.48f),new Color(.045f,.05f,.06f),loader.transform);wheel.transform.localRotation=Quaternion.Euler(0,0,90);}
            // The crew lander and its cradle are separate from the orbiter being assembled.
            var shipGround=environment.Point(-18,-12,0);shipGround.y=environment.GroundAt(shipGround);
            SagittariusA.Gameplay.Flow.ExpeditionShipVisual.Create(world.transform,shipGround);
            CreateCube("Ship cargo cradle deck",environment.Point(-15,-8,1.30f),new Vector3(2.2f,.30f,2.2f),new Color(.27f,.3f,.33f));
            leftClampVisual=CreateCube("Left cradle clamp",cradlePosition+new Vector3(-1.12f,0,0),new Vector3(.16f,.7f,.9f),Orange);
            rightClampVisual=CreateCube("Right cradle clamp",cradlePosition+new Vector3(1.12f,0,0),new Vector3(.16f,.7f,.9f),Orange);
            Bind(leftClampVisual,MarsWorldTarget.PanelKind.LeftClamp,"LOCK LEFT CLAMP");Bind(rightClampVisual,MarsWorldTarget.PanelKind.RightClamp,"LOCK RIGHT CLAMP");
            var station=CreateCube("Loader dispatch console",environment.Point(8.6f,.7f,2.15f),new Vector3(.6f,1.1f,.45f),Card);Bind(station,MarsWorldTarget.PanelKind.Loader,"LOADER / SHIP CRADLE");
            var shipConsole=CreateCube("Ship departure console",environment.Point(-13.4f,-8,1.5f),new Vector3(.5f,.8f,.4f),Card);Bind(shipConsole,MarsWorldTarget.PanelKind.Loader,"CLAMPS / PLAN / GANYMEDE");
            for(int n=1;n<loaderRoute.Length;n++)
            {
                var from=loaderRoute[n-1];var to=loaderRoute[n];var direction=to-from;float length=direction.magnitude;
                for(float d=1;d<length;d+=2)
                {var at=Vector3.Lerp(from,to,d/length);at.y=environment.GroundAt(at)+.025f;var stripe=CreateCube("Loader route marking",at,new Vector3(.12f,.02f,.65f),Orange);stripe.transform.rotation=Quaternion.LookRotation(new Vector3(direction.x,0,direction.z));Destroy(stripe.GetComponent<Collider>());}
            }
        }

        private bool HandleAuthoredTarget(MarsWorldTarget target)
        {
            if(environment==null||!environment.Loaded)return false;
            if(loaderMoving){localNotice="Loader is transporting the tested orbiter. Keep the marked lane clear.";return true;}
            if(target.Panel==MarsWorldTarget.PanelKind.Airlock)
            {
                if(airlockOpen&&Player!=null&&Mathf.Abs(Player.transform.position.z-environment.Point(4,-2.35f,0).z)<1.1f){localNotice="Step clear of the doors before closing them.";return true;}
                airlockOpen=!airlockOpen;localNotice=airlockOpen?"Bay doors opening. The frame and parts are ready inside.":"Bay doors closing.";return true;
            }
            MarsActionKind action;
            switch(target.Panel)
            {
                case MarsWorldTarget.PanelKind.PickSensor:action=MarsActionKind.SelectSensorPart;break;
                case MarsWorldTarget.PanelKind.PickAntenna:action=MarsActionKind.SelectAntennaPart;break;
                case MarsWorldTarget.PanelKind.PickShield:action=MarsActionKind.SelectShieldPart;break;
                case MarsWorldTarget.PanelKind.MountSensor:action=MarsActionKind.PlaceSensorMount;break;
                case MarsWorldTarget.PanelKind.MountAntenna:action=MarsActionKind.PlaceAntennaMount;break;
                case MarsWorldTarget.PanelKind.MountShield:action=MarsActionKind.PlaceShieldMount;break;
                case MarsWorldTarget.PanelKind.PowerPort:action=MarsActionKind.ConnectPower;break;
                case MarsWorldTarget.PanelKind.DataPort:action=MarsActionKind.ConnectData;break;
                case MarsWorldTarget.PanelKind.LeftClamp:action=MarsActionKind.CloseLeftClamp;break;
                case MarsWorldTarget.PanelKind.RightClamp:action=MarsActionKind.CloseRightClamp;break;
                default:return false;
            }
            Act(action);UpdateWorldVisuals();return true;
        }

        private void UpdateAuthoredVisuals(MarsStageContext c)
        {
            float step=Time.deltaTime*4;
            airlockLeft.transform.position=Vector3.MoveTowards(airlockLeft.transform.position,airlockLeftClosed-(airlockOpen?doorSlide:Vector3.zero),step);
            airlockRight.transform.position=Vector3.MoveTowards(airlockRight.transform.position,airlockRightClosed+(airlockOpen?doorSlide:Vector3.zero),step);
            if(c.revision==authoredRevision)return;authoredRevision=c.revision;
            sensorPickup.SetActive(!c.sensorInstalled&&c.selectedPart!="sensor");antennaPickup.SetActive(!c.antennaInstalled&&c.selectedPart!="antenna");shieldPickup.SetActive(!c.shieldInstalled&&c.selectedPart!="shield");
            if(heldPartId!=c.selectedPart)
            {
                if(heldPart!=null){heldPart.SetActive(false);Destroy(heldPart);}heldPartId=c.selectedPart;
                if(!string.IsNullOrEmpty(heldPartId)&&viewCamera!=null)
                {
                    var source=heldPartId=="sensor"?sensorPickup:heldPartId=="antenna"?antennaPickup:shieldPickup;
                    heldPart=Instantiate(source,viewCamera.transform,true);heldPart.name="Carried "+heldPartId;heldPart.SetActive(true);
                    float size=heldPart.GetComponent<Renderer>().bounds.size.magnitude;
                    heldPart.transform.localScale*=.38f/Mathf.Max(.01f,size);heldPart.transform.localPosition=new Vector3(.35f,-.35f,.7f);
                    foreach(var t in heldPart.GetComponentsInChildren<MarsWorldTarget>())Destroy(t);
                    foreach(var collider in heldPart.GetComponentsInChildren<Collider>())Destroy(collider);
                }
            }
            sensorVisual.SetActive(c.sensorInstalled);lightAntennaVisual.SetActive(c.antennaInstalled&&c.antenna=="light");directionalAntennaVisual.SetActive(c.antennaInstalled&&c.antenna=="directional");
            basicShieldVisual.SetActive(c.shieldInstalled&&c.shield=="basic");reinforcedShieldVisual.SetActive(c.shieldInstalled&&c.shield=="reinforced");
            powerCable.SetActive(c.powerConnected&&!c.orbiterSeated);dataCable.SetActive(c.dataConnected&&!c.orbiterSeated);
            leftClampVisual.transform.position=cradlePosition+new Vector3(c.leftClamp?-.62f:-1.12f,0,0);
            rightClampVisual.transform.position=cradlePosition+new Vector3(c.rightClamp?.62f:1.12f,0,0);
            if(!loaderMoving){orbiterAssembly.transform.position=c.orbiterSeated?cradlePosition:standPosition;orbiterAssembly.transform.rotation=standRotation;loader.transform.position=c.orbiterSeated?loaderRoute.Last():loaderRoute[0];}
        }

        private void BeginLoading()
        {
            if(loaderMoving)return;var c=stage.Context;
            if(c.orbiterSeated){Act(MarsActionKind.ReturnOrbiter);UpdateWorldVisuals();ShowTaskPanel(MarsWorldTarget.PanelKind.Loader);return;}
            if(!c.testPassed||!c.withinCapacity){Act(MarsActionKind.SeatOrbiter);ShowTaskPanel(MarsWorldTarget.PanelKind.Loader);return;}
            if(environment==null||!environment.Loaded){Act(MarsActionKind.SeatOrbiter);StartCoroutine(AnimateLoader());return;}
            ClosePanel(true);StartCoroutine(AnimateAuthoredLoader());
        }
        private IEnumerator AnimateAuthoredLoader()
        {
            if(loaderMoving)yield break;loaderMoving=true;airlockOpen=true;
            localNotice="Loading the tested orbiter. The transport follows the marked lane to your ship.";
            yield return new WaitForSeconds(1.6f);
            for(int i=1;i<loaderRoute.Length;i++)
            {
                var to=loaderRoute[i];
                while(Vector2.Distance(new Vector2(loader.transform.position.x,loader.transform.position.z),new Vector2(to.x,to.z))>.035f)
                {
                    var direction=to-loader.transform.position;direction.y=0;
                    if(direction.sqrMagnitude>.001f)loader.transform.rotation=Quaternion.RotateTowards(loader.transform.rotation,Quaternion.LookRotation(direction),110*Time.deltaTime);
                    var at=Vector3.MoveTowards(loader.transform.position,to,3.5f*Time.deltaTime);at.y=environment.GroundAt(at)+.01f;loader.transform.position=at;
                    orbiterAssembly.transform.position=at+Vector3.up*1.02f;orbiterAssembly.transform.rotation=loader.transform.rotation;
                    yield return null;
                }
            }
            Act(MarsActionKind.SeatOrbiter);loaderMoving=false;authoredRevision=-1;UpdateWorldVisuals();
            localNotice="Orbiter seated. Walk to the ship cradle and close both clamps, then receive the deployment plan.";
        }

        private void BuildAuthoredAssemblyPanel(RectTransform panel)
        {
            var c=stage.Context;Label(panel,"BUILD THE JUPITER ORBITER",.05f,.87f,.84f,.08f,32,Color.white);
            Label(panel,c.massKg+" / 300 kg  ·  "+(c.withinCapacity?(300-c.massKg)+" kg margin":"5 kg over limit"),.05f,.76f,.9f,.08f,28,c.withinCapacity?Green:Red);
            Label(panel,"FRAME + SENSOR · 210 kg    The sensor records charged particles; frequent mode doubles the sample count.",.05f,.65f,.90f,.07f,20,Color.white);
            ActionButton(panel,(c.sensorMode=="standard"?"✓ ":"")+"STANDARD · 6 SAMPLES",.05f,.54f,.42f,.08f,()=>PanelAct(MarsActionKind.SetStandardSensor));
            ActionButton(panel,(c.sensorMode=="frequent"?"✓ ":"")+"FREQUENT · 12 SAMPLES",.50f,.54f,.42f,.08f,()=>PanelAct(MarsActionKind.SetFrequentSensor));
            ActionButton(panel,(c.antenna=="light"?"✓ ":"")+"LIGHT ANTENNA · 20 kg",.05f,.43f,.42f,.08f,()=>PanelAct(MarsActionKind.SelectLightAntenna));
            ActionButton(panel,(c.antenna=="directional"?"✓ ":"")+"DIRECTIONAL · 45 kg",.50f,.43f,.42f,.08f,()=>PanelAct(MarsActionKind.SelectDirectionalAntenna));
            Label(panel,"Light: automatic lock, 10 s contact. Directional: aim first, then a 14 s contact at Ganymede.",.05f,.35f,.89f,.06f,18,Color.white);
            ActionButton(panel,(c.shield=="basic"?"✓ ":"")+"BASIC SHIELD · 20 kg",.05f,.24f,.42f,.08f,()=>PanelAct(MarsActionKind.SelectBasicShield));
            ActionButton(panel,(c.shield=="reinforced"?"✓ ":"")+"REINFORCED · 50 kg",.50f,.24f,.42f,.08f,()=>PanelAct(MarsActionKind.SelectReinforcedShield));
            Label(panel,"Reinforced adds long-term reserve; basic shielding can complete the first transmission.\nClose this panel. RIGHT CLICK a tabletop part, then LEFT CLICK its keyed mount on the frame.",.05f,.09f,.90f,.12f,19,Color.white);
        }
    }
}
