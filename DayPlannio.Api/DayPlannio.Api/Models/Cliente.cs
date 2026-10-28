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

        [Range(-90, 90, ErrorMessage = "Latitude inválida.")]
        public double? Latitude { get; set; }

        [Range(-180, 180, ErrorMessage = "Longitude inválida.")]
        public double? Longitude { get; set; }

        public int RaioGeofenceMetros { get; set; } = 50;

        [MaxLength(500, ErrorMessage = "Observações devem ter no máximo 500 caracteres.")]
        public string? Observacoes { get; set; }

        [MaxLength(200, ErrorMessage = "O e-mail deve ter no máximo 200 caracteres.")]
        public string? Email { get; set; }

        [MaxLength(200, ErrorMessage = "O e-mail secundário deve ter no máximo 200 caracteres.")]
        public string? EmailSecundario { get; set; }

        public string? CodigoRecuperacao { get; set; }
        public DateTime? CodigoRecuperacaoExpira { get; set; }
        public int TentativasCodigo { get; set; }
        public bool CodigoBloqueado { get; set; }

        public string? SenhaHash { get; set; }

        public bool PrimeiroAcesso { get; set; } = true;

        public DateTime? PoliticaAceitaEm { get; set; }
        public string? PoliticaVersao { get; set; }

        public bool Ativo { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}