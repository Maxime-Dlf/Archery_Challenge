// StartTarget : cible « DÉPART » : la toucher lance la manche (briefing → compte à rebours).
//
// OÙ LE POSER : sur la racine du prefab StartTarget, devant la plateforme de l'arène.
// COUCHE : Layer « Target ». Elle ne rapporte aucun point.
//
// Au clavier, la touche E déclenche la même chose (voir LevelController).

using System;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Cible qui démarre la manche quand une flèche la touche.</summary>
    public class StartTarget : Target
    {
        /// <summary>Annoncé quand la cible de départ est touchée.</summary>
        public static event Action<StartTarget> Triggered;

        /// <inheritdoc />
        public override ShotResult RegisterHit(Vector3 point, Vector3 direction, Vector3 shooterPosition)
        {
            // Pas de points ni de combo : on prévient seulement l'arène.
            ShotResult shot = new ShotResult { WorldPoint = point, Target = this };
            ScorePopup.Spawn(point + Vector3.up * 0.3f, "GO !", new Color(0.4f, 1f, 0.5f));
            onHit.Invoke();
            Triggered?.Invoke(this);
            return shot;
        }

        /// <summary>Déclenche le départ sans flèche (touche E au clavier).</summary>
        public void Trigger() => Triggered?.Invoke(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => Triggered = null;
    }
}
