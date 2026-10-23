// PenaltyTarget : cible « à ne pas toucher » (bouclier rouge de l'arène 5).
//
// OÙ LE POSER : sur la racine du prefab Target_Penalty (à la place du composant Target dont il hérite).
// RÉGLAGES : « Penalty » = 15 points retirés ; « Definition » facultative (Target_Penalty).
//
// Tout impact sur le bouclier retire des points et remet le combo à zéro.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Cible qui fait perdre des points quand on la touche.</summary>
    public class PenaltyTarget : Target
    {
        [Tooltip("Points retirés à chaque impact.")]
        [SerializeField, Min(1)] private int penalty = 15;

        [SerializeField] private TargetDefinition definition;

        /// <summary>Points retirés par impact.</summary>
        public int Penalty
        {
            get => definition != null ? definition.penaltyPoints : penalty;
            set => penalty = value;
        }

        /// <inheritdoc />
        public override ShotResult RegisterHit(Vector3 point, Vector3 direction, Vector3 shooterPosition)
        {
            ShotResult shot = new ShotResult
            {
                WorldPoint = point,
                IsPenalty = true,
                Points = Penalty,
                Ring = 0,
                Target = this,
                TargetWasMoving = IsMoving,
                Distance = Vector3.Distance(shooterPosition, point)
            };
            ApplyShot(shot);
            return shot;
        }
    }
}
