namespace DayPlannio.App.Models;

public class Servico
{
    public string Id { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public int TempoEstimado { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
}