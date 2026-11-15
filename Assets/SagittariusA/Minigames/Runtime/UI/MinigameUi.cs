using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SagittariusA.Minigames.UI
{
    // FARSIGNAL minigame palette: dark navy/charcoal, warm white, soft gray, restrained orange.
    public static class MinigameTheme
    {
        public static readonly Color Backdrop = new Color(.035f, .055f, .095f, .88f);
        public static readonly Color Panel = new Color(.078f, .102f, .149f, 1f);
        public static readonly Color Card = new Color(.114f, .145f, .2f, 1f);
        public static readonly Color CardRaised = new Color(.149f, .184f, .247f, 1f);
        public static readonly Color Line = new Color(.22f, .267f, .345f, 1f);
        public static readonly Color WarmWhite = new Color(.957f, .937f, .902f, 1f);
        public static readonly Color SoftGray = new Color(.62f, .67f, .74f, 1f);
        public static readonly Color Orange = new Color(1f, .55f, .24f, 1f);
        public static readonly Color OrangeDim = new Color(.45f, .27f, .14f, 1f);
        public static readonly Color Good = new Color(.39f, .82f, .64f, 1f);
        public static readonly Color Warn = new Color(.95f, .74f, .32f, 1f);
        public static readonly Color Bad = new Color(1f, .45f, .4f, 1f);

        private static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static Sprite rounded, roundedSmall, circle, hatch;
        // Procedural sprites so the shell needs no imported art; designers can swap them later (see docs).
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(64, 18);
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = MakeRounded(32, 8);
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(64, 32, false);
        public static Sprite Hatch => hatch != null ? hatch : hatch = MakeHatch();

        private static Sprite MakeRounded(int size, int radius, bool sliced = true)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "MinigameRounded" + size };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + .5f, radius, size - radius), cy = Mathf.Clamp(y + .5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + .5f - cx) * (x + .5f - cx) + (y + .5f - cy) * (y + .5f - cy));
                    byte a = (byte)(Mathf.Clamp01(radius - d + .5f) * 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px); tex.Apply(false, true);
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite MakeHatch()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "MinigameHatch" };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) px[y * s + x] = ((x + y) % s) < 4 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(px); tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f), 100);
        }
    }

    // Small layout helpers. Positions are pixels from the parent's top-left corner (x right, y down).
    public static class Ui
    {
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return Place((RectTransform)go.transform, x, y, w, h);
        }

        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color, Sprite sprite = null, bool raycast = false)
        {
            var img = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            img.sprite = sprite ?? MinigameTheme.Rounded; img.type = Image.Type.Sliced; img.color = color; img.raycastTarget = raycast;
            return img;
        }

        public static Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color,
            TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var t = Rect(parent, "Text", x, y, w, h).gameObject.AddComponent<Text>();
            t.font = MinigameTheme.Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = anchor; t.fontStyle = style;
            t.raycastTarget = false; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        public enum ButtonStyle { Primary, Secondary, Ghost }

        public static Button Button(Transform parent, string label, float x, float y, float w, float h, Action onClick, ButtonStyle style = ButtonStyle.Secondary, int fontSize = 20)
        {
            var bg = Panel(parent, "Button_" + label, x, y, w, h,
                style == ButtonStyle.Primary ? MinigameTheme.Orange : style == ButtonStyle.Secondary ? MinigameTheme.CardRaised : new Color(1, 1, 1, .001f), null, true);
            var b = bg.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var c = b.colors;
            c.normalColor = Color.white; c.highlightedColor = new Color(1.12f, 1.1f, 1.05f, 1f); c.selectedColor = new Color(1.15f, 1.1f, 1f, 1f);
            c.pressedColor = new Color(.82f, .82f, .82f, 1f); c.disabledColor = new Color(.5f, .5f, .5f, .55f); c.fadeDuration = .06f;
            b.colors = c;
            var t = Label(bg.transform, label, 0, 0, w, h, fontSize, style == ButtonStyle.Primary ? new Color(.1f, .07f, .05f, 1f) : MinigameTheme.WarmWhite, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 6);
            if (style == ButtonStyle.Ghost) { var o = bg.gameObject.AddComponent<Outline>(); o.effectColor = MinigameTheme.Line; o.effectDistance = new Vector2(2, -2); }
            if (onClick != null) b.onClick.AddListener(() => { MinigameAudio.Play(MinigameCue.Press); onClick(); });
            bg.gameObject.AddComponent<UiHoverCue>();
            return b;
        }

        public static void SetButtonLabel(Button b, string label) { var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = label; }

        // Horizontal meter: returns the fill RectTransform; set its anchorMax.x to the value.
        public static RectTransform Meter(Transform parent, string name, float x, float y, float w, float h, Color fill, out Image fillImage)
        {
            var track = Panel(parent, name, x, y, w, h, MinigameTheme.Line, MinigameTheme.RoundedSmall);
            var f = Panel(track.transform, "Fill", 0, 0, w, h, fill, MinigameTheme.RoundedSmall);
            var rt = f.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, .5f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            fillImage = f;
            return rt;
        }
        public static void SetMeter(RectTransform fill, float value) { fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1); }

        public static Slider Slider(Transform parent, string name, float x, float y, float w, float h, Action<float> changed)
        {
            var root = Rect(parent, name, x, y, w, h);
            var track = Panel(root, "Track", 0, h / 2 - 5, w, 10, MinigameTheme.Line, MinigameTheme.RoundedSmall);
            Stretch(track.rectTransform); track.rectTransform.anchorMin = new Vector2(0, .5f); track.rectTransform.anchorMax = new Vector2(1, .5f); track.rectTransform.sizeDelta = new Vector2(-16, 10);
            var fillArea = Rect(root, "Fill Area", 0, 0, w, h); Stretch(fillArea); fillArea.offsetMin = new Vector2(8, h / 2 - 5); fillArea.offsetMax = new Vector2(-8, -(h / 2 - 5));
            var fill = Panel(fillArea, "Fill", 0, 0, 10, 10, MinigameTheme.OrangeDim, MinigameTheme.RoundedSmall);
            var handleArea = Rect(root, "Handle Slide Area", 0, 0, w, h); Stretch(handleArea); handleArea.offsetMin = new Vector2(16, 0); handleArea.offsetMax = new Vector2(-16, 0);
            var handle = Panel(handleArea, "Handle", 0, 0, 32, 32, MinigameTheme.WarmWhite, MinigameTheme.Circle, true);
            handle.rectTransform.anchorMin = new Vector2(0, .5f); handle.rectTransform.anchorMax = new Vector2(0, .5f); handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.sizeDelta = new Vector2(34, 34);
            var hit = root.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, .001f);   // large click target
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle; s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1;
            var c = s.colors; c.highlightedColor = new Color(1f, .85f, .7f, 1f); c.selectedColor = MinigameTheme.Orange; c.pressedColor = MinigameTheme.Orange; s.colors = c;
            if (changed != null) s.onValueChanged.AddListener(v => changed(v));
            root.gameObject.AddComponent<UiHoverCue>();
            return s;
        }

        // Small rounded tag with a word, so state is never shown by color alone.
        public static Text Chip(Transform parent, string text, float x, float y, float w, float h, Color color, out Image background)
        {
            background = Panel(parent, "Chip", x, y, w, h, color, MinigameTheme.RoundedSmall);
            var t = Label(background.transform, text, 0, 0, w, h, 14, new Color(.08f, .08f, .1f, 1f), TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(t.rectTransform, 2);
            return t;
        }
        public static void SetChip(Text chip, Image bg, string text, Color color) { chip.text = text; bg.color = color; }

        public static bool Contains(RectTransform rt, PointerEventData e) =>
            rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, e.position, e.pressEventCamera);
    }

    // Plays the hover cue on pointer enter / keyboard focus (visual hover comes from the Selectable tint).
    public sealed class UiHoverCue : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        public void OnPointerEnter(PointerEventData e) { MinigameAudio.Play(MinigameCue.Hover, .35f); }
        public void OnSelect(BaseEventData e) { if (!(e is PointerEventData)) MinigameAudio.Play(MinigameCue.Hover, .35f); }
    }

    // Forwards pointer and drag events from any UI element.
    public sealed class UiPointerRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        public Action<PointerEventData> Down, Up, Enter, Exit, BeginDrag, Drag, EndDrag;
        public Action<bool> Focus;
        public Action Submit;   // keyboard/controller activation only (mouse uses Down/Up)
        public void OnPointerDown(PointerEventData e) => Down?.Invoke(e);
        public void OnPointerUp(PointerEventData e) => Up?.Invoke(e);
        public void OnPointerEnter(PointerEventData e) => Enter?.Invoke(e);
        public void OnPointerExit(PointerEventData e) => Exit?.Invoke(e);
        public void OnBeginDrag(PointerEventData e) => BeginDrag?.Invoke(e);
        public void OnDrag(PointerEventData e) => Drag?.Invoke(e);
        public void OnEndDrag(PointerEventData e) => EndDrag?.Invoke(e);
        public void OnSelect(BaseEventData e) => Focus?.Invoke(true);
        public void OnDeselect(BaseEventData e) => Focus?.Invoke(false);
        public void OnSubmit(BaseEventData e) => Submit?.Invoke();
    }
}
