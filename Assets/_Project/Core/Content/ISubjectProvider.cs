using System.Collections.Generic;
using LiarsBar.Core.Domain;

namespace LiarsBar.Core.Content
{
    /// <summary>Откуда берутся предметы для раундов (JSON, тесты, сервер).</summary>
    public interface ISubjectProvider
    {
        IReadOnlyList<Subject> GetSubjects();
    }

    /// <summary>Готовый список предметов в памяти: для тестов и простых сценариев.</summary>
    public sealed class InMemorySubjectProvider : ISubjectProvider
    {
        private readonly IReadOnlyList<Subject> _subjects;

        public InMemorySubjectProvider(IEnumerable<Subject> subjects) => _subjects = new List<Subject>(subjects).AsReadOnly();

        public IReadOnlyList<Subject> GetSubjects() => _subjects;
    }
}
