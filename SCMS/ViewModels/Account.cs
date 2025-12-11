using System.ComponentModel.DataAnnotations;

namespace SCMS.ViewModels
{
    public class RegisterVm
    {
        [Required]
        public string FullName { get; set; } = null!;

        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Phone { get; set; } = null!;

        [Required]
        public string Username { get; set; } = null!;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = null!;

        // Login as: Patient / Doctor / Receptionist / Admin
        [Required]
        public string Role { get; set; } = null!;
    }

    public class LoginVm
    {
        [Required]
        public string EmailOrUsername { get; set; } = null!;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = null!;

        [Required]
        public string Role { get; set; } = null!;   // من الـ dropdown

        public bool RememberMe { get; set; }
    }
}
