using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class TipoServico
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "O usuário é obrigatório.")]
        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O campo Tipo é obrigatório.")]
        [MaxLength(100, ErrorMessage = "O tipo deve ter no máximo 100 caracteres.")]
        public string Tipo { get; set; } = null!;

        [MaxLength(300, ErrorMessage = "A descrição deve ter no máximo 300 caracteres.")]
        public string? Descricao { get; set; }

        [Range(1, 1440, ErrorMessage = "O tempo estimado deve estar entre 1 e 1440 minutos.")]
        public int TempoEstimado { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}