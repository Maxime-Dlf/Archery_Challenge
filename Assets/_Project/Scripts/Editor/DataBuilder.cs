// DataBuilder : crée et remplit tous les ScriptableObjects du jeu (annexe C du tutoriel).
//
// Menu : Tools → Archery Challenge → Build Data.
// Idempotent : relancer le générateur remet les VALEURS PAR DÉFAUT du tutoriel. Si vous avez
// rééquilibré un fichier à la main, ne relancez pas ce menu (ou sauvegardez vos valeurs avant).
// Les champs « Prefab » (arcs, flèches) sont remplis par PrefabBuilder.

using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Monsters;
using ArcheryChallenge.Targets;
using ArcheryChallenge.World;
using UnityEditor;
using UnityEngine;

namespace ArcheryChallenge.EditorTools
{
    /// <summary>Générateur des fichiers de données.</summary>
    public static class DataBuilder
    {
        [MenuItem("Tools/Archery Challenge/Build Data")]
        public static void BuildAll()
        {
            BuildBows();
            BuildArrows();
            BuildDifficulties();
            BuildTargets();
            BuildMonsters();
            BuildTimeOfDay();
            BuildLevels();
            BuildCatalog();
            AssetDatabase.SaveAssets();
            Debug.Log("[DataBuilder] Données de jeu générées.");
        }

        // ---------------------------------------------------------------- arcs

        private static void BuildBows()
        {
            Bow("Bow_Apprentice", "bow_apprentice", "Arc d'apprenti", 1, 32f, 0.55f, 0.6f, "Arc fin et sobre pour apprendre : léger mais peu puissant. Disponible dès le départ.");
            Bow("Bow_Hunter", "bow_hunter", "Arc de chasse", 2, 42f, 0.60f, 0.7f, "Arc de chasse à bandes colorées : plus rapide et plus précis. Récompense de La Clairière.");
            Bow("Bow_Warden", "bow_warden", "Arc du gardien", 3, 50f, 0.65f, 0.8f, "Arc à garde centrale, brun et or. Récompense de La Rivière.");
            Bow("Bow_Sylvan", "bow_sylvan", "Arc sylvestre", 4, 58f, 0.72f, 1.0f, "Arc bleu-vert, très puissant mais exigeant. Bonus de La Rivière (2 étoiles) ou récompense du Ravin.");
            Bow("Bow_Thorn", "bow_thorn", "Arc épineux", 5, 65f, 0.70f, 0.9f, "Arc sombre à épines, redoutable. Bonus du Ravin (3 étoiles).");
            Bow("Bow_Royal", "bow_royal", "Arc royal", 6, 72f, 0.75f, 1.0f, "L'arc des champions, or et vert. Récompense du Tournoi.");
        }

        private static void Bow(string file, string id, string name, int tier, float speed, float draw, float desktopTime, string description)
        {
            BowDefinition d = BuildUtils.LoadOrCreate<BowDefinition>($"{BuildUtils.DataRoot}/Bows/{file}.asset");
            d.id = id;
            d.displayName = name;
            d.description = description;
            d.tier = tier;
            d.maxSpeed = speed;
            d.drawLength = draw;
            d.desktopDrawTime = desktopTime;
            d.powerCurve = new AnimationCurve(new Keyframe(0f, 0f, 0.6f, 0.6f), new Keyframe(1f, 1f, 1.5f, 1.5f));
            EditorUtility.SetDirty(d);
        }

        private static void BuildArrows()
        {
            Arrow("Arrow_Wood", "arrow_wood", "Flèche en bois", 1, 1.00f, 0.020f, 1.0f, "Flèche simple et robuste. Disponible dès le départ.");
            Arrow("Arrow_Ash", "arrow_ash", "Flèche en frêne", 2, 1.15f, 0.015f, 0.8f, "Plus rapide et moins sensible au vent. Récompense de La Forêt champêtre.");
            Arrow("Arrow_Carbon", "arrow_carbon", "Flèche en carbone", 3, 1.30f, 0.010f, 0.6f, "La plus rapide, presque insensible au vent. Récompense du Ravin.");
        }

        private static void Arrow(string file, string id, string name, int tier, float speed, float drag, float wind, string description)
        {
            ArrowDefinition d = BuildUtils.LoadOrCreate<ArrowDefinition>($"{BuildUtils.DataRoot}/Arrows/{file}.asset");
            d.id = id;
            d.displayName = name;
            d.description = description;
            d.tier = tier;
            d.speedMultiplier = speed;
            d.drag = drag;
            d.windSensitivity = wind;
            EditorUtility.SetDirty(d);
        }

        // ---------------------------------------------------------------- difficultés

        private static void BuildDifficulties()
        {
            Difficulty("Difficulty_Easy", GameDifficulty.Easy, "Facile", 1.5f, 0.6f, 1.5f, 1.5f, 0.6f, 0f, 0f, 0.10f, true, 2f, 1.3f, 0.5f, 0.35f);
            Difficulty("Difficulty_Normal", GameDifficulty.Normal, "Normal", 1f, 1f, 1f, 1f, 0.8f, 1f, 2.5f, 0.15f, false, 0f, 1f, 0.8f, 1f);
            Difficulty("Difficulty_Hard", GameDifficulty.Hard, "Difficile", 0.7f, 1.6f, 0.7f, 0.8f, 0.95f, 2f, 4.5f, 0.25f, false, 0f, 1f, 1.2f, 1.6f);
        }

        private static void Difficulty(string file, GameDifficulty difficulty, string name, float size, float speed, float time, float arrows,
            float stars, float windMin, float windMax, float minTension, bool trajectory, float assist, float nockRadius, float drag, float aimSway)
        {
            DifficultyProfile d = BuildUtils.LoadOrCreate<DifficultyProfile>($"{BuildUtils.DataRoot}/Difficulty/{file}.asset");
            d.difficulty = difficulty;
            d.displayName = name;
            d.targetSizeMultiplier = size;
            d.targetSpeedMultiplier = speed;
            d.timeMultiplier = time;
            d.arrowsMultiplier = arrows;
            d.starThresholdMultiplier = stars;
            d.windMin = windMin;
            d.windMax = windMax;
            d.minTension = minTension;
            d.showTrajectory = trajectory;
            d.aimAssistAngle = assist;
            d.nockRadiusMultiplier = nockRadius;
            d.dragMultiplier = drag;
            d.aimSwayMultiplier = aimSway;
            EditorUtility.SetDirty(d);
        }

        // ---------------------------------------------------------------- cibles et monstres

        private static void BuildTargets()
        {
            TargetDefinition std = BuildUtils.LoadOrCreate<TargetDefinition>($"{BuildUtils.DataRoot}/Targets/Target_Standard.asset");
            std.ringRadii = new[] { 0.058f, 0.116f, 0.174f, 0.232f, 0.290f, 0.348f, 0.406f, 0.464f, 0.522f, 0.580f };
            std.xRadius = 0.029f;
            std.isPenalty = false;
            EditorUtility.SetDirty(std);

            TargetDefinition pen = BuildUtils.LoadOrCreate<TargetDefinition>($"{BuildUtils.DataRoot}/Targets/Target_Penalty.asset");
            pen.isPenalty = true;
            pen.penaltyPoints = 15;
            EditorUtility.SetDirty(pen);
        }

        private static void BuildMonsters()
        {
            Monster("Monster_Slime", "slime", "Slime", 1, 2, 2, 1f, 2.5f, 15f, 1.6f, 1.5f, false);
            Monster("Monster_Turtle", "turtle", "Tortue", 3, 4, 4, 0.8f, 1.8f, 12f, 2f, 2f, true);
        }

        private static void Monster(string file, string id, string name, int hp, int damage, int heal, float walk, float run, float sense, float range, float cooldown, bool defend)
        {
            MonsterDefinition d = BuildUtils.LoadOrCreate<MonsterDefinition>($"{BuildUtils.DataRoot}/Monsters/{file}.asset");
            d.id = id;
            d.displayName = name;
            d.maxHealth = hp;
            d.damage = damage;
            d.healOnKill = heal;
            d.walkSpeed = walk;
            d.runSpeed = run;
            d.senseRadius = sense;
            d.attackRange = range;
            d.attackCooldown = cooldown;
            d.canDefend = defend;
            EditorUtility.SetDirty(d);
        }

        // ---------------------------------------------------------------- jour / nuit

        private static void BuildTimeOfDay()
        {
            TimeOfDayProfile p = BuildUtils.LoadOrCreate<TimeOfDayProfile>($"{BuildUtils.DataRoot}/TimeOfDay/TimeOfDay_Default.asset");
            p.dayLengthMinutes = 20f;
            p.startHour = 9f;
            p.sunriseHour = 6f;
            p.sunsetHour = 19f;
            p.torchOnHour = 19f;
            p.torchOffHour = 6.5f;
            p.moonColor = HexColor("#9DB4FF");

            p.sunColor = MakeGradient(
                (0f, "#2A3566"), (5f, "#2A3566"), (6.2f, "#FF9A4A"), (8f, "#FFE9C9"),
                (13f, "#FFF6E5"), (17f, "#FFE0B0"), (18.8f, "#FF8A3C"), (20.5f, "#2A3566"));
            p.sunIntensity = MakeCurve((0f, 0f), (5f, 0f), (6.5f, 0.45f), (9f, 1.05f), (13f, 1.25f), (17f, 1.0f), (19f, 0.35f), (20f, 0f), (24f, 0f));
            p.moonIntensity = MakeCurve((0f, 0.28f), (5f, 0.28f), (7f, 0f), (18f, 0f), (20.5f, 0.28f), (24f, 0.28f));

            p.ambientSky = MakeGradient((0f, "#101838"), (5f, "#141C40"), (7f, "#7C93B8"), (12f, "#8FB4E0"), (17f, "#8FA6C8"), (19f, "#9A6C70"), (21f, "#141C40"));
            p.ambientEquator = MakeGradient((0f, "#0C1230"), (5f, "#10173A"), (7f, "#6E7C99"), (12f, "#8CA0BC"), (17f, "#8A93AA"), (19f, "#7A5668"), (21f, "#10173A"));
            p.ambientGround = MakeGradient((0f, "#080C1C"), (5f, "#0A0F24"), (7f, "#4C5A48"), (12f, "#5A6A50"), (17f, "#566048"), (19f, "#3C3438"), (21f, "#0A0F24"));
            p.fogColor = MakeGradient((0f, "#0A0E22"), (5f, "#0E1430"), (6.5f, "#D4A27A"), (9f, "#B8D4E8"), (16f, "#B8D0E4"), (19f, "#D4906A"), (21f, "#0A0E22"));

            p.skyExposure = MakeCurve((0f, 0.15f), (5f, 0.15f), (7f, 1.0f), (12f, 1.3f), (17f, 1.1f), (19.5f, 0.5f), (21f, 0.15f), (24f, 0.15f));
            p.skyTint = MakeGradient((0f, "#303060"), (6f, "#C08070"), (9f, "#808890"), (17f, "#908C88"), (19f, "#C07860"), (21f, "#303060"));
            EditorUtility.SetDirty(p);
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // Dégradé indexé sur 24 h : chaque couple (heure, couleur) devient une clé à t = heure / 24.
        private static Gradient MakeGradient(params (float hour, string hex)[] keys)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                colorKeys[i] = new GradientColorKey(HexColor(keys[i].hex), keys[i].hour / 24f);
            Gradient g = new Gradient();
            g.SetKeys(colorKeys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        private static AnimationCurve MakeCurve(params (float hour, float value)[] keys)
        {
            AnimationCurve c = new AnimationCurve();
            for (int i = 0; i < keys.Length; i++)
                c.AddKey(new Keyframe(keys[i].hour / 24f, keys[i].value));
            for (int i = 0; i < c.length; i++)
                c.SmoothTangents(i, 0.3f);
            return c;
        }

        // ---------------------------------------------------------------- niveaux

        private static void BuildLevels()
        {
            Level("Level_Training", "training", "Stand d'entraînement", 0, "Arena_Training", "training", true, 0f, 0, new[] { 0, 0, 0 }, 1, 1f, "", null,
                "Exercez-vous librement sur des cibles fixes. Tout l'équipement est utilisable.");

            Level("Level_01", "arena_01", "La Clairière", 1, "Arena_01", "arena_01", false, 90f, 12, new[] { 45, 63, 81 }, 1, 1f, "",
                new[] { R("bow_hunter", 0) }, "Trois cibles fixes dans une clairière fleurie. Marquez 45 points pour réussir.");

            Level("Level_02", "arena_02", "La Forêt champêtre", 2, "Arena_02", "arena_02", false, 90f, 12, new[] { 52, 74, 94 }, 1, 1f, "arena_01",
                new[] { R("arrow_ash", 0) }, "Cherchez les trouées entre les arbres et les clôtures. Une cible pop-up se cache dans les champs.");

            Level("Level_03", "arena_03", "La Rivière", 3, "Arena_03", "arena_03", false, 75f, 15, new[] { 60, 84, 108 }, 1, 1f, "arena_02",
                new[] { R("bow_warden", 0), R("bow_sylvan", 2) }, "Visez par-dessus la rivière : trois cibles glissent le long de la berge opposée.");

            Level("Level_04", "arena_04", "Le Ravin", 4, "Arena_04", "arena_04", false, 75f, 15, new[] { 68, 95, 122 }, 2, 1.25f, "arena_03",
                new[] { R("arrow_carbon", 0), R("bow_sylvan", 0), R("bow_thorn", 3) }, "Cibles en hauteur et au fond du ravin, vent fort. Il faut 2 étoiles pour passer.");

            Level("Level_05", "arena_05", "Le Tournoi", 5, "Arena_05", "arena_05", false, 60f, 20, new[] { 90, 126, 162 }, 2, 1.5f, "arena_04",
                new[] { R("bow_royal", 0), R("trophy", 0), R("trophy_gold", 3) }, "La finale en trois vagues. Évitez les boucliers rouges ! Il faut 2 étoiles pour être sacré champion.");
        }

        private static LevelReward R(string id, int stars) => new LevelReward { itemId = id, minStars = stars };

        private static void Level(string file, string id, string name, int order, string scene, string spawn, bool training, float time, int arrows,
            int[] stars, int starsToPass, float windMultiplier, string required, LevelReward[] rewards, string briefing)
        {
            LevelDefinition d = BuildUtils.LoadOrCreate<LevelDefinition>($"{BuildUtils.DataRoot}/Levels/{file}.asset");
            d.levelId = id;
            d.displayName = name;
            d.order = order;
            d.sceneName = scene;
            d.spawnId = spawn;
            d.isTraining = training;
            d.timeSeconds = time;
            d.arrows = arrows;
            d.starThresholds = stars;
            d.starsToPass = starsToPass;
            d.windMultiplier = windMultiplier;
            d.requiredLevelId = required;
            d.rewards = rewards ?? new LevelReward[0];
            d.briefing = briefing;
            d.timeBonusPerSecond = 1;
            EditorUtility.SetDirty(d);
        }

        // ---------------------------------------------------------------- catalogue

        public static GameCatalog BuildCatalog()
        {
            GameCatalog catalog = BuildUtils.LoadOrCreate<GameCatalog>($"{BuildUtils.DataRoot}/GameCatalog.asset");
            catalog.bows = LoadAll<BowDefinition>("Bows", "Bow_Apprentice", "Bow_Hunter", "Bow_Warden", "Bow_Sylvan", "Bow_Thorn", "Bow_Royal");
            catalog.arrows = LoadAll<ArrowDefinition>("Arrows", "Arrow_Wood", "Arrow_Ash", "Arrow_Carbon");
            catalog.levels = LoadAll<LevelDefinition>("Levels", "Level_Training", "Level_01", "Level_02", "Level_03", "Level_04", "Level_05");
            catalog.difficulties = LoadAll<DifficultyProfile>("Difficulty", "Difficulty_Easy", "Difficulty_Normal", "Difficulty_Hard");
            catalog.defaultBowId = "bow_apprentice";
            catalog.defaultArrowId = "arrow_wood";
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static T[] LoadAll<T>(string folder, params string[] names) where T : ScriptableObject
        {
            T[] result = new T[names.Length];
            for (int i = 0; i < names.Length; i++)
                result[i] = AssetDatabase.LoadAssetAtPath<T>($"{BuildUtils.DataRoot}/{folder}/{names[i]}.asset");
            return result;
        }
    }
}
