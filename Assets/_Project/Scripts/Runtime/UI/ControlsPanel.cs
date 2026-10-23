// ControlsPanel : écran « Commandes » : toutes les touches et gestes du jeu, en trois pages.
//
// OÙ LE POSER : sur le Canvas du prefab « ControlsPanel ». Une instance désactivée vit dans UI_Global (ouverte par
// F1, par la touche H au clavier, ou par le bouton « Commandes » des menus) ; deux autres instances servent de
// tableaux au camp et au hub (toujours visibles, pour lire les gestes VR sans ouvrir de menu).
// ÉLÉMENTS ATTENDUS : Title, BtnPageGame, BtnPageVr, BtnPageSimulator, BtnPageDev, BtnClose, Row0 … Row15 (chacune avec
//   Action, Desktop, Vr), HeadAction, HeadDesktop, HeadVr.
// Le contenu vient de ControlsCatalog : jamais de texte à modifier ici. La page « Développement » n'existe que
// dans l'éditeur et les builds de développement.

using ArcheryChallenge.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcheryChallenge.UI
{
    /// <summary>Panneau listant les commandes du jeu.</summary>
    public class ControlsPanel : UiPanel
    {
        /// <summary>Nombre de lignes du tableau (au moins la plus longue des pages).</summary>
        public const int RowCount = 16;

        private enum Page
        {
            Game,
            Simulator,
            Dev,
            Vr
        }

        private struct Row
        {
            public GameObject Root;
            public TMP_Text Action, Desktop, Vr;
        }

        private readonly Row[] rows = new Row[RowCount];
        private TMP_Text title, headDesktop, headVr;
        private Button pageGame, pageVr, pageSimulator, pageDev, close;
        private Page current = Page.Game;
        private bool bound;

        /// <summary>Page actuellement affichée (0 = jeu, 1 = simulateur, 2 = développement, 3 = gameplay VR).</summary>
        public int CurrentPage => (int)current;

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        private void Bind()
        {
            if (bound)
                return;
            bound = true;
            title = UiBind.Text(transform, "Title", false);
            headDesktop = UiBind.Text(transform, "HeadDesktop", false);
            headVr = UiBind.Text(transform, "HeadVr", false);
            for (int i = 0; i < RowCount; i++)
            {
                Transform row = UiBind.Find(transform, "Row" + i);
                if (row == null)
                    continue;
                rows[i] = new Row
                {
                    Root = row.gameObject,
                    Action = UiBind.Text(row, "Action", false),
                    Desktop = UiBind.Text(row, "Desktop", false),
                    Vr = UiBind.Text(row, "Vr", false)
                };
            }

            pageGame = UiBind.Button(transform, "BtnPageGame", false);
            pageVr = UiBind.Button(transform, "BtnPageVr", false);
            pageSimulator = UiBind.Button(transform, "BtnPageSimulator", false);
            pageDev = UiBind.Button(transform, "BtnPageDev", false);
            close = UiBind.Button(transform, "BtnClose", false);
            if (pageGame != null) pageGame.onClick.AddListener(() => Show(Page.Game));
            if (pageVr != null) pageVr.onClick.AddListener(() => Show(Page.Vr));
            if (pageSimulator != null) pageSimulator.onClick.AddListener(() => Show(Page.Simulator));
            if (pageDev != null)
            {
                pageDev.onClick.AddListener(() => Show(Page.Dev));
                pageDev.gameObject.SetActive(DevShortcuts.Allowed);
            }
            if (close != null) close.onClick.AddListener(CloseFromButton);
        }

        private void Start() => Show(Page.Game);

        protected override void OnOpened() => Show(Page.Game);

        /// <summary>Affiche une page (0 = jeu, 1 = simulateur, 2 = développement, 3 = gameplay VR).</summary>
        public void ShowPage(int page) => Show((Page)Mathf.Clamp(page, 0, 3));

        private void Show(Page page)
        {
            Bind();
            if (page == Page.Dev && !DevShortcuts.Allowed)
                page = Page.Game;
            current = page;

            ControlEntry[] entries = page == Page.Game ? ControlsCatalog.Game : page == Page.Simulator ? ControlsCatalog.Simulator
                : page == Page.Vr ? ControlsCatalog.VrGameplay : ControlsCatalog.Dev;
            bool twoColumns = page == Page.Game || page == Page.Vr;
            UiBind.SetText(title, page == Page.Game ? "Commandes" : page == Page.Simulator ? "Commandes : simulateur XR"
                : page == Page.Vr ? "Commandes : gameplay en VR (manettes Quest)" : "Commandes : développement");
            UiBind.SetText(headDesktop, page == Page.Vr ? "Bouton / geste" : "Clavier / souris");
            UiBind.SetText(headVr, page == Page.Vr ? "Précision" : twoColumns ? "Manettes VR (Quest)" : "");

            for (int i = 0; i < RowCount; i++)
            {
                Row row = rows[i];
                if (row.Root == null)
                    continue;
                bool used = i < entries.Length;
                if (row.Root.activeSelf != used)
                    row.Root.SetActive(used);
                if (!used)
                    continue;
                UiBind.SetText(row.Action, entries[i].Action);
                UiBind.SetText(row.Desktop, entries[i].Desktop);
                UiBind.SetText(row.Vr, twoColumns ? entries[i].Vr : "");
            }
        }
    }
}
