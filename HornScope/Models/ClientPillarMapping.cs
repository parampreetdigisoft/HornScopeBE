namespace HornScope.Models
{
    public class ClientPillarMapping
    {
        public int ClientPillarMappingID { get; set; }
        public int PillarID { get; set; }
        public int UserID { get; set; }
        public Pillar? Pillar { get; set; }
        public User? User { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
    }
}
