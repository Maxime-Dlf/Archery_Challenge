// PlatformQualitySettings : réglages de rendu propres au Quest (Android) et au PC.
//
// OÙ LE POSER : objet « Systems » de la scène Boot (§P16.2 du tutoriel).
//
// Sur Android (Quest 2 : 72 Hz, budget 13,8 ms par image) :
//   - distance des arbres et de l'herbe réduites, densité d'herbe divisée par deux ;
//   - fréquence cible 72 i/s ; pas de synchronisation verticale logicielle (le casque la gère) ;
//   - au besoin, échelle de rendu XR légèrement réduite (Render Scale du tutoriel P16.4).
// Sur PC : valeurs confortables. Les terrains sont réglés à chaque chargement de scène.

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace ArcheryChallenge.Core
{
    /// <summary>Qualité adaptée à la plateforme.</summary>
    [DisallowMultipleComponent]
    public class PlatformQualitySettings : MonoBehaviour
    {
        [Header("Quest / Android")]
        [SerializeField, Min(20f)] private float questTreeDistance = 100f;
        [SerializeField, Min(5f)] private float questDetailDistance = 25f;
        [SerializeField, Range(0.1f, 1f)] private float questDetailDensity = 0.5f;
        [SerializeField, Range(0.5f, 1.2f)] private float questRenderScale = 1f;

        [Header("PC")]
        [SerializeField, Min(20f)] private float pcTreeDistance = 150f;
        [SerializeField, Min(5f)] private float pcDetailDistance = 40f;

        private void Awake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Application.targetFrameRate = 72;
            QualitySettings.vSyncCount = 0;
            XRSettings.eyeTextureResolutionScale = questRenderScale;
#endif
            Apply();
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        /// <summary>Applique les réglages au terrain actif.</summary>
        public void Apply()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return;
#if UNITY_ANDROID
            terrain.treeDistance = questTreeDistance;
            terrain.detailObjectDistance = questDetailDistance;
            terrain.detailObjectDensity = questDetailDensity;
            terrain.heightmapPixelError = 8f;
#else
            terrain.treeDistance = pcTreeDistance;
            terrain.detailObjectDistance = pcDetailDistance;
#endif
        }
    }
}
