// BowHolster : étui d'épaule où l'arc équipé attend, et où il revient quand on le lâche.
//
// OÙ LE POSER : sur l'objet « BowHolster » (enfant de « Body », position -0.20, -0.30, -0.12).
// RÉGLAGES : « Return Delay » = 0,5 s ; « Spawn Selected Bow On Start » coché.
//
// FONCTIONNEMENT
// - Au démarrage (et à chaque changement d'équipement), l'arc choisi apparaît dans l'étui :
//   le joueur le saisit en amenant une main près de l'épaule et en serrant le Grip (interaction
//   XR Grab Interactable normale).
// - Si l'arc est lâché (ou tombe), il revient tout seul dans l'étui après « Return Delay ».
// - L'étui est sur l'épaule de la main d'arc : la DROITE par défaut (les flèches se prennent alors de la main gauche), la
//   gauche si l'option « Arc dans la main gauche » est cochée (menu, page Confort). Il est volontairement écarté et placé
//   devant le carquois dorsal (voir BackQuiver) : en cherchant l'arc, la main ne tombe plus sur les flèches.
// - Aide à la saisie : le Near-Far Interactor de XRI ne voit que le petit collider de l'arc. Si la main d'arc serre le Grip à
//   moins de « Grab Assist Radius » de l'arc, il est donc saisi d'office (comme le fait le rig simulé).

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcheryChallenge.Player
{
    /// <summary>Étui d'épaule de l'arc (VR).</summary>
    [DisallowMultipleComponent]
    public class BowHolster : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float returnDelay = 0.5f;
        [SerializeField] private bool spawnSelectedBowOnStart = true;

        [Tooltip("Position de l'étui par rapport au corps (tête) pour l'épaule DROITE ; X est inversé pour l'épaule gauche.")]
        [SerializeField] private Vector3 shoulderOffset = new Vector3(0.32f, -0.18f, 0.04f);

        [Tooltip("Distance (m) à laquelle un Grip de la main d'arc saisit l'arc.")]
        [SerializeField, Min(0.05f)] private float grabAssistRadius = 0.25f;

        private Bow current;
        private float releasedAt = -1f;
        private bool initialized;
        private XRInteractionManager manager;

        /// <summary>Arc actuellement présent (dans l'étui ou en main).</summary>
        public Bow CurrentBow => current;

        private void Awake()
        {
            initialized = true;
            ApplyHandedness();
        }

        private void OnEnable()
        {
            GameEvents.EquipmentChanged += OnEquipmentChanged;
            GameEvents.SettingsChanged += ApplyHandedness;
            if (spawnSelectedBowOnStart)
                StartCoroutine(SpawnNextFrame());
        }

        private void OnDisable()
        {
            GameEvents.EquipmentChanged -= OnEquipmentChanged;
            GameEvents.SettingsChanged -= ApplyHandedness;
            DestroyCurrent();
        }

        // On attend une image pour que les services (équipement, sauvegarde) soient prêts.
        private System.Collections.IEnumerator SpawnNextFrame()
        {
            yield return null;
            ApplyHandedness();
            SpawnSelectedBow();
        }

        private void OnEquipmentChanged()
        {
            // Si le joueur tient l'arc au moment du changement, on remplace à sa prochaine lâchée.
            if (current != null && current.IsHeld)
                return;
            SpawnSelectedBow();
        }

        /// <summary>Fait apparaître l'arc choisi dans l'étui (remplace l'ancien).</summary>
        public void SpawnSelectedBow()
        {
            EquipmentService equipment = Services.Get<EquipmentService>();
            BowDefinition definition = equipment != null ? equipment.SelectedBow : null;
            if (definition == null || definition.prefab == null)
                return;
            if (current != null && current.Definition == definition)
            {
                if (!current.IsHeld) ReturnToHolster();
                return;
            }

            DestroyCurrent();
            GameObject instance = Instantiate(definition.prefab, transform);
            instance.name = definition.prefab.name;
            current = instance.GetComponent<Bow>();
            if (current == null)
            {
                Debug.LogError("[BowHolster] Le prefab d'arc n'a pas de composant Bow.", this);
                Destroy(instance);
                return;
            }
            current.Definition = definition;
            ReturnToHolster();
        }

        private void DestroyCurrent()
        {
            if (current != null)
            {
                Destroy(current.gameObject);
                current = null;
            }
        }

        private void Update()
        {
            if (current == null)
                return;

            AssistGrab();

            if (current.IsHeld)
            {
                releasedAt = -1f;
                return;
            }

            // Arc hors de la main : compte à rebours avant le retour à l'étui.
            if (current.transform.parent != transform || (current.transform.localPosition.sqrMagnitude > 0.0001f))
            {
                if (releasedAt < 0f)
                    releasedAt = Time.time;
                if (Time.time - releasedAt >= returnDelay)
                    ReturnToHolster();
            }
        }

        /// <summary>Replace l'arc dans l'étui (position et rotation nulles, physique figée).</summary>
        public void ReturnToHolster()
        {
            if (current == null)
                return;
            releasedAt = -1f;
            current.CancelDraw();
            Rigidbody body = current.GetComponent<Rigidbody>();
            if (body != null)
            {
                // Unity refuse (avertissement) de modifier la vitesse d'un corps déjà cinématique.
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
            }
            current.transform.SetParent(transform, false);
            current.transform.localPosition = Vector3.zero;
            current.transform.localRotation = Quaternion.identity;
        }

        // L'étui suit la main d'arc choisie dans les réglages : épaule droite par défaut, gauche en miroir.
        private void ApplyHandedness()
        {
            if (!initialized)
                return;
            SettingsService settings = Services.Get<SettingsService>();
            Handedness side = settings != null ? settings.BowSide : Handedness.Right;
            float x = Mathf.Abs(shoulderOffset.x);
            transform.localPosition = new Vector3(side == Handedness.Right ? x : -x, shoulderOffset.y, shoulderOffset.z);
        }

        // Saisie par la distance : la main d'arc serre le Grip près de l'arc (étui ou arc tombé) → l'arc est saisi.
        private void AssistGrab()
        {
            if (current.IsHeld || current.IsDesktop || current.GrabInteractable == null || !current.GrabInteractable.isActiveAndEnabled)
                return;
            XRArcherInput input = Services.Get<XRArcherInput>();
            SettingsService settings = Services.Get<SettingsService>();
            if (input == null || !input.IsActive || settings == null)
                return;

            Handedness side = settings.BowSide;
            if (!input.GripPressedThisFrame(side))
                return;
            Transform hand = input.GetHand(side);
            if (hand == null || Vector3.Distance(hand.position, current.transform.position) > grabAssistRadius)
                return;

            NearFarInteractor interactor = hand.GetComponentInChildren<NearFarInteractor>(true);
            if (manager == null)
                manager = FindAnyObjectByType<XRInteractionManager>();
            // Une main qui tient déjà quelque chose (une flèche) ne saisit pas l'arc.
            if (manager == null || interactor == null || interactor.hasSelection
                || !manager.IsRegistered((IXRInteractor)interactor) || !manager.IsRegistered((IXRInteractable)current.GrabInteractable))
                return;
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)current.GrabInteractable);
        }
    }
}
