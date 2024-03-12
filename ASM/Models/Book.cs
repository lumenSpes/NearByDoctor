using System.ComponentModel.DataAnnotations;

namespace ASM.Models
{
    public class Book
    {
        [Key]
        public int Id { get; set; }
        public int AmbulanceId { get; set; }
        public int PatientId { get; set; }
    }
}
