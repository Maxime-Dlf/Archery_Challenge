// LeaderboardPanel : tableau des meilleurs scores (Top 10) avec filtres.
//
// OÙ LE POSER : sur le panneau du prefab « LeaderboardPanel » (tableau du hub et menu).
// ÉLÉMENTS ATTENDUS : Title, LevelLabel, DifficultyLabel, BtnPrevLevel, BtnNextLevel, BtnDifficulty
//   (boucle Toutes → Facile → Normal → Difficile), Row0 … Row9 (chacune avec Rank, Name, Score, Difficulty),
//   EmptyText, BtnClear + ConfirmClear (double confirmation), BtnClose (panneau global).
//
// Deux vues : par arène et « Campagne » (somme des meilleurs scores des 5 arènes d'un joueur).
// Tri : score décroissant, puis plus de « X », meilleure précision, date la plus ancienne.

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Classement des meilleurs scores.</summary>
    public class LeaderboardPanel : UiPanel
    {
        private const int RowCount = 10;

        private TMP_Text levelLabel, difficultyLabel, emptyText, clearLabel;
        private Button btnPrev, btnNext, btnDifficulty, btnClear, btnClose;
        private readonly TMP_Text[][] rows = new TMP_Text[RowCount][];
        private readonly List<string> viewIds = new List<string>();
        private int viewIndex;
        private int difficultyFilter = -1; // -1 = toutes
        private bool clearArmed;
        private bool bound;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            levelLabel = UiBind.Text(transform, "LevelLabel", false);
            difficultyLabel = UiBind.Text(transform, "DifficultyLabel", false);
            emptyText = UiBind.Text(transform, "EmptyText", false);
            btnPrev = UiBind.Button(transform, "BtnPrevLevel", false);
            btnNext = UiBind.Button(transform, "BtnNextLevel", false);
            btnDifficulty = UiBind.Button(transform, "BtnDifficulty", false);
            btnClear = UiBind.Button(transform, "BtnClear", false);
            btnClose = UiBind.Button(transform, "BtnClose", false);
            if (btnClear != null)
                clearLabel = btnClear.GetComponentInChildren<TMP_Text>();

            for (int i = 0; i < RowCount; i++)
            {
                Transform row = UiBind.Find(transform, "Row" + i);
                rows[i] = new TMP_Text[4];
                if (row == null) continue;
                rows[i][0] = UiBind.Text(row, "Rank", false);
                rows[i][1] = UiBind.Text(row, "Name", false);
                rows[i][2] = UiBind.Text(row, "Score", false);
                rows[i][3] = UiBind.Text(row, "Difficulty", false);
            }

            if (btnPrev != null) btnPrev.onClick.AddListener(() => Step(-1));
            if (btnNext != null) btnNext.onClick.AddListener(() => Step(1));
            if (btnDifficulty != null) btnDifficulty.onClick.AddListener(CycleDifficulty);
            if (btnClear != null) btnClear.onClick.AddListener(OnClear);
            if (btnClose != null) btnClose.onClick.AddListener(CloseFromButton);
        }

        private void OnEnable()
        {
            Bind();
            BuildViews();
            Refresh();
        }

        protected override void OnOpened() => Refresh();

        private void BuildViews()
        {
            viewIds.Clear();
            GameCatalog catalog = Services.Get<GameCatalog>();
            if (catalog != null)
                foreach (LevelDefinition level in catalog.GetCampaignLevels())
                    viewIds.Add(level.levelId);
            viewIds.Add(LeaderboardService.CampaignId);
            viewIndex = Mathf.Clamp(viewIndex, 0, viewIds.Count - 1);
        }

        private void Step(int direction)
        {
            if (viewIds.Count == 0) return;
            viewIndex = (viewIndex + direction + viewIds.Count) % viewIds.Count;
            Refresh();
        }

        private void CycleDifficulty()
        {
            difficultyFilter++;
            if (difficultyFilter > 2) difficultyFilter = -1;
            Refresh();
        }

        private void OnClear()
        {
            // Double confirmation : un premier clic arme, le second efface.
            if (!clearArmed)
            {
                clearArmed = true;
                if (clearLabel != null) clearLabel.text = "Confirmer l'effacement ?";
                return;
            }
            clearArmed = false;
            Services.Get<LeaderboardService>()?.Clear();
            if (clearLabel != null) clearLabel.text = "Effacer le classement";
            Refresh();
        }

        /// <summary>Remet à jour les lignes affichées.</summary>
        public void Refresh()
        {
            Bind();
            LeaderboardService board = Services.Get<LeaderboardService>();
            GameCatalog catalog = Services.Get<GameCatalog>();
            if (board == null || viewIds.Count == 0)
                return;

            string viewId = viewIds[viewIndex];
            string name = viewId == LeaderboardService.CampaignId ? "Campagne (5 arènes)" : catalog?.GetLevel(viewId)?.displayName ?? viewId;
            UiBind.SetText(levelLabel, name);
            GameDifficulty? filter = difficultyFilter >= 0 ? (GameDifficulty?)difficultyFilter : null;
            UiBind.SetText(difficultyLabel, filter.HasValue ? UiBind.DifficultyName(filter.Value) : "Toutes difficultés");

            List<LeaderboardEntry> entries = board.Query(viewId, filter, RowCount);
            for (int i = 0; i < RowCount; i++)
            {
                bool has = i < entries.Count;
                if (rows[i][0] != null)
                {
                    rows[i][0].transform.parent.gameObject.SetActive(has);
                    if (!has) continue;
                    UiBind.SetText(rows[i][0], (i + 1).ToString());
                    UiBind.SetText(rows[i][1], entries[i].playerName);
                    UiBind.SetText(rows[i][2], entries[i].score.ToString());
                    UiBind.SetText(rows[i][3], TranslateDifficulty(entries[i].difficulty));
                }
            }
            UiBind.SetActive(emptyText, entries.Count == 0);
            UiBind.SetText(emptyText, "Aucun score pour l'instant.\nTerminez une arène pour apparaître ici !");
        }

        private static string TranslateDifficulty(string stored)
        {
            switch (stored)
            {
                case "Easy": return "Facile";
                case "Hard": return "Difficile";
                case "Normal": return "Normal";
                default: return stored;
            }
        }
    }
}
