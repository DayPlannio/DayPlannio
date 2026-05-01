using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public class ResetPassword
    {
        public string Email { get; set; } = null!;
        public string Code { get; set; } = null!;
        public string NewPassword { get; set; } = null!;
        public string ConfirmPassword { get; set; } = null!;
    }
}