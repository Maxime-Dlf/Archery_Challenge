// TargetMover : mouvements des cibles (latéral, vertical, orbite, pop-up, parcours).
//
// OÙ LE POSER : sur la racine d'une cible (Target_Moving, Target_PopUp ou une instance d'arène).
// RÉGLAGES : voir §P5.4 du tutoriel. « Base Speed » est multipliée par la difficulté
// (DifficultyApplier → SpeedMultiplier).
//
// Tous les mouvements sont calculés dans le repère LOCAL du parent de la cible : si l'on tourne
// l'arène, les cibles mobiles suivent. La position de départ de l'objet sert de centre.

using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.Targets
{
    /// <summary>Type de mouvement d'une cible.</summary>
    public enum TargetPattern
    {
        None,
        Lateral,
        Vertical,
        Orbit,
        PopUp,
        Waypoints
    }

    /// <summary>Axe autour duquel une cible pop-up se rabat.</summary>
    public enum FoldAxis
    {
        X,
        Z
    }

    /// <summary>Déplace une cible selon un mouvement choisi.</summary>
    [DisallowMultipleComponent]
    public class TargetMover : MonoBehaviour
    {
        [SerializeField] private TargetPattern pattern = TargetPattern.Lateral;

        [Header("Latéral / Vertical")]
        [Tooltip("Distance parcourue de part et d'autre du centre (m).")]
        [SerializeField, Min(0f)] private float amplitude = 3f;
        [Tooltip("Vitesse de base en m/s (multipliée par la difficulté).")]
        [SerializeField, Min(0f)] private float baseSpeed = 1.5f;
        [Tooltip("Pause aux extrémités (s).")]
        [SerializeField, Min(0f)] private float pauseAtEnds = 0.3f;
        [Tooltip("Décalage de départ entre cibles, de 0 à 1.")]
        [SerializeField, Range(0f, 1f)] private float phaseOffset;

        [Header("Orbite")]
        [Tooltip("Rayon du cercle (m). La vitesse est alors en degrés par seconde.")]
        [SerializeField, Min(0f)] private float radius = 2f;

        [Header("Pop-up")]
        [SerializeField, Min(0.1f)] private float upTime = 2.5f;
        [SerializeField, Min(0.1f)] private float downTime = 1.5f;
        [SerializeField] private FoldAxis foldAxis = FoldAxis.X;

        [Header("Parcours")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private bool loop = true;

        private Vector3 originLocal;
        private Quaternion originRotation;
        private float travelled;      // position le long de la course, de -amplitude à +amplitude
        private int direction = 1;
        private float pauseLeft;
        private float angle;
        private float popTimer;
        private bool isUp = true;
        private int waypointIndex;
        private Collider[] colliders;
        private Vector3 lastPosition;
        private float currentSpeed;
        private bool frozen;

        /// <summary>Multiplicateur de vitesse imposé par la difficulté.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>Type de mouvement actuel.</summary>
        public TargetPattern Pattern
        {
            get => pattern;
            set { pattern = value; ResetMotion(); }
        }

        /// <summary>Vrai si la cible est en mouvement (bonus × 1,5). Les pop-up ne comptent pas comme mobiles.</summary>
        public bool IsMoving => pattern != TargetPattern.None && pattern != TargetPattern.PopUp && currentSpeed > 0.05f;

        /// <summary>Vrai si la cible est déployée (toujours vrai hors pop-up).</summary>
        public bool IsUp => pattern != TargetPattern.PopUp || isUp;

        public float Amplitude { get => amplitude; set => amplitude = value; }
        public float BaseSpeed { get => baseSpeed; set => baseSpeed = value; }
        public float PhaseOffset { get => phaseOffset; set => phaseOffset = value; }
        public float Radius { get => radius; set => radius = value; }

        /// <summary>Vrai pendant le rabattement d'une cible pop-up figée (cycle de la mission), jusqu'à ce qu'elle soit couchée.</summary>
        public bool IsFolding => pattern == TargetPattern.PopUp && frozen && popTimer < PopFoldTime;

        private const float PopFoldTime = 0.25f;

        /// <summary>
        /// Fige la cible (cycle de la mission : cible baissée). Une cible mobile s'arrête sur place ; une cible
        /// pop-up reste rabattue jusqu'à ce qu'on la libère.
        /// </summary>
        public void Freeze(bool freeze)
        {
            frozen = freeze;
            if (pattern == TargetPattern.PopUp)
            {
                isUp = !freeze;
                popTimer = 0f;
                ApplyPopState(!freeze);
            }
            if (freeze)
                currentSpeed = 0f;
        }

        private void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
        }

        private bool originCaptured;

        private void OnEnable()
        {
            if (!originCaptured)
            {
                originLocal = transform.localPosition;
                originRotation = transform.localRotation;
                originCaptured = true;
            }
            ResetMotion();
        }

        /// <summary>Replace la cible à son point de départ (le centre du mouvement est mémorisé une seule fois).</summary>
        public void ResetMotion()
        {
            if (!originCaptured)
            {
                originLocal = transform.localPosition;
                originRotation = transform.localRotation;
                originCaptured = true;
            }
            else
            {
                transform.localPosition = originLocal;
                transform.localRotation = originRotation;
            }
            travelled = Mathf.Lerp(-amplitude, amplitude, phaseOffset);
            direction = 1;
            pauseLeft = 0f;
            angle = phaseOffset * 360f;
            popTimer = 0f;
            isUp = true;
            frozen = false;
            waypointIndex = 0;
            lastPosition = transform.position;
            ApplyPopState(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (frozen)
            {
                // Cible baissée : un pop-up poursuit son animation de rabattement, les autres ne bougent plus.
                if (pattern == TargetPattern.PopUp)
                    StepPopUp(dt);
                currentSpeed = 0f;
                lastPosition = transform.position;
                return;
            }
            switch (pattern)
            {
                case TargetPattern.Lateral:
                    StepLine(dt, Vector3.right);
                    break;
                case TargetPattern.Vertical:
                    StepLine(dt, Vector3.up);
                    break;
                case TargetPattern.Orbit:
                    angle += baseSpeed * SpeedMultiplier * dt * 20f; // « Base Speed » (≈ 1,5) → 30°/s
                    float rad = angle * Mathf.Deg2Rad;
                    transform.localPosition = originLocal + new Vector3(Mathf.Cos(rad) * radius - radius, 0f, Mathf.Sin(rad) * radius);
                    break;
                case TargetPattern.PopUp:
                    StepPopUp(dt);
                    break;
                case TargetPattern.Waypoints:
                    StepWaypoints(dt);
                    break;
            }

            // Vitesse réelle (lissée) : sert à savoir si la cible est vraiment mobile.
            float v = dt > 0f ? (transform.position - lastPosition).magnitude / dt : 0f;
            currentSpeed = Mathf.Lerp(currentSpeed, v, 0.3f);
            lastPosition = transform.position;
        }

        private void StepLine(float dt, Vector3 axis)
        {
            if (pauseLeft > 0f)
            {
                pauseLeft -= dt;
                return;
            }

            travelled += direction * baseSpeed * SpeedMultiplier * dt;
            if (travelled >= amplitude)
            {
                travelled = amplitude;
                direction = -1;
                pauseLeft = pauseAtEnds;
            }
            else if (travelled <= -amplitude)
            {
                travelled = -amplitude;
                direction = 1;
                pauseLeft = pauseAtEnds;
            }
            transform.localPosition = originLocal + axis * travelled;
        }

        private void StepPopUp(float dt)
        {
            popTimer += dt;
            float duration = isUp ? upTime : downTime;
            if (!frozen && popTimer >= duration)
            {
                popTimer = 0f;
                isUp = !isUp;
                ApplyPopState(isUp);
            }

            // Animation de rabattement : la cible bascule de 0° (debout) à 90° (couchée) en 0,25 s.
            float foldTime = PopFoldTime;
            float t = isUp ? Mathf.Clamp01(popTimer / foldTime) : 1f - Mathf.Clamp01(popTimer / foldTime);
            float fold = Mathf.SmoothStep(90f, 0f, t);
            Vector3 axis = foldAxis == FoldAxis.X ? Vector3.right : Vector3.forward;
            transform.localRotation = originRotation * Quaternion.AngleAxis(-fold, axis);
        }

        private void StepWaypoints(float dt)
        {
            if (waypoints == null || waypoints.Count == 0)
                return;
            Transform target = waypoints[waypointIndex];
            if (target == null)
                return;

            Vector3 worldTarget = target.position;
            transform.position = Vector3.MoveTowards(transform.position, worldTarget, baseSpeed * SpeedMultiplier * dt);
            if ((transform.position - worldTarget).sqrMagnitude < 0.0004f)
            {
                waypointIndex++;
                if (waypointIndex >= waypoints.Count)
                    waypointIndex = loop ? 0 : waypoints.Count - 1;
            }
        }

        /// <summary>Force le rabattement d'une cible pop-up (quand elle est touchée).</summary>
        public void ForceDown()
        {
            if (pattern != TargetPattern.PopUp)
                return;
            isUp = false;
            popTimer = 0f;
            ApplyPopState(false);
        }

        // Les colliders d'une cible rabattue sont coupés : les flèches passent au travers.
        private void ApplyPopState(bool up)
        {
            if (colliders == null)
                return;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].enabled = up;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? transform.parent != null ? transform.parent.TransformPoint(originLocal) : originLocal : transform.position;
            Gizmos.color = Color.magenta;
            switch (pattern)
            {
                case TargetPattern.Lateral:
                    Vector3 side = (transform.parent != null ? transform.parent.right : Vector3.right) * amplitude;
                    Gizmos.DrawLine(center - side, center + side);
                    break;
                case TargetPattern.Vertical:
                    Gizmos.DrawLine(center - Vector3.up * amplitude, center + Vector3.up * amplitude);
                    break;
                case TargetPattern.Orbit:
                    Gizmos.DrawWireSphere(center, radius);
                    break;
            }
        }
#endif
    }
}
