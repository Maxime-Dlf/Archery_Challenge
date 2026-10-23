// SoundBank : un événement sonore et ses variantes (ScriptableObject).
//
// OÙ LE CRÉER : Assets/_Project/Data/Audio/ → Create → Archery Challenge → Sound Bank.
// Une banque = un événement (BowDraw, ImpactTarget…) avec 1 à 3 variantes de clip : le jeu en
// choisit une au hasard à chaque lecture, avec une hauteur légèrement variable (plus naturel).

using UnityEngine;
using UnityEngine.Audio;

namespace ArcheryChallenge.Audio
{
    /// <summary>Famille de volume réglable dans le menu (Général s'applique à tout).</summary>
    public enum SoundCategory
    {
        Sfx,
        Ambience,
        Ui,
        Music
    }

    /// <summary>Banque de sons d'un événement.</summary>
    [CreateAssetMenu(menuName = "Archery Challenge/Sound Bank", fileName = "Sound_New")]
    public class SoundBank : ScriptableObject
    {
        [Tooltip("Variantes du son ; une est tirée au hasard.")]
        public AudioClip[] clips = new AudioClip[0];

        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitchMin = 0.95f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1.05f;

        [Tooltip("Famille de volume : les curseurs du menu (Effets, Ambiance, Interface, Musique) agissent dessus.")]
        public SoundCategory category = SoundCategory.Sfx;

        [Tooltip("Groupe du mixeur (facultatif : le service gère déjà les volumes par famille).")]
        public AudioMixerGroup mixerGroup;

        [Tooltip("Coché = son 3D spatialisé ; décoché = son 2D (interface, musique).")]
        public bool spatial = true;
        public float minDistance = 1f;
        public float maxDistance = 25f;
        public bool loop;

        /// <summary>Renvoie une variante au hasard, ou null si la banque est vide.</summary>
        public AudioClip PickClip()
        {
            if (clips == null || clips.Length == 0)
                return null;
            return clips[Random.Range(0, clips.Length)];
        }

        /// <summary>Hauteur aléatoire dans l'intervalle réglé.</summary>
        public float PickPitch() => Random.Range(Mathf.Min(pitchMin, pitchMax), Mathf.Max(pitchMin, pitchMax));
    }
}
