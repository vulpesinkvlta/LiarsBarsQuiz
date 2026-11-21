using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Config;

namespace LiarsBar.Networking
{
    public sealed class HostMatchConnection : IDisposable
    {
        private readonly INetworkTransport _transport;
        private readonly Dictionary<int, RemoteSeat> _guests;
        private readonly NetworkBroadcaster _broadcaster;

        public HostMatchConnection(LocalMatchSession session, INetworkTransport transport,
            IReadOnlyList<KeyValuePair<int, string>> roster, GameRulesConfig rules)
        {
            _transport = transport;
            _guests = session.Controllers.Values.OfType<RemoteSeat>().ToDictionary(s => s.ActorNumber);
            var start = new MatchStartMessage
            {
                ActorSeats = roster.Select((p, seat) => (actor: p.Key, seat)).ToDictionary(p => p.actor, p => p.seat),
                SeatNames = session.Seats.Select(s => s.Name).ToArray(),
                Settings = rules
            };
            var json = MessageCodec.Encode(start, out var code);
            transport.SendToAll(code, json);
            _broadcaster = new NetworkBroadcaster(session, transport);
            transport.MessageReceived += Receive;
        }

        private void Receive(int actor, byte code, string json)
        {
            if (code != MessageCodec.ActionCode || !_guests.TryGetValue(actor, out var seat)) return;
            if (MessageCodec.TryReadAction(json, out var action)) seat.TrySubmit(actor, action);
        }

        public void Dispose()
        {
            _transport.MessageReceived -= Receive;
            _broadcaster.Dispose();
        }
    }
}
