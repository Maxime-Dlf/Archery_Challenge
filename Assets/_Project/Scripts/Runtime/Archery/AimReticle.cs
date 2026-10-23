// AimReticle : réticule de visée en réalité virtuelle (et avec le rig VR simulé).
//
// OÙ LE POSER : sur la racine d'un arc (à côté de Bow). Il est ajouté tout seul par Bow.Awake si l'arc n'en a pas : les prefabs d'arc
// existants n'ont pas besoin d'être régénérés. L'objet enfant « AimReticle » (SpriteRenderer) est créé par le script.
//
// RÔLE : au clavier/souris, le réticule est le point au centre de l'écran (DesktopHud). En VR il n'existait rien : le joueur visait « à l'œil »
// le long de la flèche. Ce réticule apparaît sur le point que la flèche viserait en ligne droite, dès qu'une flèche est encochée, QUELLE QUE
// SOIT LA DIFFICULTÉ (la ligne de trajectoire prévue, elle, reste réservée au mode Facile : voir TrajectoryPreview).
//
// FONCTIONNEMENT
// - Un rayon part de la pointe de la flèche encochée dans la direction de tir (Bow.GetShotDirection : il inclut le tremblement de la
//   visée, donc le réticule tremble comme au clavier/souris). Le réticule se pose sur le premier obstacle touché (cible, sol…), légèrement
//   décollé de la surface, ou à 60 m dans le vide.
// - Il se tourne vers la tête du joueur et garde la même taille APPARENTE (≈ 1,6° de diamètre) quelle que soit la distance.
// - Couleur : orange tant que la corde est trop peu tendue pour que la flèche parte (sous la tension minimale de la difficulté), blanc-vert
//   ensuite.
// - Invisible en mode clavier/souris (l'arc affiché à l'écran n'a pas de réticule 3D, DesktopHud dessine le sien).

using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Réticule 3D de visée pour la VR.</summary>
    [DisallowMultipleComponent]
    public class AimReticle : MonoBehaviour
    {
        /// <summary>Distance (m) du réticule quand le rayon ne touche rien.</summary>
        public const float MaxDistance = 60f;

        [Tooltip("Diamètre apparent du réticule, en degrés de champ de vision.")]
        [SerializeField, Range(0.5f, 4f)] private float apparentSizeDegrees = 1.6f;

        private static readonly Color TooWeak = new Color(1f, 0.62f, 0.2f, 0.9f);
        private static readonly Color Ready = new Color(0.75f, 1f, 0.75f, 0.95f);

        private static Sprite cachedSprite;
        private static int hitMask;

        private Bow bow;
        private Transform reticle;
        private SpriteRenderer sprite;

        /// <summary>Vrai tant que le réticule est affiché.</summary>
        public bool IsVisible => reticle != null && reticle.gameObject.activeSelf;

        /// <summary>Position monde actuelle du réticule (valide quand <see cref="IsVisible"/>).</summary>
        public Vector3 Position => reticle != null ? reticle.position : Vector3.zero;

        private void Awake()
        {
            bow = GetComponent<Bow>();
            GameObject go = new GameObject("AimReticle");
            go.transform.SetParent(transform, false);
            reticle = go.transform;
            sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = GetSprite();
            sprite.sortingOrder = 100;
            sprite.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sprite.receiveShadows = false;
            go.SetActive(false);
        }

        private void LateUpdate()
        {
            Transform head = Services.Get<IArcherInput>()?.Head;
            bool show = bow != null && bow.IsHeld && !bow.IsDesktop && bow.HasArrow && head != null;
            if (!show)
            {
                if (reticle.gameObject.activeSelf)
                    reticle.gameObject.SetActive(false);
                return;
            }

            Vector3 origin = bow.NockedArrow.Tip.position;
            Vector3 direction = bow.GetShotDirection();
            Vector3 point = origin + direction * MaxDistance;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, MaxDistance, HitMask, QueryTriggerInteraction.Ignore))
                point = hit.point + hit.normal * 0.02f;

            Vector3 toPoint = point - head.position;
            float distance = Mathf.Max(0.5f, toPoint.magnitude);
            float diameter = Mathf.Clamp(2f * distance * Mathf.Tan(apparentSizeDegrees * 0.5f * Mathf.Deg2Rad), 0.04f, 3f);

            reticle.SetPositionAndRotation(point, Quaternion.LookRotation(toPoint.normalized, Vector3.up));
            reticle.localScale = Vector3.one * diameter;
            sprite.color = bow.Tension >= bow.MinTension ? Ready : TooWeak;
            if (!reticle.gameObject.activeSelf)
                reticle.gameObject.SetActive(true);
        }

        // Tout est touchable SAUF le joueur, les arcs, les flèches et les zones techniques (comme pour la flèche elle-même).
        private static int HitMask
        {
            get
            {
                if (hitMask != 0)
                    return hitMask;
                int mask = ~0;
                foreach (string layerName in new[] { "Player", "Bow", "Arrow", "ArrowZone", "PlatformBounds", "Ignore Raycast", "UI", "Water" })
                {
                    int layer = LayerMask.NameToLayer(layerName);
                    if (layer >= 0)
                        mask &= ~(1 << layer);
                }
                hitMask = mask;
                return hitMask;
            }
        }

        // Sprite généré par code (anneau + point central + quatre repères), pour ne dépendre d'aucun fichier image.
        private static Sprite GetSprite()
        {
            if (cachedSprite != null)
                return cachedSprite;

            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "AimReticle",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            Color32[] pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;
                    alpha = Mathf.Max(alpha, Band(r, 0.80f, 0.045f));                       // anneau
                    alpha = Mathf.Max(alpha, 1f - Smooth(0.07f, 0.10f, r));                 // point central
                    bool onAxis = Mathf.Abs(dx) < 0.035f || Mathf.Abs(dy) < 0.035f;
                    if (onAxis && r > 0.58f && r < 0.80f)                                   // repères vers l'intérieur
                        alpha = Mathf.Max(alpha, 0.9f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cachedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            cachedSprite.name = "AimReticle";
            return cachedSprite;
        }

        // Bande douce centrée sur « center », d'épaisseur demi-largeur « width ».
        private static float Band(float r, float center, float width)
        {
            return 1f - Smooth(width * 0.6f, width * 1.4f, Mathf.Abs(r - center));
        }

        // Lissage de type GLSL « smoothstep » : 0 sous edge0, 1 au-dessus de edge1 (Mathf.SmoothStep, lui, interpole de from à to).
        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
