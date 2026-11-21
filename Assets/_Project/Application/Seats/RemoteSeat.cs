using System;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Views;

namespace LiarsBar.Application.Seats
{
    public sealed class RemoteSeat : ISeatController
    {
        private readonly LocalHumanSeat _input;
        public int ActorNumber { get; }

        public RemoteSeat(int actorNumber, int seatId, string name)
        {
            ActorNumber = actorNumber;
            _input = new LocalHumanSeat(seatId, name);
        }

        public void RequestAction(PlayerView view, Action<IGameAction> submit) =>
            _input.RequestAction(view, submit);

        public bool TrySubmit(int senderActorNumber, IGameAction action) =>
            senderActorNumber == ActorNumber && _input.TrySubmit(action);

        public void Cancel() => _input.Cancel();
    }
}
