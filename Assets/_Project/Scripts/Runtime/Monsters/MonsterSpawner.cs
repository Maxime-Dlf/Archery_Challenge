// MonsterSpawner : point d'apparition de monstres.
//
// OÙ LE POSER : sur un objet vide de la scène World, sous « --- MONSTERS --- » (§P10.7).
// RÉGLAGES : « Monster Prefab » (Monster_Slime / Monster_Turtle), « Count Min/Max » (nombre de monstres),
//   « Respawn Delay » = 180 s, « Wander Radius » = 10 m, « Activation Distance » = 60 m.
//
// Les monstres sont créés seulement quand le joueur s'approche à moins de « Activation Distance »
// (économie de CPU et de mémoire). Un monstre éliminé réapparaît après « Respawn Delay » secondes.
// Chaque point doit être juste au-dessus du terrain, sur une zone bleue du NavMesh.

using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using UnityEngine;
using UnityEngine.AI;

namespace ArcheryChallenge.Monsters
{
    /// <summary>Fait apparaître et réapparaître des monstres.</summary>
    [DisallowMultipleComponent]
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField, Min(0)] private int countMin = 1;
        [SerializeField, Min(0)] private int countMax = 2;
        [SerializeField, Min(1f)] private float respawnDelay = 180f;
        [SerializeField, Min(1f)] private float wanderRadius = 10f;
        [SerializeField, Min(5f)] private float activationDistance = 60f;

        private static readonly List<MonsterSpawner> Registry = new List<MonsterSpawner>();
        private static GameObject lastPrefab;

        private readonly List<MonsterBrain> monsters = new List<MonsterBrain>();
        private bool spawned;
        private float nextCheck;

        public GameObject MonsterPrefab
        {
            get => monsterPrefab;
            set => monsterPrefab = value;
        }

        public IReadOnlyList<MonsterBrain> Monsters => monsters;

        private void OnEnable()
        {
            Registry.Add(this);
            if (monsterPrefab != null)
                lastPrefab = monsterPrefab;
        }

        private void OnDisable() => Registry.Remove(this);

        private void Update()
        {
            if (spawned || Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 1f;

            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (head == null || monsterPrefab == null)
                return;
            if ((head.position - transform.position).magnitude <= activationDistance)
                SpawnAll();
        }

        /// <summary>Crée tous les monstres du point.</summary>
        public void SpawnAll()
        {
            spawned = true;
            int count = Random.Range(countMin, countMax + 1);
            for (int i = 0; i < count; i++)
                SpawnOne();
        }

        private MonsterBrain SpawnOne()
        {
            Vector3 position = RandomPoint();
            GameObject instance = Instantiate(monsterPrefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
            MonsterBrain brain = instance.GetComponent<MonsterBrain>();
            if (brain == null)
            {
                Debug.LogError("[MonsterSpawner] Le prefab n'a pas de MonsterBrain.", this);
                Destroy(instance);
                return null;
            }
            brain.Home = position;
            brain.WanderRadius = wanderRadius;
            brain.ActivationDistance = activationDistance;
            monsters.Add(brain);

            MonsterHealth health = instance.GetComponent<MonsterHealth>();
            if (health != null)
                health.Died += OnMonsterDied;
            return brain;
        }

        private Vector3 RandomPoint()
        {
            Vector2 random = Random.insideUnitCircle * (wanderRadius * 0.6f);
            Vector3 candidate = transform.position + new Vector3(random.x, 0f, random.y);
            return NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas) ? hit.position : transform.position;
        }

        private void OnMonsterDied(MonsterHealth health)
        {
            MonsterBrain brain = health.GetComponent<MonsterBrain>();
            StartCoroutine(RespawnAfterDelay(brain));
        }

        private IEnumerator RespawnAfterDelay(MonsterBrain brain)
        {
            yield return new WaitForSeconds(respawnDelay);
            if (brain != null)
                brain.Revive(RandomPoint());
        }

        /// <summary>Raccourci F8 : un monstre apparaît à 6 m devant le joueur.</summary>
        public static void SpawnInFrontOfPlayer()
        {
            GameObject prefab = lastPrefab;
            if (prefab == null && Registry.Count > 0)
                prefab = Registry[0].monsterPrefab;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (prefab == null || head == null)
            {
                Debug.LogWarning("[MonsterSpawner] Aucun prefab de monstre connu pour F8 (placez un MonsterSpawner dans World).");
                return;
            }

            Vector3 forward = head.forward;
            forward.y = 0f;
            forward.Normalize();

            // On cherche, de plus en plus loin devant le joueur, un point praticable du NavMesh (le camp, le hub
            // et les arènes n'en ont pas : les monstres n'y entrent jamais).
            Vector3 target = Vector3.zero;
            bool found = false;
            float baseY = mode.ActiveRoot != null ? mode.ActiveRoot.position.y : head.position.y;
            for (float distance = 6f; distance <= 48f && !found; distance += 6f)
            {
                Vector3 probe = head.position + forward * distance;
                probe.y = baseY;
                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                {
                    target = hit.position;
                    found = true;
                }
            }
            if (!found)
            {
                GameEvents.RaiseToast("Aucun terrain pour un monstre ici (essayez en forêt).");
                return;
            }

            GameObject instance = Instantiate(prefab, target, Quaternion.LookRotation(-forward));
            MonsterBrain brain = instance.GetComponent<MonsterBrain>();
            if (brain != null)
            {
                brain.Home = target;
                brain.ActivationDistance = 200f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Registry.Clear();
            lastPrefab = null;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.9f, 0.3f, 0.9f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.5f);
            Gizmos.color = new Color(0.9f, 0.3f, 0.9f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, wanderRadius);
        }
#endif
    }
}
