// ToastService : file d'attente des notifications temporaires (bandeau).
//
// OÙ LE POSER : objet « Systems » de la scène Boot.
//
// N'importe quel script peut annoncer un message avec GameEvents.RaiseToast("texte") ; ce service
// les met en file et les expose un par un (3 secondes chacun) à l'interface (ToastBannerView).
// Messages typiques : déblocage d'un arc, barrière ouverte, monstre éliminé, astuces au premier tir.

using System;
using System.Collections.Generic;
using ArcheryChallenge.Core;
using UnityEngine;

namespace ArcheryChallenge.UI
{
    /// <summary>File de notifications.</summary>
    [DisallowMultipleComponent]
    public class ToastService : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float displaySeconds = 3f;
        [SerializeField, Min(1)] private int maxQueue = 6;

        private readonly Queue<string> queue = new Queue<string>();
        private float hideAt;
        private string current = "";

        /// <summary>Message actuellement affiché (vide si aucun).</summary>
        public string Current => current;

        /// <summary>Annoncé à chaque changement du message affiché.</summary>
        public event Action<string> Changed;

        private void Awake() => Services.Register(this);
        private void OnDestroy() => Services.Unregister(this);

        private void OnEnable()
        {
            GameEvents.ToastRequested += Show;
            GameEvents.EquipmentUnlocked += OnEquipmentUnlocked;
            GameEvents.LevelUnlocked += OnLevelUnlocked;
        }

        private void OnDisable()
        {
            GameEvents.ToastRequested -= Show;
            GameEvents.EquipmentUnlocked -= OnEquipmentUnlocked;
            GameEvents.LevelUnlocked -= OnLevelUnlocked;
        }

        /// <summary>Ajoute un message à la file.</summary>
        public void Show(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (text == current || queue.Contains(text))
                return;
            while (queue.Count >= maxQueue)
                queue.Dequeue();
            queue.Enqueue(text);
        }

        private void Update()
        {
            if (current.Length > 0 && Time.unscaledTime >= hideAt)
            {
                current = "";
                Changed?.Invoke(current);
            }
            if (current.Length == 0 && queue.Count > 0)
            {
                current = queue.Dequeue();
                hideAt = Time.unscaledTime + displaySeconds;
                Changed?.Invoke(current);
            }
        }

        private void OnEquipmentUnlocked(string id, EquipmentKind kind)
        {
            GameCatalog catalog = Services.Get<GameCatalog>();
            string name = id;
            if (catalog != null)
            {
                var bow = kind == EquipmentKind.Bow ? catalog.GetBow(id) : null;
                var arrow = kind == EquipmentKind.Arrow ? catalog.GetArrow(id) : null;
                name = bow != null ? bow.displayName : arrow != null ? arrow.displayName : id;
            }
            Show($"Débloqué : {name} !");
        }

        private void OnLevelUnlocked(string levelId)
        {
            GameCatalog catalog = Services.Get<GameCatalog>();
            var level = catalog != null ? catalog.GetLevel(levelId) : null;
            Show($"Nouvelle arène : {(level != null ? level.displayName : levelId)}");
        }
    }
}
