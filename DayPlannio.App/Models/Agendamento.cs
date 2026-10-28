namespace DayPlannio.App.Models;

public class Agendamento
{
    public string Id { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public string TipoServicoId { get; set; } = string.Empty;
    public DateTime DataHora { get; set; }
    public string? Status { get; set; }
    public string? Observacoes { get; set; }
    public decimal ValorCobrado { get; set; }
    public decimal CustoMaterial { get; set; }
    public string? EnderecoAtendimento { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? Inicio { get; set; }
    public DateTime? Fim { get; set; }
    public DateTime? InicioPausa { get; set; }
    public double PausaAcumuladaMinutos { get; set; }
    public DateTime? ConfirmacaoChegadaEm { get; set; }
    public DateTime? PausaConcluiEm { get; set; }
    public double? DuracaoMinutos { get; set; }
    public string? MotivoCancelamento { get; set; }
    public DateTime? DataCancelamento { get; set; }
    public DateTime? ClienteEncerradoEm { get; set; }
    public double? TrabalhadoSegundos { get; set; }
    public double? NoLocalSegundos { get; set; }
    public double? ConfirmaEmSegundos { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public System.Diagnostics.Stopwatch DesdeRecebido { get; } = System.Diagnostics.Stopwatch.StartNew();
}
