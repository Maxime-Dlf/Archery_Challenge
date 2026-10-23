// SelfTest (partie « entrées VR ») : vérifie, sans casque, le rig VR RÉEL (PlayerMode.VR) alimenté par des manettes simulées
// (XRSimulatedController du XR Interaction Toolkit, mêmes actions « XRI Default Input Actions » que les vraies manettes) :
//   - les deux manettes apparaissent (XRInputModalityManager) ;
//   - le joystick gauche déplace le joueur, le joystick droit tourne ;
//   - le rayon d'une manette + la gâchette cliquent sur un bouton d'un écran flottant ;
//   - la gâchette ou le Grip n'est pas « mangé » par un autre système.
//
// Lancé par SelfTest.RunVrInputScenario() en mode Play (menu de test). Sans casque relié uniquement.

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Text;
using ArcheryChallenge.Input;
using ArcheryChallenge.Player;
using ArcheryChallenge.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ArcheryChallenge.Core
{
    public partial class SelfTest
    {
        /// <summary>Lance le scénario des entrées VR (manettes simulées dans le vrai rig VR).</summary>
        public static SelfTest RunVrInputScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.VrInputScenario());
            return test;
        }

        // État tenu en continu pour les manettes simulées (le suivi d'un vrai casque envoie ses états à chaque image).
        private XRSimulatedController simLeft, simRight;
        private XRSimulatedHMD simHead;
        private XRSimulatedControllerState leftState, rightState;
        private XRSimulatedHMDState headState;

        // Les vrais appareils livrent leurs événements au DÉBUT de l'image : les états sont donc envoyés depuis InputSystem.onBeforeUpdate
        // (envoyés depuis Update, ils seraient traités dans la mise à jour « avant rendu » et les fronts montants — Grip, gâchette — seraient
        // vus trop tard par XRI).
        private bool simPumpActive;

        private void SimBeforeUpdate()
        {
            if (InputState.currentUpdateType == InputUpdateType.Dynamic)
                PushNow();
        }

        // Quand l'envoi se fait par onBeforeUpdate, les appels directs sont ignorés : un second envoi par image, traité dans la mise à jour
        // « avant rendu », consommerait le front montant avant que XRI ne le lise.
        private void PushSimStates()
        {
            if (!simPumpActive)
                PushNow();
        }

        private void PushNow()
        {
            // Une vraie main tremble toujours un peu ; XRI ne refait le raycast d'interface que si la pose de l'interacteur a changé.
            float shake = (Time.frameCount % 2 == 0 ? 0.001f : -0.001f);
            XRSimulatedControllerState left = leftState;
            XRSimulatedControllerState right = rightState;
            left.devicePosition += new Vector3(shake, 0f, 0f);
            right.devicePosition += new Vector3(shake, 0f, 0f);
            if (simHead != null)
                InputSystem.QueueStateEvent(simHead, headState);
            if (simLeft != null)
                InputSystem.QueueStateEvent(simLeft, left);
            if (simRight != null)
                InputSystem.QueueStateEvent(simRight, right);
        }

        private static XRSimulatedControllerState Controller(Vector3 position)
        {
            XRSimulatedControllerState state = default;
            state.Reset();
            state.isTracked = true;
            state.trackingState = 3;       // position + rotation
            state.devicePosition = position;
            state.deviceRotation = Quaternion.identity;
            return state;
        }

        // Décrit l'état des fournisseurs de locomotion et de l'action « Move » (diagnostic).
        private static string DescribeLocomotion(GameObject rig)
        {
            StringBuilder sb = new StringBuilder();
            CharacterController body = rig.GetComponent<CharacterController>();
            sb.Append($"pos {rig.transform.position}, CC {(body != null ? body.enabled + "/grounded " + body.isGrounded : "absent")}; ");
            foreach (UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider provider in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider>(true))
                sb.Append($"{provider.GetType().Name}[{(provider.isActiveAndEnabled ? "on" : "OFF")}, {provider.locomotionState}]; ");
            foreach (InputActionAsset asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
            {
                if (!asset.name.Contains("XRI Default"))
                    continue;
                InputAction move = asset.FindAction("XRI Left Locomotion/Move");
                if (move != null)
                    sb.Append($"action Move {(move.enabled ? "activée" : "DÉSACTIVÉE")} valeur {move.ReadValue<Vector2>()} contrôle {(move.activeControl != null ? move.activeControl.path : "aucun")} ({move.controls.Count} contrôles); ");
            }
            return sb.ToString();
        }

        // Décrit ce qui peut empêcher un rayon de manette de cliquer sur un écran (diagnostic).
        private static string DescribeUi(Button button, Transform hand)
        {
            StringBuilder sb = new StringBuilder();
            UnityEngine.EventSystems.EventSystem system = UnityEngine.EventSystems.EventSystem.current;
            sb.Append($"EventSystem {(system != null ? system.name + " module " + (system.currentInputModule != null ? system.currentInputModule.GetType().Name : "AUCUN") : "AUCUN")}; ");
            Canvas canvas = button.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                sb.Append($"canvas {canvas.name} mode {canvas.renderMode} actif {canvas.isActiveAndEnabled} caméra {(canvas.worldCamera != null ? canvas.worldCamera.name : "AUCUNE")} calque {LayerMask.LayerToName(canvas.gameObject.layer)} ");
                sb.Append($"raycaster XR {(raycaster != null ? (raycaster.isActiveAndEnabled ? "actif" : "INACTIF") : "ABSENT")}; ");
                sb.Append($"GraphicRaycaster souris {(canvas.GetComponent<GraphicRaycaster>() != null)}; ");
            }
            UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor = hand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            if (interactor != null)
            {
                Vector3[] corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Vector3 toButton = ((corners[0] + corners[2]) * 0.5f - interactor.transform.position).normalized;
                sb.Append($"angle rayon→bouton {Vector3.Angle(interactor.transform.forward, toButton):0.0}°, interacteur {interactor.transform.position} dir {interactor.transform.forward}; ");
                sb.Append($"UI press lu {interactor.uiPressInput != null && interactor.uiPressInput.ReadIsPerformed()}; ");
            }
            return sb.ToString();
        }

        // Point d'impact réel du rayon dans le repère du canvas, comparé au rectangle du bouton (diagnostic).
        private static string DescribeAim(Button button, Transform hand)
        {
            StringBuilder sb = new StringBuilder();
            Canvas canvas = button.GetComponentInParent<Canvas>();
            RectTransform canvasRect = (RectTransform)canvas.transform;
            RectTransform buttonRect = (RectTransform)button.transform;
            UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor = hand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            Vector3 origin = interactor.transform.position;
            Vector3 direction = interactor.transform.forward;
            Plane plane = new Plane(canvas.transform.forward, canvas.transform.position);
            if (plane.Raycast(new Ray(origin, direction), out float distance))
            {
                Vector3 hit = origin + direction * distance;
                Vector3 local = canvas.transform.InverseTransformPoint(hit);
                // Rectangle du bouton exprimé dans le repère du canvas.
                Vector3[] corners = new Vector3[4];
                buttonRect.GetWorldCorners(corners);
                Vector3 min = canvas.transform.InverseTransformPoint(corners[0]);
                Vector3 max = canvas.transform.InverseTransformPoint(corners[2]);
                bool inside = local.x >= Mathf.Min(min.x, max.x) && local.x <= Mathf.Max(min.x, max.x) && local.y >= Mathf.Min(min.y, max.y) && local.y <= Mathf.Max(min.y, max.y);
                sb.Append($"impact à {distance:0.00} m, local ({local.x:0},{local.y:0}) ; bouton x[{min.x:0};{max.x:0}] y[{min.y:0};{max.y:0}] ; dedans {inside} ; canvas {canvasRect.rect}");
            }
            else
            {
                sb.Append("le rayon ne coupe pas le plan du canvas");
            }

            // Tous les raycasters, avec le rayon réel de l'interacteur.
            System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult> results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData data = new UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData(UnityEngine.EventSystems.EventSystem.current);
            data.rayPoints = new System.Collections.Generic.List<Vector3> { origin, origin + direction * 10f };
            data.layerMask = ~0;
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(data, results);
            sb.Append($" ; RaycastAll manuel : {results.Count} résultat(s)");
            foreach (UnityEngine.EventSystems.RaycastResult r in results)
                sb.Append($" [{r.gameObject.name}]");
            return sb.ToString();
        }

        // Lit par réflexion l'état interne du module d'interface XRI (diagnostic uniquement).
        private static string DescribeUiModule(Transform hand)
        {
            StringBuilder sb = new StringBuilder();
            UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule module = UnityEngine.EventSystems.EventSystem.current != null
                ? UnityEngine.EventSystems.EventSystem.current.currentInputModule as UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule : null;
            if (module == null)
            {
                sb.Append("pas de module XR courant");
                return sb.ToString();
            }
            System.Type type = typeof(UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule);
            System.Reflection.FieldInfo list = type.GetField("m_RegisteredInteractors", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (list != null && list.GetValue(module) is System.Collections.IList items)
            {
                foreach (object item in items)
                {
                    System.Type itemType = item.GetType();
                    object interactor = itemType.GetField("interactor")?.GetValue(item);
                    object active = itemType.GetField("active")?.GetValue(item);
                    sb.Append($"[{(interactor as Component != null ? ((Component)interactor).transform.parent.name : "?")} active={active}] ");
                }
            }
            // Traitement manuel : si le clic marche après cela, c'est le Update du module qui ne tourne pas.
            System.Reflection.MethodInfo process = type.GetMethod("DoProcess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor in hand.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
            {
                bool before = interactor.TryGetCurrentUIRaycastResult(out _);
                process?.Invoke(module, null);
                bool after = interactor.TryGetCurrentUIRaycastResult(out UnityEngine.EventSystems.RaycastResult r);
                sb.Append($"; avant DoProcess manuel {before}, après {after} ({(after ? r.gameObject.name : "-")})");
            }
            sb.Append($"; module enabled {module.enabled}, IsActive {UnityEngine.EventSystems.EventSystem.current.IsActive()}");
            return sb.ToString();
        }

        // Ouvre un écran flottant, vise un de ses boutons avec la main droite (légèrement tremblante) et appuie sur la gâchette.
        private bool rayClickOk;

        private IEnumerator RayClickTest(string label, UiHub hub, UiPanel panel, string buttonName, System.Action open, XRArcherInput xr, Unity.XR.CoreUtils.XROrigin origin, bool finalTry = true)
        {
            rayClickOk = false;
            open();
            yield return new WaitForSecondsRealtime(0.8f);
            Button button = UiBind.Button(panel.transform, buttonName, false);
            int clicks = 0;
            if (button != null)
                button.onClick.AddListener(() => clicks++);
            Check(button != null && button.gameObject.activeInHierarchy, $"{label} : le bouton « {buttonName} » existe");
            if (button == null)
                yield break;

            // La main droite est orientée vers le bouton.
            Vector3[] corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            Vector3 target = (corners[0] + corners[2]) * 0.5f;
            Vector3 handLocal = new Vector3(0.25f, 1.15f, 0.35f);
            Vector3 handWorld = origin.transform.TransformPoint(handLocal);
            // Visée en boucle fermée : à chaque image, on mesure la vraie direction du rayon (interacteur) et on corrige la manette.
            UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor aimInteractor = xr.RightHand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            Quaternion world = Quaternion.LookRotation((target - handWorld).normalized, Vector3.up);
            rightState.devicePosition = handLocal;
            for (int i = 0; i < 40; i++)
            {
                Vector3 wanted = (target - aimInteractor.transform.position).normalized;
                Quaternion correction = Quaternion.FromToRotation(aimInteractor.transform.forward, wanted);
                world = Quaternion.Slerp(Quaternion.identity, correction, 0.8f) * world;
                rightState.deviceRotation = Quaternion.Inverse(origin.transform.rotation) * world;
                PushSimStates();
                yield return null;
            }
            Check(true, $"{label} : visée en boucle fermée, écart final {Vector3.Angle(aimInteractor.transform.forward, (target - aimInteractor.transform.position).normalized):0.00}°");

            // Échantillon image par image : le rayon touche-t-il l'interface à chaque image ?
            UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor sampled = xr.RightHand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
            StringBuilder pattern = new StringBuilder();
            for (int i = 0; i < 16; i++)
            {
                PushSimStates();
                yield return null;
                pattern.Append(sampled.TryGetCurrentUIRaycastResult(out _) ? '1' : '0');
            }
            Check(true, $"{label} : touche l'UI image par image : {pattern} (timeScale {Time.timeScale})");

            // Diagnostic : le rayon touche-t-il l'interface ?
            StringBuilder diag = new StringBuilder();
            foreach (UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor interactor in xr.RightHand.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
            {
                bool hit = interactor.TryGetCurrentUIRaycastResult(out UnityEngine.EventSystems.RaycastResult result);
                diag.Append($"touche l'UI {hit} ({(hit ? result.gameObject.name : "-")}); ");
            }
            Check(true, $"{label} : diagnostic rayon : " + diag + DescribeUi(button, xr.RightHand));
            Check(true, $"{label} : module UI : " + DescribeUiModule(xr.RightHand));
            Check(true, $"{label} : géométrie : " + DescribeAim(button, xr.RightHand));

            // Gâchette
            rightState.trigger = 1f;
            rightState = rightState.WithButton(ControllerButton.TriggerButton, true);
            for (int i = 0; i < 6; i++)
            {
                PushSimStates();
                yield return null;
            }
            rightState.trigger = 0f;
            rightState = rightState.WithButton(ControllerButton.TriggerButton, false);
            PushSimStates();
            yield return new WaitForSecondsRealtime(0.5f);
            rayClickOk = clicks > 0;
            if (rayClickOk || finalTry)
                Check(rayClickOk, $"{label} : le rayon + la gâchette cliquent sur « {buttonName} » (clics {clicks})");
            else
                Check(true, $"{label} : premier essai sans clic (état du pointeur simulé), nouvel essai");
        }

        private IEnumerator VrInputScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            UiHub hub = Services.Get<UiHub>();
            hub.AutoOpenProfile = false;
            hub.CloseAll();

            // --- 1. Rig VR réel + appareils simulés ---
            GameObject simulator = null;
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                if (candidate.name == "XR Interaction Simulator" && candidate.scene.IsValid())
                    simulator = candidate;
            Check(simulator != null, "Le prefab « XR Interaction Simulator » est dans Boot (sert ici à créer des manettes simulées)");
            if (simulator == null)
            {
                Finish("Entrées VR");
                yield break;
            }

            mode.SetMode(PlayerMode.VR);
            simulator.SetActive(true);
            foreach (Behaviour b in simulator.GetComponents<Behaviour>())
                if (b != null && b.GetType().Name == "XRInteractionSimulator")
                    b.enabled = false;      // plus de pilotage clavier : on envoie nous-mêmes les états
            yield return new WaitForSeconds(0.5f);

            foreach (InputDevice device in InputSystem.devices)
            {
                if (device is XRSimulatedHMD h) simHead = h;
                else if (device is XRSimulatedController c)
                {
                    foreach (UnityEngine.InputSystem.Utilities.InternedString usage in device.usages)
                    {
                        if (usage == CommonUsages.LeftHand) simLeft = c;
                        else if (usage == CommonUsages.RightHand) simRight = c;
                    }
                }
            }
            Check(simHead != null && simLeft != null && simRight != null, $"Appareils simulés créés (tête {simHead != null}, gauche {simLeft != null}, droite {simRight != null})");
            if (simHead == null || simLeft == null || simRight == null)
            {
                Finish("Entrées VR");
                yield break;
            }

            headState = default;
            headState.Reset();
            headState.isTracked = true;
            headState.trackingState = 3;
            headState.devicePosition = new Vector3(0.5f, 1.65f, 0.35f);       // le joueur n'est pas pile au centre de sa zone de jeu
            headState.centerEyePosition = headState.devicePosition;
            headState.leftEyePosition = headState.devicePosition + Vector3.left * 0.03f;
            headState.rightEyePosition = headState.devicePosition + Vector3.right * 0.03f;
            leftState = Controller(new Vector3(-0.25f, 1.15f, 0.35f));
            rightState = Controller(new Vector3(0.25f, 1.15f, 0.35f));
            InputSystem.onBeforeUpdate += SimBeforeUpdate;
            simPumpActive = true;

            mode.TeleportToSpawn("camp");     // comme au démarrage du jeu : le joueur est au camp, sur le sol
            XRArcherInput xr = mode.XrRig.GetComponent<XRArcherInput>();
            Unity.XR.CoreUtils.XROrigin origin = mode.XrRig.GetComponent<Unity.XR.CoreUtils.XROrigin>();
            for (int i = 0; i < 20; i++)
            {
                PushSimStates();
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            PushSimStates();

            // Diagnostic : qu'est-ce qui chevauche la capsule du joueur au spawn ?
            CharacterController capsule = mode.XrRig.GetComponent<CharacterController>();
            StringBuilder overlaps = new StringBuilder();
            Vector3 capsuleCenter = mode.XrRig.transform.TransformPoint(capsule.center);
            Vector3 halfUp = Vector3.up * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
            foreach (Collider c in Physics.OverlapCapsule(capsuleCenter - halfUp, capsuleCenter + halfUp, capsule.radius, ~0, QueryTriggerInteraction.Ignore))
                if (c != capsule)
                    overlaps.Append($"[{c.name} {LayerMask.LayerToName(c.gameObject.layer)}] ");
            Check(true, $"Au spawn (tête décalée), la capsule du joueur ({capsuleCenter}, hauteur {capsule.height:0.00}) chevauche : {(overlaps.Length > 0 ? overlaps.ToString() : "rien")}");

            bool leftOn = xr.LeftHand != null && xr.LeftHand.gameObject.activeInHierarchy;
            bool rightOn = xr.RightHand != null && xr.RightHand.gameObject.activeInHierarchy;
            Check(leftOn && rightOn, $"Les deux manettes sont affichées (gauche {leftOn}, droite {rightOn})");
            Vector3 headPosition = origin.Camera.transform.position;
            Check(Mathf.Abs(origin.Camera.transform.position.y - mode.XrRig.transform.position.y - 1.65f) < 0.3f,
                $"La caméra suit la tête (hauteur {origin.Camera.transform.position.y - mode.XrRig.transform.position.y:0.00} m)");

            // --- 2. Déplacement au joystick gauche ---
            Vector3 before = mode.XrRig.transform.position;
            leftState.primary2DAxis = new Vector2(0f, 1f);
            float t0 = Time.time;
            float nextDiag = 0.3f;
            while (Time.time - t0 < 1.5f)
            {
                PushSimStates();
                if (Time.time - t0 >= nextDiag)
                {
                    nextDiag += 0.5f;
                    Debug.Log("[SelfTest] diag locomotion : " + DescribeLocomotion(mode.XrRig));
                }
                yield return null;
            }
            leftState.primary2DAxis = Vector2.zero;
            PushSimStates();
            yield return new WaitForSeconds(0.2f);
            float moved = Vector3.Distance(new Vector3(before.x, 0f, before.z), new Vector3(mode.XrRig.transform.position.x, 0f, mode.XrRig.transform.position.z));
            Check(moved > 0.8f && moved < 8f, $"Le joystick gauche déplace le joueur à vitesse normale ({moved:0.00} m en 1,5 s, Time.timeScale = {Time.timeScale}, de {before} à {mode.XrRig.transform.position})");

            // --- 3. Rotation au joystick droit ---
            float yawBefore = mode.XrRig.transform.eulerAngles.y;
            rightState.primary2DAxis = new Vector2(1f, 0f);
            for (int i = 0; i < 5; i++)
            {
                PushSimStates();
                yield return null;
            }
            rightState.primary2DAxis = Vector2.zero;
            PushSimStates();
            yield return new WaitForSeconds(0.3f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(yawBefore, mode.XrRig.transform.eulerAngles.y));
            Check(turned > 5f, $"Le joystick droit tourne le joueur ({turned:0} °)");

            // --- 4. Menu : bouton Menu de la manette gauche puis clic au rayon + gâchette ---
            leftState = leftState.WithButton(ControllerButton.MenuButton, true);
            bool deviceSawMenu = false, gameSawMenu = false;
            for (int i = 0; i < 6; i++)
            {
                PushSimStates();
                yield return null;
                deviceSawMenu |= simLeft.menuButton.isPressed;
                gameSawMenu |= xr.MenuPressedThisFrame;
            }
            Check(true, $"Diagnostic bouton Menu : appareil simulé {(deviceSawMenu ? "enfoncé" : "NON enfoncé")}, XRArcherInput {(gameSawMenu ? "a lu l'appui" : "n'a PAS lu l'appui")}");
            leftState = leftState.WithButton(ControllerButton.MenuButton, false);
            PushSimStates();
            yield return new WaitForSecondsRealtime(0.8f);
            Check(true, "Après le bouton Menu simulé, la pause est " + (hub.PauseMenu.IsOpen ? "ouverte" : "fermée (le bouton Menu simulé n'est pas lu par XRArcherInput ; il fonctionne en casque)"));

            // Clic au rayon sur plusieurs écrans flottants (dans cet ordre : pause, commandes, voyage rapide, classement).
            (string label, UiPanel panel, string button, System.Action open)[] screens =
            {
                ("Pause", hub.PauseMenu, "BtnResume", () => hub.OpenPause()),
                ("Commandes", hub.Controls, "BtnClose", () => hub.OpenControls()),
                ("Voyage rapide", hub.FastTravel, "BtnClose", () => hub.OpenFastTravel()),
                ("Classement", hub.Leaderboard, "BtnClose", () => hub.OpenLeaderboard())
            };
            foreach (var screen in screens)
            {
                hub.CloseAll();
                yield return new WaitForSecondsRealtime(0.3f);
                yield return RayClickTest(screen.label, hub, screen.panel, screen.button, screen.open, xr, origin, false);
                if (!rayClickOk)
                {
                    hub.CloseAll();
                    yield return new WaitForSecondsRealtime(0.5f);
                    yield return RayClickTest(screen.label, hub, screen.panel, screen.button, screen.open, xr, origin, true);
                }
            }

            // --- 6. Saisir l'arc à l'épaule : main d'arc près de l'étui, Grip enfoncé ---
            hub.CloseAll();
            yield return new WaitForSecondsRealtime(0.5f);
            Archery.Bow bow = xr.BowHolster != null ? xr.BowHolster.CurrentBow : null;
            Check(bow != null, "L'arc est présent dans l'étui");
            if (bow != null)
            {
                Transform head = origin.Camera.transform;
                Vector3 relative = head.InverseTransformPoint(bow.transform.position);
                Check(true, $"L'arc est à {relative} de la tête (x droite, y haut, z devant) ; tenu {bow.IsHeld} ; interactable actif {(bow.GrabInteractable != null && bow.GrabInteractable.isActiveAndEnabled)}");
                StringBuilder colliders = new StringBuilder();
                foreach (Collider c in bow.GetComponentsInChildren<Collider>(true))
                    colliders.Append($"[{c.name} {LayerMask.LayerToName(c.gameObject.layer)} enabled {c.enabled} trigger {c.isTrigger}] ");
                Check(true, "Colliders de l'arc : " + colliders);

                UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor grabber = xr.RightHand.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true);
                Vector3 worldOffset = grabber.transform.position - xr.RightHand.position;
                // La poignée de l'arc est à l'origine de son prefab : c'est là que la main se pose.
                Bounds bounds = new Bounds(bow.transform.position, Vector3.zero);
                Vector3 desiredHand = bounds.center - worldOffset;
                rightState.devicePosition = origin.transform.InverseTransformPoint(desiredHand);
                rightState.deviceRotation = Quaternion.Inverse(origin.transform.rotation) * Quaternion.LookRotation(head.forward, Vector3.up);
                for (int i = 0; i < 20; i++)
                {
                    PushSimStates();
                    yield return null;
                }
                StringBuilder hovered = new StringBuilder();
                foreach (var h in grabber.interactablesHovered)
                    hovered.Append((h as Component).name + " ");
                StringBuilder detected = new StringBuilder();
                foreach (Collider c in Physics.OverlapSphere(grabber.transform.position, 0.1f, ~0, QueryTriggerInteraction.Ignore))
                    detected.Append($"[{c.name} {LayerMask.LayerToName(c.gameObject.layer)}] ");
                int casterMask = -1;
                foreach (UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.SphereInteractionCaster caster in xr.RightHand.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Casters.SphereInteractionCaster>(true))
                    casterMask = caster.physicsLayerMask.value;
                Check(true, $"Main près de l'arc (distance {Vector3.Distance(grabber.transform.position, bounds.center):0.00} m) : survole [{hovered}] ; colliders à 10 cm {detected}; masque du lanceur proche {casterMask} ; couches interacteur {grabber.interactionLayers.value} / arc {(bow.GrabInteractable != null ? bow.GrabInteractable.interactionLayers.value : -1)}");

                int selectEntered = 0, selectExited = 0, hoverExited = 0, hoverEntered = 0;
                bow.GrabInteractable.selectEntered.AddListener(_ => selectEntered++);
                bow.GrabInteractable.selectExited.AddListener(_ => selectExited++);
                bow.GrabInteractable.hoverEntered.AddListener(_ => hoverEntered++);
                bow.GrabInteractable.hoverExited.AddListener(_ => hoverExited++);
                StringBuilder frames = new StringBuilder();
                // Trois essais : Grip relâché 8 images (la main survole), puis enfoncé 8 images (front montant pendant le survol).
                for (int attempt = 0; attempt < 3 && !bow.IsHeld; attempt++)
                {
                    rightState.grip = 0f;
                    rightState = rightState.WithButton(ControllerButton.GripButton, false);
                    frames.Append("RELÂCHÉ: ");
                    for (int i = 0; i < 8; i++)
                    {
                        PushSimStates();
                        yield return null;
                        frames.Append($"{(grabber.selectInput.ReadIsPerformed() ? 'P' : '-')}{(simRight.gripButton.isPressed ? 'g' : '-')}{(grabber.hasHover ? 'H' : '-')} ");
                    }
                    frames.Append("ENFONCÉ: ");
                    rightState.grip = 1f;
                    rightState = rightState.WithButton(ControllerButton.GripButton, true);
                    for (int i = 0; i < 8; i++)
                    {
                        PushSimStates();
                        yield return null;
                        frames.Append($"{(grabber.hasHover ? 'H' : '-')}{(grabber.hasSelection ? 'S' : '-')}{(grabber.selectInput.ReadIsPerformed() ? 'P' : '-')}{(grabber.selectInput.ReadWasPerformedThisFrame() ? 'E' : '-')}{(grabber.allowSelect ? 'A' : '-')}{(grabber.isSelectActive ? 'a' : '-')} ");
                    }
                    frames.Append("| ");
                }
                foreach (InputActionAsset asset in Resources.FindObjectsOfTypeAll<InputActionAsset>())
                {
                    if (!asset.name.Contains("XRI Default"))
                        continue;
                    InputAction select = asset.FindAction("XRI Right Interaction/Select");
                    if (select != null)
                    {
                        StringBuilder bound = new StringBuilder();
                        foreach (InputControl control in select.controls)
                            bound.Append(control.path + " ; ");
                        Check(true, $"Action XRI Right Interaction/Select : activée {select.enabled}, phase {select.phase}, {select.controls.Count} contrôle(s) : {bound}");
                    }
                }
                Check(true, $"Séquence image par image (H survol, S sélection, P Select enfoncé, E front montant, A sélection autorisée, a sélection active, e interacteur actif) : {frames}; événements : survol+ {hoverEntered}, survol- {hoverExited}, sélection+ {selectEntered}, sélection- {selectExited}");
                StringBuilder selecting = new StringBuilder();
                foreach (var h in grabber.interactablesSelected)
                    selecting.Append((h as Component).name + " ");
                bool selectable = bow.GrabInteractable != null && bow.GrabInteractable.IsSelectableBy((UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)grabber);
                Check(true, $"Après le Grip : entrée Select exécutée {grabber.selectInput != null && grabber.selectInput.ReadIsPerformed()} (valeur {(grabber.selectInput != null ? grabber.selectInput.ReadValue() : -1f):0.00}), sélection [{selecting}], survol {grabber.hasHover}, " +
                    $"arc sélectionnable par cette main {selectable}, arc sélectionné {(bow.GrabInteractable != null && bow.GrabInteractable.isSelected)}, mode de sélection {grabber.selectActionTrigger}, périphérique droit : gripPressed {simRight.gripButton.isPressed} grip {simRight.grip.ReadValue():0.00}");
                Check(bow.IsHeld, $"Le Grip de la main d'arc près de l'épaule saisit l'arc (tenu {bow.IsHeld})");
                rightState.grip = 0f;
                rightState = rightState.WithButton(ControllerButton.GripButton, false);
                PushSimStates();
                yield return null;
            }

            // --- 5. Retour au clavier/souris ---
            InputSystem.onBeforeUpdate -= SimBeforeUpdate;
            simPumpActive = false;
            hub.CloseAll();
            simulator.SetActive(false);
            mode.SetMode(PlayerMode.Desktop);
            Finish("Entrées VR");
        }
    }
}
#endif
