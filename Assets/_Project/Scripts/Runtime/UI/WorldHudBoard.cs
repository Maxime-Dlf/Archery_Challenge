// WorldHudBoard : place le HUD d'arène (score, temps, flèches, vent, PV) pour qu'il soit toujours dans le champ du joueur.
//
// OÙ LE POSER : sur le Canvas du prefab « HudPanel » (avec HudController). Le panneau est enfant d'un « HudAnchor »
// (tableau en bois « BoardFrame » + HUD) posé sur le côté de la plateforme de tir.
//
// POURQUOI : sur le côté de la plateforme, le tableau sort du champ dès que la vue se resserre (zoom de visée, fenêtre étroite,
// casque) : on n'en voyait qu'une partie. Désormais :
//   - Clavier/souris et rig VR simulé (F2) : l'incrustation DesktopHud affiche déjà tout (score, vent, PV…) collée aux coins
//     de l'écran, donc toujours entière : le tableau de côté (HUD et cadre en bois) est masqué ;
//   - Vrai casque : pendant la manche, le HUD quitte son tableau et suit le regard (en haut à gauche, à 1,5 m, avec un léger
//     retard) ; avant et après la manche il reprend sa place sur le tableau.

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>HUD d'arène toujours visible, selon le mode de jeu.</summary>
    [RequireComponent(typeof(Canvas))]
    [DisallowMultipleComponent]
    public class WorldHudBoard : MonoBehaviour
    {
        [Tooltip("Position du HUD par rapport à la tête en casque : x droite, y haut, z devant (m), dans le repère du regard à plat.")]
        [SerializeField] private Vector3 headsetOffset = new Vector3(-0.5f, 0.32f, 1.5f);
        [Tooltip("Échelle du support en casque (0,8 = HUD de 0,64 m de large).")]
        [SerializeField, Min(0.1f)] private float headsetScale = 0.8f;
        [SerializeField, Min(0f)] private float followSpeed = 6f;

        private Canvas canvas;
        private Transform anchor;
        private Transform frame;
        private LevelController level;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Vector3 homeScale;
        private bool following;
        private bool visible = true;
        private bool known;

        /// <summary>Test : fait comme si la manche était en cours (pour vérifier le suivi du regard).</summary>
        public bool ForceFollow { get; set; }

        /// <summary>Vrai tant que le HUD suit le regard (casque, manche en cours).</summary>
        public bool IsFollowing => following;

        /// <summary>Vrai tant que le HUD d'arène est affiché (faux avec l'incrustation de l'écran).</summary>
        public bool IsVisible => visible;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            anchor = transform.parent;
            frame = anchor != null ? anchor.Find("BoardFrame") : null;
            level = GetComponentInParent<LevelController>();
            if (anchor != null)
            {
                homePosition = anchor.localPosition;
                homeRotation = anchor.localRotation;
                homeScale = anchor.localScale;
            }
        }

        private void OnDisable() => StopFollowing();

        private void LateUpdate()
        {
            if (anchor == null)
                return;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            // L'incrustation DesktopHud remplace le tableau tant que le joueur n'a pas de casque.
            bool overlayInstead = mode != null && mode.UsesMouse;
            SetVisible(!overlayInstead);
            if (overlayInstead)
            {
                StopFollowing();
                return;
            }

            Transform head = mode != null ? mode.ActiveHead : null;
            bool playing = ForceFollow || (level != null && (level.State == LevelState.Countdown || level.State == LevelState.Running));
            if (playing && head != null)
                Follow(head);
            else
                StopFollowing();
        }

        private void SetVisible(bool value)
        {
            if (known && visible == value)
                return;
            known = true;
            visible = value;
            canvas.enabled = value;
            if (frame != null)
                frame.gameObject.SetActive(value && !following);
        }

        private void Follow(Transform head)
        {
            bool first = !following;
            following = true;
            if (frame != null)
                frame.gameObject.SetActive(false);

            Quaternion yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            Vector3 target = head.position + yaw * headsetOffset;
            float t = first ? 1f : 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            anchor.localScale = homeScale * (headsetScale / Mathf.Max(0.0001f, homeScale.x));
            anchor.position = Vector3.Lerp(anchor.position, target, t);
            Vector3 away = anchor.position - head.position;
            away.y = 0f;
            if (away.sqrMagnitude > 0.0001f)
                anchor.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private void StopFollowing()
        {
            if (!following)
                return;
            following = false;
            if (anchor != null)
            {
                anchor.localPosition = homePosition;
                anchor.localRotation = homeRotation;
                anchor.localScale = homeScale;
            }
            if (frame != null)
                frame.gameObject.SetActive(visible);
        }
    }
}
