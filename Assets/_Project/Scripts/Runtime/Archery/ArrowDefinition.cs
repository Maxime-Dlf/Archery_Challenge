// ArrowDefinition : fiche de caractéristiques d'un type de flèche (ScriptableObject).
//
// OÙ LE CRÉER : Assets/_Project/Data/Arrows/ → Create → Archery Challenge → Arrow Definition.

using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Caractéristiques d'une flèche : vitesse, traînée, sensibilité au vent.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Arrow Definition", fileName = "Arrow_New")]
    public class ArrowDefinition : ScriptableObject
    {
        [Header("Identité")]
        public string id = "arrow_wood";
        public string displayName = "Flèche en bois";
        [TextArea(2, 4)] public string description = "";
        [Min(1)] public int tier = 1;

        [Tooltip("Prefab de la flèche (racine avec le composant Arrow).")]
        public GameObject prefab;

        [Header("Balistique")]
        [Tooltip("Multiplicateur de la vitesse de départ de l'arc.")]
        [Min(0.1f)] public float speedMultiplier = 1f;

        [Tooltip("Coefficient de traînée de l'air (plus petit = vole plus loin et plus droit).")]
        [Min(0f)] public float drag = 0.02f;

        [Tooltip("Sensibilité au vent (1 = normale, 0,6 = le carbone dévie moins).")]
        [Min(0f)] public float windSensitivity = 1f;
    }
}
