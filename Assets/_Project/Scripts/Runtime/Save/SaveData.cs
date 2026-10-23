// SaveData : toutes les classes sérialisées en JSON (paramètres, profils, emplacements, classement).
//
// OÙ LE POSER : n'est pas un composant. Ces classes sont lues/écrites par JsonFileStore.
//
// RÈGLE : chaque fichier porte un « schemaVersion ». Si l'on change un jour la structure, on
// incrémente la version et on convertit les anciens fichiers au chargement (migration).
// JsonUtility ne gère ni les dictionnaires ni les types nullables : on n'utilise donc que des
// listes, des tableaux et des types simples.

using System;
using System.Collections.Generic;

namespace ArcheryChallenge.Save
{
    /// <summary>Version actuelle du format de tous les fichiers de sauvegarde.</summary>
    public static class SaveSchema
    {
        public const int Version = 1;
    }

    /// <summary>settings.json : réglages communs à tous les profils.</summary>
    [Serializable]
    public class SettingsData
    {
        public int schemaVersion = SaveSchema.Version;

        // Volumes (0..1)
        public float masterVolume = 1f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 1f;
        public float ambienceVolume = 0.8f;
        public float uiVolume = 1f;

        // Confort
        public bool vignetteEnabled = true;
        public bool snapTurn = true;
        public float snapTurnAngle = 45f;
        public float moveSpeed = 4f;

        /// <summary>0 = pas encore migré (anciens fichiers : marche à 2 m/s) ; 1 = sprint par défaut (4 m/s). Voir SettingsService.Sanitize.</summary>
        public int speedProfile = 0;
        public float seatedHeightOffset = 0f;
        /// <summary>Faux (défaut) : l'arc se tient dans la main DROITE et les flèches se prennent de la gauche ; vrai : l'inverse. Voir SettingsService.BowSide.</summary>
        public bool bowInLeftHand;
        public bool autoNock = true;
        public float mouseSensitivity = 0.12f;

        // Jeu
        public int difficulty = 1; // GameDifficulty.Normal
        public bool dayNightCycleActive = true;
        public float fixedHour = -1f; // < 0 = l'heure avance normalement

        public string lastProfileId = "";
        public int lastSlot = 1;
    }

    /// <summary>Un profil joueur.</summary>
    [Serializable]
    public class ProfileData
    {
        public string id;
        public string name;
        public string createdUtc;
        public string lastPlayedUtc;
    }

    /// <summary>profiles.json : liste des profils.</summary>
    [Serializable]
    public class ProfilesFile
    {
        public int schemaVersion = SaveSchema.Version;
        public List<ProfileData> profiles = new List<ProfileData>();
    }

    /// <summary>Progression d'une arène dans un emplacement.</summary>
    [Serializable]
    public class LevelProgress
    {
        public string levelId;
        public int bestStars;
        public int bestScore;
        public float bestAccuracy;
        public int attempts;
        public bool passed;
    }

    /// <summary>Statistiques cumulées d'un emplacement.</summary>
    [Serializable]
    public class PlayerStats
    {
        public int arrowsShot;
        public int arrowsHit;
        public int xCount;
        public int bestCombo;
        public int monstersKilled;
        public int deaths;
    }

    /// <summary>saves/&lt;idProfil&gt;/slotN.json : une partie complète.</summary>
    [Serializable]
    public class SlotData
    {
        public int schemaVersion = SaveSchema.Version;
        public string profileId;
        public int slotIndex = 1;
        public string createdUtc;
        public string updatedUtc;
        public float playTimeSeconds;

        public List<LevelProgress> levels = new List<LevelProgress>();
        public List<string> unlockedBows = new List<string>();
        public List<string> unlockedArrows = new List<string>();
        public List<string> trophies = new List<string>();
        public string selectedBow = "bow_apprentice";
        public string selectedArrow = "arrow_wood";

        public float hourOfDay = 9f;
        public int health = 10;
        public bool victory;
        public PlayerStats stats = new PlayerStats();

        /// <summary>Progression d'une arène (null si jamais jouée).</summary>
        public LevelProgress FindLevel(string levelId)
        {
            for (int i = 0; i < levels.Count; i++)
                if (levels[i].levelId == levelId) return levels[i];
            return null;
        }

        /// <summary>Progression d'une arène, créée si absente.</summary>
        public LevelProgress GetOrCreateLevel(string levelId)
        {
            LevelProgress p = FindLevel(levelId);
            if (p == null)
            {
                p = new LevelProgress { levelId = levelId };
                levels.Add(p);
            }
            return p;
        }
    }

    /// <summary>Résumé d'un emplacement pour l'écran de sélection.</summary>
    public struct SlotSummary
    {
        public bool Exists;
        public int SlotIndex;
        public int LevelsPassed;
        public int TotalStars;
        public float PlayTimeSeconds;
        public string LastPlayedUtc;
        public string Difficulty;
    }

    /// <summary>Une ligne du classement.</summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public string playerName;
        public string profileId;
        public int score;
        public string difficulty = "Normal";
        public string levelId;
        public int stars;
        public float accuracy;
        public int xCount;
        public string bow;
        public string arrow;
        public string dateUtc;

        /// <summary>Date parsée (DateTime.MaxValue si invalide, pour qu'une date illisible soit classée en dernier).</summary>
        public DateTime ParsedDate
        {
            get
            {
                return DateTime.TryParse(dateUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime d) ? d : DateTime.MaxValue;
            }
        }
    }

    /// <summary>leaderboard.json.</summary>
    [Serializable]
    public class LeaderboardFile
    {
        public int schemaVersion = SaveSchema.Version;
        public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
    }
}
