// DesktopArcherInput : pilote clavier/souris du tireur.
//
// OÙ LE POSER : sur la racine « DesktopPlayer » (à côté de DesktopPlayerController).
//
// COMMANDES (tutoriel §P4.1) :
//   R = prendre et encocher une flèche · clic gauche maintenu = tendre · relâcher = tirer
//   clic droit maintenu = viser (léger zoom) · E = interagir · Échap = menu · T = téléportation rapide
//
// Ce script ne fait que LIRE les touches. DesktopBowView s'en sert pour faire tirer l'arc. Les
// « injections » (Force…) permettent aux tests automatiques de piloter le jeu sans clavier réel.

using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcheryChallenge.Input
{
    /// <summary>Entrées du joueur au clavier et à la souris.</summary>
    [DisallowMultipleComponent]
    public class DesktopArcherInput : MonoBehaviour, IArcherInput
    {
        // Injections pour les tests (null = lecture réelle du clavier).
        private bool? forceFire;
        private bool? forceAim;
        private bool forceNock;
        private bool forceInteract;
        private bool forceMenu;
        private bool forceFastTravel;

        private bool fireWasHeld;

        public bool IsActive => isActiveAndEnabled;
        public PlayerMode Mode => PlayerMode.Desktop;

        public Transform Head
        {
            get
            {
                Camera cam = GetComponentInChildren<Camera>(true);
                return cam != null ? cam.transform : transform;
            }
        }

        /// <summary>Vrai tant que le clic gauche est maintenu (tension de la corde).</summary>
        public bool FireHeld
        {
            get
            {
                if (forceFire.HasValue)
                    return forceFire.Value;
                if (CursorController.IsFreeRequested || Mouse.current == null)
                    return false;
                return Mouse.current.leftButton.isPressed;
            }
        }

        /// <summary>Vrai à l'image où le clic gauche vient d'être enfoncé.</summary>
        public bool FirePressedThisFrame => FireHeld && !fireWasHeld;

        /// <summary>Vrai tant que le clic droit est maintenu (visée).</summary>
        public bool AimHeld
        {
            get
            {
                if (forceAim.HasValue)
                    return forceAim.Value;
                if (CursorController.IsFreeRequested || Mouse.current == null)
                    return false;
                return Mouse.current.rightButton.isPressed;
            }
        }

        /// <summary>Touche R : prendre et encocher une flèche.</summary>
        public bool NockPressedThisFrame =>
            forceNock || (!CursorController.IsFreeRequested && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);

        public bool InteractPressedThisFrame =>
            forceInteract || (!CursorController.IsFreeRequested && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

        public bool MenuPressedThisFrame =>
            forceMenu || (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame);

        public bool FastTravelPressedThisFrame =>
            forceFastTravel || (!CursorController.IsFreeRequested && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame);

        private void OnEnable()
        {
            Services.Register<IArcherInput>(this);
            Services.Register(this);
        }

        private void OnDisable()
        {
            Services.Unregister<IArcherInput>(this);
            Services.Unregister(this);
        }

        private void LateUpdate()
        {
            fireWasHeld = FireHeld;
            // Une injection reste lisible pendant toute l'image suivant celle où elle a été posée.
            int frame = Time.frameCount;
            if (forceNock && frame > forceFrame) forceNock = false;
            if (forceInteract && frame > forceFrame) forceInteract = false;
            if (forceMenu && frame > forceFrame) forceMenu = false;
            if (forceFastTravel && frame > forceFrame) forceFastTravel = false;
        }

        private int forceFrame;

        // ----- Injections de test -----
        public void ForceFire(bool? held) => forceFire = held;
        public void ForceAim(bool? held) => forceAim = held;
        public void ForceNock() { forceNock = true; forceFrame = Time.frameCount; }
        public void ForceInteract() { forceInteract = true; forceFrame = Time.frameCount; }
        public void ForceMenu() { forceMenu = true; forceFrame = Time.frameCount; }
        public void ForceFastTravel() { forceFastTravel = true; forceFrame = Time.frameCount; }
    }
}
