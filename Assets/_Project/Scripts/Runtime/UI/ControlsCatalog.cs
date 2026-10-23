// ControlsCatalog : liste unique de TOUTES les commandes du jeu (clavier/souris, manettes VR, simulateur, développement).
//
// OÙ LE POSER : n'est pas un composant (données statiques).
//
// Cette liste alimente le panneau « Commandes » (ControlsPanel), les tableaux du camp et du hub, la barre
// d'aide en bas de l'écran (DesktopHud) et la légende de développement (DevShortcuts) : pour ajouter ou changer
// une touche, on ne modifie qu'ICI et tout l'affichage suit.

namespace ArcheryChallenge.UI
{
    /// <summary>Une commande : ce qu'elle fait et comment la déclencher.</summary>
    public readonly struct ControlEntry
    {
        /// <summary>Fonction (« Prendre une flèche »).</summary>
        public readonly string Action;

        /// <summary>Touche ou bouton au clavier/souris (vide si sans objet).</summary>
        public readonly string Desktop;

        /// <summary>Geste ou bouton avec les manettes VR (vide si sans objet).</summary>
        public readonly string Vr;

        public ControlEntry(string action, string desktop, string vr)
        {
            Action = action;
            Desktop = desktop;
            Vr = vr;
        }
    }

    /// <summary>Catalogue des commandes.</summary>
    public static class ControlsCatalog
    {
        /// <summary>Commandes de jeu (toutes versions).</summary>
        public static readonly ControlEntry[] Game =
        {
            new ControlEntry("Se déplacer (allure rapide)", "Z Q S D  (ou W A S D)", "Stick gauche (sprint par défaut)"),
            new ControlEntry("Regarder / tourner", "Souris", "Tourner la tête · stick droit (pas de 45°)"),
            new ControlEntry("Marcher doucement", "Maj (maintenu)", "Régler la vitesse : Réglages"),
            new ControlEntry("Sauter", "Espace", "—"),
            new ControlEntry("Se téléporter", "T (voyage rapide)", "Stick droit vers l'avant, viser, relâcher"),
            new ControlEntry("Prendre l'arc", "Automatique (arc équipé)", "Main d'arc (droite) près de l'épaule + Grip"),
            new ControlEntry("Prendre une flèche", "R (prend et encoche)", "Main libre dans le dos + Grip"),
            new ControlEntry("Encocher", "Automatique avec R", "Approcher la flèche de la corde"),
            new ControlEntry("Tendre la corde", "Clic gauche maintenu", "Reculer la main en gardant le Grip"),
            new ControlEntry("Tirer", "Relâcher le clic gauche", "Relâcher le Grip"),
            new ControlEntry("Viser (zoom)", "Clic droit maintenu", "—"),
            new ControlEntry("Interagir (râtelier, panneaux)", "E (en regardant l'objet)", "Rayon + gâchette ou toucher du doigt"),
            new ControlEntry("Cliquer sur un écran flottant", "Souris (curseur libéré) · Alt", "Rayon + gâchette ou toucher du doigt"),
            new ControlEntry("Menu / pause", "Échap", "Bouton Menu (manette gauche)"),
            new ControlEntry("Téléportation rapide", "T", "Bouton Y (manette gauche)"),
            new ControlEntry("Afficher les commandes", "F1 (ou H)", "Menu → Commandes")
        };

        /// <summary>Commandes du rig VR simulé (touche F2 en développement) : mains virtuelles pilotées au clavier/souris.</summary>
        public static readonly ControlEntry[] Simulator =
        {
            new ControlEntry("Passer du clavier au rig VR simulé (et retour)", "Maj + F2 (F2 sans casque)", ""),
            new ControlEntry("Se déplacer", "Z Q S D (ou W A S D) · Maj marche doucement · Espace saute", ""),
            new ControlEntry("Regarder", "Souris (Alt : libérer le curseur)", ""),
            new ControlEntry("Arc", "Tenu automatiquement par la main d'arc", ""),
            new ControlEntry("Prendre une flèche au carquois", "R (la main libre va dans le dos)", ""),
            new ControlEntry("Tendre la corde", "Clic gauche maintenu (encochage auto : prend la flèche)", ""),
            new ControlEntry("Tirer", "Relâcher le clic gauche", ""),
            new ControlEntry("Viser (zoom)", "Clic droit maintenu", ""),
            new ControlEntry("Interagir (râtelier, panneaux)", "E (en regardant l'objet)", ""),
            new ControlEntry("Cliquer sur un écran flottant", "Souris (le curseur se libère à l'ouverture)", ""),
            new ControlEntry("Menu / voyage rapide / commandes", "Échap · T · F1 ou H", ""),
            new ControlEntry("Simulateur XRI d'origine", "Case « Use Stock Simulator » de PlayerModeService", "")
        };

        /// <summary>
        /// Gameplay réel en VR (manettes Quest). Colonnes : action, bouton ou geste (colonne « Desktop » du tableau), précision
        /// (colonne « Vr »). Affichée par la page « Gameplay VR » de l'écran Commandes.
        /// </summary>
        public static readonly ControlEntry[] VrGameplay =
        {
            new ControlEntry("Se déplacer", "Stick gauche", "Allure rapide (sprint) par défaut"),
            new ControlEntry("Tourner", "Stick droit ← →", "Pas de 45° · ou tourner la tête"),
            new ControlEntry("Voyage rapide", "Bouton Y (main gauche)", "Ouvre la liste des lieux"),
            new ControlEntry("Menu / pause", "Bouton Menu (main gauche)", "Reprendre : la croix X"),
            new ControlEntry("Cliquer sur un écran", "Rayon + gâchette", "ou toucher du doigt"),
            new ControlEntry("Prendre l'arc", "Grip de la main droite près de l'épaule", "Il revient à l'épaule si lâché"),
            new ControlEntry("Prendre une flèche", "Grip de la main gauche, dans le dos", "Carquois dorsal, zone large"),
            new ControlEntry("Encocher", "Flèche près de la corde", "Automatique si l'option est active"),
            new ControlEntry("Tendre la corde", "Reculer la main, Grip serré", "Plus on recule, plus le tir est fort"),
            new ControlEntry("Viser", "Regarder le long de la flèche", "Le tir part dans l'axe de l'arc"),
            new ControlEntry("Tirer", "Relâcher le Grip", "La flèche part de l'arc"),
            new ControlEntry("Lâcher l'arc", "Relâcher le Grip (main d'arc)", "Retour à l'épaule en 0,5 s"),
            new ControlEntry("Interagir", "Rayon + gâchette", "Râtelier, panneaux, portes"),
            new ControlEntry("Arc dans la main gauche", "Menu → Réglages → Confort", "Inverse l'arc (droite par défaut) et les flèches"),
            new ControlEntry("Revoir ces commandes", "Menu → Commandes", "Aussi sur les panneaux du camp")
        };

        /// <summary>Raccourcis de développement (éditeur et builds « Development » uniquement).</summary>
        public static readonly ControlEntry[] Dev =
        {
            new ControlEntry("Écran des commandes (Maj + F1 : légende)", "F1", ""),
            new ControlEntry("Casque VR ↔ clavier/souris", "F2", ""),
            new ControlEntry("Rig VR simulé (sans casque) ↔ clavier/souris", "Maj + F2", ""),
            new ControlEntry("Heure du jour +1 h (−1 h)", "F3 (Maj + F3)", ""),
            new ControlEntry("Vitesse du jour × 1 / 10 / 60", "F4", ""),
            new ControlEntry("Tout débloquer (mode démo)", "F5", ""),
            new ControlEntry("Finir l'arène avec 3 étoiles", "F6", ""),
            new ControlEntry("Aller à l'arène suivante", "F7", ""),
            new ControlEntry("Faire apparaître un monstre", "F8", ""),
            new ControlEntry("Afficher FPS et statistiques", "F9", ""),
            new ControlEntry("Afficher le vent", "F10", ""),
            new ControlEntry("Changer de difficulté", "F11", "")
        };

        /// <summary>Barre d'aide permanente en bas de l'écran (clavier/souris).</summary>
        public const string DesktopBar =
            "<b>Z Q S D</b> avancer · <b>Maj</b> marcher doucement · <b>R</b> flèche · <b>Clic G</b> tendre/tirer · <b>Clic D</b> viser · " +
            "<b>E</b> interagir · <b>T</b> voyage rapide · <b>Échap</b> menu · <b>F1</b> commandes";

        /// <summary>Barre d'aide du rig VR simulé (F2) : mêmes touches qu'au clavier/souris, plus le retour.</summary>
        public const string SimulatorBar =
            "<b>VR simulé</b> · <b>Z Q S D</b> marcher · <b>R</b> flèche (carquois) · <b>Clic G</b> tendre/tirer · <b>Clic D</b> viser · " +
            "<b>E</b> interagir · <b>T</b> voyage rapide · <b>Échap</b> menu · <b>F1</b> commandes · <b>F2</b> retour clavier";

        /// <summary>Légende de développement : une ligne compacte par groupe de touches.</summary>
        public static string DevLegend()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>Touches de développement</b> (Maj + F1 : masquer)");
            foreach (ControlEntry e in Dev)
                sb.AppendLine($"<b>{e.Desktop}</b>  {e.Action}");
            sb.AppendLine("<b>Autres</b>  H : commandes · Alt : libérer le curseur");
            return sb.ToString().TrimEnd();
        }
    }
}
