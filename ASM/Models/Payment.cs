using System.ComponentModel.DataAnnotations;

namespace ASM.Models
{
    public class Payment
    {
        [Key]
        public int Id { get; set; }
        public int PatientId { get; set; }
        public int DoctorId { get; set; }
        public string TrxId { get; set; }
    }
}
