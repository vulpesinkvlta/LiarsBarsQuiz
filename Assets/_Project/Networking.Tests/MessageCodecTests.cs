using System.Collections.Generic;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Domain;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;
using NUnit.Framework;

namespace LiarsBar.Networking.Tests
{
    public sealed class MessageCodecTests
    {
        private static T RoundTrip<T>(T source, byte expected) where T : NetworkMessage
        {
            string json = MessageCodec.Encode(source, out var code);
            Assert.AreEqual(expected, code);
            var copy = (T)MessageCodec.Decode(code, json);
            Assert.AreEqual(json, MessageCodec.Encode(copy, out _));
            return copy;
        }

        [Test]
        public void PreservesEveryPublicEventInOrder()
        {
            var events = new GameEvent[]
            {
                new RoundStarted(3, "cats", "Кошки", 1, new Dictionary<int, int> { { 1, 4 } }),
                new CardsPlayed(1, new[] { new CardFace("a", "Текст") }, 3),
                new LiarCalled(2, 1, true),
                new FactsRevealed(1, new[] { new Fact("a", "Текст", false, "Пояснение") }, true, 1),
                new RevolverShooted(1, 2, 3, 6, true), new PlayerEliminated(1),
                new TurnChanged(2, true), new MatchEnded(2, 3)
            };
            var copy = RoundTrip(new EventsMessage { Events = events }, 3);
            for (int i = 0; i < events.Length; i++)
                Assert.AreEqual(events[i].GetType(), copy.Events[i].GetType());
        }

        [Test]
        public void PreservesMatchActionsPrivateViewAndAbort()
        {
            RoundTrip(new MatchStartMessage { SeatNames = new[] { "А", "Б" },
                ActorSeats = new Dictionary<int, int> { { 7, 0 } }, Settings = GameRulesConfig.Default }, 1);
            RoundTrip(new ActionMessage { Action = new PlayCardsAction(2, "a", "b") }, 2);
            RoundTrip(new ActionMessage { Action = new CallLiarAction(1) }, 2);
            RoundTrip(new ViewMessage { View = new PlayerView(0, MatchPhase.AwaitingAction, 1, "a", "А",
                0, false, -1, new[] { new SeatInfo(0, "А", true, 1, 0, 6, new PlayerStats()) },
                null, new[] { new HandCard("x", "Текст", true, Difficulty.Easy) }, 17) }, 4);
            RoundTrip(new MatchAbortedMessage { Reason = "Хост вышел" }, 5);
        }
    }
}
