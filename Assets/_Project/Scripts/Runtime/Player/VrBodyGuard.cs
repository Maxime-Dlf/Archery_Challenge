// VrBodyGuard : empêche le CharacterController du rig VR de devenir inutilisable quand la tête n'est pas (encore) suivie.
//
// OÙ LE POSER : sur la racine « XR Origin (XR Rig) » de la scène Boot (à côté de XRArcherInput). Aucun réglage obligatoire :
// l'objet enfant « Locomotion » (fournisseurs de déplacement XRI) et le Character Controller sont retrouvés tout seuls.
//
// POURQUOI : le XR Body Transformer de XRI règle à chaque déplacement la hauteur de la capsule du Character Controller sur la hauteur
// de la caméra dans l'espace de l'origine. Tant que le casque n'est pas suivi (les premières secondes d'une session Meta Quest Link,
// ou si le suivi est perdu), cette hauteur vaut 0 : le Character Controller devient invalide (« CharacterController.Move called on
// inactive controller ») et le reste même une fois le casque suivi. Résultat constaté en casque : le joystick gauche restreint la
// vision (vignette) mais le joueur ne bouge pas et ne tombe pas non plus.
// SOLUTION : la locomotion est suspendue (objet « Locomotion » désactivé) tant que la hauteur de la tête est inférieure à
// « Min Head Height » ; quand elle redevient valide, le Character Controller est réinitialisé (désactivé puis réactivé) et la
// locomotion reprend. Après 45 s sans tête suivie (jeu assis, suivi « Device »), la locomotion reprend quand même (45 s : le chargement du jeu puis le suivi de la tête peuvent prendre du temps).

using Unity.XR.CoreUtils;
using UnityEngine;

namespace ArcheryChallenge.Player
{
    /// <summary>Suspend la locomotion XRI tant que la tête n'est pas suivie et répare le Character Controller à la reprise.</summary>
    [DisallowMultipleComponent]
    public class VrBodyGuard : MonoBehaviour
    {
        [Tooltip("Hauteur minimale de la caméra (m, dans l'espace de l'origine) pour considérer la tête comme suivie.")]
        [SerializeField, Range(0.1f, 1f)] private float minHeadHeight = 0.3f;

        // Attente maximale d'une tête suivie avant de laisser la locomotion reprendre quand même (constante : le chargement du jeu puis le
        // suivi du casque peuvent prendre plusieurs secondes ; une valeur sérialisée dans la scène resterait figée à l'ancienne valeur).
        private const float MaxWait = 45f;

        private XROrigin origin;
        private CharacterController body;
        private GameObject locomotion;
        private bool locomotionOn = true;
        private bool guarding = true;
        private float waitingSince;

        /// <summary>Vrai tant que la locomotion est autorisée.</summary>
        public bool LocomotionAllowed => locomotionOn;

        private void Awake()
        {
            origin = GetComponent<XROrigin>();
            body = GetComponent<CharacterController>();
            Transform found = FindDeep(transform, "Locomotion");
            locomotion = found != null ? found.gameObject : null;
        }

        private void OnEnable()
        {
            // Le rig vient d'être activé : la tête n'est pas encore connue.
            guarding = true;
            waitingSince = Time.unscaledTime;
            SetLocomotion(false);
        }

        private void OnDisable()
        {
            // À la désactivation du rig, l'objet enfant redevient actif pour la prochaine activation.
            if (locomotion != null)
                locomotion.SetActive(true);
            locomotionOn = true;
        }

        private void Update()
        {
            if (origin == null || origin.Camera == null || locomotion == null)
                return;

            if (!guarding)
                return;

            bool headKnown = origin.CameraInOriginSpaceHeight >= minHeadHeight;
            if (!locomotionOn)
            {
                if (headKnown)
                {
                    SetLocomotion(true);
                }
                else if (Time.unscaledTime - waitingSince > MaxWait)
                {
                    guarding = false;       // jeu assis / suivi « Device » : on ne surveille plus, la locomotion reprend
                    SetLocomotion(true);
                }
            }
            else if (!headKnown)
            {
                SetLocomotion(false);       // suivi perdu : la locomotion attend le retour de la tête
            }
        }

        private void SetLocomotion(bool on)
        {
            if (locomotion == null)
                return;
            if (on)
                ResetBody();
            else if (locomotionOn)
                waitingSince = Time.unscaledTime;
            if (on != locomotionOn)
                Debug.Log($"[VrBodyGuard] locomotion {(on ? "reprise" : "suspendue")} (hauteur de la tête {(origin != null ? origin.CameraInOriginSpaceHeight : -1f):0.00} m)");
            locomotionOn = on;
            locomotion.SetActive(on);
        }

        // Désactiver puis réactiver le Character Controller le recrée proprement (hauteur et centre sont ensuite réglés par XRI).
        private void ResetBody()
        {
            if (body == null || !body.enabled)
                return;
            body.enabled = false;
            body.enabled = true;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName)
                    return t;
            return null;
        }
    }
}
