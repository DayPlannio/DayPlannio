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
}