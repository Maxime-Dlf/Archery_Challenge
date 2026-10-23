// FastTravelService : téléportation rapide vers le hub et les arènes débloquées.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// Déclenchée par le panneau du hub, le menu pause ou la touche T. Le voyage se fait avec un fondu :
//   fondu au noir → arène chargée si nécessaire → joueur déplacé → fondu d'entrée.
// Pendant une manche, la téléportation rapide est refusée (il faut d'abord abandonner depuis la pause).

using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Service de voyage rapide.</summary>
    [DisallowMultipleComponent]
    public class FastTravelService : MonoBehaviour
    {
        private bool traveling;

        /// <summary>Vrai pendant un voyage.</summary>
        public bool IsTraveling => traveling;

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        /// <summary>Destinations actuellement disponibles, triées par identifiant d'arène.</summary>
        public List<FastTravelDestination> GetAvailableDestinations()
        {
            List<FastTravelDestination> list = new List<FastTravelDestination>();
            foreach (FastTravelDestination d in FastTravelDestination.All)
                if (d != null && d.IsAvailable)
                    list.Add(d);
            list.Sort((a, b) => Order(a).CompareTo(Order(b)));
            return list;
        }

        private static int Order(FastTravelDestination d)
        {
            if (d.DestinationId == "hub") return -2;
            if (d.DestinationId == "training") return -1;
            return d.RequiredLevel != null ? d.RequiredLevel.order : 99;
        }

        /// <summary>Peut-on voyager maintenant ? (pas pendant une manche en cours)</summary>
        public bool CanTravel
        {
            get
            {
                if (traveling) return false;
                LevelController active = LevelController.Active;
                return active == null || active.IsTraining || active.State == LevelState.Briefing || active.State == LevelState.Finished;
            }
        }

        /// <summary>Lance le voyage vers une destination (ou un point d'apparition de même nom).</summary>
        /// <param name="destinationId">Identifiant (hub, training, arena_01…).</param>
        /// <param name="force">Ignore les verrous (raccourci de développement F7).</param>
        public Coroutine TravelTo(string destinationId, bool force = false)
        {
            if (traveling)
                return null;
            return StartCoroutine(TravelRoutine(destinationId, force));
        }

        private IEnumerator TravelRoutine(string destinationId, bool force)
        {
            FastTravelDestination destination = FastTravelDestination.Find(destinationId);
            Pose? target = null;

            if (destination != null)
            {
                if (!force && !destination.IsAvailable)
                {
                    GameEvents.RaiseToast("Destination verrouillée.");
                    yield break;
                }
                if (!force && !CanTravel)
                {
                    GameEvents.RaiseToast("Terminez ou abandonnez la manche d'abord.");
                    yield break;
                }
                target = destination.ArrivalPose;
            }
            else if (PlayerSpawnPoint.TryFind(destinationId, out PlayerSpawnPoint spawn))
            {
                target = spawn.SpawnPose;
            }

            if (target == null)
            {
                GameEvents.RaiseToast("Destination introuvable : " + destinationId);
                yield break;
            }

            traveling = true;
            // Quitter proprement une éventuelle session d'arène avant de partir.
            LevelController active = LevelController.Active;
            if (active != null && active.State != LevelState.Running)
                active.Abort();

            FadeService fade = Services.Get<FadeService>();
            PlayerModeService mode = Services.Get<PlayerModeService>();
            if (fade != null)
                yield return fade.FadeOut(0.3f);

            if (mode != null)
                mode.TeleportTo(target.Value);

            // Laisse une image à la scène d'arène (ArenaPortal) pour se charger et se mettre en place.
            yield return null;
            yield return null;
            if (mode != null)
                mode.TeleportTo(target.Value);

            if (fade != null)
                yield return fade.FadeIn(0.4f);

            GameEvents.RaiseFastTravelled(destinationId);
            traveling = false;
        }
    }
}
