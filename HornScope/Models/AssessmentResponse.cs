using HornScope.Enums;

namespace HornScope.Models
{
    //public enum ScoreValue { Four = 4, Three = 3, Two = 2, One = 1, Zero = 0, MinusOne = -1, MinusTwo = -2, MinusThree = -3, MinusFour = -4, NA, Indeterminate }
    public class AssessmentResponse
    {
        public int ResponseID { get; set; }
        public int PillarAssessmentID { get; set; }
        public int QuestionID { get; set; }
        public int QuestionOptionID { get; set; }
        public int? Score { get; set; }
        public string Justification { get; set; } 
        public string? Source { get; set; } 
        public PillarAssessment PillarAssessment { get; set; } 
        public Question Question { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
} 