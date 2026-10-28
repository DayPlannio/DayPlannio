using DayPlannio.Api.Services;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;

namespace DayPlannio.Api.Models
{
    public enum StatusAgendamento
    {
        Agendado,
        EmAtendimento,
        Cancelado,
        Concluido,
        EmPausa
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
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [Required(ErrorMessage = "O campo Data e Hora é obrigatório.")]
        public DateTime DataHora { get; set; }

        public StatusAgendamento Status { get; set; } = StatusAgendamento.Agendado;

        [MaxLength(500, ErrorMessage = "Observações podem ter no máximo 500 caracteres.")]
        public string? Observacoes { get; set; }

        [MaxLength(500, ErrorMessage = "O endereço de atendimento pode ter no máximo 500 caracteres.")]
        public string? EnderecoAtendimento { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "O valor cobrado deve ser maior ou igual a zero.")]
        public decimal ValorCobrado { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "O custo do material deve ser maior ou igual a zero.")]
        public decimal CustoMaterial { get; set; }

        public DateTime? DataConclusao { get; set; }

        [MaxLength(200, ErrorMessage = "O motivo do cancelamento pode ter no máximo 200 caracteres.")]
        public string? MotivoCancelamento { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? DataCancelamento { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? Inicio { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? Fim { get; set; }

        public double? DuracaoMinutos { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ChegadaEm { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? UltimoPingEm { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? InicioPausa { get; set; }

        public double PausaAcumuladaMinutos { get; set; }


        [BsonIgnore]
        public DateTime? ConfirmacaoChegadaEm =>
            Status == StatusAgendamento.Agendado && ChegadaEm.HasValue
                ? ChegadaEm.Value + GeofenceRegras.ConfirmacaoChegada
                : null;

        [BsonIgnore]
        public DateTime? PausaConcluiEm =>
    Status == StatusAgendamento.EmPausa && InicioPausa.HasValue
        ? InicioPausa.Value + GeofenceRegras.LimiteForaDoRaio
        : null;

        [BsonIgnore]
        public double? TrabalhadoSegundos
        {
            get
            {
                if (!Inicio.HasValue) return null;
                var pausa = PausaAcumuladaMinutos * 60;

                if (Status == StatusAgendamento.EmAtendimento)
                    return Math.Max(0, (DateTime.UtcNow - Inicio.Value).TotalSeconds - pausa);

                if (Status == StatusAgendamento.EmPausa && InicioPausa.HasValue)
                    return Math.Max(0, (InicioPausa.Value - Inicio.Value).TotalSeconds - pausa);

                return null;
            }
        }

        [BsonIgnore]
        public double? NoLocalSegundos =>
            Status == StatusAgendamento.Agendado && ChegadaEm.HasValue
                ? Math.Max(0, (DateTime.UtcNow - ChegadaEm.Value).TotalSeconds)
                : null;

        [BsonIgnore]
        public double? ConfirmaEmSegundos =>
            Status == StatusAgendamento.Agendado && ChegadaEm.HasValue
                ? Math.Max(0, (ChegadaEm.Value + GeofenceRegras.ConfirmacaoChegada - DateTime.UtcNow).TotalSeconds)
                : null;

        public bool IniciadoPorGeofence { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? ClienteEncerradoEm { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}