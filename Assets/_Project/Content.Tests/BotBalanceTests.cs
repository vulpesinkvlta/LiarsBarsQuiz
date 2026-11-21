using System.IO;
using System.Linq;
using LiarsBar.Core.Bots;
using LiarsBar.Infrastructure.Content;
using NUnit.Framework;
using UnityEngine;

namespace LiarsBar.Content.Tests
{
    /// <summary>
    /// «Готово, когда» этапа 5: 200 партий «боты против ботов» на настоящем контенте доигрываются.
    /// Отчёт (среднее число раундов, доля верных обвинений, победы по профилям) — в консоль и Temp/liarsbar_bots.txt.
    /// Цели баланса (8–14 раундов на 4 игроков, ни один профиль не выигрывает больше 50 %) проверяются на этапе 12.
    /// </summary>
    public class BotBalanceTests
    {
        private const int Matches = 200;
        private const string ReportPath = "Temp/liarsbar_bots.txt";

        [Test]
        public void TwoHundredBotMatches_FinishAndReport()
        {
            var report = "";
            foreach (var players in new[] { 4, 2, 6 })
            {
                var result = BotSimulation.Run(JsonSubjectProvider.FromResources(), BotProfile.Defaults, Matches, players, seed: 2026);

                Assert.AreEqual(Matches, result.Wins.Values.Sum(), $"{players} players: not every match has a winner");
                Assert.Greater(result.Accusations, 0);
                if (players == 4)
                {
                    Assert.That(result.AverageRounds, Is.InRange(8.0, 14.0));
                    foreach (var id in result.SeatsPlayed.Keys)
                        Assert.LessOrEqual(result.WinRate(id), 0.5, id);
                }
                report += result.ToReport() + "\n";
            }

            File.WriteAllText(ReportPath, report);
            Debug.Log("[Bots]\n" + report);
            TestContext.WriteLine(report);
        }
    }
}
