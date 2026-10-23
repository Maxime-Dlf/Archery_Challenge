// AimSway : tremblement aléatoire du réticule (et de la visée) pendant que la corde est tendue.
//
// OÙ LE POSER : objet « Systems » de la scène Boot (un seul exemplaire).
//
// FORMULE (amplitude du tremblement, en degrés, au sens « demi-débattement »)
//
//     A = A0 × D × (1 + kV × V) × (1 + T / Tref)        avec T limité à Tmax
//
//   A0   amplitude de base : 0,25° (≈ 13 cm à 30 m)
//   D    difficulté : « Aim Sway Multiplier » du DifficultyProfile (Facile 0,35 · Normal 1 · Difficile 1,6)
//   V    vitesse du vent en m/s ; kV = 0,06 : chaque m/s ajoute 6 % de tremblement
//   T    temps passé depuis l'arrivée à la tension MAXIMALE de la corde (fatigue) ; Tref = 2,5 s, Tmax = 10 s :
//        après 2,5 s de maintien l'amplitude double, après 10 s elle est multipliée par 5
//   Avant la tension maximale, l'amplitude est réduite proportionnellement à la tension (T = 0).
//
//   Exemples : Facile, sans vent, 4 s de maintien : 0,23° · Normal, vent 3 m/s, 4 s : 0,77° (≈ 40 cm à 30 m)
//              Difficile, vent 6 m/s, 4 s : 1,41° (≈ 75 cm à 30 m) ; 10 s : 2,7° (≈ 1,4 m).
//
// MOUVEMENT : bruit de Perlin sur deux axes (jamais deux fois le même geste) ; plus l'amplitude est forte, plus le
// tremblement est rapide (« plus violent »). Le résultat (Offset, en degrés : x = vers la droite, y = vers le haut)
// est lu par :
//   - DesktopBowView (clavier/souris) : la flèche part vers le réticule décalé ; DesktopHud déplace le réticule ;
//   - Bow.GetShotDirection (VR) : l'arc et la flèche encochée tremblent, le tir suit.
// Hors tension (corde au repos), l'offset est nul.

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Tremblement du réticule dépendant du vent, de la fatigue et de la difficulté.</summary>
    [DisallowMultipleComponent]
    public class AimSway : MonoBehaviour
    {
        [Header("Formule (voir l'en-tête du script)")]
        [Tooltip("Amplitude de base A0 (degrés).")]
        [SerializeField, Min(0f)] private float baseAmplitude = 0.25f;
        [Tooltip("kV : part de tremblement ajoutée par m/s de vent (0,06 = +6 % par m/s).")]
        [SerializeField, Min(0f)] private float windFactor = 0.06f;
        [Tooltip("Tref : temps de maintien à pleine tension qui double l'amplitude (s).")]
        [SerializeField, Min(0.1f)] private float fatigueSeconds = 2.5f;
        [Tooltip("Tmax : au-delà, la fatigue ne croît plus (s).")]
        [SerializeField, Min(0.1f)] private float fatigueMaxSeconds = 10f;
        [Tooltip("Tension (0..1) à partir de laquelle l'arc est considéré « à pleine tension ».")]
        [SerializeField, Range(0.5f, 1f)] private float fullDrawTension = 0.97f;

        private float tension;
        private float lastDrawEvent = -10f;
        private float fullDrawSince = -1f;
        private float seed;
        private float phase;

        /// <summary>Décalage actuel du réticule en degrés (x : vers la droite, y : vers le haut).</summary>
        public Vector2 Offset { get; private set; }

        /// <summary>Amplitude actuelle du tremblement (degrés).</summary>
        public float Amplitude { get; private set; }

        /// <summary>Temps (s) passé à pleine tension (0 si la corde n'est pas à son maximum).</summary>
        public float HoldTime { get; private set; }

        /// <summary>Vrai tant que la corde est tendue et que le réticule tremble.</summary>
        public bool IsActive => Amplitude > 0.0001f;

        private void Awake()
        {
            Services.Register(this);
            seed = Random.value * 100f;
        }

        private void OnDestroy() => Services.Unregister(this);

        private void OnEnable()
        {
            GameEvents.BowDrawing += OnBowDrawing;
            GameEvents.BowReleased += OnBowReleased;
        }

        private void OnDisable()
        {
            GameEvents.BowDrawing -= OnBowDrawing;
            GameEvents.BowReleased -= OnBowReleased;
        }

        private void OnBowDrawing(Vector3 position, float newTension)
        {
            tension = newTension;
            lastDrawEvent = Time.unscaledTime;
        }

        private void OnBowReleased(Vector3 position, float releaseTension, bool fired)
        {
            tension = 0f;
            fullDrawSince = -1f;
        }

        /// <summary>
        /// Amplitude (degrés) pour une situation donnée : c'est la formule du tremblement, publique pour les tests.
        /// </summary>
        /// <param name="difficulty">Multiplicateur de difficulté D.</param>
        /// <param name="windSpeed">Vitesse du vent V (m/s).</param>
        /// <param name="holdSeconds">Temps T passé à pleine tension (s).</param>
        /// <param name="drawTension">Tension de la corde (0..1).</param>
        public float ComputeAmplitude(float difficulty, float windSpeed, float holdSeconds, float drawTension = 1f)
        {
            float fatigue = 1f + Mathf.Min(Mathf.Max(0f, holdSeconds), fatigueMaxSeconds) / fatigueSeconds;
            float wind = 1f + windFactor * Mathf.Max(0f, windSpeed);
            return baseAmplitude * Mathf.Max(0f, difficulty) * wind * fatigue * Mathf.Clamp01(drawTension);
        }

        private void Update()
        {
            // Pas d'événement de tension récent : la corde est au repos.
            bool drawing = Time.unscaledTime - lastDrawEvent < 0.2f && tension > 0.05f;
            if (!drawing)
            {
                tension = 0f;
                fullDrawSince = -1f;
                HoldTime = 0f;
                Amplitude = 0f;
                Offset = Vector2.zero;
                return;
            }

            // Temps écoulé depuis l'arrivée à la tension maximale.
            if (tension >= fullDrawTension)
            {
                if (fullDrawSince < 0f)
                    fullDrawSince = Time.time;
                HoldTime = Time.time - fullDrawSince;
            }
            else
            {
                fullDrawSince = -1f;
                HoldTime = 0f;
            }

            DifficultyApplier applier = Services.Get<DifficultyApplier>();
            float difficulty = applier != null && applier.Current != null ? applier.Current.aimSwayMultiplier : 1f;
            WindService wind = Services.Get<WindService>();
            float windSpeed = wind != null ? wind.Speed : 0f;

            Amplitude = ComputeAmplitude(difficulty, windSpeed, HoldTime, tension);

            // Mouvement : deux bruits de Perlin indépendants (un par axe), plus rapides quand l'amplitude grandit.
            // La phase est CUMULÉE image par image : calculer « Time.time × vitesse » faisait sauter la phase à chaque variation de la
            // vitesse (tension, fatigue, vent), d'autant plus fort que la partie durait (Time.time grand) : le réticule scintillait.
            float speed = 0.9f + 0.35f * Amplitude;
            phase += speed * Time.deltaTime;
            float t = phase;
            float x = Noise(t, seed) * Amplitude;
            float y = Noise(t * 0.83f, seed + 17.3f) * Amplitude;
            Offset = new Vector2(x, y);
        }

        // Bruit centré dans [-1, 1], avec une seconde octave plus rapide pour un geste moins régulier.
        private static float Noise(float t, float s)
        {
            float a = Mathf.PerlinNoise(t, s) * 2f - 1f;
            float b = Mathf.PerlinNoise(t * 2.7f + 31f, s * 1.3f) * 2f - 1f;
            return Mathf.Clamp(a * 0.75f + b * 0.25f, -1f, 1f) * 1.4f;
        }
    }
}
