using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public class EditUserDto
    {
        [MaxLength(100, ErrorMessage = "Nome muito longo.")]
        public string? NomeCompleto { get; set; }

        [EmailAddress(ErrorMessage = "Email inválido.")]
        public string? Email { get; set; }

        [Phone(ErrorMessage = "Telefone inválido.")]
        public string? Telefone { get; set; }

        [MaxLength(100, ErrorMessage = "Cidade muito longa.")]
        public string? Cidade { get; set; }

        public bool? CidadeVisivel { get; set; }
        public bool? TelefoneVisivel { get; set; }
    }
}