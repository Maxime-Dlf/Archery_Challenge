// PlayerModeService : choisit le « joueur » actif (casque VR, clavier/souris, simulateur XR).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
// RÉGLAGES (tutoriel §P2.3 et §P4.4–P4.5) :
//   Xr Rig       ← XR Origin (XR Rig)
//   Desktop Rig  ← DesktopPlayer (désactivé dans la Hierarchy : c'est ce service qui l'active)
//   Simulator    ← XR Interaction Simulator (désactivé dans la Hierarchy)
//   Startup Mode = Auto : runtime XR démarré (Quest autonome, ou casque relié en Meta Quest Link) → VR ; sinon clavier/souris
//
// RÈGLE « UNE LOGIQUE, DEUX PILOTES » : les deux rigs existent dans Boot mais un seul est actif à la
// fois ; les deux alimentent la même logique de tir.
//   F2        : casque (VR) ↔ clavier/souris. Sur PC, le runtime XR est arrêté en clavier/souris (la fenêtre Game et le
//               casque ne montrent plus que ce qu'il faut) et redémarré au retour en VR. Sans casque relié, F2 mène au
//               rig VR simulé à la place.
//   Maj + F2  : clavier/souris ↔ rig VR sans casque (développement). Ce dernier est piloté par SimulatedRigDriver (souris
//               et clavier simples : marcher, regarder, tendre la corde au clic) ; le XR Interaction Simulator d'origine
//               reste disponible en cochant « Use Stock Simulator ».
// POURQUOI « runtime XR démarré » et non « casque actif » : avec Meta Quest Link la session OpenXR ne devient active qu'environ
// 2 s après le lancement, donc au Start le casque paraît absent alors que le chargeur est déjà prêt.

using System;
using ArcheryChallenge.Player;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace ArcheryChallenge.Core
{
    /// <summary>Mode de démarrage demandé.</summary>
    public enum StartupMode
    {
        /// <summary>Casque détecté → VR, sinon clavier/souris.</summary>
        Auto,
        Desktop,
        Vr,
        Simulator
    }

    /// <summary>Bascule entre les trois façons de jouer et déplace le joueur.</summary>
    [DisallowMultipleComponent]
    public class PlayerModeService : MonoBehaviour
    {
        [SerializeField] private GameObject xrRig;
        [SerializeField] private GameObject desktopRig;
        [SerializeField] private GameObject simulator;
        [SerializeField] private StartupMode startupMode = StartupMode.Auto;

        [Tooltip("Coché : le rig VR sans casque est piloté par le XR Interaction Simulator d'origine (touches Maj/Espace/Tab, peu pratique). "
                 + "Décoché : SimulatedRigDriver (souris et clavier simples, arc en main, hauteur normale).")]
        [SerializeField] private bool useStockSimulator;

        private XROrigin xrOrigin;
        private DesktopPlayerController desktopController;

        /// <summary>Mode actuel.</summary>
        public PlayerMode Mode { get; private set; } = PlayerMode.Desktop;

        /// <summary>Annoncé à chaque changement de mode.</summary>
        public event Action<PlayerMode> ModeChanged;

        public GameObject XrRig => xrRig;
        public GameObject DesktopRig => desktopRig;

        /// <summary>Vrai en VR (casque ou simulateur).</summary>
        public bool IsVr => Mode != PlayerMode.Desktop;

        /// <summary>Vrai seulement avec un vrai casque : interface posée dans le monde, rayons des manettes, clavier virtuel.</summary>
        public bool IsHeadset => Mode == PlayerMode.VR;

        /// <summary>Vrai quand le joueur utilise la souris et le clavier (mode PC ou rig VR sans casque) : écrans flottants cliquables à la souris.</summary>
        public bool UsesMouse => Mode != PlayerMode.VR;

        /// <summary>Racine du joueur actif (porte le Character Controller).</summary>
        public Transform ActiveRoot => IsVr ? (xrRig != null ? xrRig.transform : null) : (desktopRig != null ? desktopRig.transform : null);

        /// <summary>Caméra (tête) du joueur actif.</summary>
        public Transform ActiveHead
        {
            get
            {
                if (IsVr)
                {
                    EnsureOrigin();
                    return xrOrigin != null && xrOrigin.Camera != null ? xrOrigin.Camera.transform : null;
                }
                return desktopController != null ? desktopController.CameraTransform : null;
            }
        }

        /// <summary>Contrôleur du joueur clavier/souris (null en VR).</summary>
        public DesktopPlayerController Desktop => desktopController;

        private void Awake()
        {
            Services.Register(this);
            if (desktopRig != null)
                desktopController = desktopRig.GetComponent<DesktopPlayerController>();
        }

        private void OnDestroy() => Services.Unregister(this);

        private void Start()
        {
            // Les deux rigs sont désactivés tant que le mode n'est pas choisi : un seul AudioListener.
            SetMode(DetectStartupMode());
        }

        /// <summary>Test seulement : force le mode de démarrage (démarrage VR simulé sans casque, voir SelfTest.VrBoot).</summary>
        public static PlayerMode? StartupOverride { get; set; }

        private PlayerMode DetectStartupMode()
        {
            if (StartupOverride.HasValue)
                return StartupOverride.Value;
            switch (startupMode)
            {
                case StartupMode.Desktop: return PlayerMode.Desktop;
                case StartupMode.Vr: return PlayerMode.VR;
                case StartupMode.Simulator: return PlayerMode.Simulator;
            }
            return IsXrRuntimeActive ? PlayerMode.VR : PlayerMode.Desktop;
        }

        /// <summary>
        /// Vrai si le runtime XR est démarré (chargeur OpenXR actif) : Quest autonome, ou casque relié en Meta Quest Link.
        /// Sans casque, l'initialisation du chargeur échoue et ce test est faux.
        /// </summary>
        public static bool IsXrRuntimeActive
        {
            get
            {
                XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
                return manager != null && manager.activeLoader != null;
            }
        }

        /// <summary>Vrai si un casque VR est utilisable (voir <see cref="IsXrRuntimeActive"/>).</summary>
        public static bool IsHeadsetPresent() => IsXrRuntimeActive;

        /// <summary>(Re)démarre le runtime XR (casque) si besoin.</summary>
        /// <returns>Vrai si le runtime tourne ensuite (casque prêt).</returns>
        private static bool TryStartXrRuntime()
        {
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null)
                return false;
            if (manager.activeLoader != null)
                return true;
            try
            {
                manager.InitializeLoaderSync();
                if (manager.activeLoader == null)
                    return false;
                manager.StartSubsystems();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayerMode] Démarrage du runtime XR impossible : " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Arrête le runtime XR sur PC quand on quitte la VR : sinon le casque (et le miroir de la fenêtre Game) continuerait à
        /// attendre une image. Sur Quest, le runtime reste toujours actif.
        /// </summary>
        private static void StopXrRuntime()
        {
            if (Application.platform == RuntimePlatform.Android)
                return;
            XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || !manager.isInitializationComplete || manager.activeLoader == null)
                return;
            try
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayerMode] Arrêt du runtime XR impossible : " + e.Message);
            }
        }

        /// <summary>Active le mode demandé (et désactive les autres rigs).</summary>
        public void SetMode(PlayerMode mode)
        {
            // Le runtime XR est démarré AVANT d'activer le rig VR (XR Origin règle son mode de suivi à l'activation).
            if (mode == PlayerMode.VR)
                TryStartXrRuntime();
            else
                StopXrRuntime();
            Mode = mode;
            if (xrRig != null)
                xrRig.SetActive(mode != PlayerMode.Desktop);
            if (desktopRig != null)
                desktopRig.SetActive(mode == PlayerMode.Desktop);
            if (simulator != null)
                simulator.SetActive(mode == PlayerMode.Simulator && useStockSimulator);
            EnsureOrigin();
            ModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// F2 : bascule entre le casque (VR) et le clavier/souris. Si aucun casque n'est utilisable (Link éteint…), le rig VR
        /// simulé prend la place de la VR.
        /// </summary>
        public void TogglePlayMode()
        {
            Pose pose = CurrentPose();
            if (Mode != PlayerMode.Desktop)
            {
                SetMode(PlayerMode.Desktop);
                TeleportTo(pose);
                return;
            }

            if (TryStartXrRuntime())
            {
                SetMode(PlayerMode.VR);
                // Le casque n'est « suivi » qu'au bout de quelques images : on place le joueur une fois sa tête connue.
                StartCoroutine(PlaceAfterTracking(pose));
            }
            else
            {
                GameEvents.RaiseToast("Casque non détecté : rig VR simulé au clavier/souris (Maj + F2 pour y revenir).");
                SetMode(PlayerMode.Simulator);
                TeleportTo(pose);
            }
        }

        private System.Collections.IEnumerator PlaceAfterTracking(Pose pose)
        {
            yield return new WaitForSecondsRealtime(0.6f);
            if (Mode == PlayerMode.VR)
                TeleportTo(pose);
        }

        /// <summary>Maj + F2 : bascule entre clavier/souris et rig VR simulé (inutile avec un vrai casque).</summary>
        public void ToggleSimulator()
        {
            if (Mode == PlayerMode.VR)
                return;
            Pose pose = CurrentPose();
            SetMode(Mode == PlayerMode.Desktop ? PlayerMode.Simulator : PlayerMode.Desktop);
            TeleportTo(pose);
        }

        private void EnsureOrigin()
        {
            if (xrOrigin == null && xrRig != null)
                xrOrigin = xrRig.GetComponent<XROrigin>();
        }

        /// <summary>Pose actuelle du joueur (position au sol et lacet de la tête).</summary>
        public Pose CurrentPose()
        {
            Transform head = ActiveHead;
            Transform root = ActiveRoot;
            if (head == null || root == null)
                return new Pose(root != null ? root.position : Vector3.zero, Quaternion.identity);
            Vector3 position = new Vector3(head.position.x, root.position.y, head.position.z);
            return new Pose(position, Quaternion.Euler(0f, head.eulerAngles.y, 0f));
        }

        /// <summary>
        /// Place le joueur à une pose (au sol) en le tournant dans la direction voulue. En VR, la
        /// hauteur réelle du joueur est conservée ; la tête se retrouve au-dessus du point indiqué.
        /// </summary>
        public void TeleportTo(Pose pose)
        {
            Transform root = ActiveRoot;
            if (root == null)
                return;

            CharacterController controller = root.GetComponent<CharacterController>();
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;

            if (IsVr)
            {
                EnsureOrigin();
                if (xrOrigin != null && xrOrigin.Camera != null)
                {
                    Vector3 forward = pose.rotation * Vector3.forward;
                    xrOrigin.MatchOriginUpCameraForward(Vector3.up, forward);
                    Vector3 cameraPosition = xrOrigin.Camera.transform.position;
                    Vector3 delta = pose.position - cameraPosition;
                    root.position += new Vector3(delta.x, 0f, delta.z);
                    root.position = new Vector3(root.position.x, pose.position.y, root.position.z);
                }
                else
                {
                    root.SetPositionAndRotation(pose.position, pose.rotation);
                }
            }
            else
            {
                if (desktopController != null)
                    desktopController.PlaceAt(pose);
                else
                    root.SetPositionAndRotation(pose.position, pose.rotation);
            }

            if (wasEnabled)
                controller.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>Place le joueur sur le point d'apparition nommé (camp, hub, arena_01…).</summary>
        /// <returns>Vrai si le point existe.</returns>
        public bool TeleportToSpawn(string spawnId)
        {
            if (PlayerSpawnPoint.TryFind(spawnId, out PlayerSpawnPoint spawn))
            {
                TeleportTo(spawn.SpawnPose);
                return true;
            }
            return false;
        }
    }
}
