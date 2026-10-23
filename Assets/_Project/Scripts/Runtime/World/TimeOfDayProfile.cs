// TimeOfDayProfile : dégradés et courbes du cycle jour/nuit (ScriptableObject).
//
// OÙ LE CRÉER : Assets/_Project/Data/TimeOfDay/ → Create → Archery Challenge → Time Of Day Profile.
// Toutes les courbes et dégradés sont indexés sur 24 h : t = heure / 24 (0 = minuit, 0,5 = midi).
// Les modifications faites pendant le Play sont conservées : pratique pour régler l'ambiance.

using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Apparence du ciel et des lumières selon l'heure.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Time Of Day Profile", fileName = "TimeOfDay_New")]
    public class TimeOfDayProfile : ScriptableObject
    {
        [Header("Durée")]
        [Tooltip("Durée d'une journée complète en minutes réelles.")]
        [Min(0.1f)] public float dayLengthMinutes = 20f;

        [Tooltip("Heure de départ d'une nouvelle partie.")]
        [Range(0f, 24f)] public float startHour = 9f;

        [Header("Soleil et lune")]
        public Gradient sunColor = new Gradient();
        public AnimationCurve sunIntensity = AnimationCurve.Linear(0f, 0f, 1f, 0f);
        public Color moonColor = new Color(0.62f, 0.71f, 1f);
        public AnimationCurve moonIntensity = AnimationCurve.Linear(0f, 0.25f, 1f, 0.25f);

        [Tooltip("Heure du lever du soleil.")]
        [Range(0f, 24f)] public float sunriseHour = 6f;
        [Tooltip("Heure du coucher du soleil.")]
        [Range(0f, 24f)] public float sunsetHour = 19f;

        [Header("Ambiance")]
        public Gradient ambientSky = new Gradient();
        public Gradient ambientEquator = new Gradient();
        public Gradient ambientGround = new Gradient();
        public Gradient fogColor = new Gradient();

        [Header("Ciel")]
        public AnimationCurve skyExposure = AnimationCurve.Linear(0f, 0.3f, 1f, 0.3f);
        public Gradient skyTint = new Gradient();

        [Header("Torches")]
        [Tooltip("Heure d'allumage des torches.")]
        [Range(0f, 24f)] public float torchOnHour = 19f;
        [Tooltip("Heure d'extinction des torches.")]
        [Range(0f, 24f)] public float torchOffHour = 6.5f;

        /// <summary>Vrai si l'heure donnée est dans la période d'éclairage des torches (la nuit).</summary>
        public bool TorchesOn(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            return torchOnHour > torchOffHour
                ? (hour >= torchOnHour || hour < torchOffHour)
                : (hour >= torchOnHour && hour < torchOffHour);
        }

        /// <summary>Vrai s'il fait nuit (soleil couché).</summary>
        public bool IsNight(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            return hour < sunriseHour || hour >= sunsetHour;
        }
    }
}
