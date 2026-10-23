// UiPanel : base commune des panneaux d'interface (ouverture, fermeture, curseur).
//
// OÙ LE POSER : n'est pas posé directement : les panneaux (MainMenuController, PauseMenuController…)
// en héritent. Un panneau « global » (dans UI_Global) démarre désactivé ; Open() l'affiche comme un écran
// flottant devant le joueur (LazyFollow), en VR comme au clavier/souris (où le curseur est libéré).
// Un panneau « de décor » (tableau du hub, arène) reste toujours actif et ignore Open/Close.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>Panneau d'interface ouvrable et fermable.</summary>
    public abstract class UiPanel : MonoBehaviour
    {
        [Tooltip("Coché pour un panneau global désactivé au départ et affiché par Open().")]
        [SerializeField] protected bool hiddenUntilOpened;

        private WorldCanvasCameraBinder binder;
        private Canvas canvas;

        public bool IsOpen { get; private set; }

        protected Canvas Canvas
        {
            get
            {
                if (canvas == null)
                    canvas = GetComponentInParent<Canvas>(true);
                return canvas;
            }
        }

        protected WorldCanvasCameraBinder Binder
        {
            get
            {
                if (binder == null)
                    binder = GetComponentInParent<WorldCanvasCameraBinder>(true);
                return binder;
            }
        }

        /// <summary>Racine d'affichage (le Canvas) à activer/désactiver.</summary>
        protected GameObject Root => Canvas != null ? Canvas.gameObject : gameObject;

        protected virtual void Awake()
        {
            if (hiddenUntilOpened)
                IsOpen = false;
        }

        /// <summary>Affiche le panneau.</summary>
        public void Open()
        {
            if (IsOpen && Root.activeSelf)
                return;
            // On active d'abord : le premier Awake() d'un panneau global remet IsOpen à faux ; l'état « ouvert »
            // doit donc être posé APRÈS (sinon la première ouverture n'est pas vue comme ouverte et Échap ne ferme pas).
            Root.SetActive(true);
            IsOpen = true;
            // Écran flottant devant le joueur (VR : comme une fenêtre du Quest ; PC : curseur libéré pour cliquer).
            if (Binder != null)
                Binder.OpenFloating();
            OnOpened();
        }

        /// <summary>Referme le panneau.</summary>
        public void Close()
        {
            if (!IsOpen && !Root.activeSelf)
                return;
            IsOpen = false;
            if (Binder != null)
                Binder.CloseFloating();
            OnClosed();
            if (hiddenUntilOpened)
                Root.SetActive(false);
        }

        /// <summary>
        /// Croix de fermeture : ferme le panneau et, s'il avait été ouvert depuis la pause, revient au menu pause (le jeu reste en pause).
        /// </summary>
        protected void CloseFromButton()
        {
            UiHub hub = Services.Get<UiHub>();
            if (hub != null)
                hub.CloseSubPanel();
            else
                Close();
        }

        /// <summary>Ouvre ou ferme selon l'état actuel.</summary>
        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }
    }
}
