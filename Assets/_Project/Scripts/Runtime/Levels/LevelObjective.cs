// LevelObjective : texte d'objectif d'une arène, affiché dans le briefing et le HUD.
//
// OÙ LE POSER : n'est pas un composant (classe statique).

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Phrases décrivant ce que le joueur doit accomplir.</summary>
    public static class LevelObjective
    {
        /// <summary>Résumé d'une ligne : « Marquez 60 points en 90 s avec 12 flèches. »</summary>
        public static string Describe(LevelDefinition level, DifficultyProfile difficulty)
        {
            if (level == null)
                return string.Empty;
            if (level.isTraining)
                return "Entraînez-vous librement : flèches et temps illimités.";

            float starMultiplier = difficulty != null ? difficulty.starThresholdMultiplier : 1f;
            float timeMultiplier = difficulty != null ? difficulty.timeMultiplier : 1f;
            float arrowMultiplier = difficulty != null ? difficulty.arrowsMultiplier : 1f;

            int target = level.GetThreshold(level.starsToPass, starMultiplier);
            int seconds = Mathf.RoundToInt(level.timeSeconds * timeMultiplier);
            int arrows = Mathf.Max(1, Mathf.RoundToInt(level.arrows * arrowMultiplier));
            string stars = level.starsToPass > 1 ? $"{level.starsToPass} étoiles" : "1 étoile";
            return $"Marquez {target} points ({stars}) en {seconds} s avec {arrows} flèches.";
        }

        /// <summary>Texte « seuils des étoiles » : « ★ 60   ★★ 84   ★★★ 108 ».</summary>
        public static string DescribeStars(LevelDefinition level, DifficultyProfile difficulty)
        {
            if (level == null || level.isTraining)
                return string.Empty;
            float m = difficulty != null ? difficulty.starThresholdMultiplier : 1f;
            return $"★ {level.GetThreshold(1, m)}   ★★ {level.GetThreshold(2, m)}   ★★★ {level.GetThreshold(3, m)}";
        }

        /// <summary>Message affiché quand l'arène est verrouillée.</summary>
        public static string DescribeLock(LevelDefinition level, GameCatalog catalog)
        {
            if (level == null || catalog == null || string.IsNullOrEmpty(level.requiredLevelId))
                return "Arène verrouillée.";
            LevelDefinition required = catalog.GetLevel(level.requiredLevelId);
            string name = required != null ? required.displayName : level.requiredLevelId;
            string stars = required != null && required.starsToPass > 1 ? $" avec {required.starsToPass} étoiles" : "";
            return $"Verrouillée : réussissez d'abord « {name} »{stars}.";
        }
    }
}
