namespace DayPlannio.Web.Models;

public class EncerrarContaViewModel
{
    public string Confirmacao { get; set; } = string.Empty;

    public int AgendamentosFuturos { get; set; }

    public string? Mensagem { get; set; }

    public string? Erro { get; set; }
}