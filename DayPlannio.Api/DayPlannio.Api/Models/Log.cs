using MongoDB.Bson.Serialization.Attributes;

namespace DayPlannio.Api.Models
{
    [BsonIgnoreExtraElements]
    public class Log
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public DateTime Data { get; set; } = DateTime.UtcNow;

        public string Tipo { get; set; } = string.Empty;

        public string Descricao { get; set; } = string.Empty;

        public string Origem { get; set; } = "web";

        public string? UsuarioId { get; set; }

        public string? UsuarioNome { get; set; }

        public string? Email { get; set; }
    }
}
