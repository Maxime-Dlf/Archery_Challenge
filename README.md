# Archery Challenge

Jeu de tir à l'arc en **réalité virtuelle** pour **Meta Quest 2 et 3**, également **jouable au clavier et à la souris** sur PC.
Projet de groupe réalisé avec **Unity 6** (6000.5.10f1), URP, OpenXR et XR Interaction Toolkit 3.5.1.


## Le jeu

Le joueur commence assis devant un feu de camp, dans un petit campement en forêt. Il choisit son profil et l'un de ses trois emplacements de sauvegarde, puis rejoint le village des archers : menu, râtelier d'armes, tableau des scores, téléportation rapide et stand d'entraînement.

Un chemin relie cinq arènes de difficulté croissante : **la Clairière**, **la Forêt champêtre**, **la Rivière**, **le Ravin** et **le Tournoi**. Chaque arène est une manche chronométrée notée de 0 à 3 étoiles. Les réussites ouvrent la suite du chemin et débloquent de meilleurs arcs et de meilleures flèches.

Fonctionnalités :

- **Tir physique** : prise de la flèche dans le carquois dorsal, encochage, tension de la corde, relâchement. Trajectoire gérée par le moteur physique (gravité, traînée, vent).
- **6 arcs et 3 types de flèches** aux caractéristiques distinctes, débloqués au fil de la progression.
- **Cibles à 10 zones** de score calculées depuis le point d'impact, cibles mobiles, pop-up et pénalisantes ; score et combo en temps réel.
- **Cycle des cibles** : une cible touchée se baisse et devient inerte jusqu'à ce que toutes les cibles de la mission aient été touchées ; elles se relèvent alors toutes ensemble.
- **Visée qui tremble** : le réticule bouge de façon aléatoire quand la corde est tendue ; l'amplitude dépend du vent, du temps passé à pleine tension (fatigue) et de la difficulté.
- **Trois niveaux de difficulté** (Facile, Normal, Difficile) : taille et vitesse des cibles, temps, nombre de flèches, vent, aides au tir.
- **Mode entraînement** libre, avec tous les équipements et des statistiques de tir.
- **Monde ouvert** de 160 × 160 m avec **cycle jour/nuit** et des **monstres** qui rôdent.
- **Profils** multiples et **3 emplacements de sauvegarde** par profil, **classement** trié des meilleurs scores, saisie du nom au **clavier virtuel**. Données enregistrées en **JSON**.
- **Sons spatialisés** (tension, relâchement, vol, impacts, ambiances).

## Installation

Prérequis :

- [Unity Hub](https://unity.com/download) et l'éditeur **6000.5.10f1** avec les modules **Android Build Support** (OpenJDK, Android SDK & NDK) et **Windows Build Support (IL2CPP)** ;
- [Git](https://git-scm.com/download/win) et **Git LFS** (`git lfs install`).

```bash
git clone https://github.com/Maxime-Dlf/Archery_Challenge.git
cd Archery_Challenge
git checkout develop
```

Ouvrir ensuite le dossier avec Unity Hub (**Add → Add project from disk**). Le premier import prend plusieurs minutes.

Pour jouer dans l'éditeur, ouvrir `Assets/_Project/Scenes/Boot.unity` puis **Play** : sans casque, le jeu démarre en mode clavier/souris.

## Contrôles

### VR (manettes Quest)

| Action | Geste |
|---|---|
| Prendre l'arc | Grip près de l'épaule gauche |
| Prendre une flèche | main libre dans le dos + Grip |
| Encocher, tendre, tirer | approcher la flèche de la corde, reculer la main en gardant Grip, relâcher |
| Se déplacer / tourner | stick gauche / stick droit (pas de 45°) |
| Se téléporter | stick droit vers l'avant, viser, relâcher |
| Interface | rayon + gâchette, ou toucher du doigt |
| Pause | bouton Menu de la manette gauche |

### Clavier / souris

| Action | Touche |
|---|---|
| Se déplacer | `ZQSD` (AZERTY) / `WASD` (QWERTY) |
| Courir / sauter | `Maj` / `Espace` |
| Prendre et encocher une flèche | `R` |
| Tendre / tirer | maintenir / relâcher le clic gauche |
| Viser | clic droit |
| Interagir | `E` |
| Téléportation rapide | `T` |
| Pause | `Échap` |

### Rig VR sans casque (`F2`, éditeur)

`F2` bascule entre le clavier/souris et le rig VR joué sans casque : tête à hauteur normale, arc tenu en main, mains virtuelles pilotées par les **mêmes touches qu'au clavier/souris** (`ZQSD` marcher, souris pour regarder, `R` la main libre prend une flèche dans le carquois dorsal, clic gauche maintenu pour tendre la corde, relâcher pour tirer, clic droit pour viser, `E` interagir, `Échap` menu). Les écrans flottants se cliquent à la souris et la bande de touches reste affichée en bas de l'écran. Le simulateur XRI d'origine reste disponible en cochant « Use Stock Simulator » sur `PlayerModeService`.

Raccourcis de développement (éditeur et builds *Development*) : `F1` aide, `F2` rig VR sans casque, `F3`/`F4` heure et vitesse du cycle jour/nuit, `F5` tout débloquer, `F6` terminer l'arène, `F7` arène suivante, `F8` faire apparaître un monstre, `F9` statistiques, `F10` débogage du vent, `F11` difficulté.

## Organisation du projet

```
Assets/
├── _Project/          contenu propre au jeu
│   ├── Art/           matériaux, modèles, textures, terrain, animations
│   ├── Audio/         mixer, musiques, ambiances, effets
│   ├── Data/          ScriptableObjects d'équilibrage (arcs, flèches, niveaux, difficultés...)
│   ├── Prefabs/       arcs, joueur, cibles, monstres, monde, interface, effets, systèmes
│   ├── Scenes/        Boot, World, Arenas/, Sandbox/
│   ├── Scripts/       Runtime/ (code du jeu, par domaine) et Editor/ (outils)
│   └── UI/            sprites et polices
├── _Imports/          assets téléchargés en attente de rangement
├── Tests/             tests EditMode et PlayMode
└── (packs tiers)      Fantasy_Megakit, Pure Poly, Glowing Rifts, Leohpaz, kenney_audio,
                       RPG Monster DUO PBR Polyart, URP GanzSe Free Weapons Pack, Samples...
```

Le code est découpé en assemblies (`ArcheryChallenge.Runtime`, `ArcheryChallenge.Editor`, tests) et en espaces de noms `ArcheryChallenge.<Domaine>`.

Scènes : `Boot` (toujours chargée : joueurs VR et PC, services, interface globale) charge `World` (le monde ouvert), qui charge chaque arène à l'approche du joueur.

## Outils de développement et tests

- Menu **Tools → Archery Challenge → Build Everything** : régénère données, prefabs, interface, monde, arènes, sons et scène `Boot` (les générateurs sont dans `Scripts/Editor/`).
- Menu **Tools → Archery Challenge → Edit World + Arenas (open all scenes)** : ouvre le monde et les six arènes ensemble dans la Hiérarchie pour placer plantes, cibles et décors à la main. Les menus « Build World / Arenas / Boot / Everything » **recréent** les scènes et demandent confirmation : ne les relancez pas après des retouches manuelles.
- En jeu, **F1** (ou **H** au clavier) ouvre l'écran des commandes (clavier, VR, simulateur XR, touches de développement) ; une barre d'aide reste affichée en bas de l'écran.
- Menu **Tools → Archery Challenge → Run EditMode Tests (Console)** : exécute les tests EditMode sans la fenêtre Test Runner (la fenêtre officielle `Window → General → Test Runner` reste utilisable).
- `Core/SelfTest` (éditeur et builds de développement) : scénarios automatiques jouables en mode Play (tir au PC, manche complète, sauvegarde, tir en VR dans le simulateur, entraînement, monde).
- Raccourcis de développement `F1`–`F11` en éditeur et en build de développement.

## Équipe

| Membre | Rôle |
|---|---|
| Maxime | XR, système de tir, builds |
| Louis | Monde ouvert, cycle jour/nuit, monstres |
| Fériel | Interface, sauvegarde, audio |
| Myriam | Cibles, équipement, arènes, effets visuels |

## Conventions Git

- `main` : versions stables étiquetées (`v1.0.0`) ; `develop` : intégration ; `feature/<sujet>`, `fix/…`, `chore/…`, `perf/…`, `docs/…`, `test/…`.
- Messages au format *Conventional Commits*, en français : `feat(bow): saisie de l'arc et tension de la corde`.
- Une scène n'est modifiée que par une personne à la fois ; fusion des scènes avec UnityYAMLMerge.

## Crédits et licences des assets

| Asset | Auteur | Licence |
|---|---|---|
| Fantasy Props MegaKit | Quaternius | CC0 |
| RPG Audio | Kenney | CC0 |
| Free Low Poly Nature Pack (Pure Poly) | Pure Poly | Unity Asset Store EULA |
| Archery Target | Glowing Rifts | Unity Asset Store EULA |
| RPG Essentials Sound Effects - Free | Leohpaz | Unity Asset Store EULA |
| RPG Monster Duo PBR Polyart | Dungeon Mason | Unity Asset Store EULA |
| FREE Fantasy Low Poly Weapons Pack | GanzSe | Unity Asset Store EULA |
| XR Interaction Toolkit (Starter Assets, Simulator, Spatial Keyboard), VR Template | Unity Technologies | Unity Companion License |
| Police Inter | Rasmus Andersson | SIL Open Font License |

Les sons et polices ajoutés en cours de projet sont listés dans `Assets/_Project/Audio/CREDITS.txt`.
