// SimulatedRigDriver : pilote le rig VR « sans casque » (touche F2) avec des commandes simples au clavier et à la souris.
//
// OÙ LE POSER : sur la racine « XR Origin (XR Rig) » de la scène Boot (à côté de XRArcherInput). Aucun réglage obligatoire :
// tout est retrouvé tout seul (XR Origin, caméra, manettes, Character Controller).
//
// POURQUOI : le XR Interaction Simulator d'origine demande de basculer entre tête / main gauche / main droite avec des touches
// peu intuitives et place la tête au niveau du sol. Ici le joueur se déplace et regarde comme au clavier/souris, et ce sont les
// mains virtuelles qui jouent la scène VR réelle (le code de tir VR est donc vraiment testé) :
//   - tête à hauteur normale (« Eye Height »), arc tenu dans la main d'arc (saisie XRI réelle) et pointé vers la visée ;
//   - R : la main libre va chercher une flèche dans le carquois dorsal (Grip simulé), puis l'amène à la corde (encochage) ;
//   - clic gauche maintenu : la main libre recule, la corde se tend ; relâcher : le Grip est lâché et la flèche part ;
//   - avec l'encochage automatique (option de confort), le clic gauche prend et encoche tout seul la flèche ;
//   - clic droit : zoom léger ; E / Échap / T : interagir, menu, voyage rapide (lus par XRArcherInput) ; Alt libère le curseur.
// Pendant qu'un écran flottant est ouvert (curseur libre) les mains restent au repos.
//
// Actif seulement quand PlayerModeService est en mode Simulator ; avec un vrai casque ce composant ne fait rien (les
// Tracked Pose Driver des manettes sont réactivés).

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ArcheryChallenge.Player
{
    /// <summary>Pilotage clavier/souris du rig VR sans casque.</summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class SimulatedRigDriver : MonoBehaviour
    {
        [Tooltip("Hauteur des yeux au-dessus du sol (m).")]
        [SerializeField, Range(1.2f, 2f)] private float eyeHeight = 1.65f;
        [SerializeField, Min(0f)] private float walkSpeed = 3f;
        [SerializeField, Min(0f)] private float runSpeed = 6f;
        [Tooltip("Coché : l'allure rapide (sprint) est l'allure normale ; Maj ralentit à la vitesse de marche.")]
        [SerializeField] private bool runByDefault = true;
        [SerializeField] private float gravity = -15f;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(20f, 90f)] private float aimFov = 55f;
        [Tooltip("L'arc vise un point situé à cette distance devant le réticule (comme au clavier/souris).")]
        [SerializeField, Min(5f)] private float convergenceDistance = 30f;
        [Tooltip("Vitesse (m/s) de la main libre entre le carquois et la corde.")]
        [SerializeField, Min(0.5f)] private float handSpeed = 5f;

        private enum Phase { Idle, Fetching, Carrying, Nocked, Cooldown }

        private XRArcherInput input;
        private XROrigin origin;
        private Camera cam;
        private CharacterController body;
        private XRInteractionManager manager;
        private readonly List<Behaviour> poseDrivers = new List<Behaviour>();
        private Behaviour modalityManager;

        private bool active;
        private float pitch;
        private float verticalVelocity;
        private float baseFov = 60f;
        private float enabledAt;
        private float nextGrab;
        private float tension;
        private float phaseStart;
        private int phaseFrame;
        private Phase phase;
        private Vector3 freeHandTarget;

        // Injections pour les tests automatiques (null / faux = lecture réelle du clavier et de la souris).
        private bool? forcedFire;
        private Vector2? forcedMove;
        private int forcedTakeFrame = -10;

        /// <summary>Vrai tant que ce pilote commande le rig.</summary>
        public bool IsActive => active;

        /// <summary>Hauteur des yeux (m).</summary>
        public float EyeHeight => eyeHeight;

        /// <summary>Étape du geste de tir (texte, pour les tests).</summary>
        public string PhaseName => phase.ToString();

        /// <summary>Quand vrai, les mains ne sont plus pilotées ici (un test les place lui-même).</summary>
        public bool HandsPaused { get; set; }

        /// <summary>Test : simule le clic gauche maintenu (true), relâché (false) ou la lecture réelle (null).</summary>
        public void InjectFire(bool? held) => forcedFire = held;

        /// <summary>Oriente la vue (degrés) : lacet autour de la verticale, tangage de haut en bas (positif = vers le bas).</summary>
        public void SetLookAngles(float yaw, float newPitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            pitch = Mathf.Clamp(newPitch, -85f, 85f);
        }

        /// <summary>Test : simule un appui sur R.</summary>
        public void InjectTake() => forcedTakeFrame = Time.frameCount;

        /// <summary>Test : simule les touches de déplacement (x droite, y avant) ou la lecture réelle (null).</summary>
        public void InjectMove(Vector2? move) => forcedMove = move;

        private void Awake()
        {
            input = GetComponent<XRArcherInput>();
            origin = GetComponent<XROrigin>();
            body = GetComponent<CharacterController>();
            cam = origin != null && origin.Camera != null ? origin.Camera : GetComponentInChildren<Camera>(true);
            if (cam != null)
                baseFov = cam.fieldOfView;
            foreach (Behaviour b in GetComponentsInChildren<Behaviour>(true))
            {
                if (b == null)
                    continue;
                string typeName = b.GetType().Name;
                if (typeName == "TrackedPoseDriver")
                    poseDrivers.Add(b);
                else if (typeName == "XRInputModalityManager")
                    modalityManager = b;
            }
        }

        private void OnDisable()
        {
            if (active)
                SetActive(false);
        }

        private void SetActive(bool value)
        {
            active = value;
            foreach (Behaviour b in poseDrivers)
                if (b != null)
                    b.enabled = !value;
            // Sans casque, aucune manette n'est « suivie » : le gestionnaire de modalité éteindrait les objets des manettes
            // (donc leurs interacteurs, qui ne pourraient plus saisir l'arc). On le coupe et on allume les manettes soi-même.
            if (modalityManager != null)
                modalityManager.enabled = !value;
            if (value)
                EnsureControllersOn();

            if (value)
            {
                enabledAt = Time.time;
                pitch = 0f;
                verticalVelocity = 0f;
                tension = 0f;
                phase = Phase.Idle;
                nextGrab = 0f;
                SettingsService settings = Services.Get<SettingsService>();
                if (settings != null)
                    mouseSensitivity = settings.Data.mouseSensitivity;
            }
            else
            {
                if (input != null)
                {
                    input.ForceGrip(Handedness.Left, -1);
                    input.ForceGrip(Handedness.Right, -1);
                }
                if (cam != null)
                    cam.fieldOfView = baseFov;
            }
        }

        private void Update()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            bool wanted = mode != null && mode.Mode == PlayerMode.Simulator && isActiveAndEnabled;
            if (wanted != active)
                SetActive(wanted);
            if (!active || cam == null || input == null)
                return;

            EnsureControllersOn();
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool alt = kb != null && (kb.leftAltKey.isPressed || kb.rightAltKey.isPressed);
            bool freeCursor = CursorController.IsFreeRequested || alt;
            CursorController.Apply(!freeCursor);
            bool canAct = !freeCursor && Time.timeScale > 0f;

            bool aiming = canAct && mouse != null && mouse.rightButton.isPressed;
            UpdateLookAndMove(kb, mouse, canAct, aiming);
            UpdateHands(kb, mouse, canAct);
        }

        private void EnsureControllersOn()
        {
            if (input == null)
                return;
            foreach (Handedness side in new[] { Handedness.Left, Handedness.Right })
            {
                Transform hand = input.GetHand(side);
                if (hand != null && !hand.gameObject.activeSelf)
                    hand.gameObject.SetActive(true);
            }
        }

        // ---------------------------------------------------------------- tête et déplacement

        private void UpdateLookAndMove(Keyboard kb, Mouse mouse, bool canAct, bool aiming)
        {
            float yaw = transform.eulerAngles.y;
            if (canAct && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * (mouseSensitivity * (aiming ? 0.6f : 1f));
                yaw += delta.x;
                pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector2 move = Vector2.zero;
            bool run = false;
            bool jump = false;
            if (forcedMove.HasValue)
            {
                move = forcedMove.Value;
            }
            else if (canAct && kb != null)
            {
                if (kb.wKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed) move.x -= 1f;
                run = runByDefault ? !kb.leftShiftKey.isPressed : kb.leftShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            move = Vector2.ClampMagnitude(move, 1f);

            if (body != null && body.enabled)
            {
                Vector3 horizontal = (transform.right * move.x + transform.forward * move.y) * (run ? runSpeed : walkSpeed);
                if (body.isGrounded && verticalVelocity < 0f)
                    verticalVelocity = -2f;
                if (jump && body.isGrounded)
                    verticalVelocity = Mathf.Sqrt(1f * -2f * gravity);
                verticalVelocity += gravity * Time.deltaTime;
                body.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
            }

            // La tête est posée à hauteur normale, orientée par la souris (le casque réel n'existe pas : les Tracked Pose Driver sont coupés).
            cam.transform.SetPositionAndRotation(transform.position + Vector3.up * eyeHeight, Quaternion.Euler(pitch, yaw, 0f));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, aiming ? aimFov : baseFov, Time.unscaledDeltaTime * 12f);
        }

        // ---------------------------------------------------------------- mains

        private void UpdateHands(Keyboard kb, Mouse mouse, bool canAct)
        {
            SettingsService settings = Services.Get<SettingsService>();
            bool autoNock = settings == null || settings.Data.autoNock;
            Handedness bowSide = settings != null ? settings.BowSide : Handedness.Right;
            Handedness freeSide = XRArcherInput.Opposite(bowSide);
            Transform bowHand = input.GetHand(bowSide);
            Transform freeHand = input.GetHand(freeSide);
            if (bowHand == null || freeHand == null || HandsPaused)
                return;

            Transform head = cam.transform;
            Quaternion yawRotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            float bowSign = bowSide == Handedness.Left ? -1f : 1f;
            Bow bow = input.BowHolster != null ? input.BowHolster.CurrentBow : null;

            // --- main d'arc : devant soi, orientée vers le point de convergence ---
            Vector3 bowHandPosition = head.position + yawRotation * new Vector3(bowSign * 0.30f, -0.22f, 0.60f);   // sur le côté, pour ne pas masquer la visée
            if (bow != null && !bow.IsHeld)
                EnsureBowHeld(bow, bowHand);
            else if (bow != null && bow.IsHeld)
                SwapBowIfNeeded(bow);
            Vector3 convergence = head.position + head.forward * convergenceDistance;
            bowHand.SetPositionAndRotation(bowHandPosition, Quaternion.LookRotation(convergence - bowHandPosition, head.up));

            // --- main libre : repos, carquois, corde ---
            bool fire = forcedFire ?? (canAct && mouse != null && mouse.leftButton.isPressed);
            bool take = Time.frameCount - forcedTakeFrame <= 1 || (canAct && kb != null && kb.rKey.wasPressedThisFrame);
            Vector3 rest = head.position + yawRotation * new Vector3(-bowSign * 0.25f, -0.55f, 0.15f);
            BackQuiver quiver = input.BackQuiver;

            switch (phase)
            {
                case Phase.Idle:
                    freeHandTarget = rest;
                    if (bow != null && bow.IsHeld && !bow.HasArrow && quiver != null && (take || (fire && autoNock)))
                    {
                        if (!ArrowAllowance.CanTake)
                        {
                            if (take)
                                GameEvents.RaiseToast("Plus de flèches !");
                        }
                        else
                        {
                            EnterPhase(Phase.Fetching);
                        }
                    }
                    break;

                case Phase.Fetching:
                    freeHandTarget = quiver != null ? quiver.transform.position : rest;
                    freeHand.position = freeHandTarget;
                    if (Time.frameCount > phaseFrame)
                        input.ForceGrip(freeSide, 1);
                    if (quiver != null && quiver.GetHeldArrow(freeSide) != null)
                        EnterPhase(Phase.Carrying);
                    else if (Time.time - phaseStart > 0.6f)
                        Abort();
                    break;

                case Phase.Carrying:
                    if (bow == null || !bow.IsHeld || quiver == null || quiver.GetHeldArrow(freeSide) == null && !bow.HasArrow)
                    {
                        Abort();
                        break;
                    }
                    freeHandTarget = bow.StringRestPosition + bow.transform.right * 0.04f;
                    if (bow.HasArrow)
                        EnterPhase(Phase.Nocked);
                    else if (Time.time - phaseStart > 2.5f)
                        Abort();
                    break;

                case Phase.Nocked:
                    if (bow == null || !bow.IsHeld || !bow.HasArrow)
                    {
                        Abort();
                        break;
                    }
                    if (fire)
                    {
                        float drawTime = bow.Definition != null ? bow.Definition.desktopDrawTime : 0.6f;
                        tension = Mathf.Min(1f, tension + Time.deltaTime / Mathf.Max(0.05f, drawTime));
                    }
                    else if (tension > 0.02f)
                    {
                        // Clic relâché : on lâche le Grip, BowString relâche la corde et la flèche part.
                        input.ForceGrip(freeSide, 0);
                        EnterPhase(Phase.Cooldown);
                        break;
                    }
                    freeHandTarget = bow.StringRestPosition - bow.transform.forward * (bow.DrawLength * tension);
                    freeHand.position = freeHandTarget;
                    break;

                case Phase.Cooldown:
                    freeHandTarget = rest;
                    if (Time.time - phaseStart > 0.25f)
                    {
                        input.ForceGrip(freeSide, -1);
                        EnterPhase(Phase.Idle);
                    }
                    break;
            }

            if (phase != Phase.Nocked && phase != Phase.Fetching)
                freeHand.position = Vector3.MoveTowards(freeHand.position, freeHandTarget, handSpeed * Time.deltaTime);
            Vector3 forward = bow != null && bow.IsHeld ? bow.transform.forward : head.forward;
            freeHand.rotation = Quaternion.LookRotation(forward, head.up);
        }

        private void EnterPhase(Phase next)
        {
            phase = next;
            phaseStart = Time.time;
            phaseFrame = Time.frameCount;
            if (next == Phase.Nocked)
                tension = 0f;
        }

        // Geste interrompu : la flèche tenue est lâchée et la main revient au repos.
        private void Abort()
        {
            Handedness bowSide = Services.Get<SettingsService>()?.BowSide ?? Handedness.Right;
            input.ForceGrip(XRArcherInput.Opposite(bowSide), 0);
            tension = 0f;
            EnterPhase(Phase.Cooldown);
        }

        // ---------------------------------------------------------------- arc

        // L'arc posé à l'épaule est saisi par la main d'arc (sélection XRI réelle), comme un joueur le ferait.
        private void EnsureBowHeld(Bow bow, Transform bowHand)
        {
            if (Time.time < nextGrab || Time.time < enabledAt + 0.7f || bow.GrabInteractable == null || !bow.GrabInteractable.isActiveAndEnabled)
                return;
            nextGrab = Time.time + 0.5f;

            if (manager == null)
                manager = FindAnyObjectByType<XRInteractionManager>();
            NearFarInteractor interactor = bowHand.GetComponentInChildren<NearFarInteractor>(true);
            if (manager == null || interactor == null || !manager.IsRegistered((IXRInteractor)interactor)
                || !manager.IsRegistered((IXRInteractable)bow.GrabInteractable))
                return;

            bowHand.position = bow.transform.position;
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)bow.GrabInteractable);
        }

        // Un autre arc a été équipé (râtelier) : on lâche l'arc tenu, il retourne à l'épaule et le nouveau est saisi à son tour.
        private void SwapBowIfNeeded(Bow bow)
        {
            EquipmentService equipment = Services.Get<EquipmentService>();
            BowDefinition selected = equipment != null ? equipment.SelectedBow : null;
            if (selected == null || bow.Definition == selected || phase != Phase.Idle || manager == null)
                return;
            NearFarInteractor interactor = bow.GrabInteractable != null && bow.GrabInteractable.firstInteractorSelecting is NearFarInteractor n ? n : null;
            if (interactor == null)
                return;
            manager.SelectExit((IXRSelectInteractor)interactor, (IXRSelectInteractable)bow.GrabInteractable);
            input.BowHolster?.SpawnSelectedBow();
        }
    }
}
