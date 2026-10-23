// TrophyDisplay : trophée du hub (calice sur son socle), activé par la victoire du tournoi.
//
// OÙ LE POSER : sur l'objet « Hub » (toujours actif). RÉGLAGE : « Trophy » ← l'objet désactivé du trophée
// (« Trophy », avec le calice). Le calice devient doré avec le trophée en or (arène 5 avec 3 étoiles).
// À la première victoire, des crédits défilent sous forme de notifications.

using System.Collections;
using ArcheryChallenge.Core;
using ArcheryChallenge.Progression;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Affichage du trophée et des crédits de victoire.</summary>
    [DisallowMultipleComponent]
    public class TrophyDisplay : MonoBehaviour
    {
        [SerializeField] private GameObject trophy;

        private static readonly string[] Credits =
        {
            "Victoire ! Vous êtes le champion du tournoi.",
            "Archery Challenge : projet EFREI, équipe Archery Challengers.",
            "Modèles : GanzSe (armes), Pure Poly, Fantasy Megakit, Glowing Rifts, RPG Monster DUO.",
            "Sons : Kenney, Leohpaz, VR Template.",
            "Merci d'avoir joué !"
        };

        public GameObject Trophy
        {
            get => trophy;
            set => trophy = value;
        }

        private void OnEnable()
        {
            GameEvents.ProgressionChanged += Refresh;
            GameEvents.TrophyUnlocked += OnTrophyUnlocked;
            Refresh();
        }

        private void OnDisable()
        {
            GameEvents.ProgressionChanged -= Refresh;
            GameEvents.TrophyUnlocked -= OnTrophyUnlocked;
        }

        /// <summary>Affiche ou masque le trophée selon la progression.</summary>
        public void Refresh()
        {
            UnlockService unlocks = Services.Get<UnlockService>();
            if (trophy == null || unlocks == null)
                return;
            bool has = unlocks.HasTrophy(UnlockService.TrophyId) || unlocks.HasTrophy(UnlockService.GoldTrophyId);
            if (trophy.activeSelf != has)
                trophy.SetActive(has);
            if (has && unlocks.HasTrophy(UnlockService.GoldTrophyId))
            {
                foreach (Renderer r in trophy.GetComponentsInChildren<Renderer>())
                    if (r.gameObject.name == "Chalice" || r.transform.parent != null && r.transform.parent.name == "Chalice")
                        foreach (Material m in r.materials)
                            m.color = new Color(1f, 0.82f, 0.25f);
            }
        }

        private void OnTrophyUnlocked(string id)
        {
            Refresh();
            if (id == UnlockService.TrophyId)
                StartCoroutine(RollCredits());
        }

        private IEnumerator RollCredits()
        {
            foreach (string line in Credits)
            {
                GameEvents.RaiseToast(line);
                yield return new WaitForSecondsRealtime(3.2f);
            }
        }
    }
}
