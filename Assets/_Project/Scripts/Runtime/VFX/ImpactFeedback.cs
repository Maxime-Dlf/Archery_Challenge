// ImpactFeedback : effets visuels déclenchés par les événements du jeu (impacts, déblocages…).
//
// OÙ LE POSER : objet « Systems » de la scène Boot. RÉGLAGES (tutoriel §P15.5) : « Entries » : une ligne par
// matière (Wood → VFX_Impact_Wood, Straw, Dirt, Stone, Flesh) ; « Bullseye Effect » (éclat doré du X) ;
// « Unlock Effect » ; « Monster Die Effect » ; « Gate Dust Effect ».
//
// Les effets sont recyclés (pool) et se désactivent seuls. Le service fait aussi trembler légèrement la
// cible touchée. Contrainte Quest : moins de 50 particules par effet, aucune lumière.

using System;
using System.Collections;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.VFX
{
    /// <summary>Effet associé à une matière.</summary>
    [Serializable]
    public struct SurfaceEffect
    {
        public SurfaceType surface;
        public GameObject effect;
    }

    /// <summary>Gestion des effets visuels.</summary>
    [DisallowMultipleComponent]
    public class ImpactFeedback : MonoBehaviour
    {
        [SerializeField] private SurfaceEffect[] entries = new SurfaceEffect[0];
        [SerializeField] private GameObject bullseyeEffect;
        [SerializeField] private GameObject unlockEffect;
        [SerializeField] private GameObject monsterHitEffect;
        [SerializeField] private GameObject monsterDieEffect;
        [SerializeField] private GameObject gateDustEffect;

        private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly HashSet<Transform> wobbling = new HashSet<Transform>();

        public SurfaceEffect[] Entries
        {
            get => entries;
            set => entries = value;
        }

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        private void OnEnable()
        {
            GameEvents.ArrowImpacted += OnImpact;
            GameEvents.EquipmentUnlocked += OnUnlocked;
            GameEvents.MonsterEvent += OnMonsterEvent;
            GameEvents.BarrierOpened += OnBarrierOpened;
        }

        private void OnDisable()
        {
            GameEvents.ArrowImpacted -= OnImpact;
            GameEvents.EquipmentUnlocked -= OnUnlocked;
            GameEvents.MonsterEvent -= OnMonsterEvent;
            GameEvents.BarrierOpened -= OnBarrierOpened;
        }

        private void OnImpact(ArrowImpactInfo info)
        {
            GameObject effect = null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].surface == info.Surface)
                    effect = entries[i].effect;

            Quaternion rotation = Quaternion.LookRotation(info.Normal.sqrMagnitude > 0.01f ? info.Normal : Vector3.up);
            Spawn(effect, info.Point, rotation);

            if (info.HitTarget)
            {
                if (info.Shot.IsX)
                    Spawn(bullseyeEffect, info.Point, rotation);
                Target target = info.Collider != null ? info.Collider.GetComponentInParent<Target>() : null;
                if (target != null && !target.IsBroken && !(target.Mover != null && target.Mover.Pattern == TargetPattern.PopUp))
                    StartCoroutine(Wobble(target.transform, Mathf.Clamp(info.Speed / 10f, 1.5f, 5f)));
            }
        }

        private void OnUnlocked(string id, EquipmentKind kind)
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform root = mode != null ? mode.ActiveRoot : null;
            if (root != null)
                Spawn(unlockEffect, root.position, Quaternion.identity);
        }

        private void OnMonsterEvent(Vector3 position, string kind)
        {
            if (kind == "hit")
                Spawn(monsterHitEffect, position, Quaternion.identity);
            else if (kind == "die")
                Spawn(monsterDieEffect, position, Quaternion.identity);
        }

        private void OnBarrierOpened(string id)
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            if (head != null)
                Spawn(gateDustEffect, head.position + head.forward * 3f - Vector3.up * 1.2f, Quaternion.identity);
        }

        /// <summary>Joue un effet à une position (recyclage automatique).</summary>
        public void Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
                return;

            if (!pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools[prefab] = stack;
            }

            GameObject instance = null;
            while (stack.Count > 0 && instance == null)
                instance = stack.Pop();
            if (instance == null)
                instance = Instantiate(prefab, transform);

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(false);
            instance.SetActive(true);
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>();
            float duration = 1f;
            foreach (ParticleSystem ps in systems)
            {
                ps.Clear();
                ps.Play();
                duration = Mathf.Max(duration, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            StartCoroutine(ReturnAfter(prefab, instance, duration));
        }

        private IEnumerator ReturnAfter(GameObject prefab, GameObject instance, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (instance != null)
            {
                instance.SetActive(false);
                pools[prefab].Push(instance);
            }
        }

        // Léger tremblement de la cible touchée : oscillation amortie autour de l'axe X.
        private IEnumerator Wobble(Transform target, float degrees)
        {
            if (target == null || !wobbling.Add(target))
                yield break;

            Quaternion baseRotation = target.localRotation;
            float t = 0f;
            const float duration = 0.45f;
            while (t < duration && target != null)
            {
                t += Time.deltaTime;
                float damping = 1f - t / duration;
                float angle = Mathf.Sin(t * 40f) * degrees * damping * damping;
                target.localRotation = baseRotation * Quaternion.Euler(angle, 0f, 0f);
                yield return null;
            }
            if (target != null)
                target.localRotation = baseRotation;
            wobbling.Remove(target);
        }
    }
}
