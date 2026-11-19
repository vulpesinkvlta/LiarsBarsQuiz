using System;
using System.Collections.Generic;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Bots
{
    /// <summary>
    /// «Эрудиция» бота: справочник фактов, как у человека в голове. Это не данные партии —
    /// бот обращается к нему, только если «знает» факт (см. BotRecall), а сложность использует как
    /// человек, оценивающий «звучит ли это знакомо».
    /// </summary>
    public interface IFactKnowledge
    {
        bool TryGetFact(string factId, out Fact fact);
    }

    /// <summary>Справочник из всех предметов контента. Строится лениво один раз.</summary>
    public sealed class FactCatalog : IFactKnowledge
    {
        private readonly Func<IEnumerable<Subject>> _source;
        private Dictionary<string, Fact> _facts;

        public FactCatalog(IEnumerable<Subject> subjects)
        {
            if (subjects == null) throw new ArgumentNullException(nameof(subjects));
            _source = () => subjects;
        }

        public FactCatalog(ISubjectProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _source = provider.GetSubjects;
        }

        public bool TryGetFact(string factId, out Fact fact)
        {
            if (_facts == null)
            {
                var facts = new Dictionary<string, Fact>();
                foreach (var subject in _source())
                foreach (var f in subject.Facts)
                    facts[f.Id] = f;
                _facts = facts;
            }

            if (factId != null) return _facts.TryGetValue(factId, out fact);
            fact = null;
            return false;
        }
    }

    /// <summary>
    /// Детерминированное «знает ли бот факт»: число в [0, 1) из хэша (бот, факт).
    /// Один и тот же бот не «забывает» факт посреди партии, а результат одинаков в Mono, IL2CPP и .NET
    /// (string.GetHashCode для этого не годится).
    /// </summary>
    public static class BotRecall
    {
        public static double Roll(int botKey, string factId)
        {
            unchecked
            {
                var h = 2166136261u; // FNV-1a
                foreach (var c in factId ?? string.Empty)
                {
                    h = (h ^ (byte)c) * 16777619u;
                    h = (h ^ (byte)(c >> 8)) * 16777619u;
                }

                h ^= Mix((uint)botKey + 0x9E3779B9u);
                h = Mix(h);
                return (h >> 8) * (1.0 / (1u << 24));
            }
        }

        /// <summary>Ключ бота: соль партии + место.</summary>
        public static int BotKey(int salt, int seatId)
        {
            unchecked { return (int)Mix((uint)salt * 31u + (uint)seatId + 0x85EBCA6Bu); }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
