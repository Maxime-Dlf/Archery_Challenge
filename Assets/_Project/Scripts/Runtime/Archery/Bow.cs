// Bow : un arc (tension, encochage, tir).
//
// OÙ LE POSER : sur la racine de chaque prefab d'arc (Bow_Apprentice, Bow_Hunter…).
// Hiérarchie attendue (voir tutoriel §P3.5) :
//   Bow_Apprentice  Capsule Collider, Rigidbody, XR Grab Interactable, Bow         (Layer Bow)
//   ├── Visual      modèle de l'arc (vertical, tir vers +Z, corde côté -Z)
//   ├── GripAttach  où la main tient l'arc
//   ├── ArrowRest   appui de la flèche sur la poignée
//   ├── StringTop / StringBottom   pointes de l'arc (extrémités de la corde)
//   ├── StringRest  position de la corde au repos
//   │   └── NockPoint   point où se cale l'encoche (déplacé pendant la tension)
//   ├── String      LineRenderer + BowStringRenderer + BowString
//   ├── NockZone    Sphere Collider trigger + NockZone       (Layer ArrowZone)
//   ├── Trajectory  LineRenderer + TrajectoryPreview
//   └── DesktopViewAnchor
//
// RÔLES : ce script est le « cerveau » de l'arc. Il ne sait PAS d'où vient l'ordre de tendre
// (main en VR, clic de souris au PC) : les « pilotes » (BowString en VR, DesktopArcherInput au PC)
// appellent DrawToWorldPoint() ou SetTension(), puis Release(). Une logique, deux pilotes.
//
// BALISTIQUE AU DÉPART
//   vitesse = vitesse max de l'arc × courbe de puissance(tension) × multiplicateur de la flèche
//   direction = axe de l'arc (+Z), légèrement corrigé si la main de corde dévie de l'axe.
// Sous la tension minimale de la difficulté, la flèche part mollement et tombe au sol.

using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Targets;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcheryChallenge.Archery
{
    /// <summary>Logique d'un arc : tension, encochage, tir.</summary>
    [DisallowMultipleComponent]
    public class Bow : MonoBehaviour
    {
        [SerializeField] private BowDefinition definition;
        [SerializeField] private Transform stringRest;
        [SerializeField] private Transform nockPoint;
        [SerializeField] private Transform arrowRest;
        [SerializeField] private XRGrabInteractable grabInteractable;
        [SerializeField] private Transform desktopViewAnchor;

        [Tooltip("Durée (s) du retour de la corde au repos après un tir (animation visuelle).")]
        [SerializeField, Min(0.01f)] private float stringSnapTime = 0.07f;

        /// <summary>Déviation latérale maximale (m) de la main de corde par rapport à l'axe de l'arc.</summary>
        private const float MaxLateral = 0.12f;

        private Arrow nocked;
        private float tension;
        private bool desktopHeld;
        private Vector3 releasedLocalOffset;
        private float snapTimer = -1f;
        private Vector3 nockRestLocal;
        private float restDistance;
        private float restHeight;
        private Handedness bowHand = Handedness.Right;
        private bool heldByXR;
        private Rigidbody body;
        private float lastDrawEvent;

        // ---------------------------------------------------------------- propriétés

        public BowDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public Transform StringRest => stringRest;
        public Transform NockPoint => nockPoint;
        public Transform ArrowRest => arrowRest;
        public Transform DesktopViewAnchor => desktopViewAnchor;
        public XRGrabInteractable GrabInteractable => grabInteractable;

        /// <summary>Arc tenu par la main (VR) ou affiché à l'écran (clavier/souris).</summary>
        public bool IsHeld => desktopHeld || heldByXR;

        /// <summary>Vrai en mode clavier/souris.</summary>
        public bool IsDesktop => desktopHeld;

        /// <summary>Main qui tient l'arc (VR) ; sans signification en mode PC.</summary>
        public Handedness BowHand => bowHand;

        /// <summary>Flèche encochée (null si aucune).</summary>
        public Arrow NockedArrow => nocked;

        /// <summary>Vrai si une flèche est encochée.</summary>
        public bool HasArrow => nocked != null;

        /// <summary>Tension actuelle, de 0 (repos) à 1 (pleine).</summary>
        public float Tension => tension;

        /// <summary>Vrai tant que la corde est tendue.</summary>
        public bool IsDrawn => tension > 0.02f;

        /// <summary>Allonge (m) = recul de la main pour atteindre 100 %.</summary>
        public float DrawLength => definition != null ? definition.drawLength : 0.55f;

        /// <summary>Axe de tir (vers l'avant de l'arc).</summary>
        public Vector3 Forward => transform.forward;

        /// <summary>Tension minimale de la difficulté courante (en dessous, la flèche tombe).</summary>
        public float MinTension
        {
            get
            {
                DifficultyApplier applier = Services.Get<DifficultyApplier>();
                return applier != null && applier.Current != null ? applier.Current.minTension : 0.15f;
            }
        }

        /// <summary>Position de la main de corde idéale au repos (monde).</summary>
        public Vector3 StringRestPosition => stringRest != null ? stringRest.position : transform.position;

        // ---------------------------------------------------------------- cycle de vie

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            if (grabInteractable == null)
                grabInteractable = GetComponent<XRGrabInteractable>();
            // Une seule main à la fois : la main libre qui approche de la corde ne doit pas « voler » l'arc.
            if (grabInteractable != null)
                grabInteractable.selectFilters.Add(new OneHandFilter());
            if (stringRest != null && nockPoint != null)
                nockRestLocal = nockPoint.localPosition;
            if (stringRest != null && arrowRest != null)
            {
                Vector3 rel = transform.InverseTransformPoint(arrowRest.position) - transform.InverseTransformPoint(stringRest.position);
                restDistance = Mathf.Max(0.05f, rel.z);
                restHeight = arrowRest.localPosition.y - stringRest.localPosition.y;
            }
            else
            {
                restDistance = 0.22f;
            }
        }

        private void OnEnable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.AddListener(OnSelectEntered);
                grabInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (grabInteractable != null)
            {
                grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
            CancelDraw();
            heldByXR = false;
        }

        private void OnSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            heldByXR = true;
            bowHand = ResolveHand(args.interactorObject);
            if (body != null)
                body.isKinematic = false;
        }

        private void OnSelectExited(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
        {
            // Arc lâché : la flèche encochée tombe, la corde revient au repos.
            heldByXR = false;
            CancelDraw();
        }

        /// <summary>Filtre XRI : refuse qu'une seconde main saisisse l'arc déjà tenu.</summary>
        private sealed class OneHandFilter : IXRSelectFilter
        {
            public bool canProcess => true;

            public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
            {
                return interactable.interactorsSelecting.Count == 0 || interactable.interactorsSelecting.Contains(interactor);
            }
        }

        private static Handedness ResolveHand(IXRSelectInteractor interactor)
        {
            switch (interactor.handedness)
            {
                case InteractorHandedness.Left: return Handedness.Left;
                case InteractorHandedness.Right: return Handedness.Right;
            }
            // Repli : on regarde si l'interacteur est sous la manette gauche ou droite du rig actif.
            XRArcherInput rig = Services.Get<XRArcherInput>();
            if (rig != null && rig.LeftHand != null && interactor.transform.IsChildOf(rig.LeftHand))
                return Handedness.Left;
            return Handedness.Right;
        }

        /// <summary>Mode clavier/souris : l'arc est affiché devant la caméra par DesktopBowView.</summary>
        public void SetDesktopHeld(bool held)
        {
            desktopHeld = held;
            if (!held)
                CancelDraw();
        }

        // ---------------------------------------------------------------- encochage

        /// <summary>
        /// Encoche une flèche. Refuse si l'arc n'est pas tenu ou déjà chargé.
        /// </summary>
        public bool TryNock(Arrow arrow)
        {
            if (arrow == null || nocked != null || !IsHeld)
                return false;

            nocked = arrow;
            arrow.SetNocked(transform);
            UpdateArrowPose();
            GameEvents.RaiseArrowNocked(StringRestPosition);
            return true;
        }

        /// <summary>Retire la flèche encochée sans tirer (elle tombe).</summary>
        public void CancelDraw()
        {
            if (nocked != null)
            {
                Arrow arrow = nocked;
                nocked = null;
                arrow.Drop();
            }
            tension = 0f;
            ResetNock();
        }

        // ---------------------------------------------------------------- tension

        /// <summary>Tension pilotée par un nombre (clavier/souris) : 0 à 1.</summary>
        public void SetTension(float value)
        {
            tension = Mathf.Clamp01(value);
            snapTimer = -1f;
            if (nockPoint != null && stringRest != null)
                nockPoint.position = stringRest.position - Forward * (tension * DrawLength);
            NotifyDrawing();
            UpdateArrowPose();
        }

        /// <summary>
        /// Tension pilotée par une main (VR) : la corde suit la main, limitée par l'allonge.
        /// </summary>
        public void DrawToWorldPoint(Vector3 handWorld)
        {
            if (nockPoint == null || stringRest == null)
                return;

            snapTimer = -1f;
            Vector3 fwd = Forward;
            Vector3 rel = handWorld - stringRest.position;
            float along = Mathf.Max(0f, Vector3.Dot(rel, -fwd));
            float clamped = Mathf.Min(along, DrawLength);
            Vector3 lateral = rel + fwd * along;                     // composante hors de l'axe
            lateral = Vector3.ClampMagnitude(lateral, MaxLateral);

            nockPoint.position = stringRest.position - fwd * clamped + lateral;
            tension = DrawLength > 0.001f ? Mathf.Clamp01(clamped / DrawLength) : 0f;
            NotifyDrawing();
            UpdateArrowPose();
        }

        private void NotifyDrawing()
        {
            // Un événement tous les ~0,03 s : suffisant pour le son et les vibrations, sans les saturer.
            if (Time.unscaledTime - lastDrawEvent > 0.03f)
            {
                lastDrawEvent = Time.unscaledTime;
                GameEvents.RaiseBowDrawing(nockPoint != null ? nockPoint.position : transform.position, tension);
            }
        }

        // ---------------------------------------------------------------- tir

        /// <summary>Direction de départ de la flèche (monde).</summary>
        public Vector3 GetShotDirection()
        {
            if (nockPoint == null || stringRest == null)
                return Forward;
            // Axe de l'arc corrigé par la déviation latérale de la main de corde : tirer la corde
            // vers la gauche envoie la flèche vers la droite, comme avec un vrai arc.
            Vector3 fwd = Forward;
            Vector3 rel = nockPoint.position - stringRest.position;
            float along = Mathf.Max(0f, Vector3.Dot(rel, -fwd));
            Vector3 lateral = rel + fwd * along;
            Vector3 direction = (fwd * (restDistance + along) - lateral).normalized;

            // VR : la main tremble (vent, fatigue, difficulté) ; au clavier/souris c'est la visée de DesktopBowView qui tremble.
            if (!desktopHeld)
            {
                AimSway sway = Services.Get<AimSway>();
                if (sway != null && sway.IsActive)
                    direction = Quaternion.AngleAxis(sway.Offset.x, transform.up) * Quaternion.AngleAxis(-sway.Offset.y, transform.right) * direction;
            }
            return direction;
        }

        /// <summary>
        /// Relâche la corde. Si une flèche est encochée et la tension suffisante, elle part.
        /// </summary>
        /// <returns>La flèche tirée, ou null (tir à vide, tension trop faible).</returns>
        public Arrow Release()
        {
            float t = tension;
            Vector3 position = nockPoint != null ? nockPoint.position : transform.position;
            Arrow fired = null;
            bool launched = false;

            if (nocked != null)
            {
                Arrow arrow = nocked;
                nocked = null;
                Vector3 direction = GetShotDirection();
                arrow.transform.rotation = Quaternion.LookRotation(direction, transform.up);

                if (t >= MinTension)
                {
                    float speed = definition != null ? definition.maxSpeed : 30f;
                    speed *= definition != null ? definition.EvaluatePower(t) : t;
                    speed *= arrow.Definition != null ? arrow.Definition.speedMultiplier : 1f;

                    DifficultyApplier applier = Services.Get<DifficultyApplier>();
                    float dragFactor = applier != null && applier.Current != null ? applier.Current.dragMultiplier : 1f;
                    if (applier != null && applier.Current != null && applier.Current.aimAssistAngle > 0f)
                    {
                        direction = AimAssist.Apply(arrow.Tip.position, direction, speed, applier.Current.aimAssistAngle);
                        arrow.transform.rotation = Quaternion.LookRotation(direction, transform.up);
                    }
                    Vector3 shooter = ShooterPosition();
                    arrow.Launch(direction * speed, shooter, dragFactor);
                    launched = true;
                    fired = arrow;

                    ArrowAllowance.Consume();
                    ScoreService score = Services.Get<ScoreService>();
                    if (score != null && score.AcceptShots)
                        score.RegisterArrowShot();
                    GameEvents.RaiseArrowShot(arrow, speed, t);
                }
                else
                {
                    // Tension trop faible : la flèche glisse hors de l'arc et tombe.
                    arrow.Drop();
                    arrow.Body.linearVelocity = direction * Mathf.Lerp(0.3f, 2.5f, t / Mathf.Max(0.01f, MinTension));
                }
            }

            GameEvents.RaiseBowReleased(position, t, launched);

            // Animation du retour de la corde.
            if (nockPoint != null && stringRest != null)
            {
                releasedLocalOffset = nockPoint.localPosition;
                snapTimer = 0f;
            }
            tension = 0f;
            return fired;
        }

        private Vector3 ShooterPosition()
        {
            IArcherInput input = Services.Get<IArcherInput>();
            if (input != null && input.Head != null)
                return input.Head.position;
            return transform.position;
        }

        // ---------------------------------------------------------------- mises à jour visuelles

        private void ResetNock()
        {
            snapTimer = -1f;
            if (nockPoint != null)
                nockPoint.localPosition = nockRestLocal;
        }

        private void LateUpdate()
        {
            // Retour élastique de la corde après le tir.
            if (snapTimer >= 0f && nockPoint != null)
            {
                snapTimer += Time.deltaTime;
                float t = Mathf.Clamp01(snapTimer / stringSnapTime);
                nockPoint.localPosition = Vector3.Lerp(releasedLocalOffset, nockRestLocal, t);
                if (t >= 1f)
                    snapTimer = -1f;
            }

            // Une flèche tenue en main au repos suit l'arc (arc déplacé sans que la corde soit tirée).
            if (nocked != null)
                UpdateArrowPose();
        }

        private void UpdateArrowPose()
        {
            if (nocked == null || nockPoint == null)
                return;

            Vector3 direction = GetShotDirection();
            Quaternion rotation = Quaternion.LookRotation(direction, transform.up);
            // La flèche repose sur l'appui : on la relève de la hauteur de l'appui par rapport à la corde.
            nocked.transform.SetPositionAndRotation(nockPoint.position + transform.up * restHeight, rotation);
        }

        /// <summary>Extrémité (monde) de la flèche encochée, ou position de la corde si vide.</summary>
        public Vector3 NockWorldPosition => nockPoint != null ? nockPoint.position : transform.position;

        private void OnDrawGizmosSelected()
        {
            if (stringRest == null || arrowRest == null)
                return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(stringRest.position, stringRest.position - transform.forward * DrawLength);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(arrowRest.position, arrowRest.position + transform.forward * 1f);
        }
    }
}
