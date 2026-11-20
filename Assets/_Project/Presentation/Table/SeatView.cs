using LiarsBar.Presentation.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LiarsBar.Presentation.Table
{
    /// <summary>Место за столом: имя, число карт, выстрелы x/6, жив/выбыл, подсветка хода.</summary>
    public sealed class SeatView : MonoBehaviour
    {
        private Image _background;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _info;
        private TextMeshProUGUI _status;

        public static SeatView Create(Transform parent)
        {
            var bg = Ui.Panel("Seat", parent, Ui.PanelColor);
            Ui.Size(bg, 270, 150);
            var view = bg.gameObject.AddComponent<SeatView>();
            view._background = bg;

            view._name = Ui.Label("Name", bg.transform, string.Empty, 28);
            view._name.fontStyle = FontStyles.Bold;
            view._name.textWrappingMode = TextWrappingModes.NoWrap;
            view._name.overflowMode = TextOverflowModes.Ellipsis;
            Ui.Anchor(view._name.rectTransform, 0.04f, 0.64f, 0.96f, 0.96f);

            view._info = Ui.Label("Info", bg.transform, string.Empty, 22);
            Ui.Anchor(view._info.rectTransform, 0.04f, 0.3f, 0.96f, 0.64f);

            view._status = Ui.Label("Status", bg.transform, string.Empty, 22, color: Ui.TextDim);
            Ui.Anchor(view._status.rectTransform, 0.04f, 0.04f, 0.96f, 0.3f);
            return view;
        }

        public void Bind(SeatModel model)
        {
            _name.text = model.IsViewer ? $"{model.Name} (вы)" : model.Name;
            _info.text = $"Карт: {model.CardCount}   Выстрелы: {model.ShotsTaken}/{model.Chambers}";
            _status.text = !model.IsAlive ? "выбыл" : model.IsCurrent ? "ходит" : string.Empty;
            _background.color = !model.IsAlive ? Ui.Dead : model.IsCurrent ? Ui.Highlight : Ui.PanelColor;
            _name.color = model.IsAlive ? Ui.Text : Ui.TextDim;
            _info.color = model.IsAlive ? Ui.Text : Ui.TextDim;
        }
    }
}
