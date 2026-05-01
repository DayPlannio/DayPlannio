using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class User
    {
        public Guid Id { get; set; }

        [Display(Name = "Nome Completo")]
        public string NomeCompleto { get; set; } = null!;

        [Required(ErrorMessage = "O campo Email é obrigatório.")]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required(ErrorMessage = "O campo Senha é obrigatório.")]

        public string Senha { get; set; } = null!;
        [Display(Name = "Confirmar Senha")]
        public string? ConfirmeSenha { get; set; }

        [Phone]
        public string? Telefone { get; set; }
        public bool IsAdmin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
