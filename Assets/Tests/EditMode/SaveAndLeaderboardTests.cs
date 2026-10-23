// Tests EditMode : stockage JSON, noms, profils, emplacements, classement et déblocages.
//
// Chaque test travaille dans un dossier temporaire (JsonFileStore.SetRootForTests) : les vraies
// sauvegardes du joueur ne sont jamais touchées.

using System.IO;
using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Progression;
using ArcheryChallenge.Save;
using NUnit.Framework;
using UnityEngine;

namespace ArcheryChallenge.Tests
{
    public class SaveAndLeaderboardTests
    {
        private string root;
        private GameObject host;

        [SetUp]
        public void SetUp()
        {
            Services.Clear();
            root = Path.Combine(Path.GetTempPath(), "archery_tests_" + System.Guid.NewGuid().ToString("N"));
            JsonFileStore.SetRootForTests(root);
            host = new GameObject("SaveHost");
        }

        [TearDown]
        public void TearDown()
        {
            JsonFileStore.SetRootForTests(null);
            Object.DestroyImmediate(host);
            Services.Clear();
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        // ---------------------------------------------------------------- noms

        [TestCase("  Élodie  ", "Élodie")]
        [TestCase("Jean   Pierre", "Jean Pierre")]
        [TestCase("李明", "李明")]
        [TestCase("Ольга", "Ольга")]
        public void NamesAreTrimmedAndKeepUnicode(string raw, string expected)
        {
            Assert.IsTrue(NameValidator.TryClean(raw, out string cleaned));
            Assert.AreEqual(expected, cleaned);
        }

        [TestCase("")]
        [TestCase("    ")]
        [TestCase("\t\n")]
        public void EmptyNamesAreRejected(string raw)
        {
            Assert.IsFalse(NameValidator.TryClean(raw, out _));
        }

        [Test]
        public void LongNamesAreCutToTwentyCharacters()
        {
            Assert.IsTrue(NameValidator.TryClean(new string('A', 50), out string cleaned));
            Assert.AreEqual(20, NameValidator.Length(cleaned));
        }

        // ---------------------------------------------------------------- JSON

        [Test]
        public void LeftStickTeleportsByDefaultAndContinuousMoveIsAnOption()
        {
            // Le stick gauche téléporte (laser) au départ ; le déplacement continu se règle dans le menu Confort et se sauvegarde.
            Assert.IsFalse(new SettingsData().continuousMove);
            SettingsData data = new SettingsData { continuousMove = true };
            Assert.IsTrue(JsonFileStore.Write("settings.json", data));
            Assert.IsTrue(JsonFileStore.Read<SettingsData>("settings.json").continuousMove);
        }

        [Test]
        public void BowIsInTheRightHandByDefault()
        {
            // Arc à droite, flèches à gauche : l'option « Arc dans la main gauche » est décochée au départ.
            Assert.IsFalse(new SettingsData().bowInLeftHand);
        }

        [Test]
        public void WriteThenReadReturnsTheSameData()
        {
            SettingsData data = new SettingsData { masterVolume = 0.25f, difficulty = 2, bowInLeftHand = true };
            Assert.IsTrue(JsonFileStore.Write("settings.json", data));
            SettingsData read = JsonFileStore.Read<SettingsData>("settings.json");
            Assert.AreEqual(0.25f, read.masterVolume, 0.0001f);
            Assert.AreEqual(2, read.difficulty);
            Assert.IsTrue(read.bowInLeftHand);
            Assert.IsFalse(File.Exists(JsonFileStore.PathOf("settings.json") + ".tmp"), "Le fichier temporaire de l'écriture atomique doit disparaître.");
        }

        [Test]
        public void CorruptedFileIsMovedToBakAndReadReturnsNull()
        {
            Directory.CreateDirectory(root);
            string path = JsonFileStore.PathOf("settings.json");
            File.WriteAllText(path, "{ pas du json");
            Assert.IsNull(JsonFileStore.Read<SettingsData>("settings.json"));
            Assert.IsTrue(File.Exists(path + ".bak"));
            Assert.IsFalse(File.Exists(path));
        }

        [Test]
        public void MissingFileReturnsNull()
        {
            Assert.IsNull(JsonFileStore.Read<SettingsData>("n_existe_pas.json"));
        }

        // ---------------------------------------------------------------- profils et emplacements

        [Test]
        public void ProfilesAreCreatedRenamedAndDeletedWithTheirSaves()
        {
            ProfileService profiles = host.AddComponent<ProfileService>();
            Services.Register(profiles);
            SaveSlotService slots = host.AddComponent<SaveSlotService>();
            Services.Register(slots);

            ProfileData profile = profiles.Create("Élodie");
            Assert.IsNotNull(profile);
            Assert.IsNull(profiles.Create("   "));
            Assert.IsTrue(profiles.Rename(profile.id, "Élodie 2"));
            Assert.AreEqual("Élodie 2", profiles.Find(profile.id).name);

            slots.NewGame(profile.id, 1, null);
            Assert.IsTrue(JsonFileStore.Exists($"saves/{profile.id}/slot1.json"));

            Assert.IsTrue(profiles.Delete(profile.id));
            Assert.IsFalse(JsonFileStore.Exists($"saves/{profile.id}/slot1.json"), "Supprimer un profil supprime ses emplacements.");
            Assert.IsNull(profiles.Find(profile.id));
        }

        [Test]
        public void SlotsAreIndependentAndReloadable()
        {
            SaveSlotService slots = host.AddComponent<SaveSlotService>();
            Services.Register(slots);

            slots.NewGame("p1", 1, null);
            slots.Current.unlockedBows.Add("bow_hunter");
            slots.Save();
            slots.NewGame("p1", 2, null);
            Assert.IsFalse(slots.Current.unlockedBows.Contains("bow_hunter"));

            Assert.IsTrue(slots.Load("p1", 1));
            Assert.IsTrue(slots.Current.unlockedBows.Contains("bow_hunter"));
            Assert.IsFalse(slots.Load("p1", 3), "Un emplacement vide ne se charge pas.");
            Assert.AreEqual(1, slots.GetSummary("p1", 1).Exists ? 1 : 0);
            Assert.IsFalse(slots.GetSummary("p1", 3).Exists);
        }

        [Test]
        public void GuestSlotIsNeverWrittenToDisk()
        {
            SaveSlotService slots = host.AddComponent<SaveSlotService>();
            Services.Register(slots);
            slots.CreateGuest();
            slots.Save();
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "saves")));
        }

        // ---------------------------------------------------------------- classement

        private static LevelResult Result(string level, int score, GameDifficulty difficulty, int x = 0, float accuracy = 0.5f)
        {
            return new LevelResult { LevelId = level, Score = score, Difficulty = difficulty, XCount = x, Accuracy = accuracy, Stars = 1 };
        }

        private LeaderboardService NewBoard()
        {
            LeaderboardService board = host.AddComponent<LeaderboardService>();
            Services.Register(board);
            board.EnsureLoaded();
            return board;
        }

        [Test]
        public void LeaderboardSortsByScoreThenXThenAccuracy()
        {
            LeaderboardService board = NewBoard();
            board.Add("A", "", Result("arena_01", 80, GameDifficulty.Normal));
            board.Add("B", "", Result("arena_01", 100, GameDifficulty.Normal));
            board.Add("C", "", Result("arena_01", 80, GameDifficulty.Normal, x: 3));
            board.Add("D", "", Result("arena_01", 80, GameDifficulty.Normal, x: 3, accuracy: 0.9f));

            var top = board.Query("arena_01", null, 10);
            Assert.AreEqual("B", top[0].playerName);
            Assert.AreEqual("D", top[1].playerName);
            Assert.AreEqual("C", top[2].playerName);
            Assert.AreEqual("A", top[3].playerName);
        }

        [Test]
        public void LeaderboardFiltersByDifficultyAndLevel()
        {
            LeaderboardService board = NewBoard();
            board.Add("A", "", Result("arena_01", 80, GameDifficulty.Easy));
            board.Add("B", "", Result("arena_01", 90, GameDifficulty.Hard));
            board.Add("C", "", Result("arena_02", 70, GameDifficulty.Hard));
            Assert.AreEqual(1, board.Query("arena_01", GameDifficulty.Hard, 10).Count);
            Assert.AreEqual(2, board.Query("arena_01", null, 10).Count);
            Assert.AreEqual(1, board.Query("arena_02", null, 10).Count);
        }

        [Test]
        public void LeaderboardPersistsToJsonWithUtf8()
        {
            LeaderboardService board = NewBoard();
            board.Add("Élodie 李明", "", Result("arena_01", 50, GameDifficulty.Normal));
            string json = File.ReadAllText(JsonFileStore.PathOf("leaderboard.json"), System.Text.Encoding.UTF8);
            StringAssert.Contains("Élodie 李明", json);
            StringAssert.Contains("\"difficulty\": \"Normal\"", json);

            // Nouveau service : relecture depuis le disque.
            Object.DestroyImmediate(host);
            host = new GameObject("SaveHost2");
            LeaderboardService reloaded = host.AddComponent<LeaderboardService>();
            reloaded.EnsureLoaded();
            Assert.AreEqual(1, reloaded.Entries.Count);
            Assert.AreEqual("Élodie 李明", reloaded.Entries[0].playerName);
        }

        [Test]
        public void LeaderboardKeepsOnlyTheFiftyBestPerBoard()
        {
            LeaderboardService board = NewBoard();
            for (int i = 0; i < 70; i++)
                board.Add("J" + i, "", Result("arena_01", i, GameDifficulty.Normal));
            Assert.AreEqual(LeaderboardService.KeepPerBoard, board.Entries.Count);
            Assert.AreEqual(69, board.Query("arena_01", null, 1)[0].score);
        }

        [Test]
        public void CampaignViewSumsTheBestScorePerLevelOfEachPlayer()
        {
            LeaderboardService board = NewBoard();
            board.Add("Zoé", "z", Result("arena_01", 50, GameDifficulty.Normal));
            board.Add("Zoé", "z", Result("arena_01", 70, GameDifficulty.Normal)); // meilleur de l'arène 1
            board.Add("Zoé", "z", Result("arena_02", 40, GameDifficulty.Normal));
            board.Add("Max", "m", Result("arena_01", 90, GameDifficulty.Normal));
            var campaign = board.Query(LeaderboardService.CampaignId, GameDifficulty.Normal, 10);
            Assert.AreEqual(2, campaign.Count);
            Assert.AreEqual("Zoé", campaign[0].playerName);
            Assert.AreEqual(110, campaign[0].score); // 70 + 40
            Assert.AreEqual(90, campaign[1].score);
        }

        [Test]
        public void ClearEmptiesTheLeaderboard()
        {
            LeaderboardService board = NewBoard();
            board.Add("A", "", Result("arena_01", 80, GameDifficulty.Normal));
            board.Clear();
            Assert.AreEqual(0, board.Entries.Count);
        }

        // ---------------------------------------------------------------- déblocages

        private UnlockService MakeProgression(out GameCatalog catalog)
        {
            catalog = ScriptableObject.CreateInstance<GameCatalog>();
            LevelDefinition l1 = ScriptableObject.CreateInstance<LevelDefinition>();
            l1.levelId = "arena_01"; l1.order = 1; l1.starsToPass = 1;
            l1.rewards = new[] { new LevelReward { itemId = "bow_hunter", minStars = 0 } };
            LevelDefinition l2 = ScriptableObject.CreateInstance<LevelDefinition>();
            l2.levelId = "arena_02"; l2.order = 2; l2.requiredLevelId = "arena_01"; l2.starsToPass = 2;
            l2.rewards = new[] { new LevelReward { itemId = "arrow_ash", minStars = 0 }, new LevelReward { itemId = "bow_thorn", minStars = 3 } };
            catalog.levels = new[] { l1, l2 };
            Services.Register(catalog);

            SaveSlotService slots = host.AddComponent<SaveSlotService>();
            Services.Register(slots);
            slots.CreateGuest();
            UnlockService unlocks = host.AddComponent<UnlockService>();
            Services.Register(unlocks);
            return unlocks;
        }

        [Test]
        public void FirstLevelIsAvailableAndSecondRequiresTheFirst()
        {
            UnlockService unlocks = MakeProgression(out GameCatalog catalog);
            Assert.IsTrue(unlocks.IsLevelUnlocked("arena_01"));
            Assert.IsFalse(unlocks.IsLevelUnlocked("arena_02"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void PassingALevelGrantsRewardsAndOpensTheNextOne()
        {
            UnlockService unlocks = MakeProgression(out GameCatalog catalog);
            LevelDefinition l1 = catalog.GetLevel("arena_01");
            LevelResult result = new LevelResult { LevelId = "arena_01", Score = 70, Stars = 1 };
            unlocks.ApplyResult(l1, result);

            Assert.IsTrue(result.Passed);
            CollectionAssert.Contains(result.NewRewards, "bow_hunter");
            Assert.IsTrue(unlocks.IsBowUnlocked("bow_hunter"));
            Assert.IsTrue(unlocks.IsLevelUnlocked("arena_02"));
        }

        [Test]
        public void StarsBelowTheRequirementDoNotPassTheLevel()
        {
            UnlockService unlocks = MakeProgression(out GameCatalog catalog);
            unlocks.ApplyResult(catalog.GetLevel("arena_01"), new LevelResult { LevelId = "arena_01", Score = 70, Stars = 1 });

            LevelDefinition l2 = catalog.GetLevel("arena_02");
            LevelResult weak = new LevelResult { LevelId = "arena_02", Score = 80, Stars = 1 };
            unlocks.ApplyResult(l2, weak);
            Assert.IsFalse(weak.Passed, "L'arène 2 exige 2 étoiles dans ce test.");
            Assert.IsFalse(unlocks.IsArrowUnlocked("arrow_ash"));
        }

        [Test]
        public void BonusRewardsNeedEnoughStars()
        {
            UnlockService unlocks = MakeProgression(out GameCatalog catalog);
            unlocks.ApplyResult(catalog.GetLevel("arena_01"), new LevelResult { LevelId = "arena_01", Score = 70, Stars = 1 });
            LevelDefinition l2 = catalog.GetLevel("arena_02");

            unlocks.ApplyResult(l2, new LevelResult { LevelId = "arena_02", Score = 100, Stars = 2 });
            Assert.IsTrue(unlocks.IsArrowUnlocked("arrow_ash"));
            Assert.IsFalse(unlocks.IsBowUnlocked("bow_thorn"), "Le bonus exige 3 étoiles.");

            unlocks.ApplyResult(l2, new LevelResult { LevelId = "arena_02", Score = 200, Stars = 3 });
            Assert.IsTrue(unlocks.IsBowUnlocked("bow_thorn"));
        }

        [Test]
        public void DemoModeUnlocksEverythingWithoutTouchingTheSave()
        {
            UnlockService unlocks = MakeProgression(out GameCatalog catalog);
            unlocks.DemoUnlockAll = true;
            Assert.IsTrue(unlocks.IsLevelUnlocked("arena_02"));
            Assert.IsTrue(unlocks.IsBowUnlocked("bow_royal"));
            unlocks.DemoUnlockAll = false;
            Assert.IsFalse(unlocks.IsLevelUnlocked("arena_02"));
        }
    }
}
