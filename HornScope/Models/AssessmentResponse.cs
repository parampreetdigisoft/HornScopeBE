using HornScope.Enums;
using System;

namespace HornScope.Models
{
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