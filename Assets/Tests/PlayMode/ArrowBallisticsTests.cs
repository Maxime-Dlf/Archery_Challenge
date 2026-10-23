// Tests PlayMode : balistique de la flèche (gravité, traînée, plantage dans une cible).
//
// Les flèches sont de vrais Rigidbody : on vérifie qu'elles tombent sous l'effet de la gravité, qu'une
// flèche plus rapide arrive plus tôt, et qu'elle se plante dans un collider rencontré par le raycast
// continu (aucune traversée même à 70 m/s).

#if UNITY_EDITOR
using System.Collections;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArcheryChallenge.Tests
{
    public class ArrowBallisticsTests
    {
        private GameObject wall;
        private Arrow arrow;
        private ArrowDefinition definition;

        [SetUp]
        public void SetUp()
        {
            Services.Clear();
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Archery/Arrow_Wood.prefab");
            Assert.IsNotNull(prefab, "Le prefab Arrow_Wood doit exister (Tools → Archery Challenge → Build Prefabs).");
            definition = AssetDatabase.LoadAssetAtPath<ArrowDefinition>("Assets/_Project/Data/Arrows/Arrow_Wood.asset");

            GameObject instance = Object.Instantiate(prefab, new Vector3(0f, 10f, 0f), Quaternion.identity);
            arrow = instance.GetComponent<Arrow>();
            arrow.Definition = definition;
        }

        [TearDown]
        public void TearDown()
        {
            if (arrow != null) Object.Destroy(arrow.gameObject);
            if (wall != null) Object.Destroy(wall);
            Services.Clear();
        }

        private void MakeWall(float distance)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 10f, distance);
            wall.transform.localScale = new Vector3(20f, 20f, 0.2f);
            wall.layer = 0;
        }

        [UnityTest]
        public IEnumerator ArrowDropsUnderGravity()
        {
            arrow.Launch(Vector3.forward * 30f, Vector3.zero);
            float startY = arrow.transform.position.y;
            yield return new WaitForSeconds(0.5f);
            Assert.Less(arrow.transform.position.y, startY - 0.8f, "Après 0,5 s, la flèche doit avoir chuté d'environ 1,2 m.");
            Assert.Greater(arrow.transform.position.z, 10f, "Elle avance en même temps.");
        }

        [UnityTest]
        public IEnumerator ArrowPointsAlongItsVelocity()
        {
            arrow.Launch(new Vector3(0f, 12f, 25f), Vector3.zero);
            yield return new WaitForSeconds(0.1f);
            Vector3 velocity = arrow.Body.linearVelocity.normalized;
            Assert.Greater(Vector3.Dot(arrow.transform.forward, velocity), 0.99f);
        }

        [UnityTest]
        public IEnumerator FastArrowSticksIntoAThinWallWithoutTunnelling()
        {
            MakeWall(40f);
            arrow.Launch(Vector3.forward * 70f, Vector3.zero);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(ArrowState.Stuck, arrow.State, "La flèche doit se planter dans le mur de 20 cm à 70 m/s.");
            Assert.Less(arrow.Tip.position.z, 40.2f, "Sa pointe ne doit pas avoir traversé le mur.");
        }

        [UnityTest]
        public IEnumerator StuckArrowDisappearsAfterItsLifetime()
        {
            MakeWall(10f);
            BuildUtilsSetLifetime(arrow, 1f);
            arrow.Launch(Vector3.forward * 40f, Vector3.zero);
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(ArrowState.Stuck, arrow.State);
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(arrow == null || !arrow.gameObject.activeSelf, "Une flèche plantée disparaît après sa durée de vie.");
        }

        [UnityTest]
        public IEnumerator WindPushesAnArrowSideways()
        {
            GameObject host = new GameObject("Wind");
            WindService wind = host.AddComponent<WindService>();
            Services.Register(wind);

            // Témoin : sans vent.
            wind.SetManualLevel(0);
            yield return null;
            arrow.Launch(Vector3.forward * 30f, Vector3.zero);
            yield return new WaitForSeconds(0.8f);
            Vector3 calm = arrow.transform.position;

            // Même tir par vent fort.
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Archery/Arrow_Wood.prefab");
            Arrow windy = Object.Instantiate(prefab, new Vector3(0f, 10f, 0f), Quaternion.identity).GetComponent<Arrow>();
            windy.Definition = definition;
            wind.SetManualLevel(3);
            yield return null;
            windy.Launch(Vector3.forward * 30f, Vector3.zero);
            yield return new WaitForSeconds(0.8f);

            Vector3 delta = windy.transform.position - calm;
            delta.y = 0f;
            Assert.Greater(delta.magnitude, 0.25f, "Un vent fort fait dériver la flèche d'au moins 25 cm en 0,8 s.");
            Object.Destroy(windy.gameObject);
            Object.Destroy(host);
        }

        // Position de la flèche au moment où elle atteint 30 m de distance (null si elle n'y arrive pas).
        private IEnumerator FlyTo30m(Arrow shot, System.Action<Vector3> result)
        {
            shot.Launch(Vector3.forward * 90f, Vector3.zero);
            float t = 0f;
            while (shot != null && shot.transform.position.z < 30f && t < 2f)
            {
                t += Time.deltaTime;
                yield return null;
            }
            result(shot != null ? shot.transform.position : Vector3.zero);
        }

        [UnityTest]
        public IEnumerator StrongWindMovesTheImpactByMoreThanATargetRadiusAt30m()
        {
            GameObject host = new GameObject("Wind");
            WindService wind = host.AddComponent<WindService>();
            Services.Register(wind);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Archery/Arrow_Wood.prefab");

            // Sans vent (référence).
            wind.SetManualLevel(0);
            yield return null;
            Vector3 calm = Vector3.zero;
            yield return FlyTo30m(arrow, p => calm = p);

            // Vent « fort » de l'entraînement : il doit réellement handicaper (le blason fait 58 cm de rayon).
            wind.SetManualLevel(3);
            yield return null;
            Arrow windy = Object.Instantiate(prefab, new Vector3(0f, 10f, 0f), Quaternion.identity).GetComponent<Arrow>();
            windy.Definition = definition;
            Vector3 blown = Vector3.zero;
            yield return FlyTo30m(windy, p => blown = p);

            Vector3 delta = blown - calm;
            delta.y = 0f;
            Assert.Greater(wind.Speed, 6f, "Le niveau fort souffle à plus de 6 m/s.");
            Assert.Greater(delta.magnitude, 0.58f, $"À 30 m, un vent fort déplace l'impact de {delta.magnitude:0.00} m : plus que le rayon du blason.");
            Object.Destroy(windy.gameObject);
            Object.Destroy(host);
        }

        // La durée de vie est un champ sérialisé privé : on le règle via SerializedObject.
        private static void BuildUtilsSetLifetime(Arrow target, float seconds)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty("lifeTimeAfterImpact").floatValue = seconds;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
