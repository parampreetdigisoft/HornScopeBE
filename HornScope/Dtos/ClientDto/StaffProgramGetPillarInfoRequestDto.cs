using HornScope.Enums;

namespace HornScope.Dtos.ClientDto
{
    public class StaffProgramGetPillarInfoRequestDto
    {
        public int UserID { get; set; } = 0;
        public int ClimateProgramID { get; set; }
        public int PillarID { get; set; }
        public DateTime UpdatedAt { get; set; } = new DateTime(DateTime.Now.Year, 1, 1);
        public TieredAccessPlan Tiered { get; set; } = TieredAccessPlan.Pending;
    }
}
