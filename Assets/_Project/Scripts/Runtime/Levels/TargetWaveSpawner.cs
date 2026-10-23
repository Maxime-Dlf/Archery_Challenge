// TargetWaveSpawner : fait apparaître les cibles par vagues pendant une manche (arène 5, Le Tournoi).
//
// OÙ LE POSER : sur la racine de l'arène (à côté du LevelController).
// RÉGLAGES (tutoriel §P8-C.7) : « Waves » → +, pour chaque vague : « Root » ← l'objet qui contient
// les cibles de la vague (Wave_1, Wave_2, Wave_3) et « Start Time » = 0 / 20 / 40 (secondes après
// le début de la manche).
//
// Tant que la vague n'est pas arrivée, son objet racine est désactivé : les cibles n'existent pas
// (ni dessin ni collision). La vague précédente reste en place : le joueur doit parfois choisir.

using System;
using ArcheryChallenge.Targets;
using UnityEngine;

namespace ArcheryChallenge.Levels
{
    /// <summary>Une vague de cibles.</summary>
    [Serializable]
    public class TargetWave
    {
        [Tooltip("Objet parent des cibles de la vague.")]
        public Transform root;

        [Tooltip("Délai (s) après le début de la manche.")]
        public float startTime;
    }

    /// <summary>Enchaîne des vagues de cibles dans le temps.</summary>
    [DisallowMultipleComponent]
    public class TargetWaveSpawner : MonoBehaviour
    {
        [SerializeField] private TargetWave[] waves = new TargetWave[0];

        private float elapsed;
        private bool running;
        private bool[] spawned;

        public TargetWave[] Waves
        {
            get => waves;
            set => waves = value;
        }

        public bool IsRunning => running;

        /// <summary>Démarre la minuterie des vagues (début de manche).</summary>
        public void Begin()
        {
            elapsed = 0f;
            running = true;
            spawned = new bool[waves.Length];
            for (int i = 0; i < waves.Length; i++)
                if (waves[i].root != null)
                    waves[i].root.gameObject.SetActive(false);
            ActivateDue();
        }

        /// <summary>Arrête les vagues et affiche tout (les cibles restent visibles hors manche).</summary>
        public void End()
        {
            running = false;
            foreach (TargetWave wave in waves)
                if (wave.root != null)
                    wave.root.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!running)
                return;
            elapsed += Time.deltaTime;
            ActivateDue();
        }

        private void ActivateDue()
        {
            for (int i = 0; i < waves.Length; i++)
            {
                if (spawned[i] || waves[i].root == null || elapsed < waves[i].startTime)
                    continue;
                spawned[i] = true;
                waves[i].root.gameObject.SetActive(true);
                foreach (Target t in waves[i].root.GetComponentsInChildren<Target>(true))
                    t.ResetTarget();
            }
        }
    }
}
