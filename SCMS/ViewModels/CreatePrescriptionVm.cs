using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels.Doctor
{
    public class CreatePrescriptionVm
    {
        [Required]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "Diagnosis is required")]
        public string Diagnosis { get; set; } = null!;

        [Required(ErrorMessage = "Treatment is required")]
        public string Treatment { get; set; } = null!;

        public bool RadiologyRequested { get; set; }
    }
}
