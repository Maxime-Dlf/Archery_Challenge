// StarRowView : rangée de 3 étoiles (images) allumées selon une note de 0 à 3.
//
// OÙ LE POSER : sur l'objet « StarsRow » d'un panneau, avec trois enfants Image « Star1 », « Star2 »,
// « Star3 ». Les étoiles obtenues sont dorées, les autres grisées. (Les glyphes ★ ne sont pas dans
// toutes les polices : des images sont plus sûres, en VR comme au PC.)

using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Affichage d'une note en étoiles.</summary>
    [DisallowMultipleComponent]
    public class StarRowView : MonoBehaviour
    {
        [SerializeField] private Color onColor = new Color(1f, 0.82f, 0.25f);
        [SerializeField] private Color offColor = new Color(0.25f, 0.2f, 0.15f, 0.55f);

        private Image[] stars;

        /// <summary>Allume les <paramref name="count"/> premières étoiles.</summary>
        public void Set(int count)
        {
            if (stars == null || stars.Length == 0)
            {
                stars = new Image[3];
                for (int i = 0; i < 3; i++)
                {
                    Transform t = UiBind.Find(transform, "Star" + (i + 1));
                    stars[i] = t != null ? t.GetComponent<Image>() : null;
                }
            }
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null)
                    stars[i].color = i < count ? onColor : offColor;
        }
    }
}
