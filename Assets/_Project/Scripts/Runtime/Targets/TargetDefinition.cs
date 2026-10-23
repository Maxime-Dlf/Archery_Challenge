// TargetDefinition : rayons des anneaux d'une cible (ScriptableObject).
//
// OÙ LE CRÉER : Assets/_Project/Data/Targets/ → Create → Archery Challenge → Target Definition.
// « Target_Standard » : blason à 10 anneaux ; « Target_Penalty » : bouclier pénalisant.

using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Géométrie et score d'un blason, à l'échelle 1 (avant la taille imposée par la difficulté).</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Target Definition", fileName = "Target_New")]
    public class TargetDefinition : ScriptableObject
    {
        [Tooltip("Rayon (m) de chaque anneau, de l'anneau 10 (centre) jusqu'à l'anneau 1 (bord). 10 valeurs croissantes.")]
        public float[] ringRadii =
        {
            0.058f, 0.116f, 0.174f, 0.232f, 0.290f, 0.348f, 0.406f, 0.464f, 0.522f, 0.580f
        };

        [Tooltip("Rayon (m) du petit cercle central « X » (départage des égalités).")]
        public float xRadius = 0.029f;

        [Tooltip("Bouclier pénalisant : le tir retire des points au lieu d'en donner.")]
        public bool isPenalty;

        [Tooltip("Points retirés si la cible est pénalisante.")]
        public int penaltyPoints = 15;

        [Tooltip("Rayon (m) de la zone qui compte pour un bouclier pénalisant (tout impact dedans pénalise).")]
        public float penaltyRadius = 0.55f;

        /// <summary>
        /// Anneau (10 = centre … 1 = bord) pour une distance radiale <paramref name="radius"/> au centre,
        /// ou 0 si l'impact est en dehors du blason.
        /// </summary>
        public int RingForRadius(float radius)
        {
            for (int i = 0; i < ringRadii.Length; i++)
            {
                if (radius <= ringRadii[i])
                    return 10 - i;
            }
            return 0;
        }

        /// <summary>Vrai si l'impact est dans le petit cercle central.</summary>
        public bool IsX(float radius) => radius <= xRadius;
    }
}
