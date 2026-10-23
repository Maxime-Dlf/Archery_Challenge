// LevelDefinition : données d'une arène (ScriptableObject), voir annexe C.4 du tutoriel.
//
// OÙ LE CRÉER : Assets/_Project/Data/Levels/ → Create → Archery Challenge → Level Definition.

using System;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Récompense accordée à un nombre d'étoiles donné.</summary>
    [Serializable]
    public struct LevelReward
    {
        [Tooltip("Identifiant de l'équipement (bow_hunter, arrow_ash…) ou « trophy » / « trophy_gold ».")]
        public string itemId;
        [Tooltip("Étoiles minimales pour obtenir cette récompense (0 = dès la réussite).")]
        [Range(0, 3)] public int minStars;
    }

    /// <summary>Paramètres d'une arène en difficulté Normal (la difficulté les module).</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Level Definition", fileName = "Level_New")]
    public class LevelDefinition : ScriptableObject
    {
        [Header("Identité")]
        public string levelId = "arena_01";
        public string displayName = "La Clairière";
        [TextArea(2, 4)] public string briefing = "";
        [Tooltip("Ordre dans la campagne (0 = entraînement).")]
        public int order = 1;
        [Tooltip("Entraînement : flèches et temps illimités, aucun score enregistré.")]
        public bool isTraining;

        [Header("Scène")]
        [Tooltip("Nom de la scène de l'arène (ex. Arena_01).")]
        public string sceneName = "Arena_01";
        [Tooltip("Identifiant du point d'apparition dans l'arène (ex. arena_01).")]
        public string spawnId = "arena_01";

        [Header("Manche (valeurs Normal)")]
        [Tooltip("Durée en secondes ; 0 = illimitée.")]
        public float timeSeconds = 90f;
        [Tooltip("Nombre de flèches ; 0 = illimité.")]
        public int arrows = 12;
        [Tooltip("Seuils de score pour 1, 2 et 3 étoiles.")]
        public int[] starThresholds = { 60, 84, 108 };
        [Tooltip("Étoiles nécessaires pour réussir l'arène et ouvrir la suivante.")]
        [Range(1, 3)] public int starsToPass = 1;
        [Tooltip("Multiplicateur de vent propre à l'arène.")]
        public float windMultiplier = 1f;
        [Tooltip("Points par seconde restante si l'objectif est atteint.")]
        public int timeBonusPerSecond = 1;

        [Header("Progression")]
        [Tooltip("Identifiant du niveau à réussir avant (vide = disponible dès le départ).")]
        public string requiredLevelId = "";
        public LevelReward[] rewards = new LevelReward[0];

        /// <summary>Seuil de score pour obtenir <paramref name="stars"/> étoiles (1 à 3), difficulté appliquée.</summary>
        public int GetThreshold(int stars, float difficultyMultiplier)
        {
            stars = Mathf.Clamp(stars, 1, 3);
            int index = Mathf.Min(stars - 1, starThresholds.Length - 1);
            int baseValue = index >= 0 ? starThresholds[index] : 0;
            return Mathf.Max(1, Mathf.RoundToInt(baseValue * difficultyMultiplier));
        }
    }
}
