// FastTravelPanel : liste des destinations de téléportation rapide (hub + arènes débloquées).
//
// OÙ LE POSER : sur le panneau du prefab « FastTravelPanel » (tableau du hub, menu pause, touche T / bouton Y).
// ÉLÉMENTS ATTENDUS : Content (conteneur à disposition verticale), ButtonTemplate (un bouton modèle avec un
//   texte enfant, désactivé : il est dupliqué pour chaque destination), Message, BtnClose (croix « X » en haut à droite).
//
// Une destination n'apparaît que si son arène est DÉBLOQUÉE (même non réussie). Le voyage se fait avec
// un fondu ; il est refusé pendant une manche en cours.

using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.World;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Panneau de voyage rapide.</summary>
    public class FastTravelPanel : UiPanel
    {
        private Transform content;
        private Button template;
        private TMP_Text message;
        private Button btnClose;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private bool bound;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        private void Bind()
        {
            if (bound) return;
            bound = true;
            content = UiBind.Find(transform, "Content");
            template = UiBind.Button(transform, "ButtonTemplate", false);
            message = UiBind.Text(transform, "Message", false);
            btnClose = UiBind.Button(transform, "BtnClose", false);
            if (template != null)
                template.gameObject.SetActive(false);
            if (btnClose != null)
                btnClose.onClick.AddListener(CloseFromButton);
        }

        private void OnEnable()
        {
            Bind();
            GameEvents.ProgressionChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => GameEvents.ProgressionChanged -= Refresh;

        protected override void OnOpened() => Refresh();

        /// <summary>Reconstruit la liste des boutons.</summary>
        public void Refresh()
        {
            Bind();
            if (content == null || template == null)
                return;

            foreach (GameObject go in spawned)
                if (go != null)
                    Destroy(go);
            spawned.Clear();

            FastTravelService service = Services.Get<FastTravelService>();
            if (service == null)
            {
                UiBind.SetText(message, "Service de voyage indisponible.");
                return;
            }

            List<FastTravelDestination> destinations = service.GetAvailableDestinations();
            UiBind.SetText(message, destinations.Count == 0 ? "Aucune destination disponible." : "Choisissez une destination :");
            foreach (FastTravelDestination destination in destinations)
            {
                Button button = Instantiate(template, content);
                button.gameObject.SetActive(true);
                button.name = "Travel_" + destination.DestinationId;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.text = destination.DisplayName;
                string id = destination.DestinationId;
                button.onClick.AddListener(() => Travel(id));
                spawned.Add(button.gameObject);
            }
        }

        private void Travel(string id)
        {
            FastTravelService service = Services.Get<FastTravelService>();
            if (service == null)
                return;
            if (!service.CanTravel)
            {
                UiBind.SetText(message, "Terminez ou abandonnez la manche d'abord.");
                return;
            }
            Close();
            Services.Get<UiHub>()?.CloseAll();
            service.TravelTo(id);
        }
    }
}
