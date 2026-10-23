// StarRating : calcul des étoiles (0 à 3) à partir du score.
//
// OÙ LE POSER : n'est pas un composant (classe statique, facile à tester).
//
// 1 étoile = seuil de l'arène, 2 étoiles = seuil × 1,4, 3 étoiles = seuil × 1,8 (ces trois valeurs
// sont dans le LevelDefinition). La difficulté multiplie les seuils (Facile × 0,8 … Difficile × 1,2).

namespace ArcheryChallenge.Levels
{
    /// <summary>Évaluation des étoiles d'une manche.</summary>
    public static class StarRating
    {
        /// <summary>Étoiles obtenues pour un score donné, difficulté comprise.</summary>
        public static int Evaluate(LevelDefinition level, int score, float difficultyMultiplier)
        {
            if (level == null)
                return 0;
            int stars = 0;
            for (int s = 1; s <= 3; s++)
                if (score >= level.GetThreshold(s, difficultyMultiplier))
                    stars = s;
            return stars;
        }

        /// <summary>Score nécessaire pour la prochaine étoile (0 si les 3 sont acquises).</summary>
        public static int NextThreshold(LevelDefinition level, int score, float difficultyMultiplier)
        {
            if (level == null)
                return 0;
            for (int s = 1; s <= 3; s++)
            {
                int threshold = level.GetThreshold(s, difficultyMultiplier);
                if (score < threshold)
                    return threshold;
            }
            return 0;
        }
    }
}
