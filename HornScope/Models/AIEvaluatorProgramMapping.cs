namespace HornScope.Models
{
    public class AIEvaluatorProgramMapping // Ai program for evaluator
    {
        public int AIEvaluatorProgramMappingID { get; set; }

        public int ClimateProgramID { get; set; }

        public int UserID { get; set; }

        public int? AssignBy { get; set; }

        public bool IsActive { get; set; } = false;

        public string? Comment { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

