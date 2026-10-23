// XRArcherInput : pilote VR du tireur (casque réel ou XR Interaction Simulator).
//
// OÙ LE POSER : sur l'objet racine « XR Origin (XR Rig) » de la scène Boot.
// RÉGLAGES (voir tutoriel §P4.3) : Head ← Main Camera ; Left Hand ← Left Controller ;
//   Right Hand ← Right Controller ; Bow Holster ← Body/BowHolster ; Back Quiver ← Body/BackQuiver.
//
// RÔLE
// - Fournit aux autres scripts la position des mains et l'état du Grip (bouton latéral).
//   Le Grip est lu sur le lecteur « Select » de l'interacteur de chaque main : c'est exactement la
//   même entrée que celle qui fait saisir les objets, donc elle fonctionne avec les vraies
//   manettes ET avec le XR Interaction Simulator, sans configuration supplémentaire.
// - Lit les boutons Menu (manette gauche) et Y (téléportation rapide) par des InputActions locales.
// - Rig VR sans casque (F2, voir SimulatedRigDriver) : E interagit, Échap ouvre le menu, T le voyage rapide.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;

namespace ArcheryChallenge.Input
{
    /// <summary>Entrées du joueur en réalité virtuelle.</summary>
    [DisallowMultipleComponent]
    public class XRArcherInput : MonoBehaviour, IArcherInput
    {
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private BowHolster bowHolster;
        [SerializeField] private BackQuiver backQuiver;

        private XRBaseInputInteractor leftInteractor;
        private XRBaseInputInteractor rightInteractor;
        private InputAction menuAction;
        private InputAction fastTravelAction;

        // Valeurs forcées par les tests (simulation sans casque) : -1 = pas de forçage.
        private int forcedLeftGrip = -1;
        private int forcedRightGrip = -1;
        private int forcedLeftDownFrame = -10, forcedRightDownFrame = -10;

        public Transform Head => head;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public BowHolster BowHolster => bowHolster;
        public BackQuiver BackQuiver => backQuiver;

        public PlayerMode Mode => Services.Get<PlayerModeService>() != null ? Services.Get<PlayerModeService>().Mode : PlayerMode.VR;

        public bool IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;

        // Rig VR sans casque (F2) : mêmes touches qu'au clavier/souris (E, Échap, T), les manettes n'existent pas.
        private static bool SimulatorKeyboard =>
            Keyboard.current != null && Services.Get<PlayerModeService>() != null && Services.Get<PlayerModeService>().Mode == PlayerMode.Simulator;

        public bool InteractPressedThisFrame =>
            SimulatorKeyboard && !CursorController.IsFreeRequested && Keyboard.current.eKey.wasPressedThisFrame;

        public bool MenuPressedThisFrame =>
            (menuAction != null && menuAction.WasPressedThisFrame()) || (SimulatorKeyboard && Keyboard.current.escapeKey.wasPressedThisFrame);

        public bool FastTravelPressedThisFrame =>
            (fastTravelAction != null && fastTravelAction.WasPressedThisFrame())
            || (SimulatorKeyboard && !CursorController.IsFreeRequested && Keyboard.current.tKey.wasPressedThisFrame);

        private void Awake()
        {
            if (head == null && Camera.main != null)
                head = Camera.main.transform;
            CacheInteractors();
            EnableGrabbingGameLayers();

            menuAction = new InputAction("Menu", InputActionType.Button);
            menuAction.AddBinding("<XRController>{LeftHand}/menu");
            menuAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            fastTravelAction = new InputAction("FastTravel", InputActionType.Button);
            fastTravelAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
            fastTravelAction.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
        }

        private void OnEnable()
        {
            Services.Register<IArcherInput>(this);
            Services.Register(this);
            menuAction?.Enable();
            fastTravelAction?.Enable();
        }

        private void OnDisable()
        {
            menuAction?.Disable();
            fastTravelAction?.Disable();
            Services.Unregister<IArcherInput>(this);
            Services.Unregister(this);
        }

        private void OnDestroy()
        {
            menuAction?.Dispose();
            fastTravelAction?.Dispose();
        }

        // Le lanceur « proche » des manettes (Sphere Interaction Caster des Starter Assets) ne détecte que le calque Default. L'arc (calque
        // Bow) et les flèches (calque Arrow) étaient donc invisibles pour les mains : impossible de saisir l'arc à l'épaule.
        private void EnableGrabbingGameLayers()
        {
            int mask = 0;
            foreach (string layerName in new[] { "Bow", "Arrow" })
            {
                int layer = LayerMask.NameToLayer(layerName);
                if (layer >= 0)
                    mask |= 1 << layer;
            }
            if (mask == 0)
                return;
            foreach (Transform hand in new[] { leftHand, rightHand })
            {
                if (hand == null)
                    continue;
                foreach (SphereInteractionCaster caster in hand.GetComponentsInChildren<SphereInteractionCaster>(true))
                    caster.physicsLayerMask |= mask;
            }
        }

        private void CacheInteractors()
        {
            if (leftHand != null && leftInteractor == null)
                leftInteractor = FindInteractor(leftHand);
            if (rightHand != null && rightInteractor == null)
                rightInteractor = FindInteractor(rightHand);
        }

        // Cherche l'interacteur « Near-Far » (celui qui lit le Grip) sous une manette.
        private static XRBaseInputInteractor FindInteractor(Transform controller)
        {
            XRBaseInputInteractor[] all = controller.GetComponentsInChildren<XRBaseInputInteractor>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] is NearFarInteractor)
                    return all[i];
            return all.Length > 0 ? all[0] : null;
        }

        /// <summary>Transform de la manette (main) demandée.</summary>
        public Transform GetHand(Handedness hand) => hand == Handedness.Left ? leftHand : rightHand;

        /// <summary>Position monde de la main.</summary>
        public Vector3 GetHandPosition(Handedness hand)
        {
            Transform t = GetHand(hand);
            return t != null ? t.position : Vector3.zero;
        }

        /// <summary>Vrai tant que le Grip de la main est enfoncé.</summary>
        public bool GripHeld(Handedness hand)
        {
            int forced = hand == Handedness.Left ? forcedLeftGrip : forcedRightGrip;
            if (forced >= 0)
                return forced == 1;

            CacheInteractors();
            XRBaseInputInteractor interactor = hand == Handedness.Left ? leftInteractor : rightInteractor;
            return interactor != null && interactor.selectInput != null && interactor.selectInput.ReadIsPerformed();
        }

        /// <summary>Vrai à l'image où le Grip vient d'être enfoncé.</summary>
        public bool GripPressedThisFrame(Handedness hand)
        {
            // Une pression forcée reste lisible pendant l'image où elle est posée et la suivante.
            int forcedFrame = hand == Handedness.Left ? forcedLeftDownFrame : forcedRightDownFrame;
            if (Time.frameCount - forcedFrame <= 1 && Time.frameCount >= forcedFrame)
                return true;
            int forced = hand == Handedness.Left ? forcedLeftGrip : forcedRightGrip;
            if (forced >= 0)
                return false;

            CacheInteractors();
            XRBaseInputInteractor interactor = hand == Handedness.Left ? leftInteractor : rightInteractor;
            return interactor != null && interactor.selectInput != null && interactor.selectInput.ReadWasPerformedThisFrame();
        }

        /// <summary>
        /// Force l'état du Grip d'une main (tests automatiques sans casque). Passer -1 pour revenir
        /// à la lecture réelle.
        /// </summary>
        public void ForceGrip(Handedness hand, int state)
        {
            bool wasHeld = GripHeld(hand);
            if (hand == Handedness.Left)
            {
                forcedLeftGrip = state;
                if (state == 1 && !wasHeld) forcedLeftDownFrame = Time.frameCount;
            }
            else
            {
                forcedRightGrip = state;
                if (state == 1 && !wasHeld) forcedRightDownFrame = Time.frameCount;
            }
        }

        /// <summary>Autre main que celle indiquée.</summary>
        public static Handedness Opposite(Handedness hand) => hand == Handedness.Left ? Handedness.Right : Handedness.Left;
    }
}
