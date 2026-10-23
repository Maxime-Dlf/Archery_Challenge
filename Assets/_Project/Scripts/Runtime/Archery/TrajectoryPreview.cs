// TrajectoryPreview : ligne de trajectoire prévue (Facile et entraînement).
//
// OÙ LE POSER : sur l'objet enfant « Trajectory » d'un arc, avec un Line Renderer (Width 0,01,
// Use World Space coché, matériau M_Trajectory).
// RÉGLAGES : « Bow » ← la racine de l'arc ; « Max Points » = 40 ; « Time Step » = 0,05 s.
//
// La ligne est calculée avec la MÊME physique que la flèche (gravité, traînée) pour la tension
// actuelle : elle s'affiche seulement quand la corde est tendue avec une flèche encochée, et
// seulement si la difficulté l'autorise (« Show Trajectory ») ou si le mode entraînement la force.
// Le vent n'est volontairement pas inclus (c'est ce qui rend la difficulté plus haute plus réaliste).

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Aperçu de la trajectoire de la flèche.</summary>
    [RequireComponent(typeof(LineRenderer))]
    [DisallowMultipleComponent]
    public class TrajectoryPreview : MonoBehaviour
    {
        [SerializeField] private Bow bow;
        [SerializeField, Min(5)] private int maxPoints = 40;
        [SerializeField, Min(0.01f)] private float timeStep = 0.05f;

        private LineRenderer line;

        /// <summary>Quand vrai, la ligne est affichée quelle que soit la difficulté (entraînement).</summary>
        public static bool ForceShow { get; set; }

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 0;
            if (bow == null)
                bow = GetComponentInParent<Bow>();
        }

        private void OnDisable()
        {
            if (line != null)
                line.positionCount = 0;
        }

        private void LateUpdate()
        {
            if (bow == null || line == null)
                return;

            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            bool allowed = ForceShow || (applier != null && applier.Current != null && applier.Current.showTrajectory);
            Arrow arrow = bow.NockedArrow;
            if (!allowed || arrow == null || bow.Tension < 0.05f)
            {
                line.positionCount = 0;
                return;
            }

            Vector3 direction = bow.GetShotDirection();
            float speed = ArcherySpeed(bow, arrow);
            Vector3 position = arrow.Tip.position;
            Vector3 velocity = direction * speed;
            float drag = (arrow.Definition != null ? arrow.Definition.drag : 0.02f) * 0.1f
                         * (applier != null && applier.Current != null ? applier.Current.dragMultiplier : 1f);

            int count = 0;
            line.positionCount = maxPoints;
            line.SetPosition(count++, position);

            for (int i = 1; i < maxPoints; i++)
            {
                velocity += Physics.gravity * timeStep;
                velocity -= velocity * (drag * velocity.magnitude * timeStep);
                Vector3 next = position + velocity * timeStep;
                if (Physics.Linecast(position, next, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<Bow>() == null && hit.collider.GetComponentInParent<Arrow>() == null
                    && hit.collider.gameObject.layer != LayerMask.NameToLayer("Player"))
                {
                    line.SetPosition(count++, hit.point);
                    break;
                }
                position = next;
                line.SetPosition(count++, position);
            }
            line.positionCount = count;
        }

        /// <summary>Vitesse de départ prévue pour la tension actuelle (même formule que Bow.Release).</summary>
        public static float ArcherySpeed(Bow bow, Arrow arrow)
        {
            BowDefinition def = bow.Definition;
            float speed = def != null ? def.maxSpeed : 30f;
            speed *= def != null ? def.EvaluatePower(bow.Tension) : bow.Tension;
            speed *= arrow != null && arrow.Definition != null ? arrow.Definition.speedMultiplier : 1f;
            return speed;
        }
    }
}
