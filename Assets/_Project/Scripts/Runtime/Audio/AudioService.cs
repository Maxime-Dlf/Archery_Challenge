// AudioService : sons 3D du tir, des impacts, des monstres, de l'interface et de la musique.
//
// OÙ LE POSER : objet « Systems » de la scène Boot. RÉGLAGES : « Banks » ← toutes les banques de sons
// de Data/Audio (le service les retrouve par leur NOM de fichier : BowDraw, BowRelease, ImpactTarget…).
//
// PRINCIPE (§P14 du tutoriel)
// - Chaque événement du jeu (tension de la corde, relâchement, départ de la flèche, impact, monstre…)
//   déclenche une banque de sons : le service lit GameEvents et joue le bon son à la bonne position.
// - Sons SPATIALISÉS : « Spatial Blend = 1 », atténuation logarithmique, distances min/max de la banque.
//   En casque, on entend donc d'où vient l'impact. Les sons 2D (interface, musique) ne sont pas spatialisés.
// - Les volumes (Général, Musique, Effets, Ambiance, Interface) viennent des réglages du menu. Ils sont
//   appliqués directement sur les sources : aucun AudioMixer n'est nécessaire.
// - Un petit pool de sources évite d'en créer à chaque son.

using System.Collections.Generic;
using ArcheryChallenge.Archery;
using ArcheryChallenge.Core;
using ArcheryChallenge.Save;
using UnityEngine;

namespace ArcheryChallenge.Audio
{
    /// <summary>Lecture des sons du jeu.</summary>
    [DisallowMultipleComponent]
    public class AudioService : MonoBehaviour
    {
        [SerializeField] private SoundBank[] banks = new SoundBank[0];
        [SerializeField, Min(4)] private int poolSize = 20;

        private readonly Dictionary<string, SoundBank> map = new Dictionary<string, SoundBank>();
        private AudioSource[] pool;
        private int nextSource;
        private AudioSource musicSource;
        private float lastCreakTension;
        private float strideDistance;
        private Vector3 lastRootPosition;
        private bool hasLastRoot;
        private string currentMusic = "";

        public SoundBank[] Banks
        {
            get => banks;
            set { banks = value; Rebuild(); }
        }

        private void Awake()
        {
            Services.Register(this);
            Rebuild();
            pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                GameObject go = new GameObject("Sfx_" + i);
                go.transform.SetParent(transform, false);
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            GameObject music = new GameObject("Music");
            music.transform.SetParent(transform, false);
            musicSource = music.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
            musicSource.spatialBlend = 0f;
        }

        private void OnDestroy() => Services.Unregister(this);

        private void Rebuild()
        {
            map.Clear();
            foreach (SoundBank bank in banks)
                if (bank != null)
                    map[bank.name] = bank;
        }

        private void OnEnable()
        {
            GameEvents.BowDrawing += OnBowDrawing;
            GameEvents.BowReleased += OnBowReleased;
            GameEvents.ArrowShot += OnArrowShot;
            GameEvents.ArrowImpacted += OnArrowImpacted;
            GameEvents.ArrowPicked += OnArrowPicked;
            GameEvents.ArrowNocked += OnArrowNocked;
            GameEvents.MonsterEvent += OnMonsterEvent;
            GameEvents.PlayerHurt += OnPlayerHurt;
            GameEvents.LevelFinished += OnLevelFinished;
            GameEvents.CountdownTick += OnCountdown;
            GameEvents.EquipmentUnlocked += OnEquipmentUnlocked;
            GameEvents.TrophyUnlocked += OnTrophyUnlocked;
            GameEvents.BarrierOpened += OnBarrierOpened;
        }

        private void OnDisable()
        {
            GameEvents.BowDrawing -= OnBowDrawing;
            GameEvents.BowReleased -= OnBowReleased;
            GameEvents.ArrowShot -= OnArrowShot;
            GameEvents.ArrowImpacted -= OnArrowImpacted;
            GameEvents.ArrowPicked -= OnArrowPicked;
            GameEvents.ArrowNocked -= OnArrowNocked;
            GameEvents.MonsterEvent -= OnMonsterEvent;
            GameEvents.PlayerHurt -= OnPlayerHurt;
            GameEvents.LevelFinished -= OnLevelFinished;
            GameEvents.CountdownTick -= OnCountdown;
            GameEvents.EquipmentUnlocked -= OnEquipmentUnlocked;
            GameEvents.TrophyUnlocked -= OnTrophyUnlocked;
            GameEvents.BarrierOpened -= OnBarrierOpened;
        }

        // ---------------------------------------------------------------- volumes

        /// <summary>Volume (0..1) d'une famille de sons, Général compris.</summary>
        public float GetVolume(SoundCategory category)
        {
            SettingsService settings = Services.Get<SettingsService>();
            if (settings == null)
                return 1f;
            SettingsData d = settings.Data;
            float family;
            switch (category)
            {
                case SoundCategory.Music: family = d.musicVolume; break;
                case SoundCategory.Ambience: family = d.ambienceVolume; break;
                case SoundCategory.Ui: family = d.uiVolume; break;
                default: family = d.sfxVolume; break;
            }
            return family * d.masterVolume;
        }

        /// <summary>Retrouve une banque par son nom (null si absente).</summary>
        public SoundBank GetBank(string key)
        {
            map.TryGetValue(key, out SoundBank bank);
            return bank;
        }

        // ---------------------------------------------------------------- lecture

        /// <summary>Joue un son 3D à une position du monde.</summary>
        public AudioSource Play(string key, Vector3 position, float volumeScale = 1f)
        {
            SoundBank bank = GetBank(key);
            if (bank == null)
                return null;
            AudioClip clip = bank.PickClip();
            if (clip == null)
                return null;

            AudioSource source = NextSource();
            Configure(source, bank);
            source.transform.position = position;
            source.pitch = bank.PickPitch();
            source.volume = bank.volume * volumeScale * GetVolume(bank.category);
            source.clip = clip;
            source.loop = false;
            source.Play();
            return source;
        }

        /// <summary>Joue un son 2D (interface, jingles).</summary>
        public AudioSource Play2D(string key, float volumeScale = 1f)
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform head = mode != null ? mode.ActiveHead : null;
            Vector3 position = head != null ? head.position : Vector3.zero;
            AudioSource source = Play(key, position, volumeScale);
            if (source != null)
                source.spatialBlend = 0f;
            return source;
        }

        /// <summary>Crée une source en boucle pour une ambiance (AmbienceZone).</summary>
        public AudioSource CreateLoop(string key, Transform parent)
        {
            SoundBank bank = GetBank(key);
            if (bank == null || bank.clips == null || bank.clips.Length == 0)
                return null;
            GameObject go = new GameObject("Loop_" + key);
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            Configure(source, bank);
            source.clip = bank.clips[0];
            source.loop = true;
            source.volume = 0f;
            source.playOnAwake = false;
            source.Play();
            return source;
        }

        private AudioSource NextSource()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                int index = (nextSource + i) % pool.Length;
                if (!pool[index].isPlaying)
                {
                    nextSource = (index + 1) % pool.Length;
                    return pool[index];
                }
            }
            // Toutes occupées : on recycle la plus ancienne.
            AudioSource source = pool[nextSource];
            nextSource = (nextSource + 1) % pool.Length;
            return source;
        }

        private static void Configure(AudioSource source, SoundBank bank)
        {
            source.spatialBlend = bank.spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(0.05f, bank.minDistance);
            source.maxDistance = Mathf.Max(bank.minDistance + 0.5f, bank.maxDistance);
            source.dopplerLevel = 0f;
            source.outputAudioMixerGroup = bank.mixerGroup;
        }

        // ---------------------------------------------------------------- événements

        private void OnBowDrawing(Vector3 position, float tension)
        {
            // Un grincement à chaque tranche de 18 % de tension gagnée.
            if (tension < 0.05f)
            {
                lastCreakTension = 0f;
                return;
            }
            if (tension - lastCreakTension >= 0.18f)
            {
                lastCreakTension = tension;
                Play("BowDraw", position, Mathf.Lerp(0.4f, 1f, tension));
            }
        }

        private void OnBowReleased(Vector3 position, float tension, bool fired)
        {
            lastCreakTension = 0f;
            Play("BowRelease", position, Mathf.Lerp(0.35f, 1f, tension) * (fired ? 1f : 0.7f));
        }

        private void OnArrowShot(Component arrow, float speed, float tension)
        {
            if (arrow != null)
                Play("ArrowWhoosh", arrow.transform.position, Mathf.Clamp01(speed / 50f));
        }

        private void OnArrowImpacted(ArrowImpactInfo info)
        {
            string key;
            switch (info.Surface)
            {
                case SurfaceType.Flesh: key = "ImpactFlesh"; break;
                case SurfaceType.Wood:
                case SurfaceType.Metal:
                case SurfaceType.Stone: key = info.HitTarget ? "ImpactTarget" : "ImpactWood"; break;
                case SurfaceType.Straw: key = "ImpactTarget"; break;
                default: key = "ImpactGround"; break;
            }
            Play(key, info.Point, Mathf.Clamp(info.Speed / 35f, 0.5f, 1f));
        }

        private void OnArrowPicked(Vector3 position) => Play("ArrowPick", position);
        private void OnArrowNocked(Vector3 position) => Play("ArrowNock", position);

        private void OnMonsterEvent(Vector3 position, string kind)
        {
            switch (kind)
            {
                case "sense": Play("MonsterSense", position); break;
                case "attack": Play("MonsterAttack", position); break;
                case "hit": Play("MonsterHit", position); break;
                case "die": Play("MonsterDie", position); break;
                case "block": Play("ImpactWood", position); break;
            }
        }

        private void OnPlayerHurt(Vector3 from) => Play2D("PlayerHurt");

        private void OnLevelFinished(LevelResult result)
        {
            if (result.IsTraining)
                return;
            Play2D(result.Passed ? "LevelSuccess" : "LevelFail");
            if (result.Passed)
                Play2D("MusicVictory", 0.8f);
        }

        private void OnCountdown(int value) => Play2D(value == 0 ? "CountdownGo" : "Countdown");
        private void OnEquipmentUnlocked(string id, EquipmentKind kind) => Play2D("Unlock");
        private void OnTrophyUnlocked(string id) => Play2D("Unlock");
        private void OnBarrierOpened(string id) => Play2D("GateOpen", 0.8f);

        // ---------------------------------------------------------------- musique et pas

        private void Update()
        {
            UpdateMusic();
            UpdateFootsteps();
        }

        private void UpdateMusic()
        {
            // Musique calme au camp et au hub ; coupée en manche pour laisser entendre les sons.
            var level = Levels.LevelController.Active;
            bool quiet = level != null && (level.State == LevelState.Running || level.State == LevelState.Countdown);
            SoundBank bank = GetBank("MusicCamp");
            if (bank != null && bank.clips.Length > 0 && currentMusic != "MusicCamp")
            {
                currentMusic = "MusicCamp";
                musicSource.clip = bank.clips[0];
                musicSource.Play();
            }
            float target = quiet ? 0f : (bank != null ? bank.volume : 0.5f) * GetVolume(SoundCategory.Music);
            musicSource.volume = Mathf.MoveTowards(musicSource.volume, target, Time.unscaledDeltaTime * 0.5f);
        }

        // Un bruit de pas tous les 1,6 m parcourus à pied.
        private void UpdateFootsteps()
        {
            PlayerModeService mode = Services.Get<PlayerModeService>();
            Transform root = mode != null ? mode.ActiveRoot : null;
            if (root == null)
                return;
            Vector3 position = root.position;
            if (!hasLastRoot)
            {
                lastRootPosition = position;
                hasLastRoot = true;
                return;
            }
            Vector3 delta = position - lastRootPosition;
            lastRootPosition = position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance > 2f)
                return; // téléportation : pas de pas
            strideDistance += distance;
            if (strideDistance >= 1.6f)
            {
                strideDistance = 0f;
                Play("Footstep", position, 0.7f);
            }
        }
    }
}
