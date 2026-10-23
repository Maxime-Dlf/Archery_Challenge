// UiFactory : fabrique d'éléments d'interface (TextMeshPro, boutons, curseurs…) pour les prefabs bruts.
//
// Les coordonnées sont exprimées « depuis le coin haut-gauche du parent » (x vers la droite, y vers le
// bas), plus intuitif pour disposer un panneau 1000 × 600. Toutes les fabriques donnent un NOM aux objets :
// les scripts d'interface retrouvent leurs éléments par ce nom (voir l'en-tête de chaque script UI).

using ArcheryChallenge.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.SpatialKeyboard;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Création d'éléments d'interface par code.</summary>
    public static class UiFactory
    {
        // Charte « bois et parchemin » (tutoriel §P12.4).
        public static readonly Color Parchment = new Color(0.93f, 0.85f, 0.68f);
        public static readonly Color Wood = new Color(0.45f, 0.31f, 0.17f);
        public static readonly Color WoodDark = new Color(0.27f, 0.18f, 0.10f);
        public static readonly Color Gold = new Color(0.878f, 0.69f, 0.29f);
        public static readonly Color Forest = new Color(0.184f, 0.365f, 0.227f);
        public static readonly Color Ink = new Color(0.231f, 0.165f, 0.102f);

        public static TMP_FontAsset Font;

        // ---------------------------------------------------------------- canvas

        /// <summary>Canvas monde de la taille voulue (échelle 0,001 : 1000 unités = 1 m).</summary>
        public static GameObject WorldCanvas(string name, float width, float height, bool tracked = true)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, height);
            rt.localScale = Vector3.one * 0.001f;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;
            if (tracked)
                go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.AddComponent<WorldCanvasCameraBinder>();
            return go;
        }

        /// <summary>Canvas plein écran en superposition (HUD du clavier/souris).</summary>
        public static GameObject OverlayCanvas(string name, int sortingOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
        }

        // ---------------------------------------------------------------- éléments

        /// <summary>Place un RectTransform par coin haut-gauche (x, y vers le bas) et taille.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Ancre un RectTransform en bas au centre du parent (y = hauteur au-dessus du bord bas).</summary>
        public static RectTransform PlaceBottomCenter(RectTransform rt, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Ancre un RectTransform en bas à gauche du parent.</summary>
        public static RectTransform PlaceBottomLeft(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Bande pleine largeur collée au bord bas du parent.</summary>
        public static RectTransform PlaceBottomStrip(RectTransform rt, float height)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            return rt;
        }

        /// <summary>Étire un RectTransform sur tout son parent.</summary>
        public static RectTransform Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Objet vide avec RectTransform.</summary>
        public static RectTransform Empty(Transform parent, string name, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return Place((RectTransform)go.transform, x, y, w, h);
        }

        /// <summary>Image (sprite, couleur), 9-slice si le sprite a des bordures.</summary>
        public static Image Image(Transform parent, string name, Sprite sprite, Color color, float x, float y, float w, float h, bool raycast = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0.1f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>Panneau parchemin.</summary>
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h)
        {
            return Image(parent, name, SpriteFactory.Panel, Color.white, x, y, w, h);
        }

        /// <summary>Texte TextMeshPro.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            float x, float y, float w, float h, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, x, y, w, h);
            TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
            if (Font != null)
                t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>Bouton en bois avec libellé centré.</summary>
        public static Button Button(Transform parent, string name, string label, float x, float y, float w, float h, float fontSize = 32f)
        {
            Image image = Image(parent, name, SpriteFactory.Button, Wood, x, y, w, h, true);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.93f, 0.7f);
            colors.pressedColor = new Color(0.65f, 0.85f, 0.65f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            button.colors = colors;
            image.color = Wood;
            // La teinte du bouton passe par « color » de l'image : on garde une base claire pour les états.
            Text(image.transform, "Label", label, fontSize, Color.white, 0f, 0f, w, h, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch((RectTransform)image.transform.Find("Label"));
            return button;
        }

        /// <summary>Curseur horizontal 0..1 avec fond et poignée.</summary>
        public static Slider Slider(Transform parent, string name, float x, float y, float w, float h)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, x, y, w, h);

            Image background = Image(root.transform, "Background", SpriteFactory.Bar, WoodDark, 0f, h * 0.3f, w, h * 0.4f);
            Stretch((RectTransform)background.transform).offsetMin = new Vector2(0f, h * 0.3f);
            ((RectTransform)background.transform).offsetMax = new Vector2(0f, -h * 0.3f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root.transform, false);
            RectTransform fillAreaRt = Stretch((RectTransform)fillArea.transform);
            fillAreaRt.offsetMin = new Vector2(h * 0.5f, h * 0.3f);
            fillAreaRt.offsetMax = new Vector2(-h * 0.5f, -h * 0.3f);
            Image fill = Image(fillArea.transform, "Fill", SpriteFactory.Bar, Gold, 0f, 0f, 10f, 10f);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.sizeDelta = new Vector2(10f, 0f);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            RectTransform handleAreaRt = Stretch((RectTransform)handleArea.transform);
            handleAreaRt.offsetMin = new Vector2(h * 0.5f, 0f);
            handleAreaRt.offsetMax = new Vector2(-h * 0.5f, 0f);
            Image handle = Image(handleArea.transform, "Handle", SpriteFactory.Round, Parchment, 0f, 0f, h, h, true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(h, 0f);

            Slider slider = root.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.5f;
            // Zone de clic confortable (en VR on vise avec un rayon) : le fond reçoit les rayons.
            background.raycastTarget = true;
            return slider;
        }

        /// <summary>Case à cocher avec libellé.</summary>
        public static Toggle Toggle(Transform parent, string name, string label, float x, float y, float w, float h, float fontSize = 30f)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            Place((RectTransform)root.transform, x, y, w, h);

            float box = Mathf.Min(h, 60f);
            Image background = Image(root.transform, "Background", SpriteFactory.Button, WoodDark, 0f, (h - box) * 0.5f, box, box, true);
            Image check = Image(background.transform, "Checkmark", SpriteFactory.Round, Gold, 0f, 0f, box * 0.6f, box * 0.6f);
            check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            check.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            check.rectTransform.anchoredPosition = Vector2.zero;
            Text(root.transform, "Label", label, fontSize, Ink, box + 16f, 0f, w - box - 16f, h, TextAlignmentOptions.MidlineLeft);

            Toggle toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = check;
            toggle.isOn = true;
            return toggle;
        }

        /// <summary>Champ de saisie TextMeshPro sur une seule ligne.</summary>
        public static TMP_InputField InputField(Transform parent, string name, string placeholder, float x, float y, float w, float h, float fontSize = 34f)
        {
            Image bg = Image(parent, name, SpriteFactory.Button, new Color(1f, 0.97f, 0.88f), x, y, w, h, true);
            GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(bg.transform, false);
            RectTransform areaRt = Stretch((RectTransform)textArea.transform);
            areaRt.offsetMin = new Vector2(14f, 6f);
            areaRt.offsetMax = new Vector2(-14f, -6f);

            TextMeshProUGUI placeholderText = Text(textArea.transform, "Placeholder", placeholder, fontSize, new Color(Ink.r, Ink.g, Ink.b, 0.45f),
                0f, 0f, 10f, 10f, TextAlignmentOptions.MidlineLeft, FontStyles.Italic);
            Stretch(placeholderText.rectTransform);
            TextMeshProUGUI text = Text(textArea.transform, "Text", "", fontSize, Ink, 0f, 0f, 10f, 10f, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            TMP_InputField field = bg.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = areaRt;
            field.textComponent = text;
            field.placeholder = placeholderText;
            field.characterLimit = 20;
            field.pointSize = fontSize;
            field.targetGraphic = bg;
            if (Font != null)
                field.fontAsset = Font;
            bg.gameObject.AddComponent<NameEntryController>();
            // Clavier virtuel de XRI (Spatial Keyboard) : s'ouvre quand on vise le champ en VR.
            XRKeyboardDisplay display = bg.gameObject.AddComponent<XRKeyboardDisplay>();
            display.inputField = field;
            display.m_MonitorInputFieldCharacterLimit = true;
            return field;
        }

        /// <summary>Conteneur vertical à espacement régulier.</summary>
        public static RectTransform VerticalList(Transform parent, string name, float x, float y, float w, float h, float spacing = 12f)
        {
            RectTransform rt = Empty(parent, name, x, y, w, h);
            VerticalLayoutGroup group = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childControlWidth = true;
            group.childControlHeight = false;
            group.childForceExpandWidth = true;
            group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.UpperCenter;
            return rt;
        }

        /// <summary>Rangée d'étoiles « StarsRow » (Star1..3) avec StarRowView.</summary>
        public static RectTransform Stars(Transform parent, string name, float x, float y, float size)
        {
            RectTransform row = Empty(parent, name, x, y, size * 3.3f, size);
            for (int i = 0; i < 3; i++)
                Image(row, "Star" + (i + 1), SpriteFactory.Star, new Color(0.25f, 0.2f, 0.15f, 0.55f), i * size * 1.1f, 0f, size, size);
            row.gameObject.AddComponent<StarRowView>();
            return row;
        }

        /// <summary>Rangée de 10 cœurs « HeartsRow » (Heart1..10).</summary>
        public static RectTransform Hearts(Transform parent, string name, float x, float y, float size)
        {
            RectTransform row = Empty(parent, name, x, y, size * 1.1f * 10f, size);
            for (int i = 0; i < 10; i++)
                Image(row, "Heart" + (i + 1), SpriteFactory.Heart, new Color(0.92f, 0.22f, 0.25f), i * size * 1.1f, 0f, size, size);
            return row;
        }

        /// <summary>Ajoute un fond parchemin plein cadre au parent, derrière tout le reste.</summary>
        public static Image Background(Transform parent, Color? tint = null)
        {
            Image bg = Image(parent, "Background", SpriteFactory.Panel, tint ?? Color.white, 0f, 0f, 10f, 10f);
            Stretch(bg.rectTransform);
            bg.transform.SetAsFirstSibling();
            return bg;
        }
    }
}
