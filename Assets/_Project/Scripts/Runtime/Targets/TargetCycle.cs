// TargetCycle : cycle « touchée → baissée → toutes touchées → toutes relevées » des cibles d'une mission.
//
// OÙ LE POSER : sur la racine d'une arène (objet qui porte LevelController), sauf l'entraînement (tir libre).
// RÉGLAGES : « Lower Delay » (délai avant que la cible touchée se baisse, pour voir l'impact), « Extra Raise Delay »
// (pause supplémentaire entre la fin de la chute de la dernière cible et le relevage : 0 = instantané).
//
// RÈGLE (toutes les difficultés)
// - Quand une cible est touchée pendant la manche, elle se baisse et devient INERTE : les flèches la traversent et
//   elle ne rapporte plus rien tant que TOUTES les cibles de la mission n'ont pas été touchées.
// - Dès que la dernière cible touchée a FINI son animation de chute, toutes se relèvent instantanément et le cycle recommence.
// - Les cibles pénalisantes (arène 5) et la cible DÉPART ne font pas partie du cycle. Les vagues de l'arène 5 : seules
//   les cibles actuellement présentes comptent.
// - Hors manche (briefing, résultat), toutes les cibles sont relevées.

using System.Collections;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Gère le cycle des cibles d'une mission.</summary>
    [DisallowMultipleComponent]
    public class TargetCycle : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lowerDelay = 0.25f;
        [SerializeField, Min(0f)] private float extraRaiseDelay = 0f;

        private LevelController level;
        private Target[] targets;
        private Coroutine raising;

        /// <summary>Nombre de cycles complets (toutes les cibles touchées) depuis le début de la manche.</summary>
        public int CompletedCycles { get; private set; }

        private void Awake()
        {
            level = GetComponent<LevelController>();
            targets = GetComponentsInChildren<Target>(true);
        }

        private void OnEnable()
        {
            Target.Hit += OnTargetHit;
            GameEvents.LevelStateChanged += OnLevelState;
        }

        private void OnDisable()
        {
            Target.Hit -= OnTargetHit;
            GameEvents.LevelStateChanged -= OnLevelState;
            raising = null;
        }

        // Une cible fait-elle partie du cycle ?
        private static bool InCycle(Target t) =>
            t != null && !(t is PenaltyTarget) && !(t is StartTarget) && t.gameObject.activeInHierarchy && !t.IsBroken;

        private bool IsMine(Target t)
        {
            foreach (Target mine in targets)
                if (mine == t)
                    return true;
            return false;
        }

        private void OnLevelState(Component source, LevelState state)
        {
            if (source != level)
                return;
            if (state == LevelState.Running)
                CompletedCycles = 0;
            else
                RaiseAll(); // hors manche, aucune cible ne reste baissée
        }

        private void OnTargetHit(Target target, ShotResult shot)
        {
            if (level == null || level.State != LevelState.Running || !IsMine(target) || !InCycle(target) || target.IsLowered)
                return;
            StartCoroutine(LowerAfterDelay(target));
        }

        private IEnumerator LowerAfterDelay(Target target)
        {
            if (lowerDelay > 0f)
                yield return new WaitForSeconds(lowerDelay);
            if (target == null || level.State != LevelState.Running)
                yield break;
            target.Lower();

            if (AllLowered() && raising == null)
                raising = StartCoroutine(RaiseWhenComplete());
        }

        private bool AllLowered()
        {
            bool any = false;
            foreach (Target t in targets)
            {
                if (!InCycle(t))
                    continue;
                any = true;
                if (!t.IsLowered)
                    return false;
            }
            return any;
        }

        private IEnumerator RaiseWhenComplete()
        {
            CompletedCycles++;
            GameEvents.RaiseToast("Toutes les cibles touchées : elles se relèvent !");
            // On attend seulement que la dernière cible ait fini de tomber, puis tout se relève d'un coup.
            yield return null;
            while (AnyLoweredAnimating())
                yield return null;
            if (extraRaiseDelay > 0f)
                yield return new WaitForSeconds(extraRaiseDelay);
            RaiseAll();
            raising = null;
        }

        private bool AnyLoweredAnimating()
        {
            foreach (Target t in targets)
                if (t != null && t.IsLowered && t.IsAnimating)
                    return true;
            return false;
        }

        /// <summary>Relève toutes les cibles baissées de la mission.</summary>
        public void RaiseAll()
        {
            if (targets == null)
                return;
            foreach (Target t in targets)
                if (t != null && t.IsLowered)
                    t.Raise();
        }
    }
}
