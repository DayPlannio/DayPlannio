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

        public Guid UsuarioId { get; set; }

        public TipoFinanceiro Tipo { get; set; }

        [Required(ErrorMessage = "O campo Descrição é obrigatório.")]
        public string Descricao { get; set; } = null!;

        public decimal Valor { get; set; }

        public DateTime Data { get; set; }

        public string? Categoria { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}