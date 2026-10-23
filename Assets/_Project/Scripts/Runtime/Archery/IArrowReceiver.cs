// IArrowReceiver : tout objet qui réagit à l'impact d'une flèche (cibles, monstres).
//
// OÙ LE POSER : n'est pas un composant. Implémentée par Target et MonsterHealth ; la flèche cherche
// cette interface dans le collider touché (ou ses parents).

using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Ce que la flèche doit faire après un impact.</summary>
    public enum ArrowHitOutcome
    {
        /// <summary>La flèche se plante dans l'objet.</summary>
        Stick,
        /// <summary>La flèche est repoussée et tombe (bouclier de la tortue).</summary>
        Deflect,
        /// <summary>La flèche continue son chemin (cible rabattue).</summary>
        PassThrough
    }

    /// <summary>Objet capable de recevoir un impact de flèche.</summary>
    public interface IArrowReceiver
    {
        /// <summary>Appelé par la flèche au moment de l'impact.</summary>
        ArrowHitOutcome OnArrowHit(Arrow arrow, Vector3 point, Vector3 direction, Collider collider);
    }
}
