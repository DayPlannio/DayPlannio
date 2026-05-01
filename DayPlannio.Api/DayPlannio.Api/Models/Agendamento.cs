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

        public Guid UsuarioId { get; set; }
        public Guid ClienteId { get; set; }
        public Guid TipoServicoId { get; set; }

        [Required(ErrorMessage = "O campo Data e Hora é obrigatório.")]
        public DateTime DataHora { get; set; }

        public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

        public string? Observacoes { get; set; }

        [Display(Name = "Valor Cobrado")]
        public decimal ValorCobrado { get; set; }

        [Display(Name = "Custo do Material")]
        public decimal CustoMaterial { get; set; }
        public DateTime? DataConclusao { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}