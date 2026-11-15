using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.Flow
{
    // Reusable screen-space task panel. World progress is committed only after
    // the puzzle succeeds; closing it discards this attempt, not earlier tasks.
    public sealed class MoonRepairPanel : MonoBehaviour
    {
        public enum TaskKind { Bolts, Dust, Sequence, Wires, Weld, Panel, Calibration, Diagnostic }
        private static readonly Color Ink = new Color(.035f, .055f, .08f), Plate = new Color(.09f, .14f, .19f),
            Muted = new Color(.55f, .67f, .73f), Orange = new Color(1f, .46f, .09f), Green = new Color(.25f, .9f, .65f);
        private static readonly Color[] WireColors = { new Color(.95f,.37f,.31f), new Color(.3f,.72f,1f), new Color(1f,.8f,.25f), new Color(.75f,.48f,1f) };
        private static readonly Vector2[] Bolts = { new Vector2(.25f,.75f), new Vector2(.75f,.75f), new Vector2(.75f,.25f), new Vector2(.25f,.25f) };
        private static readonly int[] CrossOrder = { 0, 2, 1, 3 }, Sockets = { 2, 0, 3, 1 };
        private static readonly float[] CalibrationTargets = { .28f, .72f, .46f };
        private readonly List<RectTransform> elements = new List<RectTransform>();
        private readonly bool[] done = new bool[24];
        private readonly float[] settings = { .1f, .1f, .1f };
        private RectTransform board, bar, liveWire, panelPiece;
        private Text status, progress, instructions;
        private Action<bool> finished;
        private TaskKind kind;
        private string actionId;
        private int count, drag = -1;
        private bool solved, closed, fitted;
        private float completedAt, openedAt;
        private Vector2 lastPointer;
        private string[] sequence;
        private Canvas canvas;
        public RectTransform Board => board;
        public TaskKind Kind => kind;
        public bool Solved => solved;
        public string ActionId => actionId;

        public static bool Supports(string id)
        {
            switch (id)
            {
                case MoonInteractionIds.OpenWheelCover: case MoonInteractionIds.CloseWheelCover:
                case MoonInteractionIds.CleanPatch1: case MoonInteractionIds.CleanPatch2: case MoonInteractionIds.CleanPatch3:
                case MoonInteractionIds.RetestCart: case MoonInteractionIds.IsolateCircuit: case MoonInteractionIds.RepairCircuit:
                case MoonInteractionIds.RepairLaser: case MoonInteractionIds.InstallPanel: case MoonInteractionIds.RestoreEssential:
                case MoonInteractionIds.RestoreRelay: case MoonInteractionIds.UploadRoute: return true;
                default: return false;
            }
        }

        public static MoonRepairPanel Open(Transform parent, string id, Action<bool> callback)
        {
            var go = new GameObject("Moon repair task", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MoonRepairPanel>();
            view.actionId = id; view.finished = callback; view.Build();
            return view;
        }

        private void Build()
        {
            canvas = GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            var scaler = GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; openedAt = Time.unscaledTime;
            string title, hint;
            switch (actionId)
            {
                case MoonInteractionIds.OpenWheelCover: kind = TaskKind.Bolts; title = "RELEASE WHEEL COVER"; hint = "Click the bolts in cross order: 1  >  3  >  2  >  4"; break;
                case MoonInteractionIds.CloseWheelCover: kind = TaskKind.Bolts; title = "SECURE WHEEL COVER"; hint = "Tighten the bolts in cross order: 1  >  3  >  2  >  4"; break;
                case MoonInteractionIds.CleanPatch1: case MoonInteractionIds.CleanPatch2: case MoonInteractionIds.CleanPatch3:
                    kind = TaskKind.Dust; title = "CLEAR WHEEL DUST / " + actionId.Substring(actionId.Length - 1); hint = "Hold left mouse and brush across every dust deposit."; break;
                case MoonInteractionIds.IsolateCircuit:
                    kind = TaskKind.Sequence; title = "ISOLATE DAMAGED CIRCUIT"; sequence = new[] { "RELAY", "AUXILIARY", "MAIN BUS" }; hint = "Disconnect in order: RELAY  >  AUXILIARY  >  MAIN BUS"; break;
                case MoonInteractionIds.RestoreEssential:
                    kind = TaskKind.Sequence; title = "RESTORE ESSENTIAL POWER"; sequence = new[] { "LIFE SUPPORT", "COOLING", "RADIO" }; hint = "Power up in order: LIFE SUPPORT  >  COOLING  >  RADIO"; break;
                case MoonInteractionIds.RepairCircuit:
                    kind = TaskKind.Wires; title = "REPAIR CIRCUIT HARNESS"; hint = "Drag each cable to the socket with the same letter and colour."; break;
                case MoonInteractionIds.RepairLaser:
                    kind = TaskKind.Weld; title = "LASER REPAIR / TRACE THE SEAM"; hint = "Hold left mouse and trace numbered weld points from 1 to 8."; break;
                case MoonInteractionIds.InstallPanel:
                    kind = TaskKind.Panel; title = "FIT P-4 POWER PANEL"; hint = "Drag the P-4 panel into its socket, then secure all four corner latches."; break;
                case MoonInteractionIds.RetestCart:
                    kind = TaskKind.Diagnostic; title = "WHEEL DRIVE DIAGNOSTIC"; hint = "Click TEST when the moving indicator enters the green zone. Complete 3 cycles."; break;
                case MoonInteractionIds.UploadRoute:
                    kind = TaskKind.Sequence; title = "TRANSFER SURVEY DATA"; sequence = new[] { "ROUTE", "CHECKSUM", "TRANSMIT" }; hint = "Confirm the data transfer: ROUTE  >  CHECKSUM  >  TRANSMIT"; break;
                default:
                    kind = TaskKind.Calibration; title = "TUNE POLE-7 RELAY"; hint = "Drag all three sliders onto their marked targets, then press LOCK SIGNAL."; break;
            }
            Box(transform, "Dim world", 0, 0, 1, 1, new Color(.005f,.01f,.025f,.88f));
            var frame = Box(transform, "Repair console", .13f, .12f, .74f, .77f, Ink);
            Box(frame, "Accent", 0, .985f, 1, .015f, Orange);
            Label(frame, "FAR SIGNAL    /    FIELD SERVICE", .045f, .90f, .75f, .065f, 18, Orange);
            Label(frame, title, .045f, .805f, .86f, .10f, 32, Color.white);
            instructions = Label(frame, hint, .045f, .73f, .91f, .065f, 20, Muted);
            ActionButton(frame, "X", .92f, .89f, .055f, .07f, () => Close(false), Plate);
            board = Box(frame, "Task board", .045f, .19f, .91f, .52f, Plate);
            progress = Label(frame, "", .045f, .104f, .21f, .06f, 19, Green);
            var track = Box(frame, "Progress track", .26f, .12f, .695f, .014f, Plate);
            bar = Box(track, "Progress fill", 0, 0, 0, 1, Green);
            status = Label(frame, "ESC / X to close  ·  Completed tasks save automatically", .045f, .025f, .91f, .065f, 18, Muted);
            BuildBoard(); Refresh();
        }

        private RectTransform Tile(string name, Vector2 center, Vector2 size, Color color, string text = "", int fontSize = 25)
        {
            var r = Box(board, name, center.x - size.x / 2, center.y - size.y / 2, size.x, size.y, color);
            if (text.Length > 0) { var label = Label(r, text, .02f, .02f, .96f, .96f, fontSize, Color.white); label.alignment = TextAnchor.MiddleCenter; }
            return r;
        }
        private void BuildBoard()
        {
            if (kind == TaskKind.Bolts)
            {
                Tile("Cover", new Vector2(.5f,.5f), new Vector2(.42f,.48f), new Color(.14f,.21f,.27f), "WHEEL ACCESS\nPOWER ISOLATED", 25);
                for (int i = 0; i < 4; i++) elements.Add(Tile("Bolt " + (i+1), Bolts[i], new Vector2(.085f,.16f), Orange, (i+1).ToString()));
            }
            else if (kind == TaskKind.Dust)
            {
                for (int n = 0; n < 16; n++)
                {
                    float angle = n * Mathf.PI * 2 / 16;
                    var spoke = Tile("Wheel spoke", new Vector2(.5f + Mathf.Cos(angle)*.26f, .5f + Mathf.Sin(angle)*.34f), new Vector2(.03f,.12f), Muted);
                    spoke.localEulerAngles = new Vector3(0,0, angle * Mathf.Rad2Deg - 90);
                }
                for (int i = 0; i < 20; i++) elements.Add(Tile("Dust " + i, DustPoint(i), new Vector2(.08f,.13f), new Color(.56f,.43f,.25f), "•", 25));
            }
            else if (kind == TaskKind.Sequence)
            {
                for (int i = 0; i < 3; i++)
                {
                    int index = new[] { 1, 2, 0 }[i];
                    elements.Add(Tile("Switch " + i, new Vector2(.22f + .28f*i, .5f), new Vector2(.23f,.44f), new Color(.18f,.25f,.31f), sequence[index], 21));
                    Tile("Lamp " + i, new Vector2(.22f + .28f*i,.80f), new Vector2(.03f,.055f), Orange);
                }
            }
            else if (kind == TaskKind.Wires)
            {
                Label(board,"INPUT",.09f,.88f,.2f,.08f,18,Muted); Label(board,"OUTPUT",.76f,.88f,.2f,.08f,18,Muted);
                for (int i = 0; i < 4; i++)
                {
                    elements.Add(Tile("Cable " + i, WirePoint(i, false), new Vector2(.12f,.135f), WireColors[i], ((char)('A'+i)).ToString()));
                    Tile("Socket " + i, WirePoint(i, true), new Vector2(.12f,.135f), WireColors[i], ((char)('A'+i)).ToString());
                }
                liveWire = Tile("Held cable", Vector2.zero, Vector2.one * .01f, Orange); liveWire.gameObject.SetActive(false);
            }
            else if (kind == TaskKind.Weld)
            {
                Tile("Broken busbar", new Vector2(.5f,.5f), new Vector2(.8f,.1f), Muted);
                for (int i = 0; i < 8; i++) elements.Add(Tile("Weld " + i, WeldPoint(i), new Vector2(.07f,.13f), Orange, (i+1).ToString(),20));
            }
            else if (kind == TaskKind.Panel)
            {
                Tile("P-4 socket",new Vector2(.71f,.5f), new Vector2(.36f,.62f), Muted);
                Tile("P-4 socket interior",new Vector2(.71f,.5f), new Vector2(.33f,.56f), Ink, "P-4\nALIGN HERE",24);
                panelPiece = Tile("P-4 module",new Vector2(.25f,.5f), new Vector2(.29f,.46f), new Color(.15f,.38f,.5f), "P-4\nPOWER MODULE",23);
                for (int i = 0; i < 4; i++)
                {
                    var r = Tile("Latch " + i, LatchPoint(i), new Vector2(.045f,.09f), Orange, "+",20);
                    r.gameObject.SetActive(false); elements.Add(r);
                }
            }
            else if (kind == TaskKind.Calibration)
            {
                for (int i = 0; i < 3; i++)
                {
                    float y = .78f - .24f*i;
                    Label(board, new[] {"FREQUENCY", "PHASE", "GAIN"}[i], .035f, y-.06f,.19f,.12f,18,Muted);
                    Tile("Slider track",new Vector2(.59f,y),new Vector2(.63f,.018f),Muted);
                    Tile("Target band",new Vector2(.29f + .6f*CalibrationTargets[i],y),new Vector2(.035f,.13f),new Color(.16f,.5f,.36f));
                    elements.Add(Tile("Slider " + i,new Vector2(.29f + .6f*settings[i],y),new Vector2(.025f,.12f),Orange));
                }
                Tile("Lock signal",new Vector2(.5f,.08f),new Vector2(.3f,.12f),Orange,"LOCK SIGNAL",18);
            }
            else if (kind == TaskKind.Diagnostic)
            {
                Label(board,"MOTOR CURRENT / OPTIMAL RANGE",.15f,.75f,.75f,.12f,23,Muted);
                Tile("Gauge",new Vector2(.5f,.55f),new Vector2(.7f,.1f),Ink);
                Tile("Good range",new Vector2(.59f,.55f),new Vector2(.14f,.13f),new Color(.16f,.5f,.36f));
                elements.Add(Tile("Needle",new Vector2(.15f,.55f),new Vector2(.008f,.2f),Color.white));
                Tile("Test",new Vector2(.5f,.25f),new Vector2(.3f,.18f),Orange,"TEST",25);
            }
        }

        private void Update()
        {
            if (closed) return;
            if (solved) { if (Time.unscaledTime >= completedAt + .7f) Close(true); return; }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Close(false); return; }
            if (kind == TaskKind.Diagnostic) SetCenter(elements[0], new Vector2(NeedleX(), .55f));
            // Ignore the click that opened this panel.
            if (Time.unscaledTime - openedAt < .12f || Mouse.current == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(board, Mouse.current.position.ReadValue(), null, out var local);
            var p = new Vector2((local.x - board.rect.xMin) / board.rect.width, (local.y - board.rect.yMin) / board.rect.height);
            Pointer(p, Mouse.current.leftButton.wasPressedThisFrame, Mouse.current.leftButton.isPressed, Mouse.current.leftButton.wasReleasedThisFrame);
        }

        private void Pointer(Vector2 p, bool pressed, bool held, bool released)
        {
            if (solved || closed) return;
            if (kind == TaskKind.Bolts && pressed)
            {
                for (int i = 0; i < 4; i++) if (Near(p, Bolts[i], .07f, .12f) && !done[i])
                {
                    if (i == CrossOrder[count]) { done[i] = true; count++; Feedback("Bolt " + (i+1) + " secured / released", false); }
                    else Feedback("Use the cross order shown above. Next: " + (CrossOrder[count]+1), true);
                }
            }
            else if (kind == TaskKind.Dust && held && (pressed || Vector2.Distance(p,lastPointer) > .003f))
            {
                for (int i = 0; i < 20; i++) if (!done[i] && Near(p,DustPoint(i),.065f,.13f)) { done[i] = true; count++; }
            }
            else if (kind == TaskKind.Sequence && pressed)
            {
                for (int i = 0; i < 3; i++) if (Near(p,new Vector2(.22f+.28f*i,.5f),.115f,.22f) && !done[i])
                {
                    int index = new[] { 1,2,0 }[i];
                    if (index == count) { done[i] = true; count++; Feedback(sequence[index] + " confirmed", false); }
                    else Feedback("Next required step: " + sequence[count], true);
                }
            }
            else if (kind == TaskKind.Wires)
            {
                if (pressed) for (int i=0;i<4;i++) if (!done[i] && Near(p,WirePoint(i,false),.075f,.085f)) drag=i;
                if (drag >= 0 && held) { liveWire.gameObject.SetActive(true); Line(liveWire,WirePoint(drag,false),p); }
                if (released && drag >= 0)
                {
                    if (Near(p,WirePoint(drag,true),.085f,.09f))
                    {
                        done[drag]=true; count++;
                        var wire = Tile("Connected cable", Vector2.zero, Vector2.one*.01f, WireColors[drag]);
                        Line(wire,WirePoint(drag,false),WirePoint(drag,true)); wire.SetAsFirstSibling();
                        Feedback("Cable " + (char)('A'+drag) + " connected",false);
                    }
                    else Feedback("Match the letter at the other end. Try again.",true);
                    liveWire.gameObject.SetActive(false); drag=-1;
                }
            }
            else if (kind == TaskKind.Weld && held)
            {
                if (count < 8 && Near(p,WeldPoint(count),.05f,.12f)) { done[count]=true; count++; }
            }
            else if (kind == TaskKind.Panel)
            {
                if (!fitted)
                {
                    if (pressed && Near(p,new Vector2(.25f,.5f),.16f,.26f)) drag=0;
                    if (drag==0 && held) SetCenter(panelPiece,p);
                    if (drag==0 && released)
                    {
                        fitted=Near(p,new Vector2(.71f,.5f),.11f,.15f); drag=-1;
                        SetCenter(panelPiece,fitted?new Vector2(.71f,.5f):new Vector2(.25f,.5f));
                        foreach(var e in elements) e.gameObject.SetActive(fitted);
                        Feedback(fitted?"Panel aligned. Secure all four latches.":"Centre the panel inside the socket.",!fitted);
                    }
                }
                else if (pressed) for(int i=0;i<4;i++) if(!done[i] && Near(p,LatchPoint(i),.045f,.08f)){done[i]=true;count++;}
            }
            else if (kind == TaskKind.Calibration)
            {
                if (pressed) for(int i=0;i<3;i++) if(Near(p,new Vector2(.59f,.78f-.24f*i),.34f,.08f)) drag=i;
                if(drag>=0 && held){settings[drag]=Mathf.Clamp01((p.x-.29f)/.6f);SetCenter(elements[drag],new Vector2(.29f+.6f*settings[drag],.78f-.24f*drag));}
                if(released)drag=-1;
                count=0;for(int i=0;i<3;i++)if(Mathf.Abs(settings[i]-CalibrationTargets[i])<.035f)count++;
                if(pressed && Near(p,new Vector2(.5f,.08f),.15f,.065f))
                { if(count==3) Solve(); else Feedback("Align every slider with its green target before locking.",true); }
            }
            else if(kind==TaskKind.Diagnostic && pressed && Near(p,new Vector2(.5f,.25f),.15f,.1f))
            {
                if(NeedleX()>=.52f && NeedleX()<=.66f){count++;Feedback("Drive cycle " + count + " / 3 passed",false);openedAt=Time.unscaledTime;}
                else Feedback("Outside the green range. Wait for the indicator and try again.",true);
            }
            lastPointer=p; Refresh();
            if(kind!=TaskKind.Calibration && count>=Required())Solve();
        }

        private int Required() => kind==TaskKind.Dust?20:kind==TaskKind.Weld?8:kind==TaskKind.Sequence||kind==TaskKind.Calibration||kind==TaskKind.Diagnostic?3:4;
        private void Refresh()
        {
            progress.text = solved ? "TASK COMPLETE" : count + " / " + Required() + " COMPLETE";
            bar.anchorMax = new Vector2((float)count/Required(),1);
            if(kind!=TaskKind.Calibration && kind!=TaskKind.Diagnostic)
                for(int i=0;i<elements.Count;i++) if(done[i])
                { if(kind==TaskKind.Dust) elements[i].gameObject.SetActive(false); else elements[i].GetComponent<Image>().color=Green; }
        }
        private void Solve()
        {
            if(solved)return; solved=true;completedAt=Time.unscaledTime;
            instructions.text="TASK COMPLETE";instructions.color=Green;
            Feedback("Confirmed. Returning to the surface...",false);Refresh();
        }
        private void Feedback(string value,bool error){status.text=value;status.color=error?new Color(1f,.55f,.35f):Green;}
        private float NeedleX()=>.15f+.7f*Mathf.PingPong((Time.unscaledTime-openedAt)*.42f,1);
        private static Vector2 DustPoint(int i)=>new Vector2(.27f+(i%5)*.115f,.25f+(i/5)*.17f);
        private static Vector2 WeldPoint(int i)=>new Vector2(.15f+i*.1f,.5f+Mathf.Sin(i*1.6f)*.12f);
        private static Vector2 WirePoint(int i,bool output)=>new Vector2(output?.82f:.18f,.78f-.19f*(output?Sockets[i]:i));
        private static Vector2 LatchPoint(int i)=>new Vector2(.71f+(i%2==0?-.16f:.16f),.5f+(i<2?.27f:-.27f));
        private static bool Near(Vector2 p,Vector2 centre,float x,float y)=>Mathf.Abs(p.x-centre.x)<=x && Mathf.Abs(p.y-centre.y)<=y;
        private static void SetCenter(RectTransform rect,Vector2 center)
        { Vector2 size=rect.anchorMax-rect.anchorMin;rect.anchorMin=center-size/2;rect.anchorMax=center+size/2; }
        private void Line(RectTransform rect,Vector2 a,Vector2 b)
        {
            Vector2 start=Vector2.Scale(a,board.rect.size),end=Vector2.Scale(b,board.rect.size),delta=end-start;
            rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=new Vector2(0,.5f);
            rect.anchoredPosition=start;rect.sizeDelta=new Vector2(delta.magnitude,8);
            rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        private void Close(bool success)
        {
            if(closed)return;closed=true;
            var callback=finished;finished=null;
            gameObject.SetActive(false);Destroy(gameObject);callback?.Invoke(success && solved);
        }
        public void Cancel() { Close(false); }
        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            drag = -1;
            if (liveWire != null) liveWire.gameObject.SetActive(false);
            if (panelPiece != null && !fitted) SetCenter(panelPiece, new Vector2(.25f, .5f));
        }
        private void OnDestroy(){if(!closed){closed=true;var callback=finished;finished=null;callback?.Invoke(false);}}
    }
}
