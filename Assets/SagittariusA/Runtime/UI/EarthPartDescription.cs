using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SagittariusA.Gameplay.Data;
using static SagittariusA.Gameplay.UI.EarthGarageView;

namespace SagittariusA.Gameplay.UI
{
    public static class EarthPartDescription
    {
        public static void Open(Transform owner, EarthPart part, int group)
        {
            var modal = EarthOverlay.Open(owner, "Part description");
            var root = modal.transform;
            Label(root, part.id.ToUpperInvariant() + " / " + part.name, .08f, .81f, .84f, .1f, 36, Color.white);
            Label(root, Specs(part, group), .08f, .69f, .84f, .11f, 24, new Color(1, .48f, .12f));
            var viewport = Box(root, "Description", .08f, .23f, .84f, .42f, new Color(.055f, .075f, .10f));
            viewport.gameObject.AddComponent<RectMask2D>();
            string copy = string.IsNullOrWhiteSpace(part.description)
                ? "Description coming soon.\n\nThe detailed explanation will be added from the supplied files. The specifications above are the current game values."
                : part.description;
            var text = Label(viewport, copy, 0, 0, 1, 1, 26, Color.white);
            text.alignment = TextAnchor.UpperLeft;
            var content = text.rectTransform;
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.offsetMin = new Vector2(24, 0); content.offsetMax = new Vector2(-24, -20);
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 32;
            var close = ActionButton(root, "CLOSE DESCRIPTION", .62f, .09f, .30f, .085f, modal.Close, new Color(.7f, .29f, .04f));
            EventSystem.current?.SetSelectedGameObject(close.gameObject);
        }
    }
}
