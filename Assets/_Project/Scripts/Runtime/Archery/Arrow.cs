// Arrow : une flèche (tenue, encochée, en vol, plantée).
//
// OÙ LE POSER : sur la racine de chaque prefab de flèche (Arrow_Wood, Arrow_Ash, Arrow_Carbon).
// Hiérarchie attendue (voir tutoriel §P3.7) :
//   Arrow_Wood (Rigidbody, Capsule Collider axe Z, Arrow)    ← origine = ENCOCHE (arrière)
//   ├── Visual     modèle 3D (pointe vers +Z)
//   ├── Nock       (0, 0, 0)       arrière de la flèche
//   ├── Tip        (0, 0, 0.74)    pointe
//   ├── GripAttach (0, 0, 0.08)    où la main tient la flèche
//   └── Trail      Trail Renderer (désactivé au départ)
//
// PHYSIQUE (cohérence de la simulation)
// En vol, la flèche est un vrai Rigidbody : la GRAVITÉ de Unity agit, plus une TRAÎNÉE de l'air
// (décélération quadratique proportionnelle au carré de la vitesse, coefficient de la flèche) et le
// VENT (accélération latérale proportionnelle à la vitesse du vent et à la sensibilité de la flèche).
// La flèche s'oriente selon sa vitesse. Pour ne jamais traverser une cible fine à grande vitesse,
// la collision n'est PAS laissée au moteur : un raycast part de la pointe sur la distance que la
// flèche va parcourir pendant le prochain pas de physique (détection continue « maison »).
//
// À l'impact : la flèche se plante (pointe au point d'impact), informe l'objet touché (cible,
// monstre) via IArrowReceiver, annonce l'événement ArrowImpacted (son, effet), puis disparaît
// au bout de « Life Time After Impact » secondes.

using ArcheryChallenge.Core;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Archery
{
    /// <summary>État d'une flèche.</summary>
    public enum ArrowState
    {
        /// <summary>Au repos dans le pool (désactivée).</summary>
        Pooled,
        /// <summary>Tenue à la main (VR) ou apparue devant le joueur.</summary>
        Held,
        /// <summary>Encochée sur la corde d'un arc.</summary>
        Nocked,
        /// <summary>En vol balistique.</summary>
        Flying,
        /// <summary>Lâchée sans tir : tombe au sol sous l'effet de la gravité.</summary>
        Dropped,
        /// <summary>Plantée dans une surface.</summary>
        Stuck
    }

    /// <summary>Comportement complet d'une flèche.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Arrow : MonoBehaviour
    {
        [SerializeField] private ArrowDefinition definition;
        [SerializeField] private Transform tip;
        [SerializeField] private Transform nock;
        [SerializeField] private Transform gripAttach;
        [SerializeField] private TrailRenderer trail;

        [Tooltip("Durée (s) pendant laquelle une flèche plantée reste visible.")]
        [SerializeField, Min(1f)] private float lifeTimeAfterImpact = 20f;

        /// <summary>Coefficient reliant « Drag » du fichier de données à la traînée réelle.</summary>
        private const float DragScale = 0.1f;

        /// <summary>Accélération (m/s² par m/s de vent) quand la sensibilité vaut 1.</summary>
        // Modérée (1,4, contre 2 avant le réglage « jeu moins difficile ») : le vent gêne sans rendre le jeu injouable.
        // Dérive latérale ≈ ½ · 1,4 · vent · t² (t ≈ 0,9 s à 30 m).
        private const float WindAcceleration = 1.4f;

        /// <summary>Durée maximale d'un vol avant disparition (flèche perdue).</summary>
        private const float MaxFlightTime = 12f;

        private static int hitMask = -1;
        private static int flyingCount;

        /// <summary>Nombre de flèches actuellement en vol (sert à attendre la fin du dernier tir d'une manche).</summary>
        public static int FlyingCount => flyingCount;

        private Rigidbody rb;
        private Collider[] colliders;
        private float stateTime;
        private Vector3 shooterPosition;
        private Vector3 launchPosition;
        private float dragMultiplier = 1f;
        private Collider ignoredCollider;
        private bool initialized;
        private Vector3 baseScale = Vector3.one;

        public ArrowDefinition Definition
        {
            get => definition;
            set => definition = value;
        }

        public ArrowState State { get; private set; } = ArrowState.Pooled;
        public Transform Tip => tip != null ? tip : transform;
        public Transform Nock => nock != null ? nock : transform;
        public Transform GripAttach => gripAttach;

        /// <summary>Position du tireur au moment du tir (pour la distance de tir).</summary>
        public Vector3 ShooterPosition => shooterPosition;

        /// <summary>Vitesse actuelle (m/s).</summary>
        public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;

        /// <summary>Distance parcourue depuis le départ (m).</summary>
        public float DistanceFlown => Vector3.Distance(launchPosition, transform.position);

        public Rigidbody Body => rb;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            baseScale = transform.localScale;
            rb = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (hitMask == -1)
                hitMask = BuildHitMask();
            if (trail != null)
                trail.emitting = false;
        }

        // Tout est touchable SAUF le joueur, les arcs, les autres flèches, les zones d'encochage,
        // les bornes de plateforme et les couches techniques de Unity.
        private static int BuildHitMask()
        {
            int mask = ~0;
            string[] excluded = { "Player", "Bow", "Arrow", "ArrowZone", "PlatformBounds", "Ignore Raycast", "UI", "Water" };
            foreach (string name in excluded)
            {
                int layer = LayerMask.NameToLayer(name);
                if (layer >= 0)
                    mask &= ~(1 << layer);
            }
            return mask;
        }

        // ---------------------------------------------------------------- états

        /// <summary>
        /// La flèche est tenue par une main : elle devient enfant de la main, sa pointe vers l'avant,
        /// la prise (GripAttach) dans la paume.
        /// </summary>
        public void HoldBy(Transform hand)
        {
            Initialize();
            gameObject.SetActive(true);
            SetState(ArrowState.Held);
            SetPhysics(kinematic: true, collisions: true, trigger: true);
            transform.SetParent(hand, false);
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
            Vector3 grip = gripAttach != null ? gripAttach.localPosition : Vector3.zero;
            transform.localPosition = -grip;
        }

        /// <summary>Marque la flèche comme encochée (l'arc se charge de la positionner).</summary>
        public void SetNocked(Transform bowRoot)
        {
            Initialize();
            gameObject.SetActive(true);
            SetState(ArrowState.Nocked);
            SetPhysics(kinematic: true, collisions: true, trigger: true);
            transform.SetParent(bowRoot, true);
        }

        /// <summary>Tire la flèche avec la vitesse donnée.</summary>
        /// <param name="velocity">Vitesse de départ (monde).</param>
        /// <param name="shooter">Position du tireur.</param>
        /// <param name="dragFactor">Multiplicateur de traînée imposé par la difficulté.</param>
        public void Launch(Vector3 velocity, Vector3 shooter, float dragFactor = 1f)
        {
            Initialize();
            transform.SetParent(null, true);
            transform.localScale = baseScale;
            shooterPosition = shooter;
            launchPosition = transform.position;
            dragMultiplier = dragFactor;
            ignoredCollider = null;
            SetState(ArrowState.Flying);
            SetPhysics(kinematic: false, collisions: false, trigger: false);
            rb.useGravity = true;
            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            if (velocity.sqrMagnitude > 0.01f)
                rb.rotation = Quaternion.LookRotation(velocity);
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        /// <summary>La flèche est lâchée sans tir : elle tombe et disparaît peu après.</summary>
        public void Drop()
        {
            Initialize();
            transform.SetParent(null, true);
            transform.localScale = baseScale;
            SetState(ArrowState.Dropped);
            SetPhysics(kinematic: false, collisions: true, trigger: false);
            rb.useGravity = true;
            rb.constraints = RigidbodyConstraints.None;
            if (trail != null)
                trail.emitting = false;
        }

        /// <summary>Prépare la flèche à être recyclée par le pool.</summary>
        public void ResetForPool()
        {
            Initialize();
            transform.SetParent(null, true);
            transform.localScale = baseScale;
            SetState(ArrowState.Pooled);
            // Unity refuse de modifier la vitesse d'un corps cinématique (avertissement) : on ne l'annule
            // que si la flèche est encore dynamique ; un corps cinématique n'a de toute façon pas de vitesse.
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            SetPhysics(kinematic: true, collisions: true, trigger: true);
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
            gameObject.SetActive(false);
        }

        private void SetState(ArrowState state)
        {
            if (State == ArrowState.Flying)
                flyingCount = Mathf.Max(0, flyingCount - 1);
            if (state == ArrowState.Flying)
                flyingCount++;
            State = state;
            stateTime = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            flyingCount = 0;
            hitMask = -1;
        }

        private void OnDisable()
        {
            // Une flèche en vol désactivée (changement de scène) ne doit pas fausser le compteur.
            if (State == ArrowState.Flying)
                SetState(ArrowState.Pooled);
        }

        private void SetPhysics(bool kinematic, bool collisions, bool trigger)
        {
            if (rb == null)
                return;
            // Le Rigidbody ne peut recevoir une vitesse que s'il n'est pas cinématique : on bascule
            // d'abord le mode, puis les autres propriétés.
            rb.isKinematic = kinematic;
            rb.detectCollisions = collisions;
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null)
                    colliders[i].isTrigger = trigger;
        }

        // ---------------------------------------------------------------- vol

        private void FixedUpdate()
        {
            if (State != ArrowState.Flying)
                return;

            stateTime += Time.fixedDeltaTime;
            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;

            // Traînée : décélération proportionnelle au carré de la vitesse.
            float k = (definition != null ? definition.drag : 0.02f) * DragScale * dragMultiplier;
            v -= v * (k * v.magnitude * dt);

            // Vent : accélération horizontale dans le sens du vent.
            WindService wind = Services.Get<WindService>();
            if (wind != null)
            {
                float sensitivity = definition != null ? definition.windSensitivity : 1f;
                v += wind.CurrentWind * (WindAcceleration * sensitivity * dt);
            }
            rb.linearVelocity = v;

            float speed = v.magnitude;
            if (speed > 0.5f)
                rb.rotation = Quaternion.LookRotation(v);

            // Détection continue : raycast de la pointe sur la distance du prochain pas.
            Vector3 origin = Tip.position;
            Vector3 direction = speed > 0.01f ? v / speed : transform.forward;
            float distance = speed * dt + 0.04f;
            RaycastHit[] hits = RaycastBuffer;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, hitMask, QueryTriggerInteraction.Ignore);
            if (count > 0)
            {
                // On garde l'impact le plus proche qui ne soit pas le collider déjà traversé.
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].collider == ignoredCollider)
                        continue;
                    if (hits[i].distance < bestDistance)
                    {
                        bestDistance = hits[i].distance;
                        best = i;
                    }
                }
                if (best >= 0 && HandleImpact(hits[best], direction, speed))
                    return;
            }

            if (stateTime > MaxFlightTime || transform.position.y < -80f)
                Despawn();
        }

        private static readonly RaycastHit[] RaycastBuffer = new RaycastHit[8];

        // Renvoie vrai si le vol s'arrête (plantée ou repoussée), faux si la flèche traverse.
        private bool HandleImpact(RaycastHit hit, Vector3 direction, float speed)
        {
            IArrowReceiver receiver = hit.collider.GetComponentInParent<IArrowReceiver>();
            ArrowHitOutcome outcome = ArrowHitOutcome.Stick;
            if (receiver != null)
                outcome = receiver.OnArrowHit(this, hit.point, direction, hit.collider);

            if (outcome == ArrowHitOutcome.PassThrough)
            {
                ignoredCollider = hit.collider;
                return false;
            }

            ArrowImpactInfo info = new ArrowImpactInfo
            {
                Point = hit.point,
                Normal = hit.normal,
                Collider = hit.collider,
                Surface = SurfaceTag.Of(hit.collider),
                Speed = speed,
                HitTarget = receiver is Target,
                Shot = receiver is Target target ? target.LastShot : default
            };

            if (outcome == ArrowHitOutcome.Deflect)
            {
                // L'arme rebondit mollement sur la défense du monstre puis tombe.
                Vector3 bounce = Vector3.Reflect(direction, hit.normal) * (speed * 0.15f);
                transform.position = hit.point - direction * 0.1f;
                Drop();
                rb.linearVelocity = bounce;
                info.Surface = SurfaceType.Metal;
                GameEvents.RaiseArrowImpacted(info);
                return true;
            }

            Stick(hit, direction, receiver != null);
            GameEvents.RaiseArrowImpacted(info);
            return true;
        }

        private void Stick(RaycastHit hit, Vector3 direction, bool followHitObject)
        {
            // Position : la pointe s'enfonce de 6 cm dans la surface.
            const float penetration = 0.06f;
            Vector3 tipLocal = transform.InverseTransformPoint(Tip.position);
            Quaternion rotation = Quaternion.LookRotation(direction);
            transform.rotation = rotation;
            transform.position = hit.point + direction * penetration - rotation * tipLocal;

            SetState(ArrowState.Stuck);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            SetPhysics(kinematic: true, collisions: false, trigger: true);
            if (trail != null)
                trail.emitting = false;

            // On suit l'objet touché (cible mobile, monstre) seulement s'il a une échelle uniforme :
            // une flèche enfant d'un cube aplati serait déformée.
            if (followHitObject)
            {
                Transform parent = hit.collider.transform;
                Vector3 s = parent.lossyScale;
                bool uniform = Mathf.Abs(s.x - s.y) < 0.01f && Mathf.Abs(s.y - s.z) < 0.01f;
                if (uniform)
                    transform.SetParent(parent, true);
            }
        }

        private void Update()
        {
            if (State == ArrowState.Stuck)
            {
                stateTime += Time.deltaTime;
                if (stateTime >= lifeTimeAfterImpact)
                    Despawn();
            }
            else if (State == ArrowState.Dropped)
            {
                stateTime += Time.deltaTime;
                if (stateTime >= 6f || transform.position.y < -80f)
                    Despawn();
            }
        }

        /// <summary>Rend la flèche au pool (ou la détruit s'il n'y a pas de pool).</summary>
        public void Despawn()
        {
            ArrowPool pool = Services.Get<ArrowPool>();
            if (pool != null)
                pool.Release(this);
            else
                Destroy(gameObject);
        }
    }
}
