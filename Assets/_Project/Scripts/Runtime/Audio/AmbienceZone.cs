// AmbienceZone : ambiance sonore en boucle (forêt, rivière, feu de camp, foule).
//
// OÙ LE POSER : sur un objet vide de la scène World ou d'une arène (§P14.6).
// RÉGLAGES : « Day Bank » et « Night Bank » = noms de banques (ForestDay / ForestNight, River…) ;
//   « Global » coché = entendue partout (fondu jour/nuit automatique) ; décoché = son 3D autour de l'objet.
//
// Le jour et la nuit se fondent doucement selon l'heure du TimeOfDayService. Le volume suit les
// curseurs « Ambiance » et « Général » du menu.

using ArcheryChallenge.Core;
using ArcheryChallenge.World;
using UnityEngine;

namespace ArcheryChallenge.Audio
{
    /// <summary>Source d'ambiance jour/nuit.</summary>
    [DisallowMultipleComponent]
    public class AmbienceZone : MonoBehaviour
    {
        [SerializeField] private string dayBank = "ForestDay";
        [SerializeField] private string nightBank = "ForestNight";
        [SerializeField] private bool isGlobal;

        private AudioSource daySource;
        private AudioSource nightSource;
        private float nightBlend;
        private bool created;

        public string DayBank
        {
            get => dayBank;
            set => dayBank = value;
        }

        public string NightBank
        {
            get => nightBank;
            set => nightBank = value;
        }

        public bool IsGlobal
        {
            get => isGlobal;
            set => isGlobal = value;
        }

        private void Update()
        {
            AudioService audio = Services.Get<AudioService>();
            if (audio == null)
                return;

            if (!created)
            {
                created = true;
                if (!string.IsNullOrEmpty(dayBank))
                    daySource = audio.CreateLoop(dayBank, transform);
                if (!string.IsNullOrEmpty(nightBank) && nightBank != dayBank)
                    nightSource = audio.CreateLoop(nightBank, transform);
                if (isGlobal)
                {
                    // Ambiance globale : source 2D, entendue identiquement partout.
                    if (daySource != null) daySource.spatialBlend = 0f;
                    if (nightSource != null) nightSource.spatialBlend = 0f;
                }
            }

            TimeOfDayService time = Services.Get<TimeOfDayService>();
            float targetNight = time != null && time.IsNight ? 1f : 0f;
            nightBlend = Mathf.MoveTowards(nightBlend, targetNight, Time.unscaledDeltaTime / 6f);

            ApplyVolume(daySource, audio, dayBank, nightSource != null ? 1f - nightBlend : 1f);
            ApplyVolume(nightSource, audio, nightBank, nightBlend);
        }

        private static void ApplyVolume(AudioSource source, AudioService audio, string key, float blend)
        {
            if (source == null)
                return;
            SoundBank bank = audio.GetBank(key);
            float baseVolume = bank != null ? bank.volume : 1f;
            source.volume = baseVolume * blend * audio.GetVolume(SoundCategory.Ambience);
        }
    }
}
