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
using LiarsBar.Core.Views;
using NUnit.Framework;

namespace LiarsBar.Networking.Tests
{
    public sealed class LoopbackMatchTests
    {
        private sealed class LoopbackTransport : INetworkTransport
        {
            private readonly int _actor;
            private readonly int _host;
            private readonly Dictionary<int, LoopbackTransport> _peers;
            public event Action<int, byte, string> MessageReceived;

            public LoopbackTransport(int actor, int host, Dictionary<int, LoopbackTransport> peers)
            {
                _actor = actor;
                _host = host;
                _peers = peers;
                peers.Add(actor, this);
            }

            public void SendToHost(byte code, string json) => SendToPlayer(_host, code, json);
            public void SendToAll(byte code, string json)
            {
                foreach (int actor in _peers.Keys) SendToPlayer(actor, code, json);
            }
            public void SendToPlayer(int actor, byte code, string json) =>
                _peers[actor].MessageReceived?.Invoke(_actor, code, json);
        }

        [Test]
        public void HostAndTwoGuestsFinishTheSameMatch()
        {
            const int host = 21;
            int[] actors = { host, 7, 32 };
            var roster = actors.Select(a => new KeyValuePair<int, string>(a, $"Player {a}")).ToArray();
            var peers = new Dictionary<int, LoopbackTransport>();
            foreach (int actor in actors) new LoopbackTransport(actor, host, peers);
            var rules = GameRulesConfig.Default;
            var facts = Enumerable.Range(0, 24).Select(i => new Fact($"f{i}", $"Fact {i}", i % 2 == 0, "Explanation"));
            var subjects = new InMemorySubjectProvider(new[] { new Subject("s", "Subject", facts) });
            var engine = new MatchEngine(rules, subjects, new SeededRandom(47));
            var settings = new MatchSettings(roster.Select((p, i) => new SeatSetup(i, p.Value)).ToList()) { FirstSeat = 0 };
            var controllers = new Dictionary<int, ISeatController> { [0] = new LocalHumanSeat(0, roster[0].Value) };
            var guests = new Dictionary<int, RemoteMatchSession>();
            var views = new Dictionary<int, PlayerView>();
            var publicEvents = new Dictionary<int, List<string>>();
            var seatIds = new Dictionary<int, int>();
            var winners = new Dictionary<int, int>();
            foreach (int actor in actors.Skip(1))
            {
                int seat = Array.IndexOf(actors, actor);
                controllers.Add(seat, new RemoteSeat(actor, seat, roster[seat].Value));
                publicEvents[actor] = new List<string>();
                peers[actor].MessageReceived += (sender, code, json) =>
                {
                    Assert.AreEqual(host, sender);
                    switch (MessageCodec.Decode(code, json))
                    {
                        case MatchStartMessage start:
                            seatIds.Add(actor, start.ActorSeats[actor]);
                            Assert.AreEqual(seat, seatIds[actor]);
                            var guest = new RemoteMatchSession(peers[actor], host, seat);
                            guests.Add(actor, guest);
                            guest.ViewChanged += view => views[actor] = view;
                            guest.Start();
                            break;
                        case EventsMessage batch:
                            Assert.IsTrue(seatIds.ContainsKey(actor));
                            publicEvents[actor].Add(json);
                            foreach (var end in batch.Events.OfType<MatchEnded>()) winners.Add(actor, end.WinnerSeat);
                            break;
                        case ViewMessage snapshot:
                            Assert.AreEqual(seatIds[actor], snapshot.View.ViewerSeat);
                            CollectionAssert.AreEqual(engine.State.GetPlayer(seat).Hand.Cards.Select(c => c.Id),
                                snapshot.View.MyHand.Select(c => c.Id));

                            break;
                    }
                };
            }

            using (var session = new LocalMatchSession(engine, settings, controllers, 0))
            using (new HostMatchConnection(session, peers[host], roster, rules))
            {
                session.Start();
                var turns = new HashSet<int>();
                for (int turn = 0; !session.IsEnded && turn < 2000; turn++)
                {
                    int before = publicEvents[actors[1]].Count;
                    if (session.CurrentView.IsMyTurn)
                    {
                        turns.Add(host);
                        session.Submit(LocalMatchSession.FallbackAction(session.CurrentView));
                    }
                    else
                    {
                        var player = views.Single(p => p.Value.IsMyTurn);
                        turns.Add(player.Key);
                        guests[player.Key].Submit(LocalMatchSession.FallbackAction(player.Value));
                    }
                    Assert.AreEqual(before + 1, publicEvents[actors[1]].Count);
                }
                Assert.IsTrue(session.IsEnded);
                CollectionAssert.AreEquivalent(actors, turns);
                CollectionAssert.AreEqual(publicEvents[actors[1]], publicEvents[actors[2]]);
                foreach (int actor in actors.Skip(1))
                {
                    Assert.AreEqual(session.State.WinnerSeat, winners[actor]);
                    Assert.AreEqual(MatchPhase.Ended, views[actor].Phase);
                    Assert.AreEqual(winners[actor], views[actor].WinnerSeat);
                    guests[actor].Dispose();
                }
            }
        }
    }
}
