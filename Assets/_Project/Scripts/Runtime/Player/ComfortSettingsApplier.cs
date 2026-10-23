// ComfortSettingsApplier : applique les réglages de confort du menu au joueur VR.
//
// OÙ LE POSER : sur l'objet racine « XR Origin (XR Rig) » (ajouté par le générateur de la scène Boot).
//
// Réglages (menu « Confort ») : vignette anti-nausée, rotation par pas (angle de 45°) ou continue,
// vitesse de marche, hauteur assise (décalage vertical de la zone de jeu, utile assis), et mode de déplacement du stick gauche :
//   - par défaut, TÉLÉPORTATION : pousser le stick gauche vers l'avant fait apparaître le laser de téléportation, on vise le sol et on
//     relâche pour s'y rendre (le déplacement continu est alors coupé) ;
//   - option « Déplacement continu » : le stick gauche fait marcher le joueur (vitesse réglable), sans laser.
// Le basculement passe par « Smooth Motion Enabled » du ControllerInputActionManager de la manette gauche (Starter Assets de XRI), qui
// active l'une ou l'autre des actions d'entrée. Le stick droit garde la rotation par pas (et sa propre téléportation).
// Les réglages sont relus à chaque changement (GameEvents.SettingsChanged) et au démarrage.

using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort;

namespace ArcheryChallenge.Player
{
    /// <summary>Confort VR.</summary>
    [DisallowMultipleComponent]
    public class ComfortSettingsApplier : MonoBehaviour
    {
        private DynamicMoveProvider move;
        private SnapTurnProvider snapTurn;
        private ContinuousTurnProvider smoothTurn;
        private TunnelingVignetteController vignette;
        private ControllerInputActionManager leftController;
        private Transform cameraOffset;

        private void Awake()
        {
            move = GetComponentInChildren<DynamicMoveProvider>(true);
            snapTurn = GetComponentInChildren<SnapTurnProvider>(true);
            smoothTurn = GetComponentInChildren<ContinuousTurnProvider>(true);
            vignette = GetComponentInChildren<TunnelingVignetteController>(true);
            foreach (ControllerInputActionManager manager in GetComponentsInChildren<ControllerInputActionManager>(true))
                if (manager.name.Contains("Left"))
                    leftController = manager;
            Transform offset = transform.Find("Camera Offset");
            cameraOffset = offset;
        }

        private void OnEnable()
        {
            GameEvents.SettingsChanged += Apply;
            Apply();
        }

        private void OnDisable() => GameEvents.SettingsChanged -= Apply;

        /// <summary>Relit les réglages et les applique.</summary>
        public void Apply()
        {
            SettingsService settings = Services.Get<SettingsService>();
            if (settings == null)
                return;
            SettingsData d = settings.Data;

            if (move != null)
                move.moveSpeed = d.moveSpeed;
            if (leftController != null && leftController.smoothMotionEnabled != d.continuousMove)
                leftController.smoothMotionEnabled = d.continuousMove;
            if (snapTurn != null)
            {
                snapTurn.enabled = d.snapTurn;
                snapTurn.turnAmount = d.snapTurnAngle;
            }
            if (smoothTurn != null)
                smoothTurn.enabled = !d.snapTurn;
            if (vignette != null)
                vignette.enabled = d.vignetteEnabled;
            if (cameraOffset != null)
            {
                Vector3 p = cameraOffset.localPosition;
                p.y = d.seatedHeightOffset;
                cameraOffset.localPosition = p;
            }
        }
    }
}
