// DesktopPlayerController : déplacement à la première personne au clavier et à la souris.
//
// OÙ LE POSER : sur la racine « DesktopPlayer » (Character Controller : Height 1,8, Radius 0,3,
// Center 0, 0.9, 0, Step Offset 0,3, Slope Limit 45). Layer « Player ».
// RÉGLAGES (tutoriel §P4.4) : Walk Speed 3, Run Speed 6, Jump Height 1, Gravity -15,
// Mouse Sensitivity 0,12, Camera Pivot ← CameraPivot (enfant à 0, 1.7, 0 portant la caméra).
//
// COMMANDES : Z Q S D (AZERTY) ou W A S D (QWERTY), détectées par la POSITION des touches ;
// Maj court, Espace saute, la souris oriente la vue, Alt libère le curseur pour cliquer.
// Aucune dépendance à l'ancien Input Manager : tout passe par le nouvel Input System.

using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcheryChallenge.Player
{
    /// <summary>Contrôleur FPS du mode clavier/souris.</summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class DesktopPlayerController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Min(0f)] private float runSpeed = 6f;
        [Tooltip("Coché : l'allure rapide (sprint) est l'allure normale ; Maj ralentit à la vitesse de marche.")]
        [SerializeField] private bool runByDefault = true;
        [SerializeField, Min(0f)] private float jumpHeight = 1f;
        [SerializeField] private float gravity = -15f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private bool invertY;
        [SerializeField] private Transform cameraPivot;

        private CharacterController controller;
        private float yaw;
        private float pitch;
        private float verticalVelocity;
        private float seatedOffset;       // décalage de hauteur de vue (assis au feu de camp)
        private float seatedBlend = 1f;   // 1 = assis, 0 = debout
        private float basePivotHeight;
        private Camera cam;

        /// <summary>Multiplicateur de sensibilité (réduit pendant la visée avec zoom).</summary>
        public float SensitivityMultiplier { get; set; } = 1f;

        /// <summary>Quand faux, le joueur ne se déplace ni ne regarde (menus, écrans de fin).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>Caméra du joueur.</summary>
        public Transform CameraTransform
        {
            get
            {
                if (cam == null)
                    cam = GetComponentInChildren<Camera>(true);
                return cam != null ? cam.transform : cameraPivot;
            }
        }

        public Camera Camera
        {
            get
            {
                if (cam == null)
                    cam = GetComponentInChildren<Camera>(true);
                return cam;
            }
        }

        public Transform CameraPivot => cameraPivot;

        /// <summary>Vitesse horizontale actuelle (m/s), pour les pas et l'animation.</summary>
        public float HorizontalSpeed { get; private set; }

        /// <summary>Vrai si le joueur est au sol.</summary>
        public bool IsGrounded => controller != null && controller.isGrounded;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraPivot != null)
                basePivotHeight = cameraPivot.localPosition.y;
            yaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            GameEvents.SettingsChanged += ApplySettings;
            ApplySettings();
        }

        private void OnDisable()
        {
            GameEvents.SettingsChanged -= ApplySettings;
            HorizontalSpeed = 0f;
        }

        private void ApplySettings()
        {
            SettingsService settings = Services.Get<SettingsService>();
            if (settings != null)
                mouseSensitivity = settings.Data.mouseSensitivity;
        }

        /// <summary>
        /// Départ « assis » : la caméra démarre plus bas de |offset| mètres et se relève dès que le
        /// joueur bouge (mise en scène du feu de camp).
        /// </summary>
        public void SetSeated(float offset)
        {
            seatedOffset = offset;
            seatedBlend = Mathf.Abs(offset) > 0.001f ? 1f : 0f;
        }

        /// <summary>Oriente la vue (degrés) : lacet autour de la verticale, tangage de haut en bas (positif = vers le bas).</summary>
        public void SetLookAngles(float newYaw, float newPitch)
        {
            yaw = newYaw;
            pitch = Mathf.Clamp(newPitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        /// <summary>Place le joueur à une pose (déplacement instantané).</summary>
        public void PlaceAt(Pose pose)
        {
            bool wasEnabled = controller != null && controller.enabled;
            if (wasEnabled)
                controller.enabled = false;
            transform.position = pose.position;
            yaw = pose.rotation.eulerAngles.y;
            pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            verticalVelocity = 0f;
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.identity;
            if (wasEnabled)
                controller.enabled = true;
            Physics.SyncTransforms();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool altHeld = keyboard != null && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);
#if UNITY_EDITOR
            // Dans l'éditeur, la vue Game peut ne pas avoir le focus pendant un test automatique : on ne s'en soucie pas.
            bool unfocused = false;
#else
            bool unfocused = !Application.isFocused;
#endif
            bool freeCursor = CursorController.IsFreeRequested || altHeld || unfocused;
            CursorController.Apply(!freeCursor);

            bool canAct = InputEnabled && !freeCursor && Time.timeScale > 0f;

            // --- Regard ---
            if (canAct && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * (mouseSensitivity * SensitivityMultiplier);
                yaw += delta.x;
                pitch += invertY ? delta.y : -delta.y;
                pitch = Mathf.Clamp(pitch, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // --- Déplacement ---
            Vector2 move = Vector2.zero;
            bool run = false;
            bool jump = false;
            if (canAct && keyboard != null)
            {
                if (keyboard.wKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed) move.x -= 1f;
                run = runByDefault ? !keyboard.leftShiftKey.isPressed : keyboard.leftShiftKey.isPressed;
                jump = keyboard.spaceKey.wasPressedThisFrame;
            }
            move = Vector2.ClampMagnitude(move, 1f);

            if (controller.enabled)
            {
                Vector3 horizontal = (transform.right * move.x + transform.forward * move.y) * (run ? runSpeed : walkSpeed);
                if (controller.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -2f;
                if (jump && controller.isGrounded)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                verticalVelocity += gravity * Time.deltaTime;

                Vector3 velocity = horizontal + Vector3.up * verticalVelocity;
                controller.Move(velocity * Time.deltaTime);
                HorizontalSpeed = horizontal.magnitude;
            }

            // --- Hauteur de vue assise ---
            if (move.sqrMagnitude > 0.01f || jump)
                seatedBlend = Mathf.MoveTowards(seatedBlend, 0f, Time.deltaTime * 1.5f);
            if (cameraPivot != null)
            {
                Vector3 p = cameraPivot.localPosition;
                p.y = basePivotHeight + seatedOffset * Mathf.SmoothStep(0f, 1f, seatedBlend);
                cameraPivot.localPosition = p;
            }
        }
    }
}
