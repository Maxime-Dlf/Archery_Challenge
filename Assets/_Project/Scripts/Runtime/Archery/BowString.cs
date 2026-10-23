// BowString : pilote VR de la corde. La main libre attrape la corde, la tend, puis la relâche.
//
// OÙ LE POSER : sur l'objet enfant « String » d'un arc (avec LineRenderer et BowStringRenderer).
// RÉGLAGE : « Grab Radius » = 0,15 m (la main libre attrape la corde à 15 cm près).
//
// DÉROULEMENT (une seule main « de corde » à la fois, toujours l'autre main que celle qui tient l'arc)
//  1. Main de corde avec une FLÈCHE (prise au carquois) qui entre dans la zone d'encochage
//     (rayon 0,25 m, × 1,3 en Facile) → la flèche s'encoche toute seule ; le Grip continue d'être tenu.
//  2. Main de corde SANS flèche : Grip enfoncé à moins de « Grab Radius » de la corde → on saisit la
//     corde. Si l'encochage automatique est activé (option de confort) et qu'il reste des flèches, une
//     flèche apparaît encochée ; sinon c'est un tir à vide.
//  3. Tant que le Grip est tenu : la corde suit la main (tension = recul / allonge).
//  4. Grip relâché : Bow.Release() → la flèche part si la tension dépasse le minimum de la difficulté.
//
// Pourquoi par distance et pas par collision : le Near-Far Interactor de XRI 3.5 ignore les colliders
// « trigger » (voir BackQuiver). Seuls l'arc et les flèches sont de vrais objets saisissables.

using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Gestion VR de la corde de l'arc.</summary>
    [DisallowMultipleComponent]
    public class BowString : MonoBehaviour
    {
        [SerializeField, Min(0.02f)] private float grabRadius = 0.15f;

        [Tooltip("Rayon (m) de la zone d'encochage autour de la corde (la flèche s'encoche dès qu'elle y entre).")]
        [SerializeField, Min(0.05f)] private float nockRadius = 0.25f;

        private Bow bow;
        private bool drawing;
        private Handedness stringHand;

        /// <summary>Vrai tant qu'une main tient la corde.</summary>
        public bool IsGrabbed => drawing;

        /// <summary>Main qui tient la corde (valide si <see cref="IsGrabbed"/>).</summary>
        public Handedness StringHand => stringHand;

        public float GrabRadius => grabRadius;

        private void Awake() => bow = GetComponentInParent<Bow>();

        private float NockRadius
        {
            get
            {
                DifficultyApplier applier = Services.Get<DifficultyApplier>();
                float mult = applier != null && applier.Current != null ? applier.Current.nockRadiusMultiplier : 1f;
                return nockRadius * mult;
            }
        }

        private void Update()
        {
            XRArcherInput input = Services.Get<XRArcherInput>();
            if (bow == null || input == null || !input.IsActive)
                return;

            if (!bow.IsHeld || bow.IsDesktop)
            {
                if (drawing)
                    EndDraw(release: false);
                return;
            }

            Handedness freeHand = XRArcherInput.Opposite(bow.BowHand);
            Transform handTransform = input.GetHand(freeHand);
            if (handTransform == null)
                return;

            if (drawing)
            {
                if (stringHand != freeHand)
                {
                    EndDraw(release: false);
                    return;
                }
                if (input.GripHeld(freeHand))
                    bow.DrawToWorldPoint(handTransform.position);
                else
                    EndDraw(release: true);
                return;
            }

            // Pas encore de corde saisie : cherche une flèche à encocher ou une corde à saisir.
            BackQuiver quiver = input.BackQuiver;
            Arrow held = quiver != null ? quiver.GetHeldArrow(freeHand) : null;
            float distance = Vector3.Distance(handTransform.position, bow.StringRestPosition);

            if (held != null && distance <= NockRadius && input.GripHeld(freeHand))
            {
                // Étape 1 : la flèche tenue entre dans la zone d'encochage.
                if (bow.TryNock(held))
                {
                    quiver.ClearHeld(freeHand, held);
                    BeginDraw(freeHand);
                }
                return;
            }

            if (held == null && distance <= grabRadius && input.GripPressedThisFrame(freeHand))
            {
                // Étape 2 : saisie de la corde main vide.
                if (!bow.HasArrow && IsAutoNockEnabled() && ArrowAllowance.CanTake)
                    SpawnAndNock();
                BeginDraw(freeHand);
            }
        }

        private void BeginDraw(Handedness hand)
        {
            drawing = true;
            stringHand = hand;
        }

        private void EndDraw(bool release)
        {
            drawing = false;
            if (release)
                bow.Release();
            else
                bow.CancelDraw();
        }

        private static bool IsAutoNockEnabled()
        {
            SettingsService settings = Services.Get<SettingsService>();
            return settings == null || settings.Data.autoNock;
        }

        private void SpawnAndNock()
        {
            EquipmentService equipment = Services.Get<EquipmentService>();
            ArrowPool pool = Services.Get<ArrowPool>();
            ArrowDefinition definition = equipment != null ? equipment.SelectedArrow : null;
            if (pool == null || definition == null)
                return;
            Arrow arrow = pool.Get(definition);
            if (arrow == null)
                return;
            if (!bow.TryNock(arrow))
                pool.Release(arrow);
        }

        private void OnDrawGizmosSelected()
        {
            Bow b = bow != null ? bow : GetComponentInParent<Bow>();
            if (b == null || b.StringRest == null)
                return;
            Gizmos.color = new Color(0.9f, 0.7f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(b.StringRest.position, grabRadius);
            Gizmos.color = new Color(0.2f, 0.8f, 0.9f, 0.3f);
            Gizmos.DrawWireSphere(b.StringRest.position, nockRadius);
        }
    }
}
