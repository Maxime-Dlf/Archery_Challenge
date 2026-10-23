// NightLight : lumière (torche, feu de camp) qui s'allume la nuit.
//
// OÙ LE POSER : sur le prefab de torche allumée (Torch_Lit) ou sur n'importe quel objet qui porte une
// Point Light. RÉGLAGES (tutoriel §P9.5) : « Light » ← la Point Light ; « Flame » ← l'effet de flamme
// (facultatif) ; « Always On » coché pour le feu de camp.
//
// Le TimeOfDayService décide quelles lumières éclairent vraiment (4 au plus, les plus proches du
// joueur) : l'objet « flamme » reste visible dès que la torche est « souhaitée allumée », même si sa
// lumière est coupée par cette limite. C'est ce qui garde le Quest fluide.

using System.Collections.Generic;
using UnityEngine;

namespace ArcheryChallenge.World
{
    /// <summary>Source de lumière nocturne.</summary>
    [DisallowMultipleComponent]
    public class NightLight : MonoBehaviour
    {
        [SerializeField] private Light lightSource;
        [SerializeField] private GameObject flame;
        [SerializeField] private bool alwaysOn;

        private static readonly List<NightLight> Registry = new List<NightLight>();

        /// <summary>Toutes les lumières de nuit actives.</summary>
        public static IReadOnlyList<NightLight> All => Registry;

        /// <summary>Vrai pour un feu qui brûle jour et nuit.</summary>
        public bool AlwaysOn
        {
            get => alwaysOn;
            set => alwaysOn = value;
        }

        public Light LightSource => lightSource;

        private void OnEnable()
        {
            if (lightSource == null)
                lightSource = GetComponentInChildren<Light>(true);
            if (!Registry.Contains(this))
                Registry.Add(this);
            // En attendant le premier calcul du service : lumière éteinte (sauf feu permanent).
            SetDesiredOn(alwaysOn);
            SetLightEnabled(alwaysOn);
        }

        private void OnDisable() => Registry.Remove(this);

        /// <summary>La torche doit-elle brûler (flamme visible) ?</summary>
        public void SetDesiredOn(bool on)
        {
            if (flame != null && flame.activeSelf != on)
                flame.SetActive(on);
        }

        /// <summary>Allume ou éteint la lumière temps réel (limitée à 4 par le service).</summary>
        public void SetLightEnabled(bool on)
        {
            if (lightSource != null && lightSource.enabled != on)
                lightSource.enabled = on;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => Registry.Clear();
    }
}
