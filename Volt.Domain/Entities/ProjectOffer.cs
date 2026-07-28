namespace Volt.Domain.Entities
{
    public class ProjectOffer
    {
        public int Id { get; set; }
        public int ProjectId { get; set; }
        public decimal Power { get; set; }
        public byte PowerType { get; set; }
        public string AreaType { get; set; }
        public bool IsActive { get; set; }

        public Project Project { get; set; }
    }
}
