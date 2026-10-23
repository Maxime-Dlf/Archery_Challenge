// WindService : vent de la manche en cours (direction et force qui varient lentement).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// PRINCIPE (§P7.2 du tutoriel)
// - Au début de chaque manche, la direction est tirée au hasard et la force dans l'intervalle de la
//   difficulté (Facile : aucun vent ; Normal : 1,5 à 3,5 m/s ; Difficile : 3 à 6 m/s), multipliée
//   par le coefficient de l'arène (× 1,5 au Ravin, × 2 au Tournoi : jusqu'à 12 m/s). Chaque m/s de vent
//   accélère latéralement la flèche de 2 m/s² : à 30 m, 6 m/s déplacent l'impact d'environ 0,7 m, soit
//   plus que le rayon du blason (0,58 m) ; à 12 m/s et 40 m, la flèche dévie de plus de 2 m.
// - Pendant la manche, une variation lente (bruit de Perlin) fait osciller force (± 25 %) et
//   direction (± 20°) : le vent n'est jamais figé, mais reste lisible grâce à la bannière.
// - En entraînement, le joueur règle le vent à la main (aucun / faible / moyen / fort).

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>Service du vent horizontal appliqué aux flèches.</summary>
    [DisallowMultipleComponent]
    public class WindService : MonoBehaviour
    {
        // Vitesse du vent pour les 4 niveaux manuels de l'entraînement (aucun, faible, moyen, fort), en m/s.
        // Le niveau « fort » (9 m/s) dévie une flèche de plus de 1 m à 30 m : il faut viser dans le vent pour toucher.
        private static readonly float[] manualStrengths = { 0f, 2f, 5f, 9f };

        private float baseSpeed;
        private float baseAngle;
        private float seed;
        private bool manual;
        private int manualLevel;

        /// <summary>Vent actuel (vecteur horizontal, m/s).</summary>
        public Vector3 CurrentWind { get; private set; }

        /// <summary>Force actuelle en m/s.</summary>
        public float Speed => CurrentWind.magnitude;

        /// <summary>Niveau manuel (0 à 3) quand le vent est réglé à la main.</summary>
        public int ManualLevel => manualLevel;

        /// <summary>Vrai quand le vent est réglé à la main (entraînement).</summary>
        public bool IsManual => manual;

        private void Awake()
        {
            Services.Register(this);
            seed = Random.value * 100f;
        }

        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Tire un nouveau vent pour une manche.</summary>
        /// <param name="difficulty">Profil de difficulté (force min/max).</param>
        /// <param name="levelMultiplier">Coefficient de l'arène.</param>
        public void BeginRound(Levels.DifficultyProfile difficulty, float levelMultiplier)
        {
            manual = false;
            seed = Random.value * 100f;
            baseAngle = Random.Range(0f, 360f);
            float min = difficulty != null ? difficulty.windMin : 0f;
            float max = difficulty != null ? difficulty.windMax : 0f;
            baseSpeed = max <= 0f ? 0f : Random.Range(min, max) * Mathf.Max(0f, levelMultiplier);
            Recompute();
        }

        /// <summary>Supprime le vent (hors manche).</summary>
        public void Stop()
        {
            manual = false;
            baseSpeed = 0f;
            CurrentWind = Vector3.zero;
        }

        /// <summary>Règle le vent à la main (entraînement) : 0 = aucun … 3 = fort.</summary>
        public void SetManualLevel(int level)
        {
            manual = true;
            manualLevel = Mathf.Clamp(level, 0, manualStrengths.Length - 1);
            baseSpeed = manualStrengths[manualLevel];
            if (baseSpeed > 0f && CurrentWind.sqrMagnitude < 0.0001f)
                baseAngle = Random.Range(0f, 360f);
            Recompute();
        }

        private void Update() => Recompute();

        private void Recompute()
        {
            if (baseSpeed <= 0f)
            {
                CurrentWind = Vector3.zero;
                return;
            }

            float t = Time.time * 0.15f + seed;
            float speed = baseSpeed * (1f + (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 0.5f);
            float angle = baseAngle + (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 40f;
            float rad = angle * Mathf.Deg2Rad;
            CurrentWind = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * Mathf.Max(0f, speed);
        }
    }
}
