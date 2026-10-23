// GameCatalog : liste de toutes les données de jeu (ScriptableObject unique).
//
// OÙ LE CRÉER : Assets/_Project/Data/GameCatalog.asset (Create → Archery Challenge → Game Catalog).
// OÙ LE BRANCHER : champ « Catalog » du GameBootstrap (objet Systems de la scène Boot).
//
// Les services retrouvent ici les arcs, flèches, niveaux et difficultés par leur identifiant,
// sans avoir à connaître l'emplacement des fichiers.

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.Core
{
    /// <summary>Annuaire des données d'équilibrage du jeu.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Game Catalog", fileName = "GameCatalog")]
    public class GameCatalog : ScriptableObject
    {
        public BowDefinition[] bows = new BowDefinition[0];
        public ArrowDefinition[] arrows = new ArrowDefinition[0];
        public LevelDefinition[] levels = new LevelDefinition[0];
        public DifficultyProfile[] difficulties = new DifficultyProfile[0];

        [Tooltip("Équipement de départ.")]
        public string defaultBowId = "bow_apprentice";
        public string defaultArrowId = "arrow_wood";

        /// <summary>Arc par identifiant (null si inconnu).</summary>
        public BowDefinition GetBow(string id)
        {
            for (int i = 0; i < bows.Length; i++)
                if (bows[i] != null && bows[i].id == id) return bows[i];
            return null;
        }

        /// <summary>Flèche par identifiant (null si inconnue).</summary>
        public ArrowDefinition GetArrow(string id)
        {
            for (int i = 0; i < arrows.Length; i++)
                if (arrows[i] != null && arrows[i].id == id) return arrows[i];
            return null;
        }

        /// <summary>Niveau par identifiant (null si inconnu).</summary>
        public LevelDefinition GetLevel(string id)
        {
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].levelId == id) return levels[i];
            return null;
        }

        /// <summary>Niveau par nom de scène (null si inconnu).</summary>
        public LevelDefinition GetLevelByScene(string sceneName)
        {
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].sceneName == sceneName) return levels[i];
            return null;
        }

        /// <summary>Profil de difficulté (le premier du bon type).</summary>
        public DifficultyProfile GetDifficulty(GameDifficulty difficulty)
        {
            for (int i = 0; i < difficulties.Length; i++)
                if (difficulties[i] != null && difficulties[i].difficulty == difficulty) return difficulties[i];
            return null;
        }

        /// <summary>Niveaux de campagne (hors entraînement) triés par ordre.</summary>
        public List<LevelDefinition> GetCampaignLevels()
        {
            List<LevelDefinition> list = new List<LevelDefinition>();
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && !levels[i].isTraining) list.Add(levels[i]);
            list.Sort((a, b) => a.order.CompareTo(b.order));
            return list;
        }

        /// <summary>Le niveau d'entraînement (ou null).</summary>
        public LevelDefinition GetTrainingLevel()
        {
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] != null && levels[i].isTraining) return levels[i];
            return null;
        }
    }
}
