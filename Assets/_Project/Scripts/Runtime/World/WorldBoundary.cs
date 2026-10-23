// WorldBoundary : murs invisibles autour de la carte et réapparition si le joueur tombe.
//
// OÙ LE POSER : sur l'objet « WorldBoundary » de la scène World (§P8-A.10).
// RÉGLAGES : « Map Size » = 160 m ; « Wall Height » = 60 m ; « Respawn Height » = -20 m (sous cette
// altitude, le joueur réapparaît au hub). Menu contextuel du composant : « Create Walls ».

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Limites de la carte.</summary>
    [DisallowMultipleComponent]
    public class WorldBoundary : MonoBehaviour
    {
        [SerializeField, Min(10f)] private float mapSize = 160f;
        [SerializeField, Min(5f)] private float wallHeight = 60f;
        [SerializeField, Min(0.1f)] private float wallThickness = 2f;
        [SerializeField] private float respawnHeight = -20f;
        [SerializeField] private string respawnSpawnId = "hub";

        private float nextCheck;

        private void Awake()
        {
            if (transform.childCount == 0)
                CreateWalls();
        }

        /// <summary>Crée les 4 murs invisibles sur les bords du terrain (0,0) → (map size, map size).</summary>
        [ContextMenu("Create Walls")]
        public void CreateWalls()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject old = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(old); else DestroyImmediate(old);
            }

            float half = mapSize * 0.5f;
            float h = wallHeight;
            float t = wallThickness;
            // Chaque mur est posé juste à l'extérieur du carré de la carte.
            CreateWall("Wall_North", new Vector3(half, h * 0.5f, mapSize + t * 0.5f), new Vector3(mapSize + 2f * t, h, t));
            CreateWall("Wall_South", new Vector3(half, h * 0.5f, -t * 0.5f), new Vector3(mapSize + 2f * t, h, t));
            CreateWall("Wall_East", new Vector3(mapSize + t * 0.5f, h * 0.5f, half), new Vector3(t, h, mapSize));
            CreateWall("Wall_West", new Vector3(-t * 0.5f, h * 0.5f, half), new Vector3(t, h, mapSize));
        }

        private void CreateWall(string wallName, Vector3 center, Vector3 size)
        {
            GameObject wall = new GameObject(wallName);
            wall.transform.SetParent(transform, false);
            wall.transform.position = center;
            BoxCollider box = wall.AddComponent<BoxCollider>();
            box.size = size;
            int layer = LayerMask.NameToLayer("Environment");
            if (layer >= 0)
                wall.layer = layer;
        }

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.5f;

            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform root = mode != null ? mode.ActiveRoot : null;
            if (root == null || root.position.y > respawnHeight)
                return;

            // Tombé sous la carte : retour au hub (ou au camp si le hub est absent).
            if (!mode.TeleportToSpawn(respawnSpawnId))
                mode.TeleportToSpawn("camp");
            GameEvents.RaiseToast("Retour au village.");
        }
    }
}
