// DifficultyProfile : réglages d'une difficulté (ScriptableObject), voir §P7.2 du tutoriel.
//
// OÙ LE CRÉER : Assets/_Project/Data/Difficulty/ → Create → Archery Challenge → Difficulty Profile.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Paramètres qui changent selon Facile / Normal / Difficile.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Difficulty Profile", fileName = "Difficulty_New")]
    public class DifficultyProfile : ScriptableObject
    {
        public GameDifficulty difficulty = GameDifficulty.Normal;
        public string displayName = "Normal";

        [Header("Niveau")]
        [Tooltip("Multiplicateur de la taille des cibles.")]
        public float targetSizeMultiplier = 1f;
        [Tooltip("Multiplicateur de la vitesse des cibles mobiles.")]
        public float targetSpeedMultiplier = 1f;
        [Tooltip("Multiplicateur du temps disponible.")]
        public float timeMultiplier = 1f;
        [Tooltip("Multiplicateur du nombre de flèches.")]
        public float arrowsMultiplier = 1f;
        [Tooltip("Multiplicateur des seuils d'étoiles.")]
        public float starThresholdMultiplier = 1f;

        [Header("Vent")]
        [Tooltip("Vitesse minimale du vent (m/s). 0 et 0 = pas de vent.")]
        public float windMin = 0.5f;
        [Tooltip("Vitesse maximale du vent (m/s).")]
        public float windMax = 1.5f;

        [Header("Tremblement du réticule")]
        [Tooltip("Multiplicateur D du tremblement de la visée (voir AimSway) : Facile 0,35 · Normal 1 · Difficile 1,6.")]
        [Min(0f)] public float aimSwayMultiplier = 1f;

        [Header("Tir")]
        [Tooltip("Tension minimale (0..1) pour que la flèche parte vraiment.")]
        [Range(0f, 1f)] public float minTension = 0.15f;
        [Tooltip("Affiche la ligne de trajectoire prévue.")]
        public bool showTrajectory;
        [Tooltip("Aimantation légère vers la cible la plus proche de la visée (degrés, 0 = aucune).")]
        public float aimAssistAngle;
        [Tooltip("Multiplicateur du rayon de la zone d'encochage.")]
        public float nockRadiusMultiplier = 1f;
        [Tooltip("Multiplicateur de la traînée (1 = réaliste ; moins = plus indulgent).")]
        public float dragMultiplier = 1f;
    }
}
