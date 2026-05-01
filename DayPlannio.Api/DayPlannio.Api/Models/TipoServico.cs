using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class TipoServico
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O campo Tipo é obrigatório.")]
        public string Tipo { get; set; } = null!;

        [Display(Name = "Descrição")]
        public string? Descricao { get; set; }

        [Display(Name = "Tempo Estimado (minutos)")]
        public int TempoEstimado { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}