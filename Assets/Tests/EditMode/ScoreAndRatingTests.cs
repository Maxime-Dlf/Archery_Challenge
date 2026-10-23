// Tests EditMode : score, combo, pénalités, anneaux, étoiles et difficulté.
//
// Ces tests n'ont besoin d'aucune scène : les services sont créés à la main (en EditMode, Awake n'est pas appelé,
// on s'inscrit donc nous-mêmes dans le registre de services).

using ArcheryChallenge.Core;
using ArcheryChallenge.Levels;
using ArcheryChallenge.Targets;
using NUnit.Framework;
using UnityEngine;

namespace ArcheryChallenge.Tests
{
    public class ScoreAndRatingTests
    {
        private GameObject host;
        private ScoreService score;

        [SetUp]
        public void SetUp()
        {
            Services.Clear();
            host = new GameObject("ScoreHost");
            score = host.AddComponent<ScoreService>();
            Services.Register(score);
            score.ResetRun();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            Services.Clear();
        }

        private static ShotResult Shot(int ring, bool isX = false, bool moving = false, bool penalty = false)
        {
            return new ShotResult { Ring = ring, Points = penalty ? 15 : ring, IsX = isX, TargetWasMoving = moving, IsPenalty = penalty };
        }

        [Test]
        public void CenterShotScoresTenAndCountsAnX()
        {
            int added = score.RegisterShot(Shot(10, isX: true));
            Assert.AreEqual(10, added);
            Assert.AreEqual(10, score.Score);
            Assert.AreEqual(1, score.XCount);
        }

        [Test]
        public void MovingTargetsGiveOnePointFiveTimesThePoints()
        {
            Assert.AreEqual(12, score.RegisterShot(Shot(8, moving: true))); // 8 × 1,5 = 12
        }

        [Test]
        public void ComboAddsOneThenTwoThenThree()
        {
            Assert.AreEqual(10, score.RegisterShot(Shot(10)));       // 1re flèche à 9+ : pas de bonus
            Assert.AreEqual(10 + 1, score.RegisterShot(Shot(10)));   // 2e : +1
            Assert.AreEqual(9 + 2, score.RegisterShot(Shot(9)));     // 3e : +2
            Assert.AreEqual(3, score.Combo);
            Assert.AreEqual(10 + 11 + 11, score.Score);
        }

        [Test]
        public void ShotBelowNineResetsTheCombo()
        {
            score.RegisterShot(Shot(10));
            score.RegisterShot(Shot(10));
            score.RegisterShot(Shot(5));
            Assert.AreEqual(0, score.Combo);
            Assert.AreEqual(10, score.RegisterShot(Shot(10))); // la série repart de zéro
        }

        [Test]
        public void PenaltyTargetRemovesFifteenPointsAndResetsComboButNeverGoesNegative()
        {
            score.RegisterShot(Shot(10));
            score.RegisterShot(Shot(10));
            int added = score.RegisterShot(Shot(0, penalty: true));
            Assert.AreEqual(-15, added);
            Assert.AreEqual(6, score.Score); // 10 + 11 - 15
            Assert.AreEqual(0, score.Combo);
            score.RegisterShot(Shot(0, penalty: true));
            Assert.AreEqual(0, score.Score);
        }

        [Test]
        public void MissAndFootCountZero()
        {
            Assert.AreEqual(0, score.RegisterShot(Shot(0)));
            Assert.AreEqual(0, score.ArrowsHit);
        }

        [Test]
        public void AccuracyIsHitsOverShots()
        {
            score.RegisterArrowShot();
            score.RegisterArrowShot();
            score.RegisterShot(Shot(7));
            Assert.AreEqual(0.5f, score.Accuracy, 0.001f);
        }

        // ---------------------------------------------------------------- anneaux

        [Test]
        public void TargetDefinitionRingsAreComputedFromRadius()
        {
            TargetDefinition definition = ScriptableObject.CreateInstance<TargetDefinition>();
            Assert.AreEqual(10, definition.RingForRadius(0.01f));
            Assert.AreEqual(9, definition.RingForRadius(0.10f));
            Assert.AreEqual(1, definition.RingForRadius(0.57f));
            Assert.AreEqual(0, definition.RingForRadius(0.9f));
            Assert.IsTrue(definition.IsX(0.02f));
            Assert.IsFalse(definition.IsX(0.04f));
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void TargetFaceUsesTheLocalCenterAndRejectsShotsFromBehind()
        {
            GameObject go = new GameObject("Face");
            go.transform.position = new Vector3(0f, 1f, 10f);
            TargetFace face = go.AddComponent<TargetFace>();
            face.Definition = ScriptableObject.CreateInstance<TargetDefinition>();

            // Tir de face (la flèche va vers -Z, le blason regarde +Z) au centre : 10 + X.
            ShotResult center = face.Evaluate(go.transform.position, Vector3.back, out _);
            Assert.AreEqual(10, center.Ring);
            Assert.IsTrue(center.IsX);

            // À 23 cm du centre : anneau 7 (rayons 0,232 … 0,29 → anneau 6 ; 0,174 … 0,232 → anneau 7).
            ShotResult off = face.Evaluate(go.transform.position + Vector3.right * 0.20f, Vector3.back, out _);
            Assert.AreEqual(7, off.Ring);

            // Par l'arrière : rien.
            ShotResult behind = face.Evaluate(go.transform.position, Vector3.forward, out _);
            Assert.AreEqual(0, behind.Ring);

            Object.DestroyImmediate(face.Definition);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TargetScaleEnlargesTheRings()
        {
            // Une cible agrandie ×2 : un impact à 0,40 m du centre vaut l'anneau de 0,20 m à l'échelle 1.
            GameObject root = new GameObject("Root");
            root.transform.localScale = Vector3.one * 2f;
            GameObject faceObject = new GameObject("Face");
            faceObject.transform.SetParent(root.transform, false);
            TargetFace face = faceObject.AddComponent<TargetFace>();
            face.Definition = ScriptableObject.CreateInstance<TargetDefinition>();

            ShotResult scaled = face.Evaluate(root.transform.position + Vector3.right * 0.40f, Vector3.back, out _);
            Assert.AreEqual(7, scaled.Ring);

            Object.DestroyImmediate(face.Definition);
            Object.DestroyImmediate(root);
        }

        // ---------------------------------------------------------------- étoiles

        private static LevelDefinition MakeLevel()
        {
            LevelDefinition level = ScriptableObject.CreateInstance<LevelDefinition>();
            level.starThresholds = new[] { 60, 84, 108 };
            return level;
        }

        [Test]
        public void StarsFollowTheThresholds()
        {
            LevelDefinition level = MakeLevel();
            Assert.AreEqual(0, StarRating.Evaluate(level, 59, 1f));
            Assert.AreEqual(1, StarRating.Evaluate(level, 60, 1f));
            Assert.AreEqual(2, StarRating.Evaluate(level, 84, 1f));
            Assert.AreEqual(3, StarRating.Evaluate(level, 200, 1f));
            Object.DestroyImmediate(level);
        }

        [Test]
        public void DifficultyScalesTheThresholds()
        {
            LevelDefinition level = MakeLevel();
            Assert.AreEqual(48, level.GetThreshold(1, 0.8f));  // Facile
            Assert.AreEqual(72, level.GetThreshold(1, 1.2f));  // Difficile
            Assert.AreEqual(1, StarRating.Evaluate(level, 50, 0.8f));
            Assert.AreEqual(0, StarRating.Evaluate(level, 50, 1.2f));
            Object.DestroyImmediate(level);
        }

        [Test]
        public void NextThresholdPointsToTheNextStar()
        {
            LevelDefinition level = MakeLevel();
            Assert.AreEqual(60, StarRating.NextThreshold(level, 10, 1f));
            Assert.AreEqual(84, StarRating.NextThreshold(level, 70, 1f));
            Assert.AreEqual(0, StarRating.NextThreshold(level, 500, 1f));
            Object.DestroyImmediate(level);
        }

        [Test]
        public void DifficultyApplierScalesTimeAndArrows()
        {
            GameObject go = new GameObject("Difficulty");
            DifficultyApplier applier = go.AddComponent<DifficultyApplier>();
            Services.Register(applier);

            GameCatalog catalog = ScriptableObject.CreateInstance<GameCatalog>();
            DifficultyProfile easy = ScriptableObject.CreateInstance<DifficultyProfile>();
            easy.difficulty = GameDifficulty.Easy;
            easy.timeMultiplier = 1.5f;
            easy.arrowsMultiplier = 1.5f;
            easy.targetSizeMultiplier = 1.5f;
            catalog.difficulties = new[] { easy };
            Services.Register(catalog);

            // Sans SettingsService, la difficulté par défaut est Normal : aucun profil Normal → pas de changement.
            Assert.AreEqual(90f, applier.ScaleTime(90f), 0.001f);

            // Avec un profil figé (une manche en cours), les paramètres de la difficulté s'appliquent.
            GameObject settingsHost = new GameObject("Settings");
            var settings = settingsHost.AddComponent<ArcheryChallenge.Save.SettingsService>();
            Services.Register(settings);
            ArcheryChallenge.Save.JsonFileStore.SetRootForTests(System.IO.Path.Combine(Application.temporaryCachePath, "tests_" + System.Guid.NewGuid().ToString("N")));
            settings.Data.difficulty = (int)GameDifficulty.Easy;
            Assert.AreEqual(135f, applier.ScaleTime(90f), 0.001f);
            Assert.AreEqual(18, applier.ScaleArrows(12));

            ArcheryChallenge.Save.JsonFileStore.SetRootForTests(null);
            Object.DestroyImmediate(settingsHost);
            Object.DestroyImmediate(easy);
            Object.DestroyImmediate(catalog);
            Object.DestroyImmediate(go);
        }
    }
}
