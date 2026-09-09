using HornScope.Enums;
using HornScope.Models;

namespace HornScope.Common.Implementation
{
    // Extension methods for the enum
    public static class ScoreValueExtensions
    {
        private static readonly Dictionary<string, int> ScoreToDisplayOrderMap = new()
        {
            { "100", (int)ScoreValueDisplayOrder.Hundred },
            { "75", (int)ScoreValueDisplayOrder.SeventyFive },
            { "50", (int)ScoreValueDisplayOrder.Fifty },
            { "25", (int)ScoreValueDisplayOrder.TwentyFive },
            { "0", (int)ScoreValueDisplayOrder.Zero},
            { "N/A", (int)ScoreValueDisplayOrder.NA },
            { "Indeterminate", (int)ScoreValueDisplayOrder.Indeterminate }
        };

        /// <summary>
        /// Gets display order by score value using dictionary lookup (O(1) instead of O(n))
        /// </summary>
        public static int? GetDisplayOrderByScore(string scoreValue)
        {
            return ScoreToDisplayOrderMap.TryGetValue(scoreValue, out int displayOrder) ? displayOrder : null;
        }

        public static int GetMaxDisplayOrder()
        {
            return Enum.GetValues(typeof(ScoreValueDisplayOrder)).Cast<ScoreValueDisplayOrder>().Max(x => (int)x);
        }
    }

    public static class QuestionWeightTierExtensions
    {
        /// <summary>
        /// Gets the weight multiplier value (1=3.0, 2=1.5, 3=1.0)
        /// </summary>
        public static float GetWeight(this QuestionWeightTier tier)
        {
            return tier switch
            {
                QuestionWeightTier.Critical => 3.0f,
                QuestionWeightTier.HighImportance => 1.5f,
                QuestionWeightTier.Standard => 1.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Converts Weight value to WeightID based on QuestionWeightTier enum
        /// Weight 3.0 → WeightID 1 (Critical)
        /// Weight 1.5 → WeightID 2 (HighImportance)
        /// Weight 1.0 → WeightID 3 (Standard)
        /// </summary>
        public static int GetWeightIdFromWeight(double weight)
        {
            if (Math.Abs(weight - 3.0) < 0.01)
                return (int)QuestionWeightTier.Critical;

            if (Math.Abs(weight - 1.5) < 0.01)
                return (int)QuestionWeightTier.HighImportance;

            return (int)QuestionWeightTier.Standard;
        }
    }
}
