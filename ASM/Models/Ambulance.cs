using System.ComponentModel.DataAnnotations;

namespace ASM.Models
{
    public class Ambulance
    {
        [Key]
        public int Id { get; set; }
        public int LocationId { get; set; }
        public string Rate { get; set; }
        public string LicenceNo { get; set; }
    }
}
