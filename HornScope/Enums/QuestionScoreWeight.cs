using System.ComponentModel;

namespace HornScope.Enums
{
    public enum ScoreValue
    {
        [Description("4")]
        Score4 = 1,

        [Description("3")]
        Score3 = 2,

        [Description("2")]
        Score2 = 3,

        [Description("1")]
        Score1 = 4,

        [Description("0")]
        Score0 = 5,

        [Description("-1")]
        ScoreMinus1 = 6,

        [Description("-2")]
        ScoreMinus2 = 7,

        [Description("-3")]
        ScoreMinus3 = 8,

        [Description("-4")]
        ScoreMinus4 = 9,

        [Description("N/A")]
        NA = 10,

        [Description("Indeterminate")]
        Indeterminate = 11
    }

    public enum QuestionWeightTier
    {
        Critical = 1,
        HighImportance = 2,
        Standard = 3
    }
}
