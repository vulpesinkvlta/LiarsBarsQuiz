using System;
using System.Collections.Generic;
using UnityEngine;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Ряд карт. Переиспользует CardView, лишние прячет.</summary>
    public sealed class HandView : MonoBehaviour
    {
        private readonly List<CardView> _cards = new List<CardView>();

        public event Action Dropped;
        public event Action<string> CardClicked;

        internal System.Collections.IEnumerator FlipRevealed(CardModel model)
        {
            var card = _cards.Find(c => c.gameObject.activeSelf && c.Id == model.Id);
            if (card == null) yield break;
            const float halfDuration = 0.16f;
            for (int side = 0; side < 2; side++)
            {
                if (side == 1) card.Bind(model);
                for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
                {
                    float fraction = Mathf.SmoothStep(0, 1, t / halfDuration);
                    card.transform.localScale = new Vector3(side == 0 ? 1 - fraction : fraction, 1, 1);
                    yield return null;
                }
            }
            card.transform.localScale = Vector3.one;
        }

        public void Show(IReadOnlyList<CardModel> models)
        {
            while (_cards.Count < models.Count)
            {
                var card = CardView.Create(transform);
                card.Dropped += () => Dropped?.Invoke();
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
