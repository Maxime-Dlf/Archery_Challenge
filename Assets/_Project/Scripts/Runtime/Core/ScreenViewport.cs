// ScreenViewport : zone de l'écran réellement visible par le joueur, pour que l'interface ne soit jamais coupée.
//
// OÙ LE POSER : n'est pas un composant (classe statique).
//
// Dans l'éditeur, la fenêtre Game peut être zoomée ou plus petite que la résolution choisie : seule une partie de l'image est
// visible et l'interface collée aux coins de l'écran sortait du cadre. L'éditeur fournit alors la zone visible (EditorProvider,
// voir GameViewViewport) ; sur un appareil, c'est la « zone sûre » de l'écran (encoches, coins arrondis). Dans les deux cas
// l'interface s'ajoute une petite marge.
//
// Toutes les valeurs sont normalisées (0..1) avec l'origine en bas à gauche, comme Screen.

using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Zone visible de l'écran.</summary>
    public static class ScreenViewport
    {
        /// <summary>Fournisseur de l'éditeur : renvoie vrai et la zone visible si la fenêtre Game est zoomée ou tronquée.</summary>
        public delegate bool VisibleProvider(out Rect visible);

        /// <summary>Renseigné par l'éditeur (GameViewViewport) ; nul dans une version jouée.</summary>
        public static VisibleProvider EditorProvider;

        /// <summary>Test : impose une zone visible (nul = lecture réelle).</summary>
        public static Rect? Override;

        /// <summary>Marge intérieure ajoutée à la zone visible (part de la hauteur de l'écran visible).</summary>
        public const float Margin = 0.012f;

        /// <summary>Zone visible, marge comprise, normalisée (0..1, origine en bas à gauche).</summary>
        public static Rect Visible
        {
            get
            {
                Rect rect = new Rect(0f, 0f, 1f, 1f);
                if (Override.HasValue)
                {
                    rect = Override.Value;
                }
                else
                {
                    Rect safe = Screen.safeArea;
                    if (Screen.width > 0 && Screen.height > 0)
                        rect = new Rect(safe.x / Screen.width, safe.y / Screen.height, safe.width / Screen.width, safe.height / Screen.height);
                    if (EditorProvider != null && EditorProvider(out Rect shown))
                        rect = Intersect(rect, shown);
                }

                float margin = Margin * rect.height;
                float marginX = margin * (Screen.height > 0 ? (float)Screen.height / Mathf.Max(1, Screen.width) : 1f);
                return Rect.MinMaxRect(rect.xMin + marginX, rect.yMin + margin, rect.xMax - marginX, rect.yMax - margin);
            }
        }

        /// <summary>Zone visible en pixels d'écran (origine en haut à gauche, pour IMGUI).</summary>
        public static Rect VisiblePixelsTopLeft
        {
            get
            {
                Rect v = Visible;
                return new Rect(v.xMin * Screen.width, (1f - v.yMax) * Screen.height, v.width * Screen.width, v.height * Screen.height);
            }
        }

        /// <summary>Vrai si une part notable de l'image est masquée (fenêtre zoomée, zone sûre…).</summary>
        public static bool IsCropped
        {
            get
            {
                Rect v = Visible;
                return v.width < 0.97f || v.height < 0.95f;
            }
        }

        private static Rect Intersect(Rect a, Rect b)
        {
            float xMin = Mathf.Max(a.xMin, b.xMin);
            float yMin = Mathf.Max(a.yMin, b.yMin);
            float xMax = Mathf.Min(a.xMax, b.xMax);
            float yMax = Mathf.Min(a.yMax, b.yMax);
            if (xMax <= xMin || yMax <= yMin)
                return a;
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
