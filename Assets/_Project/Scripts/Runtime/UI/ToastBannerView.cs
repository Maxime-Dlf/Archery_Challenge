// ToastBannerView : bandeau de notifications (déblocages, barrière ouverte, monstre éliminé, astuces).
//
// OÙ LE POSER : sur le Canvas du prefab « UI_Global/ToastBanner ».
// ÉLÉMENTS ATTENDUS : « Bar » (le bandeau : fond + texte) contenant « Background » et « ToastText ».
// En mode clavier/souris, le bandeau est une incrustation centrée en haut de l'écran : seul « Bar » est
// dessiné, jamais tout l'écran. En VR, c'est un petit panneau placé dans le champ de vision, devant la tête,
// légèrement au-dessus du regard.

using ArcheryChallenge.Core;
using TMPro;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>Affichage du message courant du ToastService.</summary>
    public class ToastBannerView : MonoBehaviour
    {
        // Taille du bandeau à l'écran (référence 1920 × 1080) et marge haute, en pixels.
        private static readonly Vector2 BarSize = new Vector2(1000f, 110f);
        private const float TopMargin = 40f;

        private Canvas canvas;
        private CanvasGroup group;
        private TMP_Text text;
        private RectTransform bar;
        private ToastService service;
        private RectTransform rect;
        private Vector2 worldSize = new Vector2(1000f, 120f);
        private bool worldValuesKnown;
        private float targetAlpha;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            text = UiBind.Text(transform, "ToastText", false);
            bar = UiBind.Find(transform, "Bar") as RectTransform;
            if (group != null)
                group.alpha = 0f;
        }

        private void OnDisable()
        {
            if (service != null)
                service.Changed -= OnChanged;
            service = null;
        }

        private void OnChanged(string message)
        {
            if (text != null)
                text.text = message;
            targetAlpha = string.IsNullOrEmpty(message) ? 0f : 1f;
        }

        private void LateUpdate()
        {
            if (service == null)
            {
                service = Services.Get<ToastService>();
                if (service != null)
                {
                    service.Changed += OnChanged;
                    OnChanged(service.Current);
                }
            }

            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.unscaledDeltaTime * 4f);

            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode == null || canvas == null)
                return;

            if (mode.IsHeadset)
            {
                if (canvas.renderMode != RenderMode.WorldSpace)
                {
                    canvas.renderMode = RenderMode.WorldSpace;
                    rect.sizeDelta = worldSize;
                    rect.localScale = Vector3.one * 0.001f;
                    StretchBar();
                }
                Transform head = mode.ActiveHead;
                if (head != null)
                {
                    canvas.worldCamera = head.GetComponent<Camera>();
                    Vector3 target = head.position + head.forward * 1.5f + Vector3.up * 0.3f;
                    transform.position = Vector3.Lerp(transform.position, target, Time.unscaledDeltaTime * 6f);
                    transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
                }
            }
            else if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && bar != null)
            {
                // Le bandeau reste sous le bord haut de la partie visible de l'écran (fenêtre Game zoomée ou tronquée).
                float hiddenTop = (1f - ScreenViewport.Visible.yMax) * rect.rect.height;
                bar.anchoredPosition = new Vector2(0f, -(TopMargin + hiddenTop));
            }
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && !mode.IsHeadset)
            {
                if (!worldValuesKnown)
                {
                    worldSize = rect.sizeDelta;
                    worldValuesKnown = true;
                }
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
                // En superposition, le Canvas fait la taille de l'écran : le bandeau, lui, reste une barre en haut.
                if (bar != null)
                {
                    bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 1f);
                    bar.pivot = new Vector2(0.5f, 1f);
                    bar.sizeDelta = BarSize;
                    bar.anchoredPosition = new Vector2(0f, -TopMargin);
                }
            }
        }

        // En espace monde, le bandeau remplit tout le petit Canvas.
        private void StretchBar()
        {
            if (bar == null)
                return;
            bar.anchorMin = Vector2.zero;
            bar.anchorMax = Vector2.one;
            bar.offsetMin = Vector2.zero;
            bar.offsetMax = Vector2.zero;
        }
    }
}
