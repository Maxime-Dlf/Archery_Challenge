// ScoreService : score, combo et précision de la manche en cours.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// RÈGLES (§P5.1 du tutoriel)
// - Points d'une flèche = anneau touché (10 au centre … 1 au bord) ; le pied ou le cadre = 0.
// - Cible mobile : points × 1,5 (arrondi à l'entier le plus proche).
// - Combo sans plafond : chaque flèche consécutive à 9 ou plus allonge la série. À partir de la
//   2e flèche de la série, on ajoute un bonus +1, puis +2, puis +3… Un tir sous 9 remet la série à zéro.
// - Cible pénalisante : −15 points et série remise à zéro.
// - Le score ne descend jamais sous 0.
// - Le « X » (cercle central) compte 10 points et est comptabilisé à part pour départager.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Calcul du score d'une manche.</summary>
    [DisallowMultipleComponent]
    public class ScoreService : MonoBehaviour
    {
        /// <summary>Multiplicateur appliqué aux cibles mobiles.</summary>
        public const float MovingMultiplier = 1.5f;

        /// <summary>Anneau minimal pour entretenir le combo.</summary>
        public const int ComboRing = 9;

        private int score;
        private int combo;
        private int bestCombo;
        private int arrowsShot;
        private int arrowsHit;
        private int xCount;
        private int lastShotPoints;
        private ShotResult lastShot;

        /// <summary>Faux = les impacts ne comptent pas (briefing, fin de manche). Vrai par défaut (bac à sable).</summary>
        public bool AcceptShots { get; set; } = true;

        public int Score => score;
        public int Combo => combo;
        public int BestCombo => bestCombo;
        public int ArrowsShot => arrowsShot;
        public int ArrowsHit => arrowsHit;
        public int XCount => xCount;
        public int LastShotPoints => lastShotPoints;
        public ShotResult LastShot => lastShot;

        /// <summary>Précision 0..1 : flèches ayant marqué au moins 1 point / flèches tirées.</summary>
        public float Accuracy => arrowsShot > 0 ? Mathf.Clamp01((float)arrowsHit / arrowsShot) : 0f;

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Remet tout à zéro (début de manche ou réinitialisation de l'entraînement).</summary>
        public void ResetRun()
        {
            score = 0; combo = 0; bestCombo = 0; arrowsShot = 0; arrowsHit = 0; xCount = 0;
            lastShotPoints = 0; lastShot = default;
            GameEvents.RaiseScoreChanged(score, combo);
        }

        /// <summary>Compte une flèche tirée (qu'elle touche ou non).</summary>
        public void RegisterArrowShot()
        {
            arrowsShot++;
        }

        /// <summary>
        /// Enregistre le résultat d'un tir sur une cible et renvoie les points ajoutés (peut être négatif).
        /// </summary>
        public int RegisterShot(ShotResult shot)
        {
            int added;
            if (shot.IsPenalty)
            {
                added = -Mathf.Abs(shot.Points);
                combo = 0;
            }
            else if (shot.Points <= 0)
            {
                added = 0;
                combo = 0;
            }
            else
            {
                float multiplier = shot.TargetWasMoving ? MovingMultiplier : 1f;
                added = Mathf.RoundToInt(shot.Points * multiplier);
                arrowsHit++;
                if (shot.IsX)
                    xCount++;

                if (shot.Ring >= ComboRing)
                {
                    combo++;
                    bestCombo = Mathf.Max(bestCombo, combo);
                    added += Mathf.Max(0, combo - 1);
                }
                else
                {
                    combo = 0;
                }
            }

            int before = score;
            score = Mathf.Max(0, score + added);
            lastShotPoints = score - before;
            lastShot = shot;

            GameEvents.RaiseShotScored(shot, lastShotPoints);
            GameEvents.RaiseScoreChanged(score, combo);
            return lastShotPoints;
        }

        /// <summary>Ajoute des points hors tir (bonus de temps).</summary>
        public void AddBonus(int points)
        {
            if (points == 0) return;
            score = Mathf.Max(0, score + points);
            GameEvents.RaiseScoreChanged(score, combo);
        }
    }
}
