// GameBootstrap : démarre le jeu depuis la scène Boot.
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
// RÉGLAGES (tutoriel §P2.3) : Catalog ← Data/GameCatalog ; World Scene = « World » ;
//   Default Spawn Id = « camp ».
//
// SÉQUENCE DE DÉMARRAGE
//  1. Le catalogue de données est enregistré dans le registre de services.
//  2. L'écran reste noir (FadeService démarre opaque) pendant que la scène World est chargée en additif.
//  3. Le joueur (VR ou clavier/souris selon PlayerModeService) est placé sur le point d'apparition « camp ».
//  4. Fondu d'entrée : le joueur se retrouve assis au feu de camp.
//
// Pour tester une scène isolée (bac à sable), mettez son nom dans « World Scene » : elle est chargée à
// la place de World.

using System.Collections;
using ArcheryChallenge.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcheryChallenge.Core
{
    /// <summary>Point d'entrée du jeu.</summary>
    [DisallowMultipleComponent]
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameCatalog catalog;
        [SerializeField] private string worldScene = "World";
        [SerializeField] private string defaultSpawnId = "camp";

        [Tooltip("Délai maximal (s) d'attente du point d'apparition avant d'abandonner.")]
        [SerializeField, Min(1f)] private float spawnTimeout = 10f;

        /// <summary>Vrai quand le monde est chargé et le joueur en place.</summary>
        public bool IsReady { get; private set; }

        public GameCatalog Catalog => catalog;
        public string WorldScene
        {
            get => worldScene;
            set => worldScene = value;
        }

        private void Awake()
        {
            Services.Register(this);
            if (catalog != null)
                Services.Register(catalog);
            else
                Debug.LogError("[GameBootstrap] Aucun GameCatalog assigné : glissez Data/GameCatalog dans le champ « Catalog ».", this);

            // Quest : 72 Hz natif ; au PC on laisse la synchronisation verticale décider.
            Application.targetFrameRate = -1;
        }

        private void OnDestroy() => Services.Unregister(this);

        private IEnumerator Start()
        {
            // Une image d'attente : tous les services ont eu leur Awake/Start.
            yield return null;

            SceneService scenes = Services.Get<SceneService>();
            FadeService fade = Services.Get<FadeService>();
            PlayerModeService mode = Services.Get<PlayerModeService>();

            if (scenes != null && !string.IsNullOrEmpty(worldScene) && !scenes.IsLoaded(worldScene))
            {
                yield return scenes.LoadRoutine(worldScene);
                Scene world = SceneManager.GetSceneByName(worldScene);
                if (world.IsValid() && world.isLoaded)
                    SceneManager.SetActiveScene(world);
            }

            // Attente du point d'apparition (au cas où la scène chargée l'enregistre une image plus tard).
            float waited = 0f;
            PlayerSpawnPoint spawn = null;
            while (!PlayerSpawnPoint.TryFind(defaultSpawnId, out spawn) && waited < spawnTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (mode != null)
            {
                if (spawn != null)
                    mode.TeleportTo(spawn.SpawnPose);
                else
                    Debug.LogWarning($"[GameBootstrap] Point d'apparition « {defaultSpawnId} » introuvable.");
            }

            yield return null;
            IsReady = true;
            if (fade != null)
                yield return fade.FadeIn(0.8f);
        }
    }
}
