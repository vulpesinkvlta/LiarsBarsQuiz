using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Ряд карт. Переиспользует CardView, лишние прячет.</summary>
    public sealed class HandView : MonoBehaviour
    {
        private readonly List<CardView> _cards = new List<CardView>();

        public event Action<string> CardClicked;

        public void Show(IReadOnlyList<CardModel> models)
        {
            while (_cards.Count < models.Count)
            {
                var card = CardView.Create(transform);
                card.Clicked += id => CardClicked?.Invoke(id);
                _cards.Add(card);
            }

            for (var i = 0; i < _cards.Count; i++)
            {
                var visible = i < models.Count;
                _cards[i].gameObject.SetActive(visible);
                if (visible) _cards[i].Bind(models[i]);
            }
        }
    }
}
