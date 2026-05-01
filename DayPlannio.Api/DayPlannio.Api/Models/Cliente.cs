using System.ComponentModel.DataAnnotations;
using MongoDB.Bson.Serialization.Attributes;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class Cliente
    {
        public Guid Id { get; set; }

        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O campo Nome é obrigatório.")]
        public string Nome { get; set; } = null!;

        [Phone]
        public string? Telefone { get; set; }

        [Display(Name = "Endereço")]
        public string? Endereco { get; set; }

        [Display(Name = "Observações")]
        public string? Observacoes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}