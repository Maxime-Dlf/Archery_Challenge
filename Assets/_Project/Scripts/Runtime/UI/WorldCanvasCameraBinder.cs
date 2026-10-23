// WorldCanvasCameraBinder : relie un Canvas d'interface à la bonne caméra, en VR comme au PC.
//
// OÙ LE POSER : sur le Canvas racine de chaque prefab d'interface (avec Graphic Raycaster ET
// Tracked Device Graphic Raycaster : le premier pour la souris, le second pour les rayons et le doigt en VR).
//
// FONCTIONNEMENT
// - Espace monde (panneaux du hub, du camp, des arènes, montre) : la « caméra d'événements » du Canvas est
//   toujours la caméra du joueur actif (VR ou clavier/souris), elle change toute seule quand on bascule (F2).
// - Écran flottant (OpenFloating) : le panneau se détache de son support et se pose devant le joueur, comme
//   une fenêtre du Meta Quest (VR) ou comme un écran de taille raisonnable (clavier/souris et rig VR simulé, où
//   le curseur est libéré pour cliquer). À la fermeture (CloseFloating), il retrouve sa place d'origine. Si le joueur
//   change de mode (F2) pendant que l'écran est ouvert, celui-ci se repose devant la nouvelle tête.
// - Tous les boutons reçoivent automatiquement les sons de survol et de clic.

using ArcheryChallenge.Audio;
using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Gestion de la caméra et de l'affichage flottant d'un Canvas.</summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public class WorldCanvasCameraBinder : MonoBehaviour
    {
        private Canvas canvas;
        private LazyFollow follow;
        private bool floating;
        private Transform savedParent;
        private Vector3 savedLocalPosition;
        private Quaternion savedLocalRotation;
        private Vector3 savedLocalScale;
        private float nextCameraCheck;
        private bool soundsAdded;
        private bool modeKnown;
        private PlayerMode lastMode;
        private int resnapFrame = -1;
        private TrackedDeviceGraphicRaycaster tracked;
        private bool disabledTracked;

        /// <summary>Vrai tant que le panneau flotte devant le joueur.</summary>
        public bool IsFloating => floating;

        private void Awake() => canvas = GetComponent<Canvas>();

        private void OnEnable()
        {
            AddSounds();
            if (tracked == null)
                tracked = GetComponent<TrackedDeviceGraphicRaycaster>();
            if (tracked != null && disabledTracked)
            {
                disabledTracked = false;
                tracked.enabled = true;
            }
            Bind();
        }

        private void OnDisable()
        {
            if (floating)
                CloseFloating();
            // Le raycaster XRI se désenregistre avec la caméra du Canvas : si la caméra est détruite avant lui (arrêt du jeu,
            // déchargement d'une arène), son OnDisable lève une KeyNotFoundException. On le coupe ici, tant que la caméra existe.
            if (tracked != null && tracked.enabled)
            {
                tracked.enabled = false;
                disabledTracked = true;
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextCameraCheck)
            {
                nextCameraCheck = Time.unscaledTime + 0.5f;
                Bind();
            }
            if (floating)
            {
                RefreshForModeChange();
                if (resnapFrame >= 0 && Time.frameCount >= resnapFrame)
                {
                    resnapFrame = -1;
                    Bind();
                    if (follow != null)
                        follow.SnapToHead();
                }
            }
        }

        // F2 : le joueur change de rig (clavier/souris ↔ rig VR simulé) alors qu'un écran flottant est ouvert. Il faut alors
        // rattacher le Canvas à la nouvelle caméra, reposer l'écran devant la nouvelle tête et rendre/libérer le curseur
        // (sans cela le menu ouvert d'un côté n'était plus cliquable de l'autre).
        private void RefreshForModeChange()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode == null)
                return;
            if (!modeKnown)
            {
                modeKnown = true;
                lastMode = mode.Mode;
                return;
            }
            if (mode.Mode == lastMode)
                return;
            lastMode = mode.Mode;
            ApplyCursorRequest(mode);
            Bind();
            // La nouvelle tête n'est posée à sa vraie place qu'après l'image du changement de mode : on repose l'écran un peu plus tard.
            resnapFrame = Time.frameCount + 3;
        }

        // Le curseur est libéré tant qu'un écran flottant est ouvert, sauf avec un vrai casque (rayons des manettes).
        private void ApplyCursorRequest(PlayerModeService mode)
        {
            if (mode == null || mode.UsesMouse)
                CursorController.RequestFree(this);
            else
                CursorController.Release(this);
        }

        /// <summary>Associe le Canvas monde à la caméra du joueur actif.</summary>
        public void Bind()
        {
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace)
                return;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            Camera cam = head != null ? head.GetComponent<Camera>() : Camera.main;
            if (cam != null && canvas.worldCamera != cam)
            {
                // Le raycaster XRI enregistre le Canvas AVEC sa caméra d'événements. S'il n'est pas désactivé pendant le changement,
                // son OnDisable cherche l'ancienne entrée et lève une KeyNotFoundException (menu fermé après un F2, arrêt du jeu…).
                if (tracked == null)
                    tracked = GetComponent<TrackedDeviceGraphicRaycaster>();
                bool wasEnabled = tracked != null && tracked.enabled;
                if (wasEnabled)
                    tracked.enabled = false;
                canvas.worldCamera = cam;
                if (wasEnabled)
                    tracked.enabled = true;
            }
        }

        /// <summary>
        /// Pose le panneau comme un écran flottant devant le joueur. Au clavier/souris, le curseur est libéré.
        /// </summary>
        public void OpenFloating()
        {
            if (canvas == null)
                canvas = GetComponent<Canvas>();
            if (follow == null)
            {
                follow = GetComponent<LazyFollow>();
                if (follow == null)
                    follow = gameObject.AddComponent<LazyFollow>();
            }
            Bind();

            if (!floating)
            {
                floating = true;
                savedParent = transform.parent;
                savedLocalPosition = transform.localPosition;
                savedLocalRotation = transform.localRotation;
                savedLocalScale = transform.localScale;

                // Taille normale d'un écran flottant (1000 unités = 1 m), quelle que soit l'échelle du support.
                float parentScale = savedParent != null ? Mathf.Max(0.0001f, savedParent.lossyScale.x) : 1f;
                transform.localScale = Vector3.one * (LazyFollow.WorldScale / parentScale);

                PlayerModeService mode = Services.Get<PlayerModeService>();
                ApplyCursorRequest(mode);
                modeKnown = mode != null;
                if (mode != null)
                    lastMode = mode.Mode;
            }
            follow.SnapToHead();
        }

        /// <summary>Referme l'écran flottant : le panneau retrouve sa place d'origine.</summary>
        public void CloseFloating()
        {
            if (!floating)
                return;
            floating = false;
            CursorController.Release(this);
            if (follow != null)
                follow.Release();
            if (transform.parent == savedParent)
            {
                transform.localPosition = savedLocalPosition;
                transform.localRotation = savedLocalRotation;
                transform.localScale = savedLocalScale;
            }
        }

        // Ajoute les sons de survol et de clic à tous les boutons du Canvas.
        private void AddSounds()
        {
            if (soundsAdded)
                return;
            soundsAdded = true;
            foreach (Selectable selectable in GetComponentsInChildren<Selectable>(true))
                if (selectable.GetComponent<UiButtonSound>() == null)
                    selectable.gameObject.AddComponent<UiButtonSound>();
        }
    }

    /// <summary>Son de survol et de clic d'un élément d'interface.</summary>
    public class UiButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        public void OnPointerEnter(PointerEventData eventData) => Services.Get<AudioService>()?.Play2D("UiHover", 0.6f);

        public void OnPointerClick(PointerEventData eventData) => Services.Get<AudioService>()?.Play2D("UiClick");
    }
}
