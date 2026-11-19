using LiarsBar.Core.Actions;
using LiarsBar.Core.Randomness;
using LiarsBar.Core.Views;

namespace LiarsBar.Core.Bots
{
    /// <summary>
    /// Как бот выбирает действие. Видит только свой PlayerView, как человек:
    /// чужие руки и позиция пули ему недоступны.
    /// </summary>
    public interface IBotStrategy
    {
        /// <summary>Допустимое действие для view.ViewerSeat; вызывать только когда view.IsMyTurn.</summary>
        IGameAction Decide(PlayerView view, BotProfile profile, IRandom random);
    }
}
