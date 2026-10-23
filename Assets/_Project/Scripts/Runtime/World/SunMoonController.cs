// SunMoonController : soleil, lune, ambiance, brouillard et ciel selon l'heure.
//
// OÙ LE POSER : sur l'objet « SunMoon » de la scène World, avec deux enfants Directional Light
// (« Sun » : ombres dures ; « Moon » : sans ombres, teinte bleutée).
// RÉGLAGES (tutoriel §P9.3) : Sun ← Sun ; Moon ← Moon ; Profile ← TimeOfDay_Default ;
//   Sky Material ← M_Sky (Skybox/Procedural) ; Night Sky Material ← ciel étoilé facultatif.
//
// Le soleil se lève à l'est (+X), passe au sud et se couche à l'ouest (−X). La lune fait l'inverse.
// Une seule des deux lumières est allumée à la fois. Les couleurs viennent du TimeOfDayProfile.

using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArcheryChallenge.World
{
    /// <summary>Pilote les lumières du ciel.</summary>
    [DisallowMultipleComponent]
    public class SunMoonController : MonoBehaviour
    {
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [SerializeField] private TimeOfDayProfile profile;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Material nightSkyMaterial;

        [Tooltip("Inclinaison de la course du soleil vers le sud (degrés).")]
        [SerializeField, Range(0f, 45f)] private float tilt = 20f;

        [Tooltip("Intensité des reflets du ciel sur les surfaces (0 à 1). Plein reflet = sol et décors « plastique » ; 0,1 garde un léger éclat.")]
        [SerializeField, Range(0f, 1f)] private float reflectionIntensity = 0.12f;

        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

        public Light Sun => sun;
        public Light Moon => moon;

        private void OnEnable()
        {
            if (sun != null)
                RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Les reflets du ciel à pleine intensité donnent au sol un voile blanc brillant (aspect plastique).
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 140f;
            if (skyMaterial != null)
                RenderSettings.skybox = skyMaterial;
        }

        private void Update()
        {
            TimeOfDayService time = Services.Get<TimeOfDayService>();
            TimeOfDayProfile p = profile != null ? profile : time != null ? time.Profile : null;
            if (p == null || time == null)
                return;
            Apply(p, time.Hour);
        }

        /// <summary>Applique l'ambiance d'une heure donnée.</summary>
        public void Apply(TimeOfDayProfile p, float hour)
        {
            float t = Mathf.Repeat(hour, 24f) / 24f;

            // Position du soleil : θ = 0 au lever (6 h, à l'est), π/2 à midi, π au coucher (18 h, à l'ouest).
            float theta = (hour - 6f) / 12f * Mathf.PI;
            float tiltRad = tilt * Mathf.Deg2Rad;
            Vector3 sunPosition = new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), -Mathf.Sin(tiltRad) * Mathf.Sin(theta)).normalized;
            float sunElevation = sunPosition.y;

            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(-sunPosition);
                float intensity = Mathf.Max(0f, p.sunIntensity.Evaluate(t));
                sun.intensity = intensity;
                sun.color = p.sunColor.Evaluate(t);
                bool sunUp = intensity > 0.01f && sunElevation > -0.05f;
                sun.enabled = sunUp;
            }

            if (moon != null)
            {
                moon.transform.rotation = Quaternion.LookRotation(sunPosition); // lune à l'opposé du soleil
                float intensity = Mathf.Max(0f, p.moonIntensity.Evaluate(t));
                moon.intensity = intensity;
                moon.color = p.moonColor;
                bool sunOff = sun == null || !sun.enabled;
                moon.enabled = sunOff && intensity > 0.01f;
            }

            RenderSettings.ambientSkyColor = p.ambientSky.Evaluate(t);
            RenderSettings.ambientEquatorColor = p.ambientEquator.Evaluate(t);
            RenderSettings.ambientGroundColor = p.ambientGround.Evaluate(t);
            RenderSettings.fogColor = p.fogColor.Evaluate(t);

            // Ciel : exposition et teinte ; bascule sur le ciel étoilé en pleine nuit s'il existe.
            Material sky = skyMaterial;
            bool deepNight = hour < p.sunriseHour - 1f || hour > p.sunsetHour + 2f;
            if (nightSkyMaterial != null && deepNight)
                sky = nightSkyMaterial;
            if (sky != null && RenderSettings.skybox != sky)
                RenderSettings.skybox = sky;
            if (skyMaterial != null && sky == skyMaterial)
            {
                if (skyMaterial.HasProperty(ExposureId))
                    skyMaterial.SetFloat(ExposureId, p.skyExposure.Evaluate(t));
                if (skyMaterial.HasProperty(SkyTintId))
                    skyMaterial.SetColor(SkyTintId, p.skyTint.Evaluate(t));
            }
        }
    }
}
