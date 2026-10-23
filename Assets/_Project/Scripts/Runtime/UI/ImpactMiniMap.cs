// ImpactMiniMap : mini-blason qui montre les derniers impacts (panneau d'entraînement).
//
// OÙ LE POSER : sur l'objet « ImpactMiniMap » du panneau d'entraînement : un carré (Image du blason)
// qui contient des petits points créés à la volée. Les positions sont normalisées par le rayon
// extérieur du blason : (0,0) = centre, (1,0) = bord droit.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Points d'impact sur un blason miniature.</summary>
    [DisallowMultipleComponent]
    public class ImpactMiniMap : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxDots = 12;
        [SerializeField, Min(2f)] private float dotSize = 12f;
        [SerializeField] private Sprite dotSprite;

        private readonly List<Image> dots = new List<Image>();
        private int next;

        /// <summary>Ajoute un impact (coordonnées normalisées -1 à 1).</summary>
        public void AddImpact(Vector2 normalized, int ring)
        {
            Image dot;
            if (dots.Count < maxDots)
            {
                GameObject go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                dot = go.GetComponent<Image>();
                dot.sprite = dotSprite;
                dot.raycastTarget = false;
                dots.Add(dot);
            }
            else
            {
                dot = dots[next];
                next = (next + 1) % maxDots;
            }

            RectTransform self = (RectTransform)transform;
            RectTransform rect = dot.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * dotSize;
            Vector2 half = self.rect.size * 0.5f;
            rect.anchoredPosition = new Vector2(Mathf.Clamp(normalized.x, -1.1f, 1.1f) * half.x, Mathf.Clamp(normalized.y, -1.1f, 1.1f) * half.y);
            dot.color = ring >= 10 ? new Color(1f, 0.85f, 0.2f) : ring >= 7 ? new Color(0.4f, 1f, 0.5f) : ring > 0 ? Color.white : new Color(1f, 0.35f, 0.3f);
        }

        /// <summary>Efface tous les points.</summary>
        public void Clear()
        {
            foreach (Image dot in dots)
                if (dot != null)
                    Destroy(dot.gameObject);
            dots.Clear();
            next = 0;
        }
    }
}
