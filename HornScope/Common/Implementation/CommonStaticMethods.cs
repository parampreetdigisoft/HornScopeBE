using System.Net;
using System.Text.RegularExpressions;

namespace HornScope.Common.Implementation
{
    public class CommonStaticMethods
    {
        public static string GetConditionByScore(decimal score)
        {
            if (score <= 20)
                return "Critical";

            if (score <= 40)
                return "Fragile";

            if (score <= 60)
                return "Developing";

            if (score <= 80)
                return "Stable";

            return "Strong";
        }

        public static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Remove HTML tags
            var noTags = Regex.Replace(input, "<.*?>", string.Empty);

            // Decode HTML entities (e.g., &mdash;)
            return WebUtility.HtmlDecode(noTags);
        }

        public static class PillarScoreCalculator
        {
            private const double Tier1Weight = 3.0, Tier2Weight = 1.5;

            public class ScoredResponse
            {
                public int Score { get; set; }
                public double Weight { get; set; }
            }

            /// <summary>
            /// Calculates a single pillar's score: weighted average of responses,
            /// minus Critical (Tier 1) and High-Importance (Tier 2) failure adjustments.
            /// </summary>
            public static decimal CalculatePillarScore(IEnumerable<ScoredResponse> responses)
            {
                var responseList = responses?.ToList() ?? new List<ScoredResponse>();
                if (responseList.Count == 0) return 0m;

                var totalWeight = responseList.Sum(r => r.Weight);
                if (totalWeight <= 0) return 0m;

                var weightedScoreSum = responseList.Sum(r => (decimal)(r.Score * r.Weight));
                var pillarScore = weightedScoreSum / (decimal)totalWeight;

                pillarScore -= GetCriticalFailureDeduction(responseList);

                return Math.Max(Math.Round((decimal)pillarScore, 1), 0m);
            }

            /// <summary>
            /// Returns just the total deduction (Tier 1 + Tier 2 failures), in case
            /// you need to display/log it separately from the raw pillar score.
            /// </summary>
            public static decimal GetCriticalFailureDeduction(IEnumerable<ScoredResponse> responses)
            {
                var responseList = responses?.ToList() ?? new List<ScoredResponse>();

                var tier1ZeroCount = responseList.Count(r => r.Weight == Tier1Weight && r.Score == 0);
                var tier2ZeroCount = responseList.Count(r => r.Weight == Tier2Weight && r.Score == 0);

                return GetTier1Deduction(tier1ZeroCount) + GetTier2Deduction(tier2ZeroCount);
            }

            private static decimal GetTier1Deduction(int tier1ZeroCount) => tier1ZeroCount switch
            {
                <= 0 => 0m,
                1 => 10m,
                2 => 20m,
                _ => 25m 
            };

            private static decimal GetTier2Deduction(int tier2ZeroCount) => tier2ZeroCount switch
            {
                <= 0 => 0m,
                1 => 2m,
                2 => 4m,
                _ => 6m 
            };

            /// <summary>
            /// Total score across all pillars, rounded to 1 decimal place.
            /// </summary>
            public static decimal CalculateTotalScore(IEnumerable<decimal> pillarScores, int totalPillarCount)
            {
                if (totalPillarCount <= 0) return 0m;
                var scores = pillarScores?.ToList() ?? new List<decimal>();
                return Math.Round(scores.Sum() / totalPillarCount, 1);
            }
        }
    }
}
