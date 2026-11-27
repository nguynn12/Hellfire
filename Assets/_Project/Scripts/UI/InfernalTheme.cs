using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hellfire.UI
{
    internal static class InfernalTheme
    {
        public static readonly Color Obsidian = new Color32(16, 16, 20, 255);
        public static readonly Color Bone = new Color32(232, 222, 208, 255);
        public static readonly Color Gold = new Color32(204, 167, 105, 255);
        public static readonly Color Ember = new Color32(229, 106, 41, 255);
        public static readonly Color Blood = new Color32(158, 37, 37, 255);
        private static TMP_FontAsset _body;
        private static TMP_FontAsset Body => _body != null ? _body : _body = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        public static Sprite Art(string file) => Resources.Load<Sprite>("Infernal/" + file);

        public static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            foreach (var item in root.GetComponentsInChildren<RectTransform>(true))
                if (item.name == name) return item;
            return null;
        }

        public static void Place(Transform element, Vector2 anchor, float x, float y, float width, float height)
        {
            if (!(element is RectTransform rect)) return;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Place(Transform parent, string name, float x, float y, float width, float height) =>
            Place(Find(parent, name), new Vector2(.5f, .5f), x, y, width, height);

        public static void Paint(Transform element, string artwork, Color tint, bool sliced = true)
        {
            if (element == null || !element.TryGetComponent<Image>(out var image)) return;
            var sprite = Art(artwork);
            if (sprite == null) return;
            image.sprite = sprite;
            image.overrideSprite = null;
            image.color = tint;
            image.type = sliced && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = artwork == "Panel" || artwork == "Button" ? 1.55f : 1f;
        }

        public static void Backdrop(Transform parent, string artwork)
        {
            var image = new GameObject("InfernalArtwork", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.transform.SetAsFirstSibling();
            image.rectTransform.anchorMin = Vector2.zero;
            image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
            Paint(image.transform, artwork, Color.white, false);
            image.raycastTarget = false;
            if (image.sprite == null) image.color = Obsidian;
        }

        public static Image Ornament(Transform parent, string artwork, Vector2 anchor, float x, float y, float w, float h)
        {
            var image = new GameObject(artwork, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            Place(image.transform, anchor, x, y, w, h);
            image.raycastTarget = false;
            Paint(image.transform, artwork, Color.white, false);
            image.preserveAspect = true;
            if (image.sprite == null) image.enabled = false;
            return image;
        }

        public static void Panel(Transform parent, float w, float h, float x = 0f, float y = 0f)
        {
            if (parent == null || Art("Panel") == null) return;
            var image = new GameObject("InfernalPanel", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            bool aboveBackdrop = parent.childCount > 1 && parent.GetChild(0).name == "InfernalArtwork";
            image.transform.SetSiblingIndex(aboveBackdrop ? 1 : 0);
            Place(image.transform, new Vector2(.5f, .5f), x, y, w, h);
            image.raycastTarget = false;
            Paint(image.transform, "Panel", Color.white);
        }

        public static void Type(TMP_Text label, float size, Color color, bool heading = false)
        {
            if (label == null) return;
            if (Body != null) label.font = Body;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
        }

        public static void Type(Transform parent, string name, float size, Color color, bool heading = false) =>
            Type(Find(parent, name)?.GetComponent<TMP_Text>(), size, color, heading);

        public static TMP_Text Label(Transform parent, string name, string value, float x, float y, float w, float h, float size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            label.transform.SetParent(parent, false);
            Place(label.transform, new Vector2(.5f, .5f), x, y, w, h);
            Type(label, size, Bone);
            label.alignment = TextAlignmentOptions.Center;
            label.text = value;
            label.raycastTarget = false;
            return label;
        }

        public static void Controls(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                Type(text, text.fontSize, Bone, (text.fontStyle & FontStyles.Bold) != 0);
            foreach (var action in root.GetComponentsInChildren<Button>(true))
                Button(action);
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                StyleSelectable(input, "Panel");
                Type(input.textComponent, 22, Bone);
                if (input.placeholder is TMP_Text placeholder) Type(placeholder, 22, Gold);
                if (input.textViewport != null)
                {
                    var offset = input.textViewport.offsetMin;
                    offset.x = 19;
                    input.textViewport.offsetMin = offset;
                }
            }
            foreach (var slider in root.GetComponentsInChildren<Slider>(true))
            {
                Meter(slider, Ember);
                if (slider.handleRect != null)
                    Paint(slider.handleRect, "Sigil", Color.white, false);
            }
            foreach (var toggle in root.GetComponentsInChildren<Toggle>(true))
                StyleSelectable(toggle, "Panel");
        }

        public static void Button(Button button)
        {
            StyleSelectable(button, "Button");
            if (button.GetComponentInChildren<TMP_Text>(true) is { } label)
            {
                Type(label, 23, Bone, true);
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;
            }
        }

        private static void StyleSelectable(Selectable selectable, string art)
        {
            if (selectable.targetGraphic is Image graphic)
                Paint(graphic.transform, art, Color.white);

            var colors = selectable.colors;
            colors.normalColor = new Color(.76f, .72f, .68f);
            colors.highlightedColor = new Color(1f, .89f, .73f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.76f, .49f, .35f);
            colors.disabledColor = new Color(.40f, .39f, .38f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = .1f;
            selectable.transition = Selectable.Transition.ColorTint;
            selectable.colors = colors;
            var navigation = selectable.navigation;
            if (navigation.mode == Navigation.Mode.None)
            {
                navigation.mode = Navigation.Mode.Automatic;
                selectable.navigation = navigation;
            }
        }

        public static void Meter(Slider slider, Color color, string artwork = "Meter")
        {
            if (slider == null) return;
            Paint(Find(slider.transform, "Background"), artwork, Color.white);
            if (slider.fillRect != null && slider.fillRect.TryGetComponent<Image>(out var fill))
            {
                fill.sprite = null;
                fill.overrideSprite = null;
                fill.type = Image.Type.Simple;
                fill.color = color;
                if (slider.fillRect.parent is RectTransform fillArea && slider.handleRect == null)
                {
                    fillArea.offsetMin = new Vector2(19f, 4f);
                    fillArea.offsetMax = new Vector2(-19f, -4f);
                }
            }
        }

        public static void Focus(Selectable selectable)
        {
            if (selectable != null && selectable.isActiveAndEnabled && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        public static void Dim(Transform element, float alpha)
        {
            if (element != null && element.TryGetComponent<Image>(out var image))
                image.color = new Color(Obsidian.r, Obsidian.g, Obsidian.b, alpha);
        }
    }
}
