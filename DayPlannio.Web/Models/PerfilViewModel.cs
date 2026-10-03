namespace DayPlannio.Web.Models;

public class PerfilViewModel
{
    public string Email { get; set; } = string.Empty;
    public string EmailSecundario { get; set; } = string.Empty;
    public int AgendamentosFuturos { get; set; }
    public string? Mensagem { get; set; }
    public string? Erro { get; set; }
}
