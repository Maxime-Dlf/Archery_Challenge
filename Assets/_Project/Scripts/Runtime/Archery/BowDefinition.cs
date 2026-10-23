// BowDefinition : fiche de caractéristiques d'un arc (ScriptableObject).
//
// OÙ LE CRÉER : Assets/_Project/Data/Bows/ → clic droit → Create → Archery Challenge → Bow Definition.
// Les 6 arcs du jeu existent déjà (valeurs de l'annexe C.1 du tutoriel).
//
// Pour rééquilibrer un arc, on modifie ce fichier, jamais le code.

using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Caractéristiques d'un arc : puissance, allonge, tension au clavier, prefab.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Bow Definition", fileName = "Bow_New")]
    public class BowDefinition : ScriptableObject
    {
        [Header("Identité")]
        [Tooltip("Identifiant stable utilisé dans les sauvegardes (ex. bow_apprentice).")]
        public string id = "bow_apprentice";

        [Tooltip("Nom affiché dans les menus.")]
        public string displayName = "Arc d'apprenti";

        [TextArea(2, 4)]
        [Tooltip("Description affichée dans le râtelier et le menu.")]
        public string description = "";

        [Tooltip("Rang de l'arc (1 = débutant … 6 = royal), pour trier.")]
        [Min(1)] public int tier = 1;

        [Tooltip("Prefab de l'arc (racine avec le composant Bow).")]
        public GameObject prefab;

        [Header("Puissance")]
        [Tooltip("Vitesse de départ (m/s) à pleine tension, avant le multiplicateur de la flèche.")]
        [Min(1f)] public float maxSpeed = 32f;

        [Tooltip("Distance (m) dont il faut reculer la main de corde pour atteindre 100 % de tension.")]
        [Min(0.1f)] public float drawLength = 0.55f;

        [Tooltip("Durée (s) pour tendre complètement la corde avec la souris (mode clavier/souris).")]
        [Min(0.1f)] public float desktopDrawTime = 0.6f;

        [Tooltip("Tension (0..1) → puissance (0..1). Légèrement convexe : les derniers centimètres comptent plus.")]
        public AnimationCurve powerCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.5f, 1.5f));

        /// <summary>Puissance 0..1 pour une tension 0..1 (applique la courbe).</summary>
        public float EvaluatePower(float tension)
        {
            tension = Mathf.Clamp01(tension);
            return Mathf.Clamp01(powerCurve != null && powerCurve.length > 0 ? powerCurve.Evaluate(tension) : tension);
        }
    }
}
