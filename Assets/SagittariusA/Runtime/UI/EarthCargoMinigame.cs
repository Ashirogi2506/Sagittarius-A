using System;
using System.Linq;
using SagittariusA.Gameplay.Data;
using SagittariusA.Gameplay.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.UI
{
    public sealed class EarthCargoMinigame : MonoBehaviour, IEarthMinigameRunner
    {
        public EarthCargoPacking Packing { get; private set; }
        private EarthMinigameContext context;
        private Action<EarthMinigameResult> finished;
        private GameObject root, contents;
        private RectTransform[] slots;
        private string selected;
        private float oldTime;
        private CursorLockMode oldCursor;
        private bool oldVisible;
        private static readonly Color CardColor=new Color(.065f,.095f,.14f),AccentColor=new Color(1,.46f,.08f),Teal=new Color(.22f,.73f,.71f);
        public bool Supports(EarthActivity activity)=>activity==EarthActivity.Cargo;
        public void Begin(EarthMinigameContext request,Action<EarthMinigameResult> callback)
        {
            if(root!=null)throw new InvalidOperationException("Cargo packing is already open.");
            if(request==null||request.activity!=EarthActivity.Cargo)throw new ArgumentException("Cargo context required.");
            context=request;finished=callback;Packing=new EarthCargoPacking(GetComponent<EarthGameplaySession>().Catalog,request.build,request.checks);
            selected=Packing.Items.FirstOrDefault()?.id;
            oldTime=Time.timeScale;oldCursor=Cursor.lockState;oldVisible=Cursor.visible;Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            root=new GameObject("Earth cargo packing",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));root.transform.SetParent(transform,false);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;root.GetComponent<Canvas>().sortingOrder=1100;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Render();
        }
        private void Render()
        {
            if(contents!=null){contents.SetActive(false);Destroy(contents);}
            var background=Box(root.transform,"Cargo task",0,0,1,1,new Color(.015f,.025f,.045f,.98f));contents=background.gameObject;
            Label(background,"EARTH / PRE-LAUNCH CHECK",.04f,.92f,.80f,.04f,20,AccentColor);
            Label(background,"PACK & LOCK THE CARGO",.04f,.83f,.84f,.08f,40,Color.white);
            Label(background,"Drag an item into a slot, or select it and click its starting slot. The panel occupies two adjacent spaces. Lock every item.",.04f,.75f,.91f,.07f,23,Color.white);
            Label(background,"STAGING AREA",.04f,.68f,.28f,.04f,20,AccentColor);
            Label(background,"CARGO BAY · "+Packing.Capacity+" SPACES",.36f,.68f,.58f,.04f,20,AccentColor);
            int index=0;
            foreach(var item in Packing.Items)
            {
                string id=item.id;var placed=Packing.Placement(id);float y=.58f-index*.085f;
                var choose=ActionButton(background,item.name+" · "+item.slots+" space(s)",.04f,y,.29f,.075f,()=>{selected=id;Render();},selected==id?AccentColor:CardColor);
                choose.gameObject.AddComponent<EarthCargoDrag>().Configure(this,id);
                index++;
            }
            int rows=(Packing.Capacity+Packing.Columns-1)/Packing.Columns;
            float cellW=.58f/Packing.Columns, cellH=rows==1?.24f:.15f;
            slots=new RectTransform[Packing.Capacity];
            for(int i=0;i<Packing.Capacity;i++)
            {
                int slot=i;var occupant=Packing.Occupant(slot);var placement=occupant==null?null:Packing.Placement(occupant.id);
                float x=.36f+(i%Packing.Columns)*cellW, y=.60f-cellH-(i/Packing.Columns)*(cellH+.02f);
                string label=occupant==null?"SLOT "+(i+1):occupant.name+"\n"+(placement.locked?"LOCKED":"LOOSE");
                var button=ActionButton(background,label,x,y,cellW-.012f,cellH,()=>{if(selected!=null)Packing.Place(selected,slot);Render();},occupant==null?CardColor:placement.locked?new Color(.12f,.40f,.37f):new Color(.34f,.22f,.12f));
                slots[i]=button.GetComponent<RectTransform>();
                Label(slots[i],(i+1).ToString(),.04f,.75f,.20f,.20f,17,Color.white);
                if(occupant?.id=="panel")for(int col=0;col<4;col++)Box(slots[i],"Solar cell",.09f+col*.21f,.06f,.16f,.16f,new Color(.17f,.38f,.63f));
            }
            Label(background,"CARGO RESTRAINTS",.36f,.27f,.59f,.04f,20,AccentColor);
            int locks=0;
            foreach(var item in Packing.Items)
            {
                string id=item.id;var placed=Packing.Placement(id);float width=.58f/Mathf.Max(1,Packing.Items.Length);
                var button=ActionButton(background,(placed!=null&&placed.locked?"SECURED":"LOCK")+"\n"+item.name,.36f+locks*width,.17f,width-.01f,.085f,()=>{Packing.Lock(id);Render();},placed!=null&&placed.locked?Teal:CardColor);
                button.interactable=placed!=null&&!placed.locked;locks++;
            }
            Label(background,Packing.Notice,.04f,.075f,.69f,.07f,23,Packing.Complete?Teal:Color.white);
            ActionButton(background,"RETURN SELECTED ITEM",.04f,.17f,.29f,.07f,()=>{if(selected!=null)Packing.Remove(selected);Render();},CardColor);
            ActionButton(background,"CANCEL",.76f,.045f,.09f,.065f,Cancel,CardColor);
            ActionButton(background,"CONFIRM CARGO",.86f,.045f,.11f,.065f,()=>Finish(true),AccentColor).interactable=Packing.Complete;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(contents.GetComponentsInChildren<Button>().FirstOrDefault(b=>b.interactable)?.gameObject);
        }
        internal void DragStarted(string id){selected=id;}
        internal void Drop(string id,Vector2 pointer)
        {
            for(int i=0;i<slots.Length;i++)if(RectTransformUtility.RectangleContainsScreenPoint(slots[i],pointer,null)){Packing.Place(id,i);Render();return;}
            Render();
        }
        public void Pause(){if(root!=null)root.SetActive(false);}
        public void Resume(){if(root!=null)root.SetActive(true);}
        public void Cancel()=>Finish(false);
        private void Update(){if(root!=null&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)Cancel();}
        private void Finish(bool completed)
        {
            if(root==null||completed&&!Packing.Complete)return;
            var callback=finished;finished=null;var result=new EarthMinigameResult{runId=context.runId,buildRevision=context.buildRevision,activity=context.activity,completed=completed,checks=completed?Packing.Result():null};
            root.SetActive(false);Destroy(root);root=null;Time.timeScale=oldTime;Cursor.lockState=oldCursor;Cursor.visible=oldVisible;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);callback?.Invoke(result);
        }
        private void OnDestroy(){Cancel();}
    }
    internal sealed class EarthCargoDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private EarthCargoMinigame owner;private string id;
        public void Configure(EarthCargoMinigame value,string item){owner=value;id=item;}
        public void OnBeginDrag(PointerEventData e){owner.DragStarted(id);}
        public void OnDrag(PointerEventData e){}
        public void OnEndDrag(PointerEventData e){owner.Drop(id,e.position);}
    }
}
