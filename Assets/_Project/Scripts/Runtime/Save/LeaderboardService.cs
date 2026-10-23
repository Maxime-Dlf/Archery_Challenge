// LeaderboardService : classement des meilleurs scores (leaderboard.json).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// RÈGLES (§P13.1 du tutoriel)
// - Chaque partie enregistrée ajoute une ligne : nom, score, difficulté, arène, étoiles, précision,
//   arc, flèches, date.
// - Tri : score décroissant ; à égalité, plus de « X », puis meilleure précision, puis date la plus ancienne.
// - Deux vues : par arène, et « campagne » (somme des meilleurs scores des arènes d'un même joueur
//   dans une difficulté).
// - Le fichier conserve le Top 50 de chaque couple (arène, difficulté) ; l'écran affiche le Top 10.

using System;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Save
{
    /// <summary>Service du classement.</summary>
    [DisallowMultipleComponent]
    public class LeaderboardService : MonoBehaviour
    {
        private const string FileName = "leaderboard.json";

        /// <summary>Identifiant de la vue « campagne ».</summary>
        public const string CampaignId = "campaign";

        /// <summary>Nombre de lignes conservées par couple (arène, difficulté).</summary>
        public const int KeepPerBoard = 50;

        private LeaderboardFile file;

        /// <summary>Toutes les lignes enregistrées (non triées).</summary>
        public IReadOnlyList<LeaderboardEntry> Entries
        {
            get { EnsureLoaded(); return file.entries; }
        }

        private void Awake()
        {
            Services.Register(this);
            EnsureLoaded();
        }

        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Charge le fichier une seule fois.</summary>
        public void EnsureLoaded()
        {
            if (file != null)
                return;
            file = JsonFileStore.Read<LeaderboardFile>(FileName) ?? new LeaderboardFile();
            if (file.entries == null)
                file.entries = new List<LeaderboardEntry>();
        }

        /// <summary>Comparateur : meilleur d'abord (voir règles ci-dessus).</summary>
        public static int Compare(LeaderboardEntry a, LeaderboardEntry b)
        {
            int c = b.score.CompareTo(a.score);
            if (c != 0) return c;
            c = b.xCount.CompareTo(a.xCount);
            if (c != 0) return c;
            c = b.accuracy.CompareTo(a.accuracy);
            if (c != 0) return c;
            return a.ParsedDate.CompareTo(b.ParsedDate);
        }

        /// <summary>
        /// Ajoute un score. Le nom est nettoyé (1–20 caractères) ; renvoie null s'il est invalide.
        /// </summary>
        public LeaderboardEntry Add(string playerName, string profileId, LevelResult result)
        {
            EnsureLoaded();
            if (!NameValidator.TryClean(playerName, out string cleaned) || result == null)
                return null;

            LeaderboardEntry entry = new LeaderboardEntry
            {
                playerName = cleaned,
                profileId = profileId ?? "",
                score = result.Score,
                difficulty = result.Difficulty.ToString(),
                levelId = result.LevelId,
                stars = result.Stars,
                accuracy = result.Accuracy,
                xCount = result.XCount,
                bow = result.BowId ?? "",
                arrow = result.ArrowId ?? "",
                dateUtc = DateTime.UtcNow.ToString("o")
            };
            AddEntry(entry);
            return entry;
        }

        /// <summary>Ajoute une ligne déjà construite, applique la limite du Top 50 et écrit le fichier.</summary>
        public void AddEntry(LeaderboardEntry entry)
        {
            EnsureLoaded();
            file.entries.Add(entry);
            Trim(entry.levelId, entry.difficulty);
            JsonFileStore.Write(FileName, file);
        }

        /// <summary>
        /// Renvoie le classement trié d'une arène (ou de la campagne), filtré par difficulté.
        /// </summary>
        /// <param name="levelId">Identifiant d'arène, ou <see cref="CampaignId"/>.</param>
        /// <param name="difficulty">Filtre de difficulté ; null = toutes.</param>
        /// <param name="top">Nombre de lignes maximum.</param>
        public List<LeaderboardEntry> Query(string levelId, GameDifficulty? difficulty, int top = 10)
        {
            EnsureLoaded();
            List<LeaderboardEntry> result = levelId == CampaignId
                ? BuildCampaign(difficulty)
                : FilterLevel(levelId, difficulty);
            result.Sort(Compare);
            if (top > 0 && result.Count > top)
                result.RemoveRange(top, result.Count - top);
            return result;
        }

        /// <summary>Efface tout le classement.</summary>
        public void Clear()
        {
            EnsureLoaded();
            file.entries.Clear();
            JsonFileStore.Write(FileName, file);
        }

        private List<LeaderboardEntry> FilterLevel(string levelId, GameDifficulty? difficulty)
        {
            List<LeaderboardEntry> list = new List<LeaderboardEntry>();
            string diffName = difficulty?.ToString();
            for (int i = 0; i < file.entries.Count; i++)
            {
                LeaderboardEntry e = file.entries[i];
                if (e.levelId != levelId)
                    continue;
                if (diffName != null && e.difficulty != diffName)
                    continue;
                list.Add(e);
            }
            return list;
        }

        // Campagne : pour chaque joueur (profil, sinon nom), on garde son meilleur score dans
        // chaque arène (difficulté donnée) puis on additionne. Une ligne synthétique est produite.
        private List<LeaderboardEntry> BuildCampaign(GameDifficulty? difficulty)
        {
            string diffName = difficulty?.ToString();
            Dictionary<string, Dictionary<string, LeaderboardEntry>> bestByPlayer =
                new Dictionary<string, Dictionary<string, LeaderboardEntry>>();

            for (int i = 0; i < file.entries.Count; i++)
            {
                LeaderboardEntry e = file.entries[i];
                if (e.levelId == "training" || string.IsNullOrEmpty(e.levelId))
                    continue;
                if (diffName != null && e.difficulty != diffName)
                    continue;

                string playerKey = string.IsNullOrEmpty(e.profileId) ? "name:" + e.playerName : "id:" + e.profileId;
                if (!bestByPlayer.TryGetValue(playerKey, out var levels))
                {
                    levels = new Dictionary<string, LeaderboardEntry>();
                    bestByPlayer[playerKey] = levels;
                }
                string levelKey = e.levelId + "|" + e.difficulty;
                if (!levels.TryGetValue(levelKey, out LeaderboardEntry best) || Compare(e, best) < 0)
                    levels[levelKey] = e;
            }

            List<LeaderboardEntry> list = new List<LeaderboardEntry>();
            foreach (var player in bestByPlayer)
            {
                LeaderboardEntry sum = new LeaderboardEntry { levelId = CampaignId, difficulty = diffName ?? "All" };
                DateTime latest = DateTime.MinValue;
                float accuracySum = 0f;
                int count = 0;
                foreach (LeaderboardEntry e in player.Value.Values)
                {
                    sum.playerName = e.playerName;
                    sum.profileId = e.profileId;
                    sum.score += e.score;
                    sum.stars += e.stars;
                    sum.xCount += e.xCount;
                    accuracySum += e.accuracy;
                    count++;
                    if (e.ParsedDate > latest) latest = e.ParsedDate;
                }
                sum.accuracy = count > 0 ? accuracySum / count : 0f;
                sum.dateUtc = latest == DateTime.MinValue ? DateTime.UtcNow.ToString("o") : latest.ToString("o");
                list.Add(sum);
            }
            return list;
        }

        private void Trim(string levelId, string difficulty)
        {
            List<LeaderboardEntry> board = new List<LeaderboardEntry>();
            for (int i = 0; i < file.entries.Count; i++)
                if (file.entries[i].levelId == levelId && file.entries[i].difficulty == difficulty)
                    board.Add(file.entries[i]);

            if (board.Count <= KeepPerBoard)
                return;

            board.Sort(Compare);
            for (int i = KeepPerBoard; i < board.Count; i++)
                file.entries.Remove(board[i]);
        }
    }
}
