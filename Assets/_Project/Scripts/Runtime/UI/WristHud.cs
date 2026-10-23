// WristHud : montre au poignet gauche (VR) : on tourne le poignet vers soi pour voir score, temps, PV.
//
// OÙ LE POSER : sur le Canvas du prefab « UI_Global/WristHud » (à côté de WorldCanvasCameraBinder).
// RÉGLAGE : « Wrist Anchor » ← XR Origin / Camera Offset / Left Controller (voir §P12.6).
// Le panneau est collé au poignet (décalage réglable) et n'apparaît que lorsque le dessus de la
// manette regarde la tête (angle < 55°). Au clavier/souris ce composant n'affiche rien.

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>HUD attaché au poignet.</summary>
    public class WristHud : HudController
    {
        [SerializeField] private Transform wristAnchor;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.06f, -0.04f);
        [SerializeField] private Vector3 localEuler = new Vector3(60f, 0f, 0f);
        [SerializeField, Range(10f, 90f)] private float showAngle = 55f;
        [SerializeField, Min(0.0001f)] private float scale = 0.00035f;

        private Canvas canvas;

        public Transform WristAnchor
        {
            get => wristAnchor;
            set => wristAnchor = value;
        }

        protected override void Awake()
        {
            base.Awake();
            canvas = GetComponent<Canvas>();
        }

        protected override bool ShouldShow()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            return mode != null && mode.IsHeadset && IsWristRaised();
        }

        private bool IsWristRaised()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (wristAnchor == null || head == null)
                return false;
            Vector3 toHead = (head.position - wristAnchor.position).normalized;
            return Vector3.Angle(wristAnchor.up, toHead) < showAngle;
        }

        protected override void Update()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            bool vr = mode != null && mode.IsHeadset;
            if (canvas != null && canvas.enabled != vr)
                canvas.enabled = vr;
            if (!vr || wristAnchor == null)
                return;

            transform.SetPositionAndRotation(
                wristAnchor.TransformPoint(localOffset),
                wristAnchor.rotation * Quaternion.Euler(localEuler));
            transform.localScale = Vector3.one * scale;

            bool raised = IsWristRaised();
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, raised ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            base.Update();
        }
    }
}
