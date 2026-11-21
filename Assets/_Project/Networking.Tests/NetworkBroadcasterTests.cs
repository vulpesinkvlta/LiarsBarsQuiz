using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Application;
using LiarsBar.Application.Seats;
using LiarsBar.Application.Sessions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Engine;
using LiarsBar.Core.Events;
using LiarsBar.Core.Randomness;
using NUnit.Framework;

namespace LiarsBar.Networking.Tests
{
    public sealed class NetworkBroadcasterTests
    {
        private sealed class RecordingTransport : INetworkTransport
        {
            public readonly List<(int? Actor, NetworkMessage Message, string Json)> Sent =
                new List<(int?, NetworkMessage, string)>();
            public event Action<int, byte, string> MessageReceived { add { } remove { } }
            public void SendToAll(byte code, string json) => Sent.Add((null, MessageCodec.Decode(code, json), json));
            public void SendToPlayer(int actor, byte code, string json) => Sent.Add((actor, MessageCodec.Decode(code, json), json));
            public void SendToHost(byte code, string json) => Assert.Fail("Unexpected message to host");
        }

        [Test]
        public void StartAndPlay_SendPublicEventsBeforeEachGuestsOwnHand()
        {
            var facts = Enumerable.Range(0, 24).Select(i => new Fact($"card-{i}", $"Fact {i}", i % 2 == 0, "Explanation"));
            var subjects = new InMemorySubjectProvider(new[] { new Subject("subject", "Subject", facts) });
            var engine = new MatchEngine(GameRulesConfig.Default, subjects, new SeededRandom(12));
            var settings = MatchSettings.ForPlayers(3);
            settings.FirstSeat = 0;
            var controllers = new Dictionary<int, ISeatController>
            {
                [0] = new LocalHumanSeat(0, "Host"),
                [1] = new RemoteSeat(17, 1, "Guest A"),
                [2] = new RemoteSeat(8, 2, "Guest B")
            };
            var transport = new RecordingTransport();
            using (var session = new LocalMatchSession(engine, settings, controllers, 0))
            using (var broadcaster = new NetworkBroadcaster(session, transport))
            {
                session.Start();
                CheckBatch(session, transport, typeof(RoundStarted));
                transport.Sent.Clear();
                session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));
                CheckBatch(session, transport, typeof(CardsPlayed));
            }
        }

        private static void CheckBatch(LocalMatchSession session, RecordingTransport transport, Type firstEvent)
        {
            Assert.AreEqual(3, transport.Sent.Count);
            var broadcast = transport.Sent[0];
            Assert.IsNull(broadcast.Actor);
            Assert.AreEqual(firstEvent, ((EventsMessage)broadcast.Message).Events[0].GetType());
            foreach (var seat in session.Seats)
                foreach (var card in session.State.GetPlayer(seat.SeatId).Hand.Cards)
                    StringAssert.DoesNotContain("\"" + card.Id + "\"", broadcast.Json);

            foreach (var target in new[] { (actor: 17, seat: 1), (actor: 8, seat: 2) })
            {
                var packet = transport.Sent.Single(p => p.Actor == target.actor);
                var view = ((ViewMessage)packet.Message).View;
                Assert.AreEqual(target.seat, view.ViewerSeat);
                CollectionAssert.AreEqual(session.State.GetPlayer(target.seat).Hand.Cards.Select(card => card.Id),
                    view.MyHand.Select(card => card.Id));
            }
        }
    }
}
