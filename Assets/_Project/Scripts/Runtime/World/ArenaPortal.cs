// ArenaPortal : charge la scène d'une arène quand le joueur s'en approche, la décharge quand il s'éloigne.
//
// OÙ LE POSER : sur un objet vide de la scène World placé à l'emplacement de la plateforme de l'arène
// (Portal_A1 … Portal_A5, Portal_Training), sous « --- ARENAS --- » (§P8-B.6).
// RÉGLAGES : « Scene Name » (Arena_01…), « Load Radius » = 40 m, « Unload Radius » = 60 m.
//
// Les arènes sont des scènes séparées (une par membre de l'équipe) chargées en additif : seule l'arène
// proche existe en mémoire, ce qui préserve les performances du Quest. L'écart entre les deux rayons
// (hystérésis) évite de charger/décharger en boucle quand le joueur reste à la limite.

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Chargement dynamique d'une arène.</summary>
    [DisallowMultipleComponent]
    public class ArenaPortal : MonoBehaviour
    {
        [SerializeField] private string sceneName = "Arena_01";
        [SerializeField] private LevelDefinition level;
        [SerializeField, Min(5f)] private float loadRadius = 40f;
        [SerializeField, Min(5f)] private float unloadRadius = 60f;

        private float nextCheck;

        public string SceneName
        {
            get => sceneName;
            set => sceneName = value;
        }

        public LevelDefinition Level
        {
            get => level;
            set => level = value;
        }

        public float LoadRadius => loadRadius;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.4f;

            SceneService scenes = Services.Get<SceneService>();
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (scenes == null || head == null || string.IsNullOrEmpty(sceneName))
                return;

            Vector3 delta = head.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            bool loaded = scenes.IsLoaded(sceneName);

            if (!loaded && distance <= loadRadius && !scenes.IsBusy(sceneName))
            {
                scenes.LoadAdditive(sceneName);
            }
            else if (loaded && distance > unloadRadius && !scenes.IsBusy(sceneName) && !IsInUse())
            {
                scenes.Unload(sceneName);
            }
        }

        // On ne décharge jamais une arène dont une manche est en cours.
        private bool IsInUse()
        {
            LevelController active = LevelController.Active;
            return active != null && active.gameObject.scene.name == sceneName;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, loadRadius);
            Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, unloadRadius);
        }
#endif
    }
}
