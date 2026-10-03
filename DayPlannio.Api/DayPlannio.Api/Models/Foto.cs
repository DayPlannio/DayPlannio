using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class Foto
    {
        public Guid Id { get; set; }

        [Required]
        public Guid AgendamentoId { get; set; }

        [Required]
        public Guid PrestadorId { get; set; }

        [Required]
        public string Url { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Descricao { get; set; }

        public bool Publica { get; set; }

        public bool Aprovado { get; set; }

        public DateTime DataUpload { get; set; } = DateTime.UtcNow;
    }
}
