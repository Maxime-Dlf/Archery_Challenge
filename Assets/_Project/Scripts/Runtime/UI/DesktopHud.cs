// DesktopHud : incrustation plein écran (clavier/souris) : score, temps, flèches, vent, PV, réticule et aides de touches.
//
// OÙ LE POSER : sur le Canvas du prefab « UI_Global/DesktopHud » (Screen Space – Overlay).
// ÉLÉMENTS : ceux de HudController + « Crosshair » (point central), « TensionBar » (barre de tension de
// la corde, Image de type Filled), « Prompt » (astuce passagère), « KeyBar » (bande permanente des touches,
// en bas de l'écran) et « ContextHint » (la touche utile ici et maintenant : « E : équiper l'arc Chasse »,
// « R : prendre une flèche »…).
// Apparaît en mode clavier/souris ET avec le rig VR simulé (F2, voir SimulatedRigDriver) : le bandeau de touches
// reste alors affiché en permanence ; il disparaît seulement avec un vrai casque (l'incrustation n'existe pas en VR).

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Input;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Player;
using ArcheryChallenge.Progression;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>HUD du mode clavier/souris.</summary>
    public class DesktopHud : HudController
    {
        private Canvas canvas;
        private Image tensionBar;
        private TMP_Text prompt;
        private TMP_Text keyBar;
        private TMP_Text contextHint;
        private RectTransform crosshair;
        private DesktopBowView bowView;
        private float promptUntil;
        private bool shownTips;
        private PlayerMode barMode = (PlayerMode)(-1);
        private RectTransform safeArea;

        [Tooltip("Taille de l'ATH d'écran (informations en haut à gauche, PV en bas à gauche) : 1 = taille de la maquette, 0,5 = discret.")]
        [SerializeField, Range(0.3f, 1f)] private float hudScale = 0.5f;

        /// <summary>Aide contextuelle actuellement affichée (vide si aucune).</summary>
        public string CurrentContextHint { get; private set; } = "";

        protected override void Awake()
        {
            base.Awake();
            canvas = GetComponent<Canvas>();
            Transform bar = UiBind.Find(transform, "TensionBar");
            tensionBar = bar != null ? bar.GetComponent<Image>() : null;
            prompt = UiBind.Text(transform, "Prompt", false);
            keyBar = UiBind.Text(transform, "KeyBar", false);
            contextHint = UiBind.Text(transform, "ContextHint", false);
            if (keyBar != null)
            {
                keyBar.text = ControlsCatalog.DesktopBar;
                // La bande s'adapte à la largeur de la fenêtre (fenêtre Game étroite, casque simulé) au lieu de déborder.
                keyBar.enableAutoSizing = true;
                keyBar.fontSizeMax = 22f;
                keyBar.fontSizeMin = 10f;
            }

            // Astuces et aide contextuelle : une seule ligne qui rétrécit pour tenir dans l'écran (fenêtre étroite ou haute).
            foreach (TMP_Text line in new[] { prompt, contextHint })
            {
                if (line == null)
                    continue;
                line.textWrappingMode = TextWrappingModes.NoWrap;
                line.enableAutoSizing = true;
                line.fontSizeMax = line.fontSize;
                line.fontSizeMin = Mathf.Max(10f, line.fontSize * 0.45f);
            }

            BuildSafeArea();

            // Le réticule est ancré au CENTRE de l'écran (quel que soit le format) pour pouvoir trembler autour de lui.
            crosshair = UiBind.Find(transform, "Crosshair") as RectTransform;
            if (crosshair != null)
            {
                crosshair.anchorMin = crosshair.anchorMax = crosshair.pivot = new Vector2(0.5f, 0.5f);
                crosshair.anchoredPosition = Vector2.zero;
            }
        }

        // Tous les éléments collés aux bords (score, PV, bande de touches, astuces…) sont rangés dans un conteneur qui épouse la
        // PARTIE VISIBLE de l'écran (voir ScreenViewport) : si la fenêtre Game est zoomée ou tronquée, l'interface reste entière.
        // Le réticule reste directement sous le Canvas : il doit rester au centre exact de l'image.
        private void BuildSafeArea()
        {
            GameObject container = new GameObject("SafeArea", typeof(RectTransform));
            safeArea = (RectTransform)container.transform;
            safeArea.SetParent(transform, false);
            List<Transform> movers = new List<Transform>();
            foreach (Transform child in transform)
                if (child != safeArea && child.name != "Crosshair")
                    movers.Add(child);
            foreach (Transform child in movers)
                child.SetParent(safeArea, false);
            safeArea.SetAsFirstSibling();
            // ATH discret et conventionnel : informations de manche en petit en haut à gauche, vie en petit en bas à gauche
            // (les deux ont leur pivot dans le coin : la réduction les laisse collés à leur coin).
            foreach (string element in new[] { "StatePanel", "HeartsRow" })
            {
                Transform t = UiBind.Find(transform, element);
                if (t != null)
                    t.localScale = Vector3.one * hudScale;
            }
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            if (safeArea == null)
                return;
            Rect visible = ScreenViewport.Visible;
            safeArea.anchorMin = visible.min;
            safeArea.anchorMax = visible.max;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Conteneur des éléments d'écran (suit la zone visible).</summary>
        public RectTransform SafeArea => safeArea;

        protected override bool ShouldShow()
        {
            LevelController level = Level;
            return level != null && (level.State == LevelState.Countdown || level.State == LevelState.Running) && !level.IsTraining;
        }

        protected override void Update()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            bool desktop = mode != null && mode.UsesMouse;
            if (canvas != null && canvas.enabled != desktop)
                canvas.enabled = desktop;
            if (!desktop)
                return;

            if (keyBar != null && barMode != mode.Mode)
            {
                barMode = mode.Mode;
                keyBar.text = mode.Mode == PlayerMode.Simulator ? ControlsCatalog.SimulatorBar : ControlsCatalog.DesktopBar;
            }

            ApplySafeArea();
            base.Update();

            if (bowView == null)
                bowView = FindAnyObjectByType<DesktopBowView>();
            if (tensionBar != null)
            {
                Bow current = CurrentBow();
                float t = current != null ? current.Tension : 0f;
                tensionBar.fillAmount = t;
                tensionBar.gameObject.SetActive(t > 0.01f);
            }

            UpdateContextHint();
            UpdateCrosshairSway();

            if (prompt != null)
            {
                // Un écran flottant est ouvert : les astuces passagères attendent qu'il soit refermé.
                if (CursorController.IsFreeRequested && prompt.gameObject.activeSelf)
                    prompt.gameObject.SetActive(false);
                if (!shownTips && Time.unscaledTime > 3f && !CursorController.IsFreeRequested)
                {
                    shownTips = true;
                    ShowPrompt("R : prendre une flèche · clic gauche maintenu : tendre · relâcher : tirer", 6f);
                }
                if (prompt.gameObject.activeSelf && Time.unscaledTime >= promptUntil)
                    prompt.gameObject.SetActive(false);
            }
        }

        // Le réticule suit le tremblement de la visée (AimSway) : la flèche part là où il se trouve.
        private void UpdateCrosshairSway()
        {
            if (crosshair == null)
                return;
            AimSway sway = Services.Get<AimSway>();
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Camera cam = mode != null && mode.ActiveHead != null ? mode.ActiveHead.GetComponent<Camera>() : null;
            if (sway == null || cam == null || !sway.IsActive || canvas == null)
            {
                crosshair.anchoredPosition = Vector2.zero;
                return;
            }
            // Angle → pixels d'écran (projection perspective), puis pixels → unités du Canvas.
            float halfHeightPixels = Screen.height * 0.5f;
            float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector2 pixels = new Vector2(Mathf.Tan(sway.Offset.x * Mathf.Deg2Rad), Mathf.Tan(sway.Offset.y * Mathf.Deg2Rad)) * (halfHeightPixels / tanHalfFov);
            crosshair.anchoredPosition = pixels / Mathf.Max(0.01f, canvas.scaleFactor);
        }

        // Cherche la touche utile à cet instant : râtelier ou tableau regardé, sinon geste de tir en manche.
        private void UpdateContextHint()
        {
            string hint = "";
            IArcherInput input = Services.Get<IArcherInput>();
            Transform head = input != null ? input.Head : null;
            if (head != null && !CursorController.IsFreeRequested)
            {
                hint = ArmoryRack.FindHint(head);
                if (hint.Length == 0)
                    hint = PanelOpener.FindHint(head);
                if (hint.Length == 0)
                    hint = ShootingHint();
            }

            CurrentContextHint = hint;
            if (contextHint == null)
                return;
            bool show = hint.Length > 0;
            if (contextHint.gameObject.activeSelf != show)
                contextHint.gameObject.SetActive(show);
            if (show)
                UiBind.SetText(contextHint, hint);
        }

        // Arc affiché : celui de la vue clavier/souris, ou celui tenu par la main d'arc du rig VR simulé.
        private Bow CurrentBow()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (mode != null && mode.Mode == PlayerMode.Simulator)
            {
                XRArcherInput xr = Services.Get<XRArcherInput>();
                return xr != null && xr.BowHolster != null ? xr.BowHolster.CurrentBow : null;
            }
            return bowView != null ? bowView.CurrentBow : null;
        }

        private string ShootingHint()
        {
            LevelController level = Level;
            Bow bow = CurrentBow();
            if (level == null || bow == null || !bow.IsHeld || level.State != LevelState.Running)
                return "";
            if (!bow.HasArrow)
                return ArrowAllowance.CanTake ? "R : prendre une flèche" : "";
            return bow.Tension > 0.01f ? "Relâchez le clic gauche pour tirer" : "Clic gauche maintenu : tendre la corde";
        }

        /// <summary>Affiche une aide à l'écran quelques secondes.</summary>
        public void ShowPrompt(string text, float seconds)
        {
            if (prompt == null)
                return;
            prompt.text = text;
            prompt.gameObject.SetActive(true);
            promptUntil = Time.unscaledTime + seconds;
        }
    }
}
