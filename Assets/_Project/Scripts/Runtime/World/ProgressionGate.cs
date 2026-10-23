// ProgressionGate : barrière qui s'ouvre quand l'arène précédente est réussie.
//
// OÙ LE POSER : sur la racine du prefab ProgressionGate (§P8-B.5) :
//   ProgressionGate ─ PostLeft, PostRight, Hinge (pivot) └ Door (avec un Box Collider), Sign (texte TMP).
// RÉGLAGES : « Required Level » ← l'arène à réussir (Level_01 pour Gate_A2…) ; « Hinge » ← Hinge ;
//   « Open Angle » = 100° ; « Open Duration » = 1,5 s ; « Sign Text » ← Sign.
//
// La carte reste ouverte : on peut contourner une barrière à pied, mais une arène VERROUILLÉE refuse de
// démarrer (le panneau de briefing dit quelle arène réussir d'abord). La barrière est donc surtout un
// repère visuel de la progression.

using System.Collections;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using TMPro;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Barrière de progression qui pivote à l'ouverture.</summary>
    [DisallowMultipleComponent]
    public class ProgressionGate : MonoBehaviour
    {
        [SerializeField] private LevelDefinition requiredLevel;
        [SerializeField] private Transform hinge;
        [SerializeField, Range(10f, 170f)] private float openAngle = 100f;
        [SerializeField, Min(0.1f)] private float openDuration = 1.5f;
        [SerializeField] private TMP_Text signText;
        [SerializeField] private string lockedMessage = "Réussissez {0}";

        private bool isOpen;
        private Coroutine animation;
        private Quaternion closedRotation;
        private Collider[] doorColliders;

        public bool IsOpen => isOpen;

        public LevelDefinition RequiredLevel
        {
            get => requiredLevel;
            set => requiredLevel = value;
        }

        private void Awake()
        {
            if (hinge != null)
            {
                closedRotation = hinge.localRotation;
                doorColliders = hinge.GetComponentsInChildren<Collider>(true);
            }
        }

        private void OnEnable()
        {
            GameEvents.ProgressionChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => GameEvents.ProgressionChanged -= Refresh;

        /// <summary>Recalcule l'état (ouvert si l'arène exigée est réussie ou si le mode démo est actif).</summary>
        public void Refresh()
        {
            UnlockService unlocks = Services.Get<UnlockService>();
            bool shouldOpen = requiredLevel == null || (unlocks != null && (unlocks.DemoUnlockAll || unlocks.IsLevelPassed(requiredLevel.levelId)));

            if (signText != null)
            {
                string levelName = requiredLevel != null ? requiredLevel.displayName : "";
                if (requiredLevel != null && requiredLevel.starsToPass > 1)
                    levelName += $" avec {requiredLevel.starsToPass} étoiles";
                signText.text = shouldOpen ? "Passage ouvert" : string.Format(lockedMessage, levelName);
            }

            if (shouldOpen == isOpen)
                return;

            bool announce = isOpen != shouldOpen && gameObject.activeInHierarchy && Application.isPlaying;
            isOpen = shouldOpen;
            if (animation != null)
                StopCoroutine(animation);
            animation = StartCoroutine(Animate(shouldOpen));
            if (announce && shouldOpen)
            {
                GameEvents.RaiseBarrierOpened(requiredLevel != null ? requiredLevel.levelId : name);
                GameEvents.RaiseToast("Une barrière s'ouvre !");
            }
        }

        private IEnumerator Animate(bool open)
        {
            if (hinge == null)
                yield break;

            Quaternion from = hinge.localRotation;
            Quaternion to = open ? closedRotation * Quaternion.Euler(0f, -openAngle, 0f) : closedRotation;

            // Le collider de la porte est coupé dès le début de l'ouverture : le joueur ne reste pas coincé.
            if (open)
                SetDoorColliders(false);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / openDuration;
                hinge.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            hinge.localRotation = to;
            if (!open)
                SetDoorColliders(true);
            animation = null;
        }

        private void SetDoorColliders(bool enabled)
        {
            if (doorColliders == null)
                return;
            foreach (Collider c in doorColliders)
                if (c != null)
                    c.enabled = enabled;
        }
    }
}
