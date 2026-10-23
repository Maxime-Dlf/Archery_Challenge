// DifficultyApplier : applique la difficulté choisie aux cibles et expose ses paramètres.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// La difficulté est choisie dans le menu et peut changer à tout moment (réglage enregistré dans
// settings.json). Ce service retrouve le profil correspondant dans le catalogue et fournit :
// - la taille et la vitesse des cibles (ApplyTo) ;
// - le temps et le nombre de flèches d'une manche (ScaleTime, ScaleArrows) ;
// - les aides au tir : trajectoire affichée, aimantation, zone d'encochage, traînée (via Current).
// Le changement de difficulté pendant une manche ne s'applique qu'à la manche suivante, pour ne pas
// modifier les règles d'un score en cours.

using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Service d'application de la difficulté.</summary>
    [DisallowMultipleComponent]
    public class DifficultyApplier : MonoBehaviour
    {
        private DifficultyProfile frozen;

        /// <summary>Difficulté choisie dans les réglages.</summary>
        public GameDifficulty Difficulty
        {
            get
            {
                SettingsService settings = Services.Get<SettingsService>();
                return settings != null ? settings.Difficulty : GameDifficulty.Normal;
            }
        }

        /// <summary>
        /// Profil actif : celui de la manche en cours s'il est gelé, sinon celui des réglages.
        /// </summary>
        public DifficultyProfile Current
        {
            get
            {
                if (frozen != null)
                    return frozen;
                GameCatalog catalog = Services.Get<GameCatalog>();
                return catalog != null ? catalog.GetDifficulty(Difficulty) : null;
            }
        }

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Fige le profil pour la durée d'une manche (le menu ne le modifie plus).</summary>
        public void Freeze()
        {
            if (frozen == null)
                frozen = CurrentFromSettings();
        }

        /// <summary>Libère le profil figé.</summary>
        public void Unfreeze() => frozen = null;

        private DifficultyProfile CurrentFromSettings()
        {
            GameCatalog catalog = Services.Get<GameCatalog>();
            return catalog != null ? catalog.GetDifficulty(Difficulty) : null;
        }

        /// <summary>Applique taille et vitesse de la difficulté à une cible.</summary>
        public void ApplyTo(Target target)
        {
            if (target == null)
                return;
            DifficultyProfile p = Current;
            float size = p != null ? p.targetSizeMultiplier : 1f;
            float speed = p != null ? p.targetSpeedMultiplier : 1f;
            target.SetSizeMultiplier(size);
            if (target.Mover != null)
                target.Mover.SpeedMultiplier = speed;
        }

        /// <summary>Remet taille et vitesse normales (entraînement).</summary>
        public void ResetTarget(Target target)
        {
            if (target == null)
                return;
            target.SetSizeMultiplier(1f);
            if (target.Mover != null)
                target.Mover.SpeedMultiplier = 1f;
        }

        public float ScaleTime(float seconds)
        {
            DifficultyProfile p = Current;
            return seconds * (p != null ? p.timeMultiplier : 1f);
        }

        public int ScaleArrows(int arrows)
        {
            DifficultyProfile p = Current;
            return Mathf.Max(1, Mathf.RoundToInt(arrows * (p != null ? p.arrowsMultiplier : 1f)));
        }

        public float StarMultiplier
        {
            get
            {
                DifficultyProfile p = Current;
                return p != null ? p.starThresholdMultiplier : 1f;
            }
        }
    }
}
