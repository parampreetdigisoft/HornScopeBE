namespace HornScope.Dtos.ClientDto
{
    public class AddClientKpisProgramAndPillar
    {
        public List<int> Programs { get; set; } = new List<int>();
        public List<int> Pillars { get; set; } = new List<int>();
        public bool IsAllPrograms { get; set; }
        //public List<int> Kpis { get; set; }
    }
}
