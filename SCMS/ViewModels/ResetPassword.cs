namespace SCMS.ViewModels
{
    public class ResetPasswordVm
    {
        public int UserId { get; set; }
        public string? NewPassword { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
