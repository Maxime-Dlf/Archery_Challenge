// AimAssist : aimantation légère de la visée vers la cible la plus proche (difficulté Facile).
//
// OÙ LE POSER : n'est pas un composant (classe statique appelée par l'arc au moment du tir).
//
// PRINCIPE (§P7.2 du tutoriel : « aimantation légère ≤ 2° »)
// Si une cible se trouve à moins de « maxAngle » degrés de la direction visée, la flèche est
// légèrement tournée vers son centre. Le calcul tient compte de la chute de la flèche : on vise un
// point un peu au-dessus du centre, ce qui compense la gravité sur la distance estimée. L'effet est
// un simple « coup de pouce » (60 % de la correction) : il ne remplace pas la visée.

using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Aimantation de visée.</summary>
    public static class AimAssist
    {
        private const float Strength = 0.6f;

        /// <summary>
        /// Renvoie la direction corrigée (ou la direction d'origine si aucune cible n'est assez proche).
        /// </summary>
        /// <param name="origin">Point de départ de la flèche.</param>
        /// <param name="direction">Direction visée (normalisée).</param>
        /// <param name="speed">Vitesse de départ estimée (m/s), pour compenser la chute.</param>
        /// <param name="maxAngleDegrees">Angle d'aimantation maximal (0 = désactivé).</param>
        public static Vector3 Apply(Vector3 origin, Vector3 direction, float speed, float maxAngleDegrees)
        {
            if (maxAngleDegrees <= 0.01f || speed < 1f)
                return direction;

            Vector3 best = direction;
            float bestAngle = maxAngleDegrees;
            var targets = Target.All;
            for (int i = 0; i < targets.Count; i++)
            {
                Target t = targets[i];
                if (t == null || t.Face == null || t.IsBroken || t.IsLowered || t is PenaltyTarget || t is StartTarget)
                    continue;
                if (t.Mover != null && !t.Mover.IsUp)
                    continue;

                Vector3 center = t.Face.Center;
                float distance = Vector3.Distance(origin, center);
                if (distance < 3f)
                    continue;

                // Chute estimée sur cette distance : 0,5 × g × t², avec t = distance / vitesse.
                float flightTime = distance / speed;
                float drop = 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime * flightTime;
                Vector3 aimPoint = center + Vector3.up * drop;
                Vector3 wanted = (aimPoint - origin).normalized;

                float angle = Vector3.Angle(direction, wanted);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = wanted;
                }
            }

            return best == direction ? direction : Vector3.Slerp(direction, best, Strength).normalized;
        }
    }
}
