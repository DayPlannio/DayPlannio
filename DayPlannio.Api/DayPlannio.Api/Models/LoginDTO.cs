using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public class LoginDTO
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        public string Senha { get; set; } = null!;
    }
}
