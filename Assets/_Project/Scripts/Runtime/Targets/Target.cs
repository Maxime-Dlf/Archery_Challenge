// Target : reçoit les impacts de flèche et calcule les points.
//
// OÙ LE POSER : sur la racine d'un prefab de cible (Target_Static, Target_Moving…).
// RÉGLAGES : « Face » ← l'objet enfant « Face » (avec TargetFace) ; « Hits To Break » = 0 pour une
// cible indestructible, ou N pour une cible qui tombe/se rabat après N impacts.
// COUCHE : Layer « Target » pour toute la cible.
//
// DÉROULEMENT D'UN IMPACT
// La flèche (Arrow) détecte l'impact par raycast puis appelle RegisterHit(). La cible demande à sa
// Face l'anneau touché, applique le score via ScoreService, affiche le chiffre flottant, puis annonce
// l'événement. Elle gère aussi sa destruction éventuelle (Hits To Break).

using System;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.Events;

namespace ArcheryChallenge.Targets
{
    /// <summary>Cible de tir à l'arc avec zones de score.</summary>
    [DisallowMultipleComponent]
    public class Target : MonoBehaviour, IArrowReceiver
    {
        [SerializeField] protected TargetFace face;

        [Tooltip("Nombre d'impacts avant que la cible tombe (0 = indestructible).")]
        [SerializeField, Min(0)] protected int hitsToBreak;

        [Tooltip("Appelé à chaque impact (pour des effets personnalisés).")]
        public UnityEvent onHit = new UnityEvent();

        private static readonly System.Collections.Generic.List<Target> Active = new System.Collections.Generic.List<Target>();

        /// <summary>Toutes les cibles actives de la scène (pour l'aimantation et les arènes).</summary>
        public static System.Collections.Generic.IReadOnlyList<Target> All => Active;

        private TargetMover mover;
        private int hitCount;
        private bool broken;
        private Vector3 baseScale;
        private Quaternion baseRotation;
        private bool baseScaleKnown;
        private Coroutine tilt;

        /// <summary>Annoncé quand une cible est touchée : (cible, résultat). Écouté par les arènes (objectifs).</summary>
        public static event Action<Target, ShotResult> Hit;

        /// <summary>Annoncé quand une cible est détruite/abattue.</summary>
        public static event Action<Target> Broken;

        /// <summary>Dernier tir évalué sur cette cible (lu par la flèche pour son événement d'impact).</summary>
        public ShotResult LastShot { get; private set; }

        /// <inheritdoc />
        public virtual ArrowHitOutcome OnArrowHit(Arrow arrow, Vector3 point, Vector3 direction, Collider collider)
        {
            if (IsLowered || (Mover != null && !Mover.IsUp))
                return ArrowHitOutcome.PassThrough;
            LastShot = RegisterHit(point, direction, arrow.ShooterPosition);
            return ArrowHitOutcome.Stick;
        }

        public TargetFace Face
        {
            get => face;
            set => face = value;
        }

        public int HitsToBreak
        {
            get => hitsToBreak;
            set => hitsToBreak = value;
        }

        /// <summary>Vrai si la cible a été abattue (Hits To Break atteint).</summary>
        public bool IsBroken => broken;

        /// <summary>
        /// Vrai tant que la cible est baissée (cycle de la mission) : elle est inerte, les flèches la traversent
        /// et elle ne rapporte aucun point jusqu'à ce que toutes les cibles aient été touchées.
        /// </summary>
        public bool IsLowered { get; private set; }

        /// <summary>Vrai tant que la cible joue son animation de chute ou de relevage (bascule ou rabattement d'une cible pop-up).</summary>
        public bool IsAnimating => tilt != null || (Mover != null && Mover.IsFolding);

        /// <summary>Vrai si la cible est actuellement en mouvement.</summary>
        public bool IsMoving => Mover != null && Mover.IsMoving;

        public TargetMover Mover
        {
            get
            {
                if (mover == null)
                    mover = GetComponent<TargetMover>();
                return mover;
            }
        }

        protected virtual void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        protected virtual void OnDisable() => Active.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            Active.Clear();
            Hit = null;
            Broken = null;
        }

        protected virtual void Awake()
        {
            if (face == null)
                face = GetComponentInChildren<TargetFace>();
            RememberScale();
        }

        private void RememberScale()
        {
            if (baseScaleKnown) return;
            baseScale = transform.localScale;
            baseRotation = transform.localRotation;
            baseScaleKnown = true;
        }

        /// <summary>Applique la taille imposée par la difficulté (multiple de la taille de base du prefab).</summary>
        public void SetSizeMultiplier(float multiplier)
        {
            RememberScale();
            transform.localScale = baseScale * multiplier;
        }

        /// <summary>
        /// Enregistre un impact de flèche et renvoie le résultat.
        /// </summary>
        /// <param name="point">Point d'impact (monde).</param>
        /// <param name="direction">Direction de vol de la flèche.</param>
        /// <param name="shooterPosition">Position du tireur (pour la distance de tir).</param>
        public virtual ShotResult RegisterHit(Vector3 point, Vector3 direction, Vector3 shooterPosition)
        {
            ShotResult shot = face != null
                ? face.Evaluate(point, direction, out _)
                : new ShotResult { WorldPoint = point };

            shot.Target = this;
            shot.TargetWasMoving = IsMoving;
            shot.Distance = Vector3.Distance(shooterPosition, point);
            ApplyShot(shot);
            return shot;
        }

        /// <summary>Marque la cible comme touchée : score, popup, événements, destruction éventuelle.</summary>
        protected void ApplyShot(ShotResult shot)
        {
            ScoreService score = Services.Get<ScoreService>();
            int added = 0;
            if (score != null && score.AcceptShots)
                added = score.RegisterShot(shot);

            SpawnPopup(shot, added);

            onHit.Invoke();
            Hit?.Invoke(this, shot);

            hitCount++;
            if (hitsToBreak > 0 && hitCount >= hitsToBreak)
                Break();
        }

        private void SpawnPopup(ShotResult shot, int added)
        {
            string text;
            Color color;
            if (shot.IsPenalty)
            {
                text = (-Mathf.Abs(shot.Points)).ToString();
                color = new Color(1f, 0.25f, 0.2f);
            }
            else if (shot.IsX)
            {
                text = "X";
                color = new Color(1f, 0.85f, 0.2f);
            }
            else if (shot.Points <= 0)
            {
                text = "0";
                color = new Color(0.85f, 0.85f, 0.85f);
            }
            else
            {
                text = added > shot.Points ? "+" + added : shot.Points.ToString();
                color = shot.Points >= 9 ? new Color(0.5f, 1f, 0.5f) : Color.white;
            }
            ScorePopup.Spawn(shot.WorldPoint + Vector3.up * 0.3f, text, color);
        }

        /// <summary>Abat la cible (elle se rabat si pop-up, sinon bascule puis disparaît).</summary>
        public void Break()
        {
            if (broken)
                return;
            broken = true;

            if (Mover != null && Mover.Pattern == TargetPattern.PopUp)
            {
                Mover.ForceDown();
                // Une cible pop-up « cassée » se relève pour la manche suivante : on la réarme tout de suite.
                broken = false;
                hitCount = 0;
            }
            else
            {
                // Les flèches plantées restent attachées : on fait basculer la cible en arrière.
                StartCoroutine(FallRoutine());
            }
            Broken?.Invoke(this);
        }

        private System.Collections.IEnumerator FallRoutine()
        {
            Quaternion start = transform.rotation;
            Quaternion end = start * Quaternion.Euler(-80f, 0f, 0f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.4f;
                transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
        }

        /// <summary>
        /// Baisse la cible (cycle de la mission) : elle bascule en arrière, devient inerte (colliders coupés, le
        /// mouvement s'arrête) et reste ainsi jusqu'à <see cref="Raise"/>.
        /// </summary>
        public void Lower()
        {
            if (IsLowered || broken)
                return;
            IsLowered = true;
            SetCollidersEnabled(false);
            if (Mover != null)
                Mover.Freeze(true);
            RememberScale();
            // Une cible pop-up se rabat toute seule (TargetMover) ; les autres basculent d'un quart de tour vers l'arrière.
            if (Mover == null || Mover.Pattern != TargetPattern.PopUp)
            {
                if (tilt != null)
                    StopCoroutine(tilt);
                tilt = StartCoroutine(TiltRoutine(baseRotation * Quaternion.Euler(-88f, 0f, 0f), 0.35f));
            }
        }

        /// <summary>Relève la cible baissée : elle redevient active.</summary>
        public void Raise()
        {
            if (!IsLowered)
                return;
            IsLowered = false;
            SetCollidersEnabled(true);
            if (Mover != null)
                Mover.Freeze(false);
            if (Mover == null || Mover.Pattern != TargetPattern.PopUp)
            {
                if (tilt != null)
                    StopCoroutine(tilt);
                tilt = StartCoroutine(TiltRoutine(baseRotation, 0.45f));
            }
        }

        private System.Collections.IEnumerator TiltRoutine(Quaternion end, float duration)
        {
            Quaternion start = transform.localRotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.05f, duration);
                transform.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }
            transform.localRotation = end;
            tilt = null;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                c.enabled = enabled;
        }

        /// <summary>Remet la cible debout (nouvelle manche).</summary>
        public virtual void ResetTarget()
        {
            StopAllCoroutines();
            broken = false;
            hitCount = 0;
            IsLowered = false;
            if (Mover != null)
                Mover.Freeze(false);
            RememberScale();
            if (Mover == null || Mover.Pattern != TargetPattern.PopUp)
                transform.localRotation = baseRotation;
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                c.enabled = true;
            if (Mover != null)
                Mover.ResetMotion();
        }
    }
}
