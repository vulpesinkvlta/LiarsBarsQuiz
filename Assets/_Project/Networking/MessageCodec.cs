using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Actions;
using LiarsBar.Core.Config;
using LiarsBar.Core.Events;
using LiarsBar.Core.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LiarsBar.Networking
{
    public abstract class NetworkMessage { }
    public sealed class MatchStartMessage : NetworkMessage
    {
        public Dictionary<int, int> ActorSeats;
        public string[] SeatNames;
        public GameRulesConfig Settings;
    }
    public sealed class ActionMessage : NetworkMessage { public IGameAction Action; }
    public sealed class EventsMessage : NetworkMessage { public GameEvent[] Events; }
    public sealed class ViewMessage : NetworkMessage { public PlayerView View; }
    public sealed class MatchAbortedMessage : NetworkMessage { public string Reason; }

    public static class MessageCodec
    {
        public const byte ActionCode = 2;

        public static bool TryReadAction(string json, out IGameAction action)
        {
            action = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                action = ((ActionMessage)Decode(ActionCode, json)).Action;
                return action != null;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException ||
                ex is OverflowException || ex is ArgumentException || ex is InvalidCastException)
            {
                return false;
            }
        }
        private static readonly Type[] Messages =
        {
            typeof(MatchStartMessage), typeof(ActionMessage), typeof(EventsMessage),
            typeof(ViewMessage), typeof(MatchAbortedMessage)
        };
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Converters = { new PayloadConverter() }, MetadataPropertyHandling = MetadataPropertyHandling.Ignore
        };

        public static string Encode(NetworkMessage message, out byte code)
        {
            int index = Array.IndexOf(Messages, message.GetType());
            if (index < 0) throw new ArgumentException("Unknown message", nameof(message));
            code = (byte)(index + 1);
            return JsonConvert.SerializeObject(message, Settings);
        }

        public static NetworkMessage Decode(byte code, string json)
        {
            if (code < 1 || code > Messages.Length) throw new ArgumentOutOfRangeException(nameof(code));
            return (NetworkMessage)JsonConvert.DeserializeObject(json, Messages[code - 1], Settings)
                ?? throw new JsonSerializationException("Missing message");
        }

        private sealed class PayloadConverter : JsonConverter
        {
            private static readonly Type[] Types =
            {
                typeof(RoundStarted), typeof(CardsPlayed), typeof(LiarCalled), typeof(FactsRevealed),
                typeof(RevolverShooted), typeof(PlayerEliminated), typeof(TurnChanged), typeof(MatchEnded),
                typeof(PlayCardsAction), typeof(CallLiarAction)
            };

            public override bool CanConvert(Type type) =>
                typeof(GameEvent).IsAssignableFrom(type) || typeof(IGameAction).IsAssignableFrom(type);

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                var data = JObject.FromObject(value);
                data["kind"] = value.GetType().Name;
                data.WriteTo(writer);
            }

            public override object ReadJson(JsonReader reader, Type expected, object existing, JsonSerializer serializer)
            {
                var data = JObject.Load(reader);
                string kind = (string)data["kind"];
                var type = Types.FirstOrDefault(t => t.Name == kind && expected.IsAssignableFrom(t));
                if (type == null) throw new JsonSerializationException("Unexpected payload: " + kind);
                if (typeof(IGameAction).IsAssignableFrom(type) && data["SeatId"]?.Type != JTokenType.Integer)
                    throw new JsonSerializationException("Missing or invalid seat.");
                if (type == typeof(PlayCardsAction))
                {
                    if (!(data["CardIds"] is JArray ids) || ids.Any(id => id.Type != JTokenType.String))
                        throw new JsonSerializationException("Invalid card list.");
                    return new PlayCardsAction((int)data["SeatId"], data["CardIds"].ToObject<string[]>());
                }
                return data.ToObject(type);
            }
        }
    }
}
