using System;
using SagittariusA.Gameplay.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.UI
{
    // Reusable pre-task explanation. Reading, cancelling and Help never grant
    // mission progress. The caller owns the actual action after acceptance.
    public sealed class TaskBriefingView : MonoBehaviour
    {
        public Button BeginButton { get; private set; }
        public Button BackButton { get; private set; }
        public CampaignTaskBriefing Briefing { get; private set; }
        private Action<bool> completed;
        private bool closed;
        private float savedTimeScale, openedAt;
        private CursorLockMode savedCursorLock;
        private bool savedCursorVisible;
        private GameObject savedSelection;

        public static TaskBriefingView Open(Transform parent, CampaignTaskBriefing data, Action<bool> callback, bool helpOnly = false)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var root = new GameObject("Task briefing",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            root.transform.SetParent(parent,false);
            var view = root.AddComponent<TaskBriefingView>();
            view.Briefing = data; view.completed = callback; view.Build(helpOnly); return view;
        }

        private void Build(bool helpOnly)
        {
            savedTimeScale = Time.timeScale; savedCursorLock = Cursor.lockState; savedCursorVisible = Cursor.visible;
            savedSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            Time.timeScale = 0; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; openedAt = Time.unscaledTime;
            var canvas=GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1200;
            var scaler=GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            Color ink=new Color(.03f,.045f,.07f),card=new Color(.075f,.105f,.145f),muted=new Color(.7f,.77f,.83f),orange=new Color(1,.46f,.09f);
            Box(transform,"Dim surface",0,0,1,1,new Color(.005f,.01f,.02f,.9f));
            var frame=Box(transform,"Briefing card",.13f,.10f,.74f,.80f,ink);
            Box(frame,"Orange rule",0,.99f,1,.01f,orange);
            Label(frame,"FAR SIGNAL  /  BEFORE YOU BEGIN",.045f,.915f,.8f,.05f,20,orange);
            Label(frame,Briefing.Title,.045f,.815f,.91f,.09f,36,Color.white);
            Label(frame,Briefing.Speaker,.045f,.755f,.91f,.045f,19,orange);
            Label(frame,Briefing.Context,.045f,.60f,.91f,.15f,26,Color.white);
            var why=Box(frame,"Why this matters",.045f,.405f,.43f,.17f,card);
            Label(why,"WHY IT MATTERS",.045f,.68f,.91f,.25f,20,orange);
            Label(why,Briefing.Purpose,.045f,.06f,.91f,.60f,24,Color.white);
            var how=Box(frame,"What to do",.495f,.27f,.46f,.305f,card);
            Label(how,"WHAT TO DO",.045f,.82f,.91f,.13f,20,orange);
            Label(how,Briefing.Steps,.045f,.07f,.91f,.73f,24,Color.white);
            Label(frame,"CONTROLS",.045f,.34f,.43f,.045f,19,orange);
            Label(frame,Briefing.Controls,.045f,.20f,.43f,.135f,22,muted);
            Label(frame,"Take your time. On the Moon, press H to read the task explanation again.",.045f,.13f,.91f,.045f,18,muted);
            BackButton=ActionButton(frame,"BACK",.045f,.04f,.24f,.075f,()=>Close(false),card);
            BeginButton=ActionButton(frame,helpOnly?"RETURN TO TASK":"BEGIN TASK",.64f,.04f,.315f,.075f,()=>Close(true),orange);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(BeginButton.gameObject);
        }

        private void Update()
        {
            if(closed||Time.unscaledTime-openedAt<.15f)return;
            if(Keyboard.current!=null && Keyboard.current.escapeKey.wasPressedThisFrame)Close(false);
        }
        public void Cancel()=>Close(false);
        private void Close(bool accepted)
        {
            if(closed)return;closed=true;Restore();
            var callback=completed;completed=null;gameObject.SetActive(false);Destroy(gameObject);callback?.Invoke(accepted);
        }
        private void Restore()
        {
            Time.timeScale=savedTimeScale;Cursor.lockState=savedCursorLock;Cursor.visible=savedCursorVisible;
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(savedSelection!=null && savedSelection.activeInHierarchy?savedSelection:null);
        }
        private void OnDestroy(){if(!closed){closed=true;Restore();var callback=completed;completed=null;callback?.Invoke(false);}}
    }
}
