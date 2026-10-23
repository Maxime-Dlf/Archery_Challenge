// TargetFace : repère du centre du blason et calcul de l'anneau touché.
//
// OÙ LE POSER : sur l'objet enfant « Face » d'une cible. Son axe bleu (+Z) doit SORTIR du blason,
// vers le tireur ; son origine doit être au CENTRE du blason (voir tutoriel §P5.3).
// RÉGLAGE : « Definition » ← Data/Targets/Target_Standard.
//
// CALCUL
// Le point d'impact (monde) est converti dans le repère local de « Face ». Sa distance au centre,
// dans le plan du blason, est comparée aux rayons des anneaux. Comme les rayons sont exprimés dans
// le repère local, la taille imposée par la difficulté (échelle de la racine de la cible) agrandit
// ou rétrécit automatiquement les anneaux avec le modèle : rien à recalculer.
//
// Les cercles jaunes dessinés dans la vue Scene (gizmos) servent à calibrer les rayons du fichier
// de données sur la texture du blason.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Plan du blason : transforme un point d'impact en anneau et en points.</summary>
    [DisallowMultipleComponent]
    public class TargetFace : MonoBehaviour
    {
        [SerializeField] private TargetDefinition definition;

        /// <summary>Données des anneaux de ce blason.</summary>
        public TargetDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        /// <summary>Centre du blason dans le monde.</summary>
        public Vector3 Center => transform.position;

        /// <summary>Direction qui sort du blason (vers le tireur).</summary>
        public Vector3 Normal => transform.forward;

        /// <summary>
        /// Évalue un impact.
        /// </summary>
        /// <param name="worldPoint">Point d'impact dans le monde.</param>
        /// <param name="arrowDirection">Direction de vol de la flèche (pour refuser les tirs par l'arrière).</param>
        /// <param name="radius">Distance au centre, dans le repère local (m à l'échelle 1).</param>
        public ShotResult Evaluate(Vector3 worldPoint, Vector3 arrowDirection, out float radius)
        {
            ShotResult result = new ShotResult { WorldPoint = worldPoint };
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            radius = new Vector2(local.x, local.y).magnitude;

            // Une flèche qui traverse le blason par derrière ne marque rien : son vol va dans le
            // sens de la normale (de l'arrière vers l'avant).
            bool fromBehind = arrowDirection.sqrMagnitude > 0.0001f && Vector3.Dot(arrowDirection.normalized, Normal) > 0.35f;

            if (definition == null || fromBehind)
                return result;

            result.Ring = definition.RingForRadius(radius);
            result.IsX = result.Ring > 0 && definition.IsX(radius);
            result.Points = result.Ring; // 10 au centre … 1 au bord ; le « X » vaut aussi 10
            return result;
        }

#if UNITY_EDITOR
        // Anneaux dessinés en jaune dans la vue Scene quand l'objet est sélectionné.
        private void OnDrawGizmosSelected()
        {
            if (definition == null || definition.ringRadii == null)
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 0; i < definition.ringRadii.Length; i++)
            {
                Gizmos.color = i == 0 ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.9f, 0.1f, 0.9f);
                DrawCircle(definition.ringRadii[i]);
            }
            Gizmos.color = Color.red;
            DrawCircle(definition.xRadius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.3f);
        }

        private static void DrawCircle(float r)
        {
            const int segments = 48;
            Vector3 prev = new Vector3(r, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                Vector3 next = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
#endif
    }
}
