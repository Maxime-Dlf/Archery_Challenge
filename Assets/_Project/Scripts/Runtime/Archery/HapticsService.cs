// HapticsService : vibrations des manettes (VR).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Les vibrations ressenties : saisie de l'arc, prise d'une flèche, encochage, corde de plus en plus
// tendue (la vibration croît avec la tension), relâchement. Au clavier/souris, ce service ne fait rien.
// Le retour haptique passe par le composant HapticImpulsePlayer de XRI (un par manette).

using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ArcheryChallenge.Archery
{
    /// <summary>Envoie des impulsions de vibration aux manettes.</summary>
    [DisallowMultipleComponent]
    public class HapticsService : MonoBehaviour
    {
        [Tooltip("Active ou coupe toutes les vibrations.")]
        [SerializeField] private bool enabledInGame = true;

        [Tooltip("Intensité maximale pendant la tension de la corde (0..1).")]
        [SerializeField, Range(0f, 1f)] private float drawIntensity = 0.45f;

        private HapticImpulsePlayer left;
        private HapticImpulsePlayer right;
        private float lastDrawPulse;

        private void Awake() => Services.Register(this);

        private void OnEnable()
        {
            GameEvents.ArrowPicked += OnArrowPicked;
            GameEvents.ArrowNocked += OnArrowNocked;
            GameEvents.BowDrawing += OnBowDrawing;
            GameEvents.BowReleased += OnBowReleased;
            GameEvents.PlayerHurt += OnPlayerHurt;
        }

        private void OnDisable()
        {
            GameEvents.ArrowPicked -= OnArrowPicked;
            GameEvents.ArrowNocked -= OnArrowNocked;
            GameEvents.BowDrawing -= OnBowDrawing;
            GameEvents.BowReleased -= OnBowReleased;
            GameEvents.PlayerHurt -= OnPlayerHurt;
        }

        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Envoie une impulsion à une manette.</summary>
        /// <param name="hand">Manette visée.</param>
        /// <param name="amplitude">Intensité 0..1.</param>
        /// <param name="duration">Durée en secondes.</param>
        public void Pulse(Handedness hand, float amplitude, float duration)
        {
            if (!enabledInGame)
                return;
            HapticImpulsePlayer player = GetPlayer(hand);
            if (player != null)
                player.SendHapticImpulse(Mathf.Clamp01(amplitude), duration);
        }

        /// <summary>Impulsion sur les deux manettes.</summary>
        public void PulseBoth(float amplitude, float duration)
        {
            Pulse(Handedness.Left, amplitude, duration);
            Pulse(Handedness.Right, amplitude, duration);
        }

        private HapticImpulsePlayer GetPlayer(Handedness hand)
        {
            XRArcherInput rig = Services.Get<XRArcherInput>();
            if (rig == null || !rig.IsActive)
                return null;

            HapticImpulsePlayer cached = hand == Handedness.Left ? left : right;
            if (cached == null)
            {
                Transform controller = rig.GetHand(hand);
                if (controller != null)
                {
                    cached = controller.GetComponentInChildren<HapticImpulsePlayer>(true);
                    if (cached == null)
                        cached = controller.gameObject.AddComponent<HapticImpulsePlayer>();
                }
                if (hand == Handedness.Left) left = cached; else right = cached;
            }
            return cached;
        }

        // La main qui tire est celle qui tient la corde : on la retrouve via l'arc tenu.
        private static Handedness StringHand(Bow bow) =>
            bow != null ? XRArcherInput.Opposite(bow.BowHand) : Handedness.Right;

        private void OnArrowPicked(Vector3 position)
        {
            // La flèche vient d'être prise par la main qui est la plus proche du carquois.
            PulseBoth(0.25f, 0.06f);
        }

        private void OnArrowNocked(Vector3 position) => PulseBoth(0.35f, 0.08f);

        private void OnBowDrawing(Vector3 position, float tension)
        {
            if (Time.unscaledTime - lastDrawPulse < 0.05f)
                return;
            lastDrawPulse = Time.unscaledTime;
            Bow bow = Services.Get<XRArcherInput>()?.BowHolster?.CurrentBow;
            Pulse(StringHand(bow), tension * drawIntensity, 0.06f);
            // La main d'arc ressent une vibration plus légère.
            if (bow != null)
                Pulse(bow.BowHand, tension * drawIntensity * 0.4f, 0.06f);
        }

        private void OnBowReleased(Vector3 position, float tension, bool fired)
        {
            PulseBoth(Mathf.Lerp(0.3f, 1f, tension), fired ? 0.12f : 0.06f);
        }

        private void OnPlayerHurt(Vector3 from) => PulseBoth(1f, 0.25f);
    }
}
