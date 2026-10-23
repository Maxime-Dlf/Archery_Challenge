// TimeOfDayService : l'heure du jour (cycle de 20 minutes réelles) et les torches de nuit.
//
// OÙ LE POSER : objet « Systems » de la scène Boot. RÉGLAGE : « Profile » ← TimeOfDay_Default.
//
// PRINCIPE (§P9.1 du tutoriel)
// - Une journée dure « Day Length Minutes » minutes réelles (20 par défaut). Le cycle continue pendant
//   les manches ; il peut être mis en pause ou figé à une heure dans le menu (réglages « Cycle jour/nuit »).
// - L'heure est enregistrée dans l'emplacement de sauvegarde.
// - Les lanternes situées à moins de « Light Radius » (100 m) du joueur s'allument la nuit ; pour préserver le Quest,
//   un plafond de lumières simultanées s'applique (les plus proches d'abord) et les autres gardent leur flamme visible
//   sans lumière.
// - Raccourcis : F3 / Maj+F3 = heure ± 1 h ; F4 = vitesse × 1 → × 10 → × 60.

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Horloge du jeu.</summary>
    [DisallowMultipleComponent]
    public class TimeOfDayService : MonoBehaviour
    {
        /// <summary>
        /// Nombre maximal de torches qui éclairent en même temps (garde-fou de performance) : 8 sur Quest, 32 sur PC. Les torches
        /// plus loin que « Light Radius » ne s'allument pas.
        /// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
        public const int MaxActiveLights = 8;
#else
        public const int MaxActiveLights = 32;
#endif

        [Tooltip("Rayon d'allumage des lanternes autour du joueur (m) : toute lanterne plus proche s'allume la nuit.")]
        [SerializeField, Min(1f)] private float lightRadius = 100f;

        [SerializeField] private TimeOfDayProfile profile;

        private float hour = 9f;
        private float lastRaisedHour = -10f;
        private float nextLightUpdate;
        private SlotData lastSlot;
        private float nextSlotWrite;
        private static readonly float[] SpeedSteps = { 1f, 10f, 60f };
        private int speedIndex;

        public TimeOfDayProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        /// <summary>Heure actuelle, de 0 à 24.</summary>
        public float Hour => hour;

        /// <summary>Multiplicateur de vitesse (tests de jour en accéléré).</summary>
        public float SpeedMultiplier => SpeedSteps[speedIndex];

        /// <summary>Quand vrai, l'heure n'avance plus.</summary>
        public bool Paused { get; set; }

        /// <summary>Vrai s'il fait nuit (soleil couché).</summary>
        public bool IsNight => profile != null && profile.IsNight(hour);

        private void Awake()
        {
            Services.Register(this);
            if (profile != null)
                hour = profile.startHour;
        }

        private void OnDestroy() => Services.Unregister(this);

        private void Update()
        {
            SettingsService settings = Services.Get<SettingsService>();
            SaveSlotService slots = Services.Get<SaveSlotService>();

            // Nouvel emplacement de sauvegarde : on reprend l'heure enregistrée.
            if (slots != null)
            {
                SlotData slot = slots.Current;
                if (slot != lastSlot)
                {
                    lastSlot = slot;
                    hour = Mathf.Repeat(slot.hourOfDay, 24f);
                }
                if (Time.unscaledTime >= nextSlotWrite)
                {
                    nextSlotWrite = Time.unscaledTime + 5f;
                    slot.hourOfDay = hour;
                }
            }

            float fixedHour = settings != null ? settings.Data.fixedHour : -1f;
            bool cycleActive = settings == null || settings.Data.dayNightCycleActive;

            if (fixedHour >= 0f)
            {
                hour = Mathf.Repeat(fixedHour, 24f);
            }
            else if (cycleActive && !Paused && profile != null)
            {
                float hoursPerSecond = 24f / (profile.dayLengthMinutes * 60f);
                hour = Mathf.Repeat(hour + Time.deltaTime * hoursPerSecond * SpeedMultiplier, 24f);
            }

            if (Mathf.Abs(Mathf.DeltaAngle(hour * 15f, lastRaisedHour * 15f)) > 0.3f)
            {
                lastRaisedHour = hour;
                GameEvents.RaiseHourChanged(hour);
            }

            if (Time.unscaledTime >= nextLightUpdate)
            {
                nextLightUpdate = Time.unscaledTime + 0.5f;
                UpdateNightLights();
            }
        }

        /// <summary>Fixe l'heure (0 à 24).</summary>
        public void SetHour(float newHour)
        {
            hour = Mathf.Repeat(newHour, 24f);
            lastRaisedHour = -10f;
            SettingsService settings = Services.Get<SettingsService>();
            if (settings != null && settings.Data.fixedHour >= 0f)
            {
                settings.Data.fixedHour = hour;
                settings.MarkChanged();
            }
        }

        /// <summary>Avance (ou recule) l'heure de <paramref name="hours"/> heures.</summary>
        public void AddHours(float hours) => SetHour(hour + hours);

        /// <summary>Passe à la vitesse suivante (× 1 → × 10 → × 60 → × 1).</summary>
        public void CycleSpeed() => speedIndex = (speedIndex + 1) % SpeedSteps.Length;

        /// <summary>Rayon (m) dans lequel les lanternes s'allument.</summary>
        public float LightRadius => lightRadius;

        // Allume les torches à moins de « lightRadius » du joueur (les plus proches d'abord, dans la limite du plafond).
        // Les autres ne gardent que leur flamme.
        private void UpdateNightLights()
        {
            IReadOnlyList<NightLight> lights = NightLight.All;
            if (lights.Count == 0 || profile == null)
                return;

            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            Vector3 origin = head != null ? head.position : Vector3.zero;
            bool night = profile.TorchesOn(hour);

            // Sélection des 4 plus proches parmi celles qui doivent être allumées.
            int active = 0;
            List<NightLight> candidates = TempList;
            candidates.Clear();
            for (int i = 0; i < lights.Count; i++)
            {
                NightLight light = lights[i];
                bool wants = light.AlwaysOn || night;
                light.SetDesiredOn(wants);
                if (wants)
                    candidates.Add(light);
                else
                    light.SetLightEnabled(false);
            }

            candidates.Sort((a, b) =>
                (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));
            for (int i = 0; i < candidates.Count; i++)
            {
                // Les feux « toujours allumés » (feu de camp) comptent mais ne sont jamais éteints par la limite.
                bool inRadius = (candidates[i].transform.position - origin).sqrMagnitude <= lightRadius * lightRadius;
                bool allowed = candidates[i].AlwaysOn || (inRadius && active < MaxActiveLights);
                candidates[i].SetLightEnabled(allowed);
                if (allowed)
                    active++;
            }
        }

        private static readonly List<NightLight> TempList = new List<NightLight>();
    }
}
