// SurfaceTag : indique la matière d'un objet (bois, paille, terre, pierre, chair…).
//
// OÙ LE POSER : sur tout collider que l'on peut toucher avec une flèche (cibles, plateformes,
// rochers, terrain). La flèche lit ce composant pour choisir le son et l'effet d'impact.
// Sans SurfaceTag, la matière est « Dirt » (terre).

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Matière d'un objet, utilisée pour les sons et les effets d'impact.</summary>
    public class SurfaceTag : MonoBehaviour
    {
        [SerializeField] private SurfaceType surface = SurfaceType.Dirt;

        /// <summary>Matière de l'objet.</summary>
        public SurfaceType Surface
        {
            get => surface;
            set => surface = value;
        }

        /// <summary>Matière d'un collider (cherche dans l'objet puis ses parents ; terre par défaut).</summary>
        public static SurfaceType Of(Collider collider)
        {
            if (collider == null)
                return SurfaceType.Dirt;
            SurfaceTag tag = collider.GetComponentInParent<SurfaceTag>();
            return tag != null ? tag.surface : SurfaceType.Dirt;
        }
    }
}
