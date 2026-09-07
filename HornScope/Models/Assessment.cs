namespace HornScope.Models
{
    public class Assessment
    {
        public int AssessmentID { get; set; }
        public int StaffProgramMappingID { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public AssessmentPhase? AssessmentPhase { get; set; } = Models.AssessmentPhase.InProgress;

        // Navigation properties
        public StaffProgramMapping StaffProgramMapping { get; set; }
        public ICollection<PillarAssessment> PillarAssessments { get; set; } = new List<PillarAssessment>();
    }

    public enum AssessmentPhase : byte
    {
        NotStarted = 0,   // Assessment not submitted at all
        InProgress = 1,   // User has access to edit
        EditRequested = 2, // User requested permission to edit
        EditRejected = 3, // Admin/analyst rejected edit request
        EditApproved = 4, // Admin/analyst approved edit request
        Completed = 5     // Assessment completed
    }
}
