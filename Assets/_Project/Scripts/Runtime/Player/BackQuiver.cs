// BackQuiver : carquois dorsal. Main dans le dos + Grip = une flèche apparaît dans la main.
//
// OÙ LE POSER : sur l'objet « BackQuiver » (enfant de « Body », position 0, -0.25, -0.20).
// COMPOSANTS : Box Collider trigger de 0,40 × 0,45 × 0,30 m (la zone dans le dos), Layer « ArrowZone ».
// RÉGLAGES : « Grab Margin » = 0,10 m (tolérance autour de la boîte).
//
// POURQUOI UNE DÉTECTION PAR DISTANCE ET NON PAR COLLISION
// Le Near-Far Interactor de XRI 3.5 ignore les colliders « trigger » (réglage m_PhysicsTriggerInteraction).
// La boîte est donc seulement une ZONE : à chaque image, on teste si la main (position de la manette)
// est dans la boîte agrandie de la marge, et si le Grip vient d'être enfoncé.
//
// QUELLE MAIN : les flèches se prennent de la main « de corde », c'est-à-dire l'AUTRE main que celle de l'arc : la GAUCHE par défaut
// (réglage « Arc dans la main gauche » du menu pour inverser). Tant que l'arc n'est pas saisi, la main d'arc du réglage ne prend
// donc jamais de flèche : en cherchant l'arc à l'épaule, on ne ramasse plus de flèches par erreur. La zone est décalée du côté de
// la main à flèches (hanche arrière) et une main proche d'un arc non saisi ne prend rien non plus.
//
// La flèche donnée est celle de l'équipement choisi. Le carquois refuse quand la manche n'autorise plus
// de flèche (voir ArrowAllowance). Une flèche tenue en main qui est lâchée tombe au sol.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Player
{
    /// <summary>Carquois porté dans le dos (VR).</summary>
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public class BackQuiver : MonoBehaviour
    {
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField, Min(0f)] private float grabMargin = 0.10f;

        [Tooltip("Décalage latéral (m) du carquois vers le côté de la main qui prend les flèches.")]
        [SerializeField, Min(0f)] private float arrowSideOffset = 0.10f;

        [Tooltip("Une main plus proche que cette distance (m) d'un arc non saisi ne prend pas de flèche.")]
        [SerializeField, Min(0f)] private float bowClearance = 0.30f;

        private BoxCollider zone;
        private Vector3 baseLocalPosition;
        private bool baseCaptured;
        private Arrow heldLeft;
        private Arrow heldRight;

        public float GrabMargin
        {
            get => grabMargin;
            set => grabMargin = value;
        }

        private void Awake()
        {
            zone = GetComponent<BoxCollider>();
            baseLocalPosition = transform.localPosition;
            baseCaptured = true;
        }

        private void OnEnable()
        {
            GameEvents.SettingsChanged += ApplySide;
            ApplySide();
        }

        private void OnDisable() => GameEvents.SettingsChanged -= ApplySide;

        // Le carquois se décale vers le côté de la main à flèches (gauche par défaut).
        private void ApplySide()
        {
            if (!baseCaptured)
                return;
            SettingsService settings = Services.Get<SettingsService>();
            Handedness arrowSide = settings != null ? settings.ArrowSide : Handedness.Left;
            transform.localPosition = new Vector3(arrowSide == Handedness.Left ? -arrowSideOffset : arrowSideOffset, baseLocalPosition.y, baseLocalPosition.z);
        }

        private void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.40f, 0.45f, 0.30f);
            int layer = LayerMask.NameToLayer("ArrowZone");
            if (layer >= 0)
                gameObject.layer = layer;
        }

        /// <summary>Flèche actuellement tenue par une main (null si aucune).</summary>
        public Arrow GetHeldArrow(Handedness hand)
        {
            Arrow arrow = hand == Handedness.Left ? heldLeft : heldRight;
            if (arrow != null && arrow.State != ArrowState.Held)
            {
                // L'arc l'a encochée ou elle a été lâchée : la main ne la tient plus.
                SetHeld(hand, null);
                return null;
            }
            return arrow;
        }

        /// <summary>Indique que la main ne tient plus la flèche (elle vient d'être encochée).</summary>
        public void ClearHeld(Handedness hand, Arrow arrow)
        {
            if (GetHeldArrow(hand) == arrow)
                SetHeld(hand, null);
        }

        private void SetHeld(Handedness hand, Arrow arrow)
        {
            if (hand == Handedness.Left) heldLeft = arrow; else heldRight = arrow;
        }

        private void Update()
        {
            XRArcherInput input = Services.Get<XRArcherInput>();
            if (input == null || !input.IsActive)
                return;

            ProcessHand(input, Handedness.Left, leftHand != null ? leftHand : input.LeftHand);
            ProcessHand(input, Handedness.Right, rightHand != null ? rightHand : input.RightHand);
        }

        private void ProcessHand(XRArcherInput input, Handedness hand, Transform handTransform)
        {
            if (handTransform == null)
                return;

            Arrow held = GetHeldArrow(hand);
            if (held != null)
            {
                // Grip relâché loin de l'arc : la flèche est lâchée.
                if (!input.GripHeld(hand))
                {
                    SetHeld(hand, null);
                    held.Drop();
                }
                return;
            }

            if (!input.GripPressedThisFrame(hand))
                return;
            if (!IsInsideZone(handTransform.position))
                return;

            Bow bow = input.BowHolster != null ? input.BowHolster.CurrentBow : null;
            if (bow != null && bow.IsHeld)
            {
                // La main qui tient l'arc ne prend pas de flèche.
                if (bow.BowHand == hand)
                    return;
            }
            else
            {
                // Arc pas encore saisi : seule la main à flèches du réglage peut en prendre, et pas à côté de l'arc.
                SettingsService settings = Services.Get<SettingsService>();
                Handedness arrowSide = settings != null ? settings.ArrowSide : Handedness.Left;
                if (hand != arrowSide)
                    return;
                if (bow != null && Vector3.Distance(handTransform.position, bow.transform.position) < bowClearance)
                    return;
            }

            if (!ArrowAllowance.CanTake)
            {
                GameEvents.RaiseToast("Plus de flèches !");
                return;
            }

            EquipmentService equipment = Services.Get<EquipmentService>();
            ArrowPool pool = Services.Get<ArrowPool>();
            ArrowDefinition definition = equipment != null ? equipment.SelectedArrow : null;
            if (pool == null || definition == null)
                return;

            Arrow arrow = pool.Get(definition);
            if (arrow == null)
                return;

            arrow.HoldBy(handTransform);
            SetHeld(hand, arrow);
            GameEvents.RaiseArrowPicked(handTransform.position);
        }

        /// <summary>Vrai si le point (monde) est dans la zone du carquois, marge comprise.</summary>
        public bool IsInsideZone(Vector3 worldPoint)
        {
            if (zone == null)
                zone = GetComponent<BoxCollider>();
            Vector3 local = zone.transform.InverseTransformPoint(worldPoint) - zone.center;
            Vector3 half = zone.size * 0.5f + Vector3.one * grabMargin;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = zone != null ? zone : GetComponent<BoxCollider>();
            if (box == null)
                return;
            Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
