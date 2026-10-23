// CampfireSeat : mise en scène du départ « assis devant le feu de camp ».
//
// OÙ LE POSER : sur le point d'apparition « PlayerSpawn_Camp » (à côté de Player Spawn Point).
// RÉGLAGE : « Seated View Offset » = -0,5 : au clavier/souris, la caméra démarre 50 cm plus bas, comme
// si le joueur était assis, et se relève dès qu'il bouge. En VR, la hauteur réelle du joueur est
// conservée (on ne peut pas le forcer à s'asseoir).
// BANC : le banc du camp (objet « Seat », frère de ce point d'apparition) est solide au clavier/souris. En VR il devient
// franchissable (Is Trigger) : le joueur n'est jamais pile au centre de sa zone de jeu, sa capsule (qui suit la tête) peut donc
// se retrouver dans le banc, et le Character Controller restait coincé (« le joystick ne fait rien au camp ») jusqu'à une
// téléportation.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Player
{
    /// <summary>Vue assise au départ (mode clavier/souris).</summary>
    [DisallowMultipleComponent]
    public class CampfireSeat : MonoBehaviour
    {
        [SerializeField] private float seatedViewOffset = -0.5f;

        public float SeatedViewOffset => seatedViewOffset;

        private void OnEnable()
        {
            StartCoroutine(ApplyWhenReady());
            StartCoroutine(TrackModeForBench());
        }

        private void OnDisable()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode != null)
                mode.ModeChanged -= OnModeChanged;
        }

        private System.Collections.IEnumerator TrackModeForBench()
        {
            PlayerModeService mode = null;
            for (int i = 0; i < 600 && mode == null; i++)
            {
                mode = Services.Get<PlayerModeService>();
                if (mode == null)
                    yield return null;
            }
            if (mode == null)
                yield break;
            mode.ModeChanged -= OnModeChanged;
            mode.ModeChanged += OnModeChanged;
            OnModeChanged(mode.Mode);
        }

        // Banc solide au clavier/souris, franchissable avec un casque (ou le rig VR simulé).
        private void OnModeChanged(PlayerMode mode)
        {
            Transform bench = transform.parent != null ? transform.parent.Find("Seat") : null;
            if (bench == null)
                return;
            bool passable = mode != PlayerMode.Desktop;
            foreach (Collider c in bench.GetComponentsInChildren<Collider>(true))
                c.isTrigger = passable;
        }

        private System.Collections.IEnumerator ApplyWhenReady()
        {
            // Le joueur est placé par GameBootstrap après le chargement : on attend un peu.
            for (int i = 0; i < 90; i++)
            {
                yield return null;
                PlayerModeService mode = Services.Get<PlayerModeService>();
                GameBootstrap bootstrap = Services.Get<GameBootstrap>();
                if (mode != null && mode.Desktop != null && bootstrap != null && bootstrap.IsReady)
                {
                    mode.Desktop.SetSeated(seatedViewOffset);
                    yield break;
                }
            }
        }
    }
}
