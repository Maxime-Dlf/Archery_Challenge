// MonsterBrain : intelligence d'un monstre (machine à états) et animation.
//
// OÙ LE POSER : sur la racine d'un prefab de monstre, avec NavMeshAgent, MonsterHealth, Rigidbody
// (cinématique) et un Collider. RÉGLAGE : « Animator » ← l'enfant qui porte l'Animator (le contrôleur
// généré par Tools → Archery Challenge → Create Monster Animators).
//
// ÉTATS (§P10.1) : Errance → Repérage (joueur à moins de 15 m) → Poursuite → Attaque (à moins de
// 2 m) → Touché → Étourdi → Mort. Le slime peut aussi FUIR (humeur aléatoire). La tortue se met
// EN DÉFENSE quand le joueur la vise : elle bloque alors les flèches de face.
//
// Les animations sont choisies par le script (CrossFade sur le nom d'un état) : les contrôleurs
// générés n'ont aucune transition automatique ni paramètre.
//
// ÉCONOMIE DE CPU : le cerveau ne travaille que si le joueur est à moins de « Activation Distance »
// (réglée par le MonsterSpawner, 60 m par défaut) ; au-delà il s'endort.

using ArcheryChallenge.Core;
using ArcheryChallenge.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ArcheryChallenge.Monsters
{
    /// <summary>États d'un monstre.</summary>
    public enum MonsterState
    {
        Wander,
        Sense,
        Chase,
        Flee,
        Attack,
        Hit,
        Dizzy,
        Dead
    }

    /// <summary>IA d'un monstre.</summary>
    [DisallowMultipleComponent]
    public class MonsterBrain : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MonsterDefinition definition;

        private NavMeshAgent agent;
        private MonsterHealth health;
        private MonsterState state = MonsterState.Wander;
        private Vector3 home;
        private float wanderRadius = 10f;
        private float activationDistance = 60f;
        private float stateTimer;
        private float nextAttackAt;
        private float nextThink;
        private float fleeChance = 0.25f;
        private string currentAnimation = "";
        private bool warnedNoNavMesh;
        private Transform playerHead;
        private bool defending;

        public MonsterState State => state;
        public bool IsDefending => defending;
        public bool IsDead => state == MonsterState.Dead;

        public MonsterDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        /// <summary>Distance à laquelle le monstre se met à réfléchir (sinon il dort).</summary>
        public float ActivationDistance
        {
            get => activationDistance;
            set => activationDistance = value;
        }

        public float WanderRadius
        {
            get => wanderRadius;
            set => wanderRadius = value;
        }

        /// <summary>Point de départ autour duquel il erre.</summary>
        public Vector3 Home
        {
            get => home;
            set => home = value;
        }

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            health = GetComponent<MonsterHealth>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (definition == null && health != null)
                definition = health.Definition;
            home = transform.position;
        }

        private void OnEnable()
        {
            state = MonsterState.Wander;
            stateTimer = Random.Range(0.5f, 2f);
            currentAnimation = "";
            PlayAnimation("IdleNormal");
        }

        private void Update()
        {
            if (state == MonsterState.Dead || agent == null || definition == null)
                return;

            // Le monstre dort si le joueur est loin.
            if (Time.time < nextThink)
                return;
            Transform head = GetPlayerHead();
            if (head == null)
                return;
            Vector3 toPlayer = head.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance > activationDistance)
            {
                nextThink = Time.time + 1f;
                if (agent.enabled && agent.isOnNavMesh)
                    agent.isStopped = true;
                return;
            }

            if (!agent.isOnNavMesh)
            {
                if (!warnedNoNavMesh)
                {
                    warnedNoNavMesh = true;
                    Debug.LogWarning($"[MonsterBrain] {name} n'est pas sur le NavMesh (bake manquant ou point d'apparition trop haut) : il reste immobile.", this);
                }
                return;
            }

            stateTimer -= Time.deltaTime;
            UpdateDefense(head, distance);

            switch (state)
            {
                case MonsterState.Wander: TickWander(distance); break;
                case MonsterState.Sense: TickSense(toPlayer); break;
                case MonsterState.Chase: TickChase(head, distance, toPlayer); break;
                case MonsterState.Flee: TickFlee(toPlayer); break;
                case MonsterState.Attack: TickAttack(head, distance, toPlayer); break;
                case MonsterState.Hit:
                case MonsterState.Dizzy: TickStun(); break;
            }
        }

        // ---------------------------------------------------------------- états

        private void TickWander(float distanceToPlayer)
        {
            // Repérage du joueur.
            if (distanceToPlayer < definition.senseRadius && !PlayerIsSafe())
            {
                EnterState(MonsterState.Sense, 0.9f);
                return;
            }

            if (stateTimer <= 0f)
            {
                // Nouveau point d'errance dans le rayon autour du point d'origine.
                Vector2 random = Random.insideUnitCircle * wanderRadius;
                Vector3 candidate = home + new Vector3(random.x, 0f, random.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    agent.isStopped = false;
                    agent.speed = definition.walkSpeed;
                    agent.SetDestination(hit.position);
                    PlayAnimation("WalkFWD");
                }
                stateTimer = Random.Range(3f, 6f);
            }
            else if (agent.isOnNavMesh && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f)
            {
                PlayAnimation("IdleNormal");
            }
        }

        private void TickSense(Vector3 toPlayer)
        {
            agent.isStopped = true;
            FaceDirection(toPlayer);
            if (stateTimer <= 0f)
            {
                bool flee = definition.maxHealth == 1 && Random.value < fleeChance;
                EnterState(flee ? MonsterState.Flee : MonsterState.Chase, flee ? 4f : 0f);
                if (flee)
                    PlayAnimation("RunFWD");
            }
        }

        private void TickChase(Transform head, float distance, Vector3 toPlayer)
        {
            if (PlayerIsSafe() || distance > definition.senseRadius * 1.8f)
            {
                EnterState(MonsterState.Wander, 1f);
                return;
            }

            if (distance <= definition.attackRange)
            {
                EnterState(MonsterState.Attack, 0f);
                return;
            }

            agent.isStopped = false;
            agent.speed = definition.runSpeed;
            agent.SetDestination(head.position);
            PlayAnimation("RunFWD");
        }

        private void TickFlee(Vector3 toPlayer)
        {
            Vector3 away = (transform.position - (transform.position + toPlayer)).normalized;
            Vector3 target = transform.position + away * 6f;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                agent.isStopped = false;
                agent.speed = definition.runSpeed;
                agent.SetDestination(hit.position);
            }
            if (stateTimer <= 0f)
                EnterState(MonsterState.Wander, 2f);
        }

        private void TickAttack(Transform head, float distance, Vector3 toPlayer)
        {
            agent.isStopped = true;
            FaceDirection(toPlayer);

            if (distance > definition.attackRange * 1.4f)
            {
                EnterState(MonsterState.Chase, 0f);
                return;
            }

            if (Time.time >= nextAttackAt)
            {
                nextAttackAt = Time.time + definition.attackCooldown;
                PlayAnimation("Attack01", true);
                GameEvents.RaiseMonsterEvent(transform.position, "attack");
                StartCoroutine(DealDamageAfter(0.35f));
            }
        }

        private System.Collections.IEnumerator DealDamageAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (state != MonsterState.Attack || playerHead == null)
                yield break;
            Vector3 toPlayer = playerHead.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude <= definition.attackRange * 1.4f)
            {
                PlayerHealth player = Services.Get<PlayerHealth>();
                if (player != null)
                    player.Damage(definition.damage, transform.position);
            }
        }

        private void TickStun()
        {
            agent.isStopped = true;
            if (stateTimer <= 0f)
                EnterState(MonsterState.Chase, 0f);
        }

        // ---------------------------------------------------------------- événements

        /// <summary>Appelé par MonsterHealth quand le monstre est touché sans mourir.</summary>
        public void OnHit()
        {
            if (state == MonsterState.Dead)
                return;
            state = MonsterState.Hit;
            stateTimer = definition != null ? definition.stunDuration : 0.8f;
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
            PlayAnimation("GetHit", true);
        }

        /// <summary>Joue l'animation de mort puis désactive le monstre.</summary>
        public void Die()
        {
            state = MonsterState.Dead;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.isStopped = true;
            PlayAnimation("Die", true);
            foreach (Collider c in GetComponentsInChildren<Collider>())
                c.enabled = false;
            StartCoroutine(DisableAfter(2f));
        }

        private System.Collections.IEnumerator DisableAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            gameObject.SetActive(false);
        }

        /// <summary>Réactive un monstre (réapparition).</summary>
        public void Revive(Vector3 position)
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true))
                c.enabled = true;
            gameObject.SetActive(true);
            if (agent != null)
            {
                agent.enabled = false;
                transform.position = position;
                agent.enabled = true;
                if (agent.isOnNavMesh)
                    agent.isStopped = false;
            }
            else
            {
                transform.position = position;
            }
            if (health != null)
                health.ResetHealth();
            home = position;
            state = MonsterState.Wander;
            stateTimer = 1f;
            PlayAnimation("IdleNormal");
        }

        // ---------------------------------------------------------------- outils

        private void EnterState(MonsterState newState, float duration)
        {
            state = newState;
            stateTimer = duration;
            switch (newState)
            {
                case MonsterState.Sense:
                    PlayAnimation("SenseSomethingST", true);
                    GameEvents.RaiseMonsterEvent(transform.position, "sense");
                    break;
                case MonsterState.Wander:
                    if (agent.isOnNavMesh) agent.isStopped = true;
                    PlayAnimation("IdleNormal");
                    break;
            }
        }

        // Le joueur est « à l'abri » quand il est sur une plateforme ou au hub : les monstres abandonnent.
        private bool PlayerIsSafe()
        {
            var level = Levels.LevelController.Active;
            return level != null && level.State != LevelState.Available;
        }

        private Transform GetPlayerHead()
        {
            if (playerHead != null && playerHead.gameObject.activeInHierarchy)
                return playerHead;
            PlayerModeService mode = Services.Get<PlayerModeService>();
            playerHead = mode != null ? mode.ActiveHead : null;
            return playerHead;
        }

        // Tortue : se met en défense quand le joueur la regarde de loin (la vise).
        private void UpdateDefense(Transform head, float distance)
        {
            if (definition == null || !definition.canDefend)
            {
                defending = false;
                return;
            }
            Vector3 toMonster = (transform.position + Vector3.up * 0.5f - head.position).normalized;
            bool aimedAt = Vector3.Angle(head.forward, toMonster) < 18f && distance < 45f && distance > definition.attackRange;
            bool shouldDefend = aimedAt && state != MonsterState.Attack && state != MonsterState.Dead && state != MonsterState.Hit;
            if (shouldDefend && !defending)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                PlayAnimation("Defend", true);
            }
            defending = shouldDefend;
        }

        private void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;
            Quaternion target = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 360f * Time.deltaTime);
        }

        private void PlayAnimation(string stateName, bool restart = false)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;
            if (!restart && currentAnimation == stateName)
                return;
            currentAnimation = stateName;
            int hash = Animator.StringToHash(stateName);
            if (animator.HasState(0, hash))
                animator.CrossFadeInFixedTime(hash, restart ? 0.05f : 0.15f);
        }
    }
}
