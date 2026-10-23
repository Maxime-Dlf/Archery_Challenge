// UiHub : chef d'orchestre des panneaux globaux (profil, réglages, classement, voyage rapide, pause).
//
// OÙ LE POSER : sur le prefab « UI_Global » (scène Boot).
// RÉGLAGES : les cinq références de panneaux (instances désactivées dans UI_Global).
//
// RÔLES
// - Échap / bouton Menu : ouvre ou ferme la pause ; ferme d'abord le panneau ouvert s'il y en a un.
// - Touche T / bouton Y : ouvre la téléportation rapide.
// - Touche F1 (ou H au clavier) : ouvre l'écran des commandes.
// - Au démarrage en mode clavier/souris : ouvre le menu de profils du feu de camp (en VR, le panneau du
//   camp est déjà devant le joueur).
// - Un seul panneau global ouvert à la fois.

using System.Collections;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ArcheryChallenge.UI
{
    /// <summary>Gestion centralisée des panneaux globaux.</summary>
    [DisallowMultipleComponent]
    public class UiHub : MonoBehaviour
    {
        [SerializeField] private CampfireProfileMenu profileMenu;
        [SerializeField] private MainMenuController mainMenu;
        [SerializeField] private LeaderboardPanel leaderboard;
        [SerializeField] private FastTravelPanel fastTravel;
        [SerializeField] private PauseMenuController pauseMenu;
        [SerializeField] private ControlsPanel controls;

        [Tooltip("Ouvre le choix du profil au démarrage (clavier/souris).")]
        [SerializeField] private bool autoOpenProfile = true;

        public CampfireProfileMenu ProfileMenu => profileMenu;
        public MainMenuController MainMenu => mainMenu;
        public LeaderboardPanel Leaderboard => leaderboard;
        public FastTravelPanel FastTravel => fastTravel;
        public PauseMenuController PauseMenu => pauseMenu;
        public ControlsPanel Controls => controls;

        /// <summary>Désactive l'ouverture automatique du menu de profils (tests).</summary>
        public bool AutoOpenProfile
        {
            get => autoOpenProfile;
            set => autoOpenProfile = value;
        }

        /// <summary>Vrai si un panneau global est ouvert.</summary>
        public bool AnyOpen =>
            (profileMenu != null && profileMenu.IsOpen) || (mainMenu != null && mainMenu.IsOpen) ||
            (leaderboard != null && leaderboard.IsOpen) || (fastTravel != null && fastTravel.IsOpen) ||
            (pauseMenu != null && pauseMenu.IsOpen) || (controls != null && controls.IsOpen);

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        private IEnumerator Start()
        {
            // Attente du démarrage complet puis ouverture du menu de profils si besoin.
            GameBootstrap bootstrap = Services.Get<GameBootstrap>();
            float waited = 0f;
            while ((bootstrap == null || !bootstrap.IsReady) && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                bootstrap = Services.Get<GameBootstrap>();
                yield return null;
            }
            yield return new WaitForSecondsRealtime(1f);

            PlayerModeService mode = Services.Get<PlayerModeService>();
            ProfileService profiles = Services.Get<ProfileService>();
            if (autoOpenProfile && mode != null && mode.UsesMouse && profiles != null && profiles.Current == null)
                OpenProfileMenu();
        }

        private void Update()
        {
            HandleHelpKey();

            IArcherInput input = Services.Get<IArcherInput>();
            if (input == null)
                return;

            if (input.MenuPressedThisFrame)
            {
                if (AnyOpen && !(pauseMenu != null && pauseMenu.IsOpen))
                    CloseAll();
                else
                    TogglePause();
            }
            else if (input.FastTravelPressedThisFrame && !(pauseMenu != null && pauseMenu.IsOpen))
            {
                if (fastTravel != null && fastTravel.IsOpen) CloseAll(); else OpenFastTravel();
            }
        }

        // F1 (partout) et H (clavier/souris, hors saisie de texte) ouvrent ou ferment l'écran des commandes.
        // Maj + F1 est réservé à la légende de développement (DevShortcuts).
        private void HandleHelpKey()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || controls == null)
                return;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            bool f1 = kb.f1Key.wasPressedThisFrame && !shift;

            bool h = false;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (kb.hKey.wasPressedThisFrame && mode != null && mode.UsesMouse && !IsTyping())
                h = true;

            if (f1 || h)
            {
                if (controls.IsOpen) CloseAll(); else OpenControls();
            }
        }

        private static bool IsTyping()
        {
            EventSystem system = EventSystem.current;
            GameObject selected = system != null ? system.currentSelectedGameObject : null;
            return selected != null && selected.GetComponent<TMPro.TMP_InputField>() != null;
        }

        // Pause « tenue » : un sous-écran (réglages, commandes, classement, voyage) ouvert DEPUIS la pause garde le jeu à l'arrêt ;
        // sa croix ramène au menu pause. Échap (ou tout CloseAll) referme tout et relance le jeu.
        private bool holdingPause;

        /// <summary>Vrai tant qu'un sous-écran ouvert depuis la pause tient le jeu à l'arrêt.</summary>
        public bool HoldingPause => holdingPause;

        public void OpenControls() { BeginSubPanel(); controls?.Open(); }
        public void OpenProfileMenu() { CloseAll(); profileMenu?.Open(); }
        public void OpenMainMenu(string page = "Page_Main") { BeginSubPanel(); if (mainMenu != null) { mainMenu.Open(); mainMenu.ShowPage(page); } }
        public void OpenLeaderboard() { BeginSubPanel(); leaderboard?.Open(); }
        public void OpenFastTravel() { BeginSubPanel(); fastTravel?.Open(); }

        // Ferme les panneaux ouverts ; si l'un d'eux venait de la pause, le jeu reste à l'arrêt.
        private void BeginSubPanel()
        {
            bool fromPause = pauseMenu != null && pauseMenu.IsOpen;
            CloseAllPanels();
            if (fromPause)
            {
                holdingPause = true;
                Time.timeScale = 0f;
                GameEvents.RaisePauseChanged(true);
            }
        }

        /// <summary>Croix d'un sous-écran : le referme et revient au menu pause s'il en venait.</summary>
        public void CloseSubPanel()
        {
            bool backToPause = holdingPause;
            CloseAllPanels();
            holdingPause = false;
            if (backToPause)
                pauseMenu?.Open();
        }

        private void ReleasePauseHold()
        {
            if (!holdingPause)
                return;
            holdingPause = false;
            Time.timeScale = 1f;
            GameEvents.RaisePauseChanged(false);
        }

        /// <summary>Ouvre la pause (ferme les autres panneaux).</summary>
        public void OpenPause() { CloseAll(); pauseMenu?.Open(); }

        /// <summary>Bascule la pause.</summary>
        public void TogglePause()
        {
            if (pauseMenu != null && pauseMenu.IsOpen)
                CloseAll();
            else
                OpenPause();
        }

        /// <summary>Ferme tous les panneaux globaux (et relance le jeu si une pause était tenue).</summary>
        public void CloseAll()
        {
            CloseAllPanels();
            ReleasePauseHold();
        }

        private void CloseAllPanels()
        {
            profileMenu?.Close();
            mainMenu?.Close();
            leaderboard?.Close();
            fastTravel?.Close();
            pauseMenu?.Close();
            controls?.Close();
        }
    }
}
