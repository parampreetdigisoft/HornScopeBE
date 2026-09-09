using System.ComponentModel;

namespace HornScope.Enums
{
    public enum ScoreValue 
    { 
        Hundred = 100, 
        SeventyFive = 75, 
        Fifty = 50, 
        TwentyFive = 25, 
        Zero = 0, 
        NA, 
        Indeterminate
    }

    public enum QuestionWeightTier
    {
        Critical = 1,
        HighImportance = 2,
        Standard = 3
    }

    public enum ScoreValueDisplayOrder
    {
        [Description("100")]
        Hundred = 1,

        [Description("75")]
        SeventyFive = 2,

        [Description("50")]
        Fifty = 3,

        [Description("25")]
        TwentyFive = 4,

        [Description("0")]
        Zero = 5,

        [Description("N/A")]
        NA = 6,

        [Description("Indeterminate")]
        Indeterminate = 7
    }
}
