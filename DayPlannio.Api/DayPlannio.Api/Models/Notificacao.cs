using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class Notificacao
    {
        public Guid Id { get; set; }

        [Required]
        public Guid UsuarioId { get; set; }

        [Required]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        public string Mensagem { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public bool Lida { get; set; }

        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    }
}
