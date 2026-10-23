// SelfTest (partie « démarrage VR simulé ») : rejoue le démarrage du jeu en VR RÉELLE sans casque.
//
// POURQUOI : en casque (Meta Quest Link), la tête et les manettes sont suivies dès la première image, pendant que le monde se charge,
// puis GameBootstrap téléporte le joueur au camp. Les scénarios qui passent en VR à la main (SelfTest.VrInput) ne rejouent pas cet
// ordre. Ici, des appareils XRI simulés (tête et deux manettes suivies, tête décalée dans la pièce) sont créés AVANT le chargement
// de la scène Boot et alimentés à chaque image ; le mode de démarrage est forcé sur VR.
//
// UTILISATION (éditeur) : SelfTest.PrepareVrBootSimulation(true), lancer Play (le jeu démarre en VR simulée), puis
// SelfTest.RunVrBootScenario() ; à la fin, SelfTest.PrepareVrBootSimulation(false).

#if UNITY_EDITOR
using System.Collections;
using System.Text;
using ArcheryChallenge.Input;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ArcheryChallenge.Core
{
    /// <summary>Crée au démarrage les appareils XR simulés (tête + 2 manettes) quand la simulation est demandée.</summary>
    public static class VrBootSimulation
    {
        public const string PrefKey = "ArcheryChallenge.SimulateVrBoot";
        public const string OffsetKey = "ArcheryChallenge.SimulateVrBootOffset";
        public const string DelayKey = "ArcheryChallenge.SimulateVrBootTrackDelay";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (!UnityEditor.EditorPrefs.GetBool(PrefKey, false))
                return;
            PlayerModeService.StartupOverride = PlayerMode.VR;
            GameObject go = new GameObject("VrBootSimulation");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<VrBootSimulationPump>();
        }
    }

    /// <summary>Envoie à chaque image l'état de la tête et des manettes simulées.</summary>
    public class VrBootSimulationPump : MonoBehaviour
    {
        public static VrBootSimulationPump Instance { get; private set; }

        /// <summary>Joystick gauche simulé.</summary>
        public Vector2 LeftStick;

        private XRSimulatedHMD head;
        private XRSimulatedController left, right;
        private int frame;
        private float startTime;
        private float trackDelay;

        /// <summary>Vrai quand la tête et les manettes sont « suivies » (après le retard demandé).</summary>
        public bool Tracked => Time.realtimeSinceStartup - startTime >= trackDelay;

        private void Awake()
        {
            Instance = this;
            startTime = Time.realtimeSinceStartup;
            trackDelay = UnityEditor.EditorPrefs.GetFloat(VrBootSimulation.DelayKey, 0f);
            InputSystem.RegisterLayout<XRSimulatedHMD>();
            InputSystem.RegisterLayout<XRSimulatedController>();
            head = InputSystem.AddDevice<XRSimulatedHMD>();
            left = InputSystem.AddDevice<XRSimulatedController>();
            right = InputSystem.AddDevice<XRSimulatedController>();
            InputSystem.SetDeviceUsage(left, CommonUsages.LeftHand);
            InputSystem.SetDeviceUsage(right, CommonUsages.RightHand);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (head != null) InputSystem.RemoveDevice(head);
            if (left != null) InputSystem.RemoveDevice(left);
            if (right != null) InputSystem.RemoveDevice(right);
        }

        private void Update()
        {
            frame++;
            float shake = frame % 2 == 0 ? 0.001f : -0.001f;

            bool tracked = Tracked;
            XRSimulatedHMDState h = default;
            h.Reset();
            h.isTracked = tracked;
            h.trackingState = tracked ? 3 : 0;
            // Joueur pas pile au centre de sa zone de jeu : décalage « x,z » de la préférence (par défaut 0,5 ; 0,35), réglable pour viser un obstacle.
            string[] offset = UnityEditor.EditorPrefs.GetString(VrBootSimulation.OffsetKey, "0.5,0.35").Split(',');
            h.devicePosition = tracked ? new Vector3(float.Parse(offset[0], System.Globalization.CultureInfo.InvariantCulture), 1.65f,
                float.Parse(offset[1], System.Globalization.CultureInfo.InvariantCulture)) : Vector3.zero;
            h.centerEyePosition = h.devicePosition;
            h.leftEyePosition = h.devicePosition + Vector3.left * 0.03f;
            h.rightEyePosition = h.devicePosition + Vector3.right * 0.03f;
            InputSystem.QueueStateEvent(head, h);

            XRSimulatedControllerState l = default;
            l.Reset();
            l.isTracked = tracked;
            l.trackingState = tracked ? 3 : 0;
            l.devicePosition = tracked ? new Vector3(0.25f + shake, 1.15f, 0.35f) : Vector3.zero;
            l.primary2DAxis = LeftStick;
            InputSystem.QueueStateEvent(left, l);

            XRSimulatedControllerState r = default;
            r.Reset();
            r.isTracked = tracked;
            r.trackingState = tracked ? 3 : 0;
            r.devicePosition = tracked ? new Vector3(0.75f + shake, 1.15f, 0.35f) : Vector3.zero;
            InputSystem.QueueStateEvent(right, r);
        }
    }

    public partial class SelfTest
    {
        /// <summary>Active ou coupe la simulation de démarrage VR (prise en compte au prochain Play).</summary>
        public static void PrepareVrBootSimulation(bool on, string headOffset = "0.5,0.35", float trackDelaySeconds = 0f)
        {
            UnityEditor.EditorPrefs.SetBool(VrBootSimulation.PrefKey, on);
            UnityEditor.EditorPrefs.SetString(VrBootSimulation.OffsetKey, headOffset);
            UnityEditor.EditorPrefs.SetFloat(VrBootSimulation.DelayKey, trackDelaySeconds);
        }

        /// <summary>Lance le scénario du joueur qui marche juste après le démarrage VR (le jeu doit avoir démarré avec la simulation).</summary>
        public static SelfTest RunVrBootScenario()
        {
            SelfTest test = new GameObject("SelfTest").AddComponent<SelfTest>();
            test.StartCoroutine(test.VrBootScenario());
            return test;
        }

        private IEnumerator VrBootScenario()
        {
            Running = true;
            LastFailures = -1;
            yield return WaitReady();

            PlayerModeService mode = Services.Get<PlayerModeService>();
            Check(mode.Mode == PlayerMode.VR, $"Le jeu a démarré en VR (mode {mode.Mode})");
            VrBootSimulationPump pump = VrBootSimulationPump.Instance;
            Check(pump != null, "Les appareils XR simulés existent");
            if (pump == null || mode.Mode != PlayerMode.VR)
            {
                Finish("Démarrage VR");
                yield break;
            }

            while (!pump.Tracked)
                yield return null;
            yield return new WaitForSecondsRealtime(1.5f);

            // Variante de reproduction : banc solide comme avant le correctif, puis nouvelle mise en place au camp.
            if (UnityEditor.EditorPrefs.GetBool("ArcheryChallenge.SimulateVrBootSolidBench", false))
            {
                foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
                    if (c.name == "Seat")
                        c.isTrigger = false;
                if (UnityEditor.EditorPrefs.GetFloat(VrBootSimulation.DelayKey, 0f) <= 0f)
                    mode.TeleportToSpawn("camp");
                yield return new WaitForSecondsRealtime(0.5f);
                Check(true, "Variante : banc rendu solide");
            }

            Transform root = mode.XrRig.transform;
            Vector3 start = root.position;
            CharacterController body = mode.XrRig.GetComponent<CharacterController>();
            bool benchSolid = false;
            foreach (Collider c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include))
                if (c.name == "Seat" && !c.isTrigger)
                    benchSolid = true;
            Check(true, $"Banc solide : {benchSolid}");
            Check(true, $"Prêt : joueur en {start}, Character Controller activé {body.enabled}, au sol {body.isGrounded}, hauteur {body.height:0.00}");

            // Le stick gauche téléporte par défaut : ce test vérifie la marche, donc l'option « Déplacement continu » est activée.
            ArcheryChallenge.Save.SettingsService comfort = Services.Get<ArcheryChallenge.Save.SettingsService>();
            if (comfort != null)
            {
                comfort.Data.continuousMove = true;
                comfort.MarkChanged();
            }
            yield return null;
            yield return null;
            pump.LeftStick = new Vector2(0f, 1f);
            StringBuilder log = new StringBuilder();
            float t0 = Time.time;
            float nextLog = 0.5f;
            while (Time.time - t0 < 2f)
            {
                if (Time.time - t0 >= nextLog)
                {
                    nextLog += 0.5f;
                    log.Append($"{Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(root.position.x, 0f, root.position.z)):0.00} m, ");
                }
                yield return null;
            }
            pump.LeftStick = Vector2.zero;
            float moved = Vector3.Distance(new Vector3(start.x, 0f, start.z), new Vector3(root.position.x, 0f, root.position.z));
            if (comfort != null)
            {
                comfort.Data.continuousMove = false;
                comfort.MarkChanged();
            }
            Check(moved > 2f, $"Le joystick gauche déplace le joueur juste après le démarrage ({moved:0.00} m en 2 s ; progression {log}timeScale {Time.timeScale})");
            Finish("Démarrage VR");
        }
    }
}
#endif
