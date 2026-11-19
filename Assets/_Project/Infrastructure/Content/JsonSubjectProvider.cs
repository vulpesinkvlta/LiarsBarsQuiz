using System;
using System.Collections.Generic;
using System.Linq;
using LiarsBar.Core.Content;
using LiarsBar.Core.Domain;
using UnityEngine;

namespace LiarsBar.Infrastructure.Content
{
    /// <summary>
    /// Предметы из JSON. Источник — пары (имя, текст), поэтому провайдер тестируется без Unity;
    /// <see cref="FromResources"/> читает <c>Resources/Content/*.json</c> (в WebGL не нужен UnityWebRequest).
    /// Разбор ленивый, один раз; предметы упорядочены по Id, чтобы seed давал одну и ту же партию.
    /// </summary>
    public sealed class JsonSubjectProvider : ISubjectProvider
    {
        public const string DefaultResourcesFolder = "Content";

        private readonly IReadOnlyList<KeyValuePair<string, string>> _sources;
        private IReadOnlyList<Subject> _subjects;

        public JsonSubjectProvider(IEnumerable<KeyValuePair<string, string>> sources)
        {
            _sources = (sources ?? throw new ArgumentNullException(nameof(sources))).ToList();
        }

        public static JsonSubjectProvider FromResources(string folder = DefaultResourcesFolder)
        {
            var assets = Resources.LoadAll<TextAsset>(folder);
            var sources = assets.Select(a => new KeyValuePair<string, string>($"{folder}/{a.name}.json", a.text)).ToList();
            foreach (var a in assets) Resources.UnloadAsset(a);
            return new JsonSubjectProvider(sources);
        }

        public IReadOnlyList<Subject> GetSubjects()
        {
            if (_subjects != null) return _subjects;

            var parsed = _sources.Select(s => SubjectJsonParser.Parse(s.Value, s.Key)).ToList();
            var dup = parsed.GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ContentFormatException($"Subject id '{dup.Key}' is defined in more than one file.");

            _subjects = parsed.OrderBy(s => s.Id, StringComparer.Ordinal).ToList().AsReadOnly();
            return _subjects;
        }
    }
}
