// HudController : affichage de la manche (score, temps, flèches, objectif, étoiles, combo, vent, PV).
//
// OÙ LE POSER : sur le Canvas du prefab « HudPanel » (placé sur HudAnchor d'une arène), et, via ses
// variantes WristHud (montre au poignet gauche, VR) et DesktopHud (incrustation plein écran, PC).
// ÉLÉMENTS ATTENDUS (noms d'objets, voir §P12.5) :
//   ScoreValue, ComboValue, TimeValue, ArrowsValue, ObjectiveText, LevelName, StarsRow (Star1..3),
//   WindArrow, WindText, HeartsRow (Heart1..Heart10), CountdownText, StatePanel (conteneur facultatif)
//
// Le HUD posé dans l'arène (HudPanel) est géré par WorldHudBoard : masqué au PC (DesktopHud le remplace), suit le regard en casque.
// Le HUD se met à jour par événements (score, PV) et à chaque image (temps, vent). Il s'affiche pendant
// le compte à rebours et la manche ; en entraînement, seul le score est utile.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Targets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>HUD d'arène.</summary>
    public class HudController : MonoBehaviour
    {
        protected TMP_Text scoreValue, comboValue, timeValue, arrowsValue, objectiveText, levelName, windText, countdownText;
        protected StarRowView stars;
        protected RectTransform windArrow;
        protected Image[] hearts;
        protected CanvasGroup group;
        protected GameObject statePanel;
        private bool bound;
        private LevelController parentLevel;
        private float countdownHideAt;

        /// <summary>Arène suivie : celle dont ce HUD est un enfant, sinon l'arène active.</summary>
        protected LevelController Level => parentLevel != null ? parentLevel : LevelController.Active;

        protected virtual void Awake()
        {
            parentLevel = GetComponentInParent<LevelController>();
            Bind();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            scoreValue = UiBind.Text(transform, "ScoreValue", false);
            comboValue = UiBind.Text(transform, "ComboValue", false);
            timeValue = UiBind.Text(transform, "TimeValue", false);
            arrowsValue = UiBind.Text(transform, "ArrowsValue", false);
            objectiveText = UiBind.Text(transform, "ObjectiveText", false);
            levelName = UiBind.Text(transform, "LevelName", false);
            windText = UiBind.Text(transform, "WindText", false);
            if (windText != null)
            {
                // « Pas de vent » ou « 12,3 m/s » : le texte rétrécit pour tenir dans sa zone au lieu de déborder du panneau.
                windText.textWrappingMode = TextWrappingModes.NoWrap;
                windText.enableAutoSizing = true;
                windText.fontSizeMax = windText.fontSize;
                windText.fontSizeMin = Mathf.Max(8f, windText.fontSize * 0.5f);
            }
            countdownText = UiBind.Text(transform, "CountdownText", false);
            Transform starsTransform = UiBind.Find(transform, "StarsRow");
            stars = starsTransform != null ? starsTransform.GetComponent<StarRowView>() : null;
            Transform arrow = UiBind.Find(transform, "WindArrow");
            windArrow = arrow != null ? arrow as RectTransform : null;

            Transform heartsRow = UiBind.Find(transform, "HeartsRow");
            if (heartsRow != null)
            {
                hearts = new Image[PlayerHealth.MaxHealth];
                for (int i = 0; i < hearts.Length; i++)
                {
                    Transform h = UiBind.Find(heartsRow, "Heart" + (i + 1));
                    hearts[i] = h != null ? h.GetComponent<Image>() : null;
                }
            }
            group = GetComponent<CanvasGroup>();
            Transform state = UiBind.Find(transform, "StatePanel");
            statePanel = state != null ? state.gameObject : null;
        }

        protected virtual void OnEnable()
        {
            Bind();
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.PlayerHealthChanged += OnHealthChanged;
            GameEvents.CountdownTick += OnCountdown;
            GameEvents.LevelStateChanged += OnLevelState;
            RefreshAll();
        }

        protected virtual void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.PlayerHealthChanged -= OnHealthChanged;
            GameEvents.CountdownTick -= OnCountdown;
            GameEvents.LevelStateChanged -= OnLevelState;
        }

        private void OnScoreChanged(int score, int combo)
        {
            UiBind.SetText(scoreValue, score.ToString());
            UiBind.SetText(comboValue, combo > 1 ? "Combo ×" + combo : "");
            LevelController level = Level;
            if (stars != null && level != null && level.Definition != null && !level.IsTraining)
                stars.Set(level.CurrentStars);
        }

        private void OnHealthChanged(int hp, int max)
        {
            if (hearts == null) return;
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i] != null)
                    hearts[i].color = i < hp ? new Color(0.92f, 0.22f, 0.25f) : new Color(0.25f, 0.1f, 0.1f, 0.5f);
        }

        private void OnCountdown(int value)
        {
            if (countdownText == null) return;
            countdownText.text = value > 0 ? value.ToString() : "PARTEZ !";
            countdownText.gameObject.SetActive(true);
            countdownHideAt = Time.unscaledTime + (value > 0 ? 1.2f : 1f);
        }

        private void OnLevelState(Component level, LevelState state) => RefreshAll();

        /// <summary>Remet tous les textes à jour.</summary>
        public void RefreshAll()
        {
            ScoreService score = Services.Get<ScoreService>();
            OnScoreChanged(score != null ? score.Score : 0, score != null ? score.Combo : 0);
            PlayerHealth health = Services.Get<PlayerHealth>();
            OnHealthChanged(health != null ? health.Health : PlayerHealth.MaxHealth, PlayerHealth.MaxHealth);
            RefreshStatic();
        }

        private void RefreshStatic()
        {
            LevelController level = Level;
            if (level == null || level.Definition == null)
                return;
            UiBind.SetText(levelName, level.Definition.displayName);
            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            UiBind.SetText(objectiveText, LevelObjective.Describe(level.Definition, applier != null ? applier.Current : null));
        }

        protected virtual bool ShouldShow()
        {
            LevelController level = Level;
            if (level == null)
                return false;
            return level.State == LevelState.Countdown || level.State == LevelState.Running;
        }

        protected virtual void Update()
        {
            bool show = ShouldShow();
            if (statePanel != null)
            {
                // Variante avec « StatePanel » : les PV restent visibles, le reste suit la manche.
                if (statePanel.activeSelf != show)
                    statePanel.SetActive(show);
            }
            else if (group != null)
            {
                group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            }

            if (countdownText != null && countdownText.gameObject.activeSelf && Time.unscaledTime >= countdownHideAt)
                countdownText.gameObject.SetActive(false);

            LevelController level = Level;
            if (level == null || !show)
                return;

            // SetText n'écrit que si la valeur change : évite de régénérer le maillage du texte à chaque image.
            UiBind.SetText(timeValue, level.TotalTime > 0f ? UiBind.Time(level.TimeLeft) : "--");
            UiBind.SetText(arrowsValue, level.ArrowsLeft >= 0 ? level.ArrowsLeft.ToString() : "--");
            UpdateWind();
        }

        private void UpdateWind()
        {
            WindService wind = Services.Get<WindService>();
            if (wind == null)
                return;
            float speed = wind.Speed;
            UiBind.SetText(windText, speed > 0.05f ? $"{speed:0.0} m/s" : "Pas de vent");
            if (windArrow != null)
            {
                PlayerModeService mode = Services.Get<PlayerModeService>();
                Transform head = mode != null ? mode.ActiveHead : null;
                float heading = head != null ? head.eulerAngles.y : 0f;
                float windYaw = Mathf.Atan2(wind.CurrentWind.x, wind.CurrentWind.z) * Mathf.Rad2Deg;
                windArrow.localRotation = Quaternion.Euler(0f, 0f, -(windYaw - heading));
                windArrow.gameObject.SetActive(speed > 0.05f);
            }
        }
    }
}
