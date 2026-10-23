// BodyFollower : un « corps » invisible qui suit la tête du joueur VR.
//
// OÙ LE POSER : sur l'objet « Body », enfant de « XR Origin (XR Rig) » (voir tutoriel §P3.8).
// RÉGLAGE : « Head » ← Camera Offset / Main Camera.
//
// Les enfants (carquois dorsal, étui de l'arc) se placent par rapport à ce corps. Il copie la
// POSITION de la tête mais seulement sa rotation HORIZONTALE (lacet) : si le joueur penche ou lève la
// tête, le carquois reste dans son dos au lieu de basculer avec le casque. La rotation est lissée :
// un petit mouvement de tête ne fait pas tourner le torse.

using UnityEngine;

namespace ArcheryChallenge.Player
{
    /// <summary>Suit la tête (position + lacet) pour porter le carquois et l'étui.</summary>
    [DisallowMultipleComponent]
    public class BodyFollower : MonoBehaviour
    {
        [SerializeField] private Transform head;

        [Tooltip("Temps de lissage de la rotation du torse (s).")]
        [SerializeField, Min(0f)] private float turnSmoothTime = 0.2f;

        private float yawVelocity;

        public Transform Head
        {
            get => head;
            set => head = value;
        }

        private void LateUpdate()
        {
            if (head == null)
            {
                if (Camera.main != null)
                    head = Camera.main.transform;
                else
                    return;
            }

            transform.position = head.position;

            // Lacet de la tête : direction horizontale du regard (insensible à l'inclinaison).
            Vector3 flat = head.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
                flat = head.up; // regard vertical : on utilise le haut de la tête
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f)
                return;

            float targetYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            float yaw = turnSmoothTime > 0f
                ? Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYaw, ref yawVelocity, turnSmoothTime)
                : targetYaw;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
