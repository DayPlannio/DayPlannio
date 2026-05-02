using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class Cliente
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "O usuário é obrigatório.")]
        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O campo Nome é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")]
        public string Nome { get; set; } = null!;

        [Required(ErrorMessage = "O telefone é obrigatório.")]
        [Phone(ErrorMessage = "Telefone inválido.")]
        public string Telefone { get; set; } = null!;

        [Required(ErrorMessage = "O endereço é obrigatório.")]
        [MaxLength(200, ErrorMessage = "O endereço deve ter no máximo 200 caracteres.")]
        public string Endereco { get; set; } = null!;

        [MaxLength(500, ErrorMessage = "Observações devem ter no máximo 500 caracteres.")]
        public string? Observacoes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}