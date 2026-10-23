// DesktopBowView : tient l'arc à l'écran et le fait tirer (mode clavier/souris).
//
// OÙ LE POSER : sur la racine « DesktopPlayer » (avec DesktopArcherInput).
// RÉGLAGES (tutoriel §P4.4) : Holder ← BowHolder (enfant de la caméra, en bas à gauche de l'écran),
//   Camera ← DesktopCamera, Draw Back Distance 0,30 m, Aim Fov 55°.
//
// FONCTIONNEMENT
// - L'arc équipé est instancié dans « Holder », réduit à l'essentiel (sans saisie XR ni physique) ;
//   son point « DesktopViewAnchor » est posé sur le Holder. L'arc est orienté vers un point situé à
//   30 m devant le réticule : la flèche part donc vers le centre de l'écran.
// - R : prend une flèche et l'encoche. Clic gauche maintenu : la tension monte en « Desktop Draw Time »
//   secondes (plus lentement pour les gros arcs). Relâcher : tir. Clic droit : visée (zoom léger,
//   sensibilité réduite).
// - Avec l'encochage automatique (option de confort), le clic gauche prend et encoche la flèche tout seul.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ArcheryChallenge.Player
{
    /// <summary>Arc affiché à l'écran et piloté à la souris.</summary>
    [DisallowMultipleComponent]
    public class DesktopBowView : MonoBehaviour
    {
        [SerializeField] private Transform holder;
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(0f)] private float drawBackDistance = 0.30f;
        [SerializeField, Range(20f, 90f)] private float aimFov = 55f;
        [SerializeField, Min(5f)] private float convergenceDistance = 30f;

        [Tooltip("Échelle de l'arc affiché à l'écran (réduit pour ne pas masquer la vue).")]
        [SerializeField, Range(0.3f, 1f)] private float displayScale = 0.7f;

        private DesktopArcherInput input;
        private DesktopPlayerController player;
        private Bow bow;
        private float tension;
        private float baseFov = 70f;
        private Vector3 holderBasePosition;
        private bool drawing;

        /// <summary>Arc actuellement affiché.</summary>
        public Bow CurrentBow => bow;

        /// <summary>Tension actuelle (0 à 1), pour le réticule et le HUD.</summary>
        public float Tension => tension;

        private void Awake()
        {
            input = GetComponent<DesktopArcherInput>();
            player = GetComponent<DesktopPlayerController>();
            if (viewCamera == null)
                viewCamera = GetComponentInChildren<Camera>(true);
            if (viewCamera != null)
                baseFov = viewCamera.fieldOfView;
            if (holder != null)
                holderBasePosition = holder.localPosition;
        }

        private void OnEnable()
        {
            GameEvents.EquipmentChanged += RebuildBow;
            RebuildBow();
        }

        private void OnDisable()
        {
            GameEvents.EquipmentChanged -= RebuildBow;
            DestroyBow();
        }

        /// <summary>(Re)crée l'arc équipé devant la caméra.</summary>
        public void RebuildBow()
        {
            if (!isActiveAndEnabled)
                return;
            EquipmentService equipment = Services.Get<EquipmentService>();
            BowDefinition definition = equipment != null ? equipment.SelectedBow : null;
            if (definition == null || definition.prefab == null || holder == null)
                return;
            if (bow != null && bow.Definition == definition)
                return;

            DestroyBow();
            GameObject instance = Instantiate(definition.prefab, holder);
            instance.name = definition.prefab.name + "_Desktop";

            // En mode PC, l'arc n'est qu'un accessoire d'affichage : pas de saisie XR, pas de physique.
            XRGrabInteractable grab = instance.GetComponent<XRGrabInteractable>();
            if (grab != null)
                grab.enabled = false;
            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (body != null)
                body.isKinematic = true;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
                if (c.gameObject.layer != LayerMask.NameToLayer("ArrowZone") && !c.isTrigger)
                    c.enabled = false;

            bow = instance.GetComponent<Bow>();
            if (bow == null)
            {
                Destroy(instance);
                return;
            }
            bow.Definition = definition;
            bow.transform.localScale = Vector3.one * displayScale;
            bow.SetDesktopHeld(true);
            tension = 0f;
            drawing = false;
            PositionBow();
        }

        private void DestroyBow()
        {
            if (bow != null)
            {
                bow.SetDesktopHeld(false);
                Destroy(bow.gameObject);
                bow = null;
            }
        }

        private void Update()
        {
            if (bow == null || input == null)
            {
                RebuildBow();
                return;
            }

            // Un écran flottant est ouvert (curseur libre) : l'arc affiché est masqué pour ne pas gêner la lecture.
            if (holder != null)
            {
                bool menuOpen = CursorController.IsFreeRequested && !input.FireHeld;
                if (holder.gameObject.activeSelf == menuOpen)
                    holder.gameObject.SetActive(!menuOpen);
            }

            bool actionsAllowed = player == null || (player.InputEnabled && Time.timeScale > 0f && !CursorController.IsFreeRequested)
                                  || input.FireHeld; // l'injection de test ne dépend pas du curseur

            // --- Visée (zoom) ---
            bool aiming = input.AimHeld;
            if (viewCamera != null)
                viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, aiming ? aimFov : baseFov, Time.unscaledDeltaTime * 12f);
            if (player != null)
                player.SensitivityMultiplier = aiming ? 0.6f : 1f;

            if (!actionsAllowed)
            {
                if (drawing)
                    CancelDraw();
                PositionBow();
                return;
            }

            // --- Prendre une flèche (touche R) ---
            if (input.NockPressedThisFrame && !bow.HasArrow)
                NockNewArrow();

            // --- Tendre et tirer (clic gauche) ---
            if (input.FireHeld)
            {
                if (!bow.HasArrow && !drawing && IsAutoNock())
                    NockNewArrow();

                float drawTime = bow.Definition != null ? bow.Definition.desktopDrawTime : 0.6f;
                tension = Mathf.Min(1f, tension + Time.deltaTime / Mathf.Max(0.05f, drawTime));
                drawing = true;
                bow.SetTension(tension);
            }
            else if (drawing)
            {
                drawing = false;
                bow.Release();
                tension = 0f;
            }

            PositionBow();
        }

        private static bool IsAutoNock()
        {
            SettingsService settings = Services.Get<SettingsService>();
            return settings == null || settings.Data.autoNock;
        }

        /// <summary>Prend une flèche du carquois (virtuel) et l'encoche sur l'arc.</summary>
        public bool NockNewArrow()
        {
            if (bow == null || bow.HasArrow)
                return false;
            if (!ArrowAllowance.CanTake)
            {
                GameEvents.RaiseToast("Plus de flèches !");
                return false;
            }

            EquipmentService equipment = Services.Get<EquipmentService>();
            ArrowPool pool = Services.Get<ArrowPool>();
            ArrowDefinition definition = equipment != null ? equipment.SelectedArrow : null;
            if (pool == null || definition == null)
                return false;

            Arrow arrow = pool.Get(definition);
            if (arrow == null)
                return false;
            if (!bow.TryNock(arrow))
            {
                pool.Release(arrow);
                return false;
            }
            GameEvents.RaiseArrowPicked(bow.NockWorldPosition);
            return true;
        }

        private void CancelDraw()
        {
            drawing = false;
            tension = 0f;
            bow.SetTension(0f);
        }

        // Pose l'arc sur le support : le point DesktopViewAnchor de l'arc coïncide avec le Holder, et
        // l'arc regarde un point lointain devant le réticule. La caméra recule légèrement la main
        // quand la corde est tendue.
        private void PositionBow()
        {
            if (bow == null || holder == null)
                return;

            holder.localPosition = holderBasePosition + new Vector3(0f, 0f, -drawBackDistance * tension);

            Transform cam = viewCamera != null ? viewCamera.transform : holder.parent;
            // Tremblement du réticule (vent, fatigue, difficulté) : la flèche part vers le réticule décalé.
            Vector3 aimDirection = cam.forward;
            AimSway sway = Services.Get<AimSway>();
            if (sway != null && sway.IsActive)
                aimDirection = Quaternion.AngleAxis(sway.Offset.x, cam.up) * Quaternion.AngleAxis(-sway.Offset.y, cam.right) * aimDirection;
            Vector3 convergence = cam.position + aimDirection * convergenceDistance;
            Vector3 anchorLocal = bow.DesktopViewAnchor != null
                ? bow.transform.InverseTransformPoint(bow.DesktopViewAnchor.position)
                : Vector3.zero;

            // Direction de l'arc : du support vers le point de convergence.
            Vector3 toTarget = convergence - holder.position;
            if (toTarget.sqrMagnitude < 0.01f)
                toTarget = cam.forward;
            Quaternion rotation = Quaternion.LookRotation(toTarget.normalized, cam.up);
            bow.transform.rotation = rotation;
            bow.transform.position = holder.position - rotation * Vector3.Scale(anchorLocal, bow.transform.lossyScale);
        }
    }
}
