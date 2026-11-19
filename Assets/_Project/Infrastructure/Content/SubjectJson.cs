using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Domain;
using Newtonsoft.Json;

namespace LiarsBar.Infrastructure.Content
{
    /// <summary>JSON-схема предмета: <c>{ id, title, facts: [ { id, text, isTrue, difficulty, explanation } ] }</c>.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class SubjectDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("title", Required = Required.Always)] public string Title { get; set; }
        [JsonProperty("facts", Required = Required.Always)] public List<FactDto> Facts { get; set; }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public sealed class FactDto
    {
        [JsonProperty("id", Required = Required.Always)] public string Id { get; set; }
        [JsonProperty("text", Required = Required.Always)] public string Text { get; set; }
        [JsonProperty("isTrue", Required = Required.Always)] public bool IsTrue { get; set; }
        [JsonProperty("difficulty", Required = Required.Always)] public int Difficulty { get; set; }
        [JsonProperty("explanation", Required = Required.Always)] public string Explanation { get; set; }
    }

    /// <summary>Ошибка формата файла контента: в сообщении всегда есть имя файла.</summary>
    public sealed class ContentFormatException : Exception
    {
        public ContentFormatException(string message, Exception inner = null) : base(message, inner) { }
    }

    /// <summary>Разбор JSON предмета в доменный <see cref="Subject"/>. Лишние и пропущенные поля — ошибка.</summary>
    public static class SubjectJsonParser
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            NullValueHandling = NullValueHandling.Include
        };

        public static Subject Parse(string json, string source = "<json>")
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ContentFormatException($"{source}: file is empty.");

            SubjectDto dto;
            try
            {
                dto = JsonConvert.DeserializeObject<SubjectDto>(json, Settings);
            }
            catch (JsonException e)
            {
                throw new ContentFormatException($"{source}: {e.Message}", e);
            }

            if (dto == null) throw new ContentFormatException($"{source}: no subject object.");
            if (dto.Facts == null) throw new ContentFormatException($"{source}: 'facts' is null.");

            try
            {
                var facts = dto.Facts.Select((f, i) => ToFact(f, i, source)).ToList();
                return new Subject(dto.Id, dto.Title, facts);
            }
            catch (ArgumentException e)
            {
                throw new ContentFormatException($"{source}: {e.Message}", e);
            }
        }

        public static string Serialize(SubjectDto dto) => JsonConvert.SerializeObject(dto, Formatting.Indented);

        private static Fact ToFact(FactDto f, int index, string source)
        {
            if (f == null) throw new ContentFormatException($"{source}: fact #{index} is null.");
            if (f.Difficulty < 1 || f.Difficulty > 3)
                throw new ContentFormatException($"{source}: fact '{f.Id}' difficulty {f.Difficulty} is out of range 1–3.");
            return new Fact(f.Id, f.Text, f.IsTrue, f.Explanation, (Difficulty)f.Difficulty);
        }
    }
}
