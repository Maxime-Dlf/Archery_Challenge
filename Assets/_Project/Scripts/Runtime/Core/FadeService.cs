// FadeService : fondu au noir de l'écran, en VR comme au clavier/souris.
//
// OÙ LE POSER : sur l'objet « Systems » de la scène Boot (un seul exemplaire dans le jeu).
// RÉGLAGES : « Fade Material » doit recevoir le matériau M_ScreenFade
//            (Assets/_Project/Art/Materials/), qui utilise le shader ArcheryChallenge/ScreenFade.
//
// PRINCIPE
// Un petit carré (quad) noir est placé quelques centimètres devant la caméra active et
// devient son enfant : il suit donc la tête en VR et la caméra en mode PC. Son shader le
// dessine par-dessus toute la scène ; on fait varier son alpha de 0 (invisible) à 1 (noir).
// Une seule technique sert ainsi aux deux modes de jeu, ce qui évite d'avoir un fondu
// différent pour le casque (où les interfaces « plein écran » ne fonctionnent pas) et pour le PC.
//
// La caméra suivie est retrouvée automatiquement (Camera.main) : quand le joueur passe du
// mode VR au mode PC, le quad change de caméra tout seul à l'image suivante.

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcheryChallenge.Core
{
    /// <summary>
    /// Fondu au noir (ou vers toute autre couleur) devant la caméra active.
    /// Utilisé pour les chargements de scènes, les téléportations, la réapparition du joueur,
    /// et en version partielle (voile) quand la tête sort physiquement d'une plateforme.
    /// </summary>
    [DisallowMultipleComponent]
    public class FadeService : MonoBehaviour
    {
        [Tooltip("Matériau M_ScreenFade (shader ArcheryChallenge/ScreenFade).")]
        [SerializeField] private Material fadeMaterial;

        [Tooltip("Couleur du fondu (noir par défaut).")]
        [SerializeField] private Color fadeColor = Color.black;

        [Tooltip("Durée par défaut d'un fondu, en secondes.")]
        [SerializeField, Min(0f)] private float defaultDuration = 0.35f;

        [Tooltip("Le jeu démarre écran noir : la scène est révélée par un FadeIn quand elle est prête.")]
        [SerializeField] private bool startOpaque = true;

        // Distance minimale entre la caméra et le quad. Elle doit rester supérieure au plan de
        // découpe proche de la caméra (near clip = 0,05 m), sinon le quad ne serait pas dessiné.
        private const float MinQuadDistance = 0.08f;

        // Le quad doit couvrir tout le champ de vision, même large (≈ 140° en VR).
        // À une distance d, une largeur de 6 × d couvre largement un angle de 2 × atan(3) ≈ 143°.
        private const float QuadSizePerDistance = 6f;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Camera targetCamera;
        private Transform quad;
        private MeshRenderer quadRenderer;
        private Material runtimeMaterial;
        private Coroutine running;
        private FadeOperation currentOperation;
        private float currentAlpha;

        /// <summary>Opacité actuelle du fondu (0 = invisible, 1 = écran couvert).</summary>
        public float CurrentAlpha => currentAlpha;

        /// <summary>Vrai pendant qu'un fondu est en cours.</summary>
        public bool IsFading => running != null;

        /// <summary>Durée utilisée quand aucune durée n'est précisée.</summary>
        public float DefaultDuration => defaultDuration;

        private void Awake()
        {
            Services.Register(this);
            CreateQuad();
            SetAlpha(startOpaque ? 1f : 0f);
        }

        private void LateUpdate()
        {
            // Si la caméra suivie a disparu ou a été désactivée (changement de mode VR/PC),
            // on s'accroche à la nouvelle caméra principale.
            if (targetCamera == null || !targetCamera.isActiveAndEnabled)
            {
                Camera main = Camera.main;
                if (main != null && main != targetCamera)
                    AttachTo(main);
            }
        }

        private void OnDestroy()
        {
            Services.Unregister(this);
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
            if (quad != null)
                Destroy(quad.gameObject);
        }

        /// <summary>
        /// Place le quad devant la caméra indiquée (appelé automatiquement ; utile si l'on veut
        /// forcer une caméra particulière).
        /// </summary>
        public void AttachTo(Camera cam)
        {
            if (cam == null || quad == null)
                return;

            targetCamera = cam;
            float distance = Mathf.Max(MinQuadDistance, cam.nearClipPlane * 1.6f);
            quad.SetParent(cam.transform, false);
            quad.localPosition = new Vector3(0f, 0f, distance);
            quad.localRotation = Quaternion.identity;
            float size = distance * QuadSizePerDistance;
            quad.localScale = new Vector3(size, size, 1f);
            quad.gameObject.layer = cam.gameObject.layer;
        }

        /// <summary>
        /// Un fondu que l'on peut attendre avec <c>yield return</c>. Il se termine quand l'opacité visée est atteinte
        /// OU quand un autre fondu le remplace : ainsi, une séquence (téléportation, mort) ne reste jamais bloquée à
        /// attendre un fondu interrompu, ce qui laissait auparavant l'écran sombre pour toujours.
        /// </summary>
        public sealed class FadeOperation : CustomYieldInstruction
        {
            /// <summary>Vrai quand le fondu est terminé ou remplacé.</summary>
            public bool Done { get; internal set; }

            public override bool keepWaiting => !Done;
        }

        /// <summary>Assombrit l'écran jusqu'au noir complet.</summary>
        public FadeOperation FadeOut(float duration = -1f) => FadeTo(1f, duration);

        /// <summary>Révèle la scène (l'écran redevient visible).</summary>
        public FadeOperation FadeIn(float duration = -1f) => FadeTo(0f, duration);

        /// <summary>
        /// Fait varier l'opacité jusqu'à <paramref name="targetAlpha"/>. Un fondu en cours est
        /// interrompu (ceux qui l'attendaient sont libérés) et le nouveau repart de l'opacité actuelle.
        /// </summary>
        /// <param name="targetAlpha">Opacité visée, entre 0 et 1.</param>
        /// <param name="duration">Durée en secondes ; négative = <see cref="DefaultDuration"/>.</param>
        /// <returns>Un objet à attendre avec <c>yield return</c>.</returns>
        public FadeOperation FadeTo(float targetAlpha, float duration = -1f)
        {
            if (running != null)
                StopCoroutine(running);
            if (currentOperation != null)
                currentOperation.Done = true;
            if (duration < 0f)
                duration = defaultDuration;
            FadeOperation operation = new FadeOperation();
            currentOperation = operation;
            running = StartCoroutine(FadeRoutine(Mathf.Clamp01(targetAlpha), duration, operation));
            return operation;
        }

        /// <summary>Change l'opacité instantanément (interrompt un fondu en cours).</summary>
        public void SetAlphaImmediate(float alpha)
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
            if (currentOperation != null)
            {
                currentOperation.Done = true;
                currentOperation = null;
            }
            SetAlpha(Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// Enchaîne : fondu au noir → action (ex. déplacer le joueur, charger une scène) → retour.
        /// L'action peut être une coroutine : on attend qu'elle se termine avant de révéler la scène.
        /// </summary>
        public Coroutine FadeAround(Func<IEnumerator> actionWhileBlack, float duration = -1f)
        {
            return StartCoroutine(FadeAroundRoutine(actionWhileBlack, duration));
        }

        private IEnumerator FadeAroundRoutine(Func<IEnumerator> actionWhileBlack, float duration)
        {
            yield return FadeOut(duration);
            if (actionWhileBlack != null)
                yield return actionWhileBlack();
            yield return FadeIn(duration);
        }

        private IEnumerator FadeRoutine(float targetAlpha, float duration, FadeOperation operation)
        {
            float startAlpha = currentAlpha;
            float elapsed = 0f;

            // Temps « non mis à l'échelle » : le fondu fonctionne même quand le jeu est en pause
            // (Time.timeScale = 0), par exemple pour quitter une arène depuis le menu pause.
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                // SmoothStep adoucit le début et la fin : plus agréable qu'une variation linéaire.
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            SetAlpha(targetAlpha);
            running = null;
            operation.Done = true;
        }

        private void SetAlpha(float alpha)
        {
            currentAlpha = alpha;
            if (runtimeMaterial == null)
                return;

            Color c = fadeColor;
            c.a = alpha;
            runtimeMaterial.SetColor(ColorId, c);
            // On coupe le rendu quand le voile est invisible : aucun coût pour le GPU.
            quadRenderer.enabled = alpha > 0.001f;
        }

        private void CreateQuad()
        {
            if (fadeMaterial == null)
            {
                Debug.LogError("[FadeService] Le champ « Fade Material » est vide : assignez M_ScreenFade.", this);
                return;
            }

            // Copie du matériau : on modifie sa couleur à l'exécution sans toucher au fichier du projet.
            runtimeMaterial = new Material(fadeMaterial);

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "ScreenFadeQuad";
            // Le collider créé avec la primitive est inutile (et gênerait les flèches) : on le retire.
            Destroy(go.GetComponent<Collider>());

            quadRenderer = go.GetComponent<MeshRenderer>();
            quadRenderer.sharedMaterial = runtimeMaterial;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quadRenderer.lightProbeUsage = LightProbeUsage.Off;
            quadRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            quad = go.transform;
            // Le quad n'appartient à aucune scène chargée/déchargée : il survit aux changements de scène.
            DontDestroyOnLoad(go);

            Camera main = Camera.main;
            if (main != null)
                AttachTo(main);
        }
    }
}
