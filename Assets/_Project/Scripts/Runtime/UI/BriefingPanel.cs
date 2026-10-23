// BriefingPanel : panneau d'objectif d'une arène (briefing avant la manche).
//
// OÙ LE POSER : sur le Canvas du prefab « BriefingPanel », placé sur « Panels/BriefingAnchor » du
// modèle d'arène (§P12.2).
// ÉLÉMENTS ATTENDUS : Title, Objective, Details (temps, flèches, étoiles), BestScores (meilleurs scores
// de l'arène), LockMessage (arène verrouillée), Hint (« Tirez sur DÉPART ou appuyez sur E »), Root.
//
// Visible quand le joueur est sur la plateforme (état Briefing), ou quand l'arène est verrouillée et que
// le joueur s'en approche (le panneau dit alors quelle arène réussir d'abord).

using System.Text;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Save;
using TMPro;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>Panneau de briefing d'arène.</summary>
    public class BriefingPanel : MonoBehaviour
    {
        private LevelController level;
        private TMP_Text title, objective, details, bestScores, lockMessage, hint;
        private GameObject root;
        private float nextRefresh;
        private LevelState lastState = LevelState.Finished;
        private bool lastOnPlatform;

        private void Awake()
        {
            level = GetComponentInParent<LevelController>();
            title = UiBind.Text(transform, "Title", false);
            objective = UiBind.Text(transform, "Objective", false);
            details = UiBind.Text(transform, "Details", false);
            bestScores = UiBind.Text(transform, "BestScores", false);
            lockMessage = UiBind.Text(transform, "LockMessage", false);
            hint = UiBind.Text(transform, "Hint", false);
            Transform r = UiBind.Find(transform, "Root");
            root = r != null ? r.gameObject : null;
        }

        private void OnEnable()
        {
            GameEvents.DifficultyChanged += OnDifficultyChanged;
            Refresh(true);
        }

        private void OnDisable() => GameEvents.DifficultyChanged -= OnDifficultyChanged;

        private void OnDifficultyChanged(GameDifficulty d) => Refresh(true);

        private void Update()
        {
            if (level == null)
                return;
            bool changed = level.State != lastState || level.PlayerOnPlatform != lastOnPlatform;
            if (changed || Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 1f;
                Refresh(changed);
            }
        }

        private void Refresh(bool full)
        {
            if (level == null || level.Definition == null)
                return;
            lastState = level.State;
            lastOnPlatform = level.PlayerOnPlatform;

            bool show = (level.State == LevelState.Briefing) || (level.State == LevelState.Locked && level.PlayerOnPlatform);
            if (root != null && root.activeSelf != show)
                root.SetActive(show);
            if (!show || !full)
                return;

            LevelDefinition def = level.Definition;
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            DifficultyProfile profile = applier != null ? applier.Current : null;
            GameCatalog catalog = Services.Get<GameCatalog>();

            UiBind.SetText(title, def.displayName);
            bool locked = level.State == LevelState.Locked;
            UiBind.SetActive(lockMessage, locked);
            UiBind.SetText(lockMessage, LevelObjective.DescribeLock(def, catalog));
            UiBind.SetActive(objective, !locked);
            UiBind.SetActive(details, !locked);
            UiBind.SetActive(bestScores, !locked);
            UiBind.SetActive(hint, !locked);
            if (locked)
                return;

            UiBind.SetText(objective, string.IsNullOrEmpty(def.briefing) ? LevelObjective.Describe(def, profile) : def.briefing + "\n" + LevelObjective.Describe(def, profile));

            int seconds = Mathf.RoundToInt(def.timeSeconds * (profile != null ? profile.timeMultiplier : 1f));
            int arrows = Mathf.Max(1, Mathf.RoundToInt(def.arrows * (profile != null ? profile.arrowsMultiplier : 1f)));
            UiBind.SetText(details,
                $"Difficulté : {UiBind.DifficultyName(applier != null ? applier.Difficulty : GameDifficulty.Normal)}\n" +
                $"Temps : {UiBind.Time(seconds)}    Flèches : {arrows}\n" +
                $"Étoiles : {LevelObjective.DescribeStars(def, profile).Replace("★", "*")}");

            UiBind.SetText(bestScores, BuildBestScores(def));
            UiBind.SetText(hint, "Tirez sur la cible DÉPART (ou touche E) pour commencer.");
        }

        private static string BuildBestScores(LevelDefinition def)
        {
            LeaderboardService board = Services.Get<LeaderboardService>();
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            if (board == null)
                return "";
            GameDifficulty difficulty = applier != null ? applier.Difficulty : GameDifficulty.Normal;
            var entries = board.Query(def.levelId, difficulty, 3);
            StringBuilder sb = new StringBuilder("Meilleurs scores :\n");
            if (entries.Count == 0)
                sb.Append("— aucun pour l'instant —");
            for (int i = 0; i < entries.Count; i++)
                sb.AppendLine($"{i + 1}. {entries[i].playerName} — {entries[i].score}");
            return sb.ToString().TrimEnd();
        }
    }
}
