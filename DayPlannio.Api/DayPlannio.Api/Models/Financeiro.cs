using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public enum TipoFinanceiro
    {
        Entrada,
        Saida
    }

    [BsonIgnoreExtraElements]
    public class Financeiro
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "O usuário é obrigatório.")]
        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O tipo financeiro é obrigatório.")]
        public TipoFinanceiro Tipo { get; set; }

        [Required(ErrorMessage = "O campo Descrição é obrigatório.")]
        [MaxLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")]
        public string Descricao { get; set; } = null!;

        [Range(0.01, double.MaxValue, ErrorMessage = "O valor deve ser maior que zero.")]
        public decimal Valor { get; set; }

        [Required(ErrorMessage = "A data é obrigatória.")]
        public DateTime Data { get; set; }

        [MaxLength(100, ErrorMessage = "A categoria deve ter no máximo 100 caracteres.")]
        public string? Categoria { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}