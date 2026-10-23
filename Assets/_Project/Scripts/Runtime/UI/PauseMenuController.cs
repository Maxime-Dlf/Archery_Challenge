// PauseMenuController : menu pause (Échap au PC, bouton Menu de la manette gauche en VR).
//
// OÙ LE POSER : sur le panneau du prefab « PauseMenu » (dans UI_Global, désactivé au départ).
// ÉLÉMENTS ATTENDUS : Title, Status, BtnClose (croix), BtnResume, BtnRestart, BtnAbandon, BtnFastTravel, BtnSettings, BtnControls, BtnHub.
//
// Pendant la pause, le temps du jeu est arrêté (Time.timeScale = 0) : la minuterie de manche s'arrête.
// « Recommencer » relance le briefing de l'arène ; « Abandonner » quitte la manche sans résultat ;
// « Retour au hub » téléporte au village.

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Targets;
using ArcheryChallenge.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Menu pause.</summary>
    public class PauseMenuController : UiPanel
    {
        private TMP_Text status;
        private Button btnRestart, btnAbandon, btnFastTravel;
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
            status = UiBind.Text(transform, "Status", false);
            btnRestart = UiBind.Button(transform, "BtnRestart", false);
            btnAbandon = UiBind.Button(transform, "BtnAbandon", false);
            btnFastTravel = UiBind.Button(transform, "BtnFastTravel", false);
            UiBind.Button(transform, "BtnResume")?.onClick.AddListener(Close);
            UiBind.Button(transform, "BtnClose", false)?.onClick.AddListener(Close);   // croix « X » : même effet que Reprendre
            btnRestart?.onClick.AddListener(OnRestart);
            btnAbandon?.onClick.AddListener(OnAbandon);
            btnFastTravel?.onClick.AddListener(() => Services.Get<UiHub>()?.OpenFastTravel());
            UiBind.Button(transform, "BtnSettings")?.onClick.AddListener(() => Services.Get<UiHub>()?.OpenMainMenu());
            UiBind.Button(transform, "BtnControls", false)?.onClick.AddListener(() => Services.Get<UiHub>()?.OpenControls());
            UiBind.Button(transform, "BtnHub")?.onClick.AddListener(OnHub);
        }

        protected override void OnOpened()
        {
            Bind();
            Time.timeScale = 0f;
            GameEvents.RaisePauseChanged(true);
            RefreshStatus();
        }

        protected override void OnClosed()
        {
            Time.timeScale = 1f;
            GameEvents.RaisePauseChanged(false);
        }

        private void OnDisable()
        {
            // Sécurité : jamais de jeu bloqué à l'arrêt si le panneau est désactivé en pause.
            if (Time.timeScale == 0f)
                Time.timeScale = 1f;
        }

        private void RefreshStatus()
        {
            LevelController level = LevelController.Active;
            bool inRound = level != null && !level.IsTraining
                           && (level.State == LevelState.Running || level.State == LevelState.Countdown || level.State == LevelState.Briefing);
            if (btnRestart != null) btnRestart.gameObject.SetActive(level != null);
            if (btnAbandon != null) btnAbandon.gameObject.SetActive(level != null);

            ScoreService score = Services.Get<ScoreService>();
            if (level != null && level.Definition != null)
                UiBind.SetText(status, $"{level.Definition.displayName}\nScore : {(score != null ? score.Score : 0)}");
            else
                UiBind.SetText(status, "Partie en pause");

            // La téléportation rapide est refusée en pleine manche.
            if (btnFastTravel != null)
                btnFastTravel.interactable = !inRound || level.State == LevelState.Briefing;
        }

        private void OnRestart()
        {
            LevelController level = LevelController.Active;
            Close();
            level?.Restart();
        }

        private void OnAbandon()
        {
            LevelController level = LevelController.Active;
            Close();
            level?.Abort();
        }

        private void OnHub()
        {
            Close();
            Services.Get<FastTravelService>()?.TravelTo("hub", true);
        }
    }
}
