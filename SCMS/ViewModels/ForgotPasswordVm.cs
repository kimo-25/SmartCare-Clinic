using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class ForgotPasswordVm
    {
        [Required(ErrorMessage = "Please enter email or username")]
        public string EmailOrUsername { get; set; } = string.Empty;
    }
}
