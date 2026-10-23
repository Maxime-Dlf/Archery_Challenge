// LevelResultPanel : écran de fin de manche (score, étoiles, précision, récompenses, saisie du nom).
//
// OÙ LE POSER : sur le Canvas du prefab « LevelResultPanel », placé sur « Panels/BriefingAnchor » d'une arène.
// ÉLÉMENTS ATTENDUS : Root, Title, ScoreValue, StarsRow (Star1..3), AccuracyValue, DetailText, RewardsText,
//   NameInput (TMP_InputField, 20 caractères), SaveButton, SaveMessage, RetryButton, ContinueButton.
//
// SAISIE DU NOM : au clavier virtuel (VR : XR Keyboard Display sur NameInput) ou au clavier physique (PC).
// Le champ est pré-rempli avec le nom du profil. « Enregistrer » ajoute la ligne au classement JSON
// (nom, score, difficulté, arène, étoiles, précision, arc, flèches, date). Le panneau s'affiche comme un
// écran flottant devant le joueur (et, au clavier/souris, le curseur est libéré pour cliquer).

using System.Text;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Save;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Résultat d'une manche et enregistrement du score.</summary>
    public class LevelResultPanel : MonoBehaviour
    {
        private LevelController level;
        private GameObject root;
        private TMP_Text title, scoreValue, accuracyValue, detailText, rewardsText, saveMessage;
        private StarRowView stars;
        private TMP_InputField nameInput;
        private Button saveButton, retryButton, continueButton;
        private WorldCanvasCameraBinder binder;
        private bool saved;
        private LevelResult result;

        private void Awake()
        {
            level = GetComponentInParent<LevelController>();
            binder = GetComponent<WorldCanvasCameraBinder>();
            Transform r = UiBind.Find(transform, "Root");
            root = r != null ? r.gameObject : null;
            title = UiBind.Text(transform, "Title", false);
            scoreValue = UiBind.Text(transform, "ScoreValue", false);
            accuracyValue = UiBind.Text(transform, "AccuracyValue", false);
            detailText = UiBind.Text(transform, "DetailText", false);
            rewardsText = UiBind.Text(transform, "RewardsText", false);
            saveMessage = UiBind.Text(transform, "SaveMessage", false);
            Transform starsTransform = UiBind.Find(transform, "StarsRow");
            stars = starsTransform != null ? starsTransform.GetComponent<StarRowView>() : null;
            nameInput = UiBind.Input(transform, "NameInput", false);
            saveButton = UiBind.Button(transform, "SaveButton", false);
            retryButton = UiBind.Button(transform, "RetryButton", false);
            continueButton = UiBind.Button(transform, "ContinueButton", false);

            if (nameInput != null)
                nameInput.characterLimit = NameValidator.MaxLength;
            if (saveButton != null) saveButton.onClick.AddListener(OnSave);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        }

        private void OnEnable()
        {
            GameEvents.LevelFinished += OnLevelFinished;
            GameEvents.LevelStateChanged += OnLevelState;
            SetVisible(false);
        }

        private void OnDisable()
        {
            GameEvents.LevelFinished -= OnLevelFinished;
            GameEvents.LevelStateChanged -= OnLevelState;
            if (binder != null)
                binder.CloseFloating();
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
            // Le résultat s'affiche comme un écran flottant devant le joueur, puis retourne sur son support.
            if (binder != null)
            {
                if (visible) binder.OpenFloating(); else binder.CloseFloating();
            }
        }

        private void OnLevelState(Component source, LevelState state)
        {
            if (source != level)
                return;
            if (state != LevelState.Finished)
                SetVisible(false);
        }

        private void OnLevelFinished(LevelResult finished)
        {
            if (level == null || level.Definition == null || finished.LevelId != level.Definition.levelId)
                return;
            result = finished;
            saved = false;
            Populate();
            SetVisible(true);
        }

        private void Populate()
        {
            UiBind.SetText(title, result.Passed ? "Arène réussie !" : "Arène manquée");
            UiBind.SetText(scoreValue, result.Score.ToString());
            if (stars != null)
                stars.Set(result.Stars);
            UiBind.SetText(accuracyValue, $"Précision : {result.Accuracy * 100f:0} %  ({result.ArrowsHit}/{result.ArrowsShot})");

            StringBuilder detail = new StringBuilder();
            detail.AppendLine($"Difficulté : {UiBind.DifficultyName(result.Difficulty)}");
            if (result.TimeBonus > 0)
                detail.AppendLine($"Bonus de temps : +{result.TimeBonus}");
            detail.AppendLine($"Centres (X) : {result.XCount}    Meilleur combo : {result.BestCombo}");
            if (!result.Passed && level.Definition.starsToPass > 1)
                detail.AppendLine($"Il faut {level.Definition.starsToPass} étoiles pour passer.");
            UiBind.SetText(detailText, detail.ToString().TrimEnd());

            GameCatalog catalog = Services.Get<GameCatalog>();
            StringBuilder rewards = new StringBuilder();
            foreach (string id in result.NewRewards)
                rewards.AppendLine("Débloqué : " + DescribeReward(catalog, id));
            UiBind.SetText(rewardsText, rewards.Length > 0 ? rewards.ToString().TrimEnd() : "");

            string defaultName = "";
            ProfileService profiles = Services.Get<ProfileService>();
            if (profiles != null && profiles.Current != null)
                defaultName = profiles.Current.name;
            if (nameInput != null)
                nameInput.text = defaultName;
            UiBind.SetText(saveMessage, "");
            if (saveButton != null)
                saveButton.interactable = true;
        }

        private static string DescribeReward(GameCatalog catalog, string id)
        {
            if (catalog != null)
            {
                var bow = catalog.GetBow(id);
                if (bow != null) return bow.displayName;
                var arrow = catalog.GetArrow(id);
                if (arrow != null) return arrow.displayName;
            }
            if (id == "trophy") return "Trophée du tournoi";
            if (id == "trophy_gold") return "Trophée en or";
            return id;
        }

        /// <summary>Enregistre le score dans le classement (bouton « Enregistrer »).</summary>
        public void OnSave()
        {
            if (result == null || saved)
                return;
            LeaderboardService board = Services.Get<LeaderboardService>();
            ProfileService profiles = Services.Get<ProfileService>();
            string raw = nameInput != null ? nameInput.text : "";

            if (!NameValidator.TryClean(raw, out string cleaned))
            {
                UiBind.SetText(saveMessage, "Saisissez un nom (1 à 20 caractères).");
                return;
            }
            if (board == null)
                return;

            LeaderboardEntry entry = board.Add(cleaned, profiles != null && profiles.Current != null ? profiles.Current.id : "", result);
            if (entry != null)
            {
                saved = true;
                UiBind.SetText(saveMessage, $"Score enregistré pour {cleaned} !");
                if (saveButton != null)
                    saveButton.interactable = false;
                GameEvents.RaiseToast("Score enregistré dans le classement.");
            }
        }

        private void OnRetry()
        {
            if (level == null)
                return;
            level.Dismiss(); // l'arène redevient disponible ; le joueur est encore sur la plateforme : nouveau briefing
        }

        private void OnContinue()
        {
            if (level == null)
                return;
            level.Dismiss();
        }
    }
}
