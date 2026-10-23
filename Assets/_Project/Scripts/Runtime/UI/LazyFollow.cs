// LazyFollow : place un panneau comme un « écran flottant » devant le joueur, à la manière des fenêtres du Meta Quest.
//
// OÙ LE POSER : sur le Canvas (espace monde) d'un panneau ouvrable (pause, réglages, menus, résultat…).
// Il est ajouté tout seul par WorldCanvasCameraBinder si besoin ; le prefab UI_Global le porte déjà.
//
// - VR : à l'ouverture, le panneau se place à « Distance » mètres devant la tête, légèrement sous le regard,
//   face au joueur. Ensuite il ne se réajuste que si l'on tourne beaucoup la tête (confort : pas de panneau
//   collé au visage).
// - Clavier/souris (et rig VR simulé, F2) : le panneau se place devant la caméra à la distance qui lui fait occuper environ 60 % de la
//   hauteur (ou de la largeur) de l'écran ; il reste en place tant qu'il est ouvert (le curseur est libre).
// Tant que le panneau n'a pas été « posé » par SnapToHead(), ce composant ne bouge rien : les panneaux de décor
// (tableaux du hub, etc.) ne sont pas affectés.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>Placement d'un panneau en écran flottant devant le joueur.</summary>
    [DisallowMultipleComponent]
    public class LazyFollow : MonoBehaviour
    {
        /// <summary>Taille du panneau, en mètres, une fois posé (1000 × 0,001 m = 1 m de large).</summary>
        public const float WorldScale = 0.001f;

        [Tooltip("Distance devant la tête en VR (m).")]
        [SerializeField, Min(0.5f)] private float distance = 1.5f;
        [SerializeField] private float heightOffset = -0.05f;
        [SerializeField, Min(0f)] private float moveSpeed = 4f;
        [SerializeField, Min(0f)] private float angleThreshold = 25f;

        [Tooltip("Part de l'écran occupée par le panneau au clavier/souris (0,6 = 60 %).")]
        [SerializeField, Range(0.3f, 0.9f)] private float desktopScreenFill = 0.6f;

        private bool placed;
        private bool following;

        /// <summary>Vrai tant que le panneau est posé devant le joueur.</summary>
        public bool IsFloating => following;

        /// <summary>Distance utilisée en VR (m).</summary>
        public float Distance
        {
            get => distance;
            set => distance = Mathf.Max(0.5f, value);
        }

        /// <summary>Pose le panneau devant la tête (à l'ouverture) et active le suivi.</summary>
        public void SnapToHead()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (head == null)
                return;

            following = true;
            bool vr = mode.IsHeadset;
            Vector3 forward = head.forward;
            if (vr)
            {
                // En VR on garde le panneau à hauteur d'yeux, sans suivre l'inclinaison de la tête (confort).
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.01f)
                    forward = head.forward;
                forward.Normalize();
            }

            Camera cam = head.GetComponent<Camera>();
            float d = vr ? distance : FitDistance(cam);
            if (vr)
            {
                transform.position = head.position + forward * d + Vector3.up * heightOffset;
                transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
            }
            else
            {
                // Clavier/souris : le panneau est centré sur la PARTIE VISIBLE de l'écran (fenêtre Game zoomée ou décentrée), à la
                // profondeur calculée, et parallèle à l'écran. Sans recadrage, c'est exactement le centre de la vue.
                Vector3 direction = forward;
                if (cam != null)
                {
                    Vector2 center = ScreenViewport.Visible.center;
                    direction = cam.ViewportPointToRay(new Vector3(center.x, center.y, 0f)).direction;
                }
                float along = Mathf.Max(0.3f, Vector3.Dot(direction, head.forward));
                transform.position = head.position + direction * (d / along);
                transform.rotation = Quaternion.LookRotation(head.forward, Vector3.up);
            }
            placed = true;
        }

        /// <summary>Arrête le suivi (le panneau reste où il est ; le propriétaire le remet à sa place).</summary>
        public void Release()
        {
            following = false;
            placed = false;
        }

        /// <summary>
        /// Distance à laquelle le panneau occupe « desktopScreenFill » de l'écran, dans la dimension la plus
        /// contraignante (hauteur ou largeur selon le format de la fenêtre).
        /// </summary>
        public float FitDistance(Camera cam)
        {
            RectTransform rect = (RectTransform)transform;
            Vector3 scale = transform.lossyScale;
            float width = Mathf.Max(0.1f, rect.rect.width * Mathf.Abs(scale.x));
            float height = Mathf.Max(0.1f, rect.rect.height * Mathf.Abs(scale.y));
            if (cam == null)
                return 1f;

            // Si la fenêtre Game est zoomée, seule une partie du champ est visible : le panneau doit tenir dans cette partie.
            Rect visible = ScreenViewport.Visible;
            float halfV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Clamp(visible.height, 0.3f, 1f);
            float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.5f, cam.aspect) * Mathf.Clamp(visible.width, 0.3f, 1f);
            float byHeight = height / (2f * halfV * desktopScreenFill);
            float byWidth = width / (2f * halfH * desktopScreenFill);
            return Mathf.Clamp(Mathf.Max(byHeight, byWidth), 0.6f, 4f);
        }

        private void OnEnable() => placed = false;

        private void LateUpdate()
        {
            if (!following)
                return;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode == null || !mode.IsHeadset)
                return; // au clavier/souris, le panneau reste là où il a été posé

            Transform head = mode.ActiveHead;
            if (head == null)
                return;

            if (!placed)
            {
                SnapToHead();
                return;
            }

            Vector3 flat = head.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
                return;
            flat.Normalize();
            Vector3 targetPosition = head.position + flat * distance + Vector3.up * heightOffset;

            Vector3 toPanel = transform.position - head.position;
            toPanel.y = 0f;
            float angle = Vector3.Angle(flat, toPanel);
            if (angle > angleThreshold)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.unscaledDeltaTime * moveSpeed);
                Quaternion look = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.unscaledDeltaTime * moveSpeed);
            }
            else
            {
                Quaternion look = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.unscaledDeltaTime * 2f);
            }
        }
    }
}
