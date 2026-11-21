using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Events;

namespace LiarsBar.Networking
{
    public sealed class NetworkBroadcaster : IDisposable
    {
        private readonly LocalMatchSession _session;
        private readonly INetworkTransport _transport;

        public NetworkBroadcaster(LocalMatchSession session, INetworkTransport transport)
        {
            _session = session;
            _transport = transport;
            _session.EventsRaised += Publish;
        }

        private void Publish(IReadOnlyList<GameEvent> events)
        {
            var batch = MessageCodec.Encode(new EventsMessage { Events = events.ToArray() }, out var eventsCode);
            _transport.SendToAll(eventsCode, batch);
            foreach (var seat in _session.Controllers)
            {
                if (!(seat.Value is RemoteSeat guest)) continue;
                var view = new ViewMessage { View = _session.SnapshotFor(seat.Key) };
                var json = MessageCodec.Encode(view, out var viewCode);
                _transport.SendToPlayer(guest.ActorNumber, viewCode, json);
            }
        }

        public void Dispose() => _session.EventsRaised -= Publish;
    }
}
