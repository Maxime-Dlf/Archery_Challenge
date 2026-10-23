// SettingsService : réglages du jeu (volumes, confort, difficulté, jour/nuit) enregistrés dans settings.json.
//
// OÙ LE POSER : objet « Systems » de la scène Boot (via le prefab Systems).
//
// FONCTIONNEMENT
// - Au démarrage : lit settings.json ; s'il n'existe pas, le crée avec les valeurs par défaut.
// - Chaque changement (Set…) prévient les autres scripts par GameEvents.SettingsChanged puis
//   programme une écriture différée (0,5 s) : on évite d'écrire à chaque cran d'un curseur de volume.
// - Le fichier est aussi écrit quand le jeu se met en pause ou se ferme (important sur Quest,
//   où retirer le casque suspend l'application).

using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.Save
{
    /// <summary>Service des réglages persistants.</summary>
    [DisallowMultipleComponent]
    public class SettingsService : MonoBehaviour
    {
        private const string FileName = "settings.json";
        private const float SaveDelay = 0.5f;

        private SettingsData data;
        private float saveAt = -1f;

        /// <summary>Réglages actuels (toujours non null après <see cref="EnsureLoaded"/>).</summary>
        public SettingsData Data
        {
            get { EnsureLoaded(); return data; }
        }

        /// <summary>Difficulté choisie.</summary>
        public GameDifficulty Difficulty => (GameDifficulty)Mathf.Clamp(Data.difficulty, 0, 2);

        /// <summary>Main qui tient l'arc : la droite par défaut, la gauche si l'option « Arc dans la main gauche » est cochée.</summary>
        public Handedness BowSide => Data.bowInLeftHand ? Handedness.Left : Handedness.Right;

        /// <summary>Main qui prend les flèches au carquois et tend la corde (l'autre main que celle de l'arc).</summary>
        public Handedness ArrowSide => Data.bowInLeftHand ? Handedness.Right : Handedness.Left;

        private void Awake()
        {
            Services.Register(this);
            EnsureLoaded();
        }

        private void OnDestroy() => Services.Unregister(this);

        private void Update()
        {
            if (saveAt >= 0f && Time.unscaledTime >= saveAt)
                SaveNow();
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        private void OnApplicationQuit() => SaveNow();

        /// <summary>Charge le fichier une seule fois (peut être appelé depuis les tests).</summary>
        public void EnsureLoaded()
        {
            if (data != null)
                return;
            data = JsonFileStore.Read<SettingsData>(FileName);
            if (data == null)
            {
                data = new SettingsData();
                SaveNow();
            }
            Sanitize();
        }

        /// <summary>Écrit le fichier immédiatement.</summary>
        public void SaveNow()
        {
            if (data == null)
                return;
            saveAt = -1f;
            JsonFileStore.Write(FileName, data);
        }

        /// <summary>
        /// À appeler après avoir modifié <see cref="Data"/> : prévient les abonnés et programme l'écriture.
        /// </summary>
        public void MarkChanged()
        {
            Sanitize();
            saveAt = Time.unscaledTime + SaveDelay;
            GameEvents.RaiseSettingsChanged();
        }

        /// <summary>Change la difficulté (modifiable à tout moment).</summary>
        public void SetDifficulty(GameDifficulty difficulty)
        {
            if (Data.difficulty == (int)difficulty)
                return;
            Data.difficulty = (int)difficulty;
            MarkChanged();
            GameEvents.RaiseDifficultyChanged(difficulty);
        }

        /// <summary>Remet tous les réglages à leurs valeurs par défaut (conserve le dernier profil).</summary>
        public void ResetToDefaults()
        {
            string lastProfile = Data.lastProfileId;
            data = new SettingsData { lastProfileId = lastProfile };
            MarkChanged();
            GameEvents.RaiseDifficultyChanged(Difficulty);
        }

        private void Sanitize()
        {
            data.masterVolume = Mathf.Clamp01(data.masterVolume);
            data.musicVolume = Mathf.Clamp01(data.musicVolume);
            data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
            data.ambienceVolume = Mathf.Clamp01(data.ambienceVolume);
            data.uiVolume = Mathf.Clamp01(data.uiVolume);
            data.snapTurnAngle = Mathf.Clamp(data.snapTurnAngle, 15f, 90f);
            // Migration : les anciens fichiers (sans « speedProfile ») gardaient la marche à 2 m/s ; le sprint devient l'allure normale.
            if (data.speedProfile < 1)
            {
                data.speedProfile = 1;
                data.moveSpeed = Mathf.Max(data.moveSpeed, 4f);
            }
            data.moveSpeed = Mathf.Clamp(data.moveSpeed, 0.5f, 6f);
            data.mouseSensitivity = Mathf.Clamp(data.mouseSensitivity, 0.02f, 0.6f);
            data.difficulty = Mathf.Clamp(data.difficulty, 0, 2);
            data.lastSlot = Mathf.Clamp(data.lastSlot, 1, 3);
        }
    }
}
