namespace HornScope.Models
{
    public class Question
    {
        public int QuestionID { get; set; }
        public double Weight { get; set; } = 1.0;
        public int PillarID { get; set; }
        public string QuestionText { get; set; }
        public int DisplayOrder { get; set; }
        public Pillar Pillar { get; set; }
        public bool IsDeleted { get; set; } = false;
        public ICollection<QuestionOption> QuestionOptions { get; set; } = new List<QuestionOption>();
    }
} 