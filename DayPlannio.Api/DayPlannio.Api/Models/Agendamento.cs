using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public enum StatusAgendamento
    {
        Agendado,
        Cancelado,
        Concluido
    }

    [BsonIgnoreExtraElements]
    public class Agendamento
    {
        public Guid Id { get; set; }

        [Required(ErrorMessage = "O usuário é obrigatório.")]
        public Guid UsuarioId { get; set; }

        [Required(ErrorMessage = "O cliente é obrigatório.")]
        public Guid ClienteId { get; set; }

        [Required(ErrorMessage = "O tipo de serviço é obrigatório.")]
        public Guid TipoServicoId { get; set; }

        [Required(ErrorMessage = "O campo Data e Hora é obrigatório.")]
        public DateTime DataHora { get; set; }

        public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

        [MaxLength(500, ErrorMessage = "Observações podem ter no máximo 500 caracteres.")]
        public string? Observacoes { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "O valor cobrado deve ser maior ou igual a zero.")]
        public decimal ValorCobrado { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "O custo do material deve ser maior ou igual a zero.")]
        public decimal CustoMaterial { get; set; }

        public DateTime? DataConclusao { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}