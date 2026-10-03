namespace DayPlannio.Web.Models;

public class TrocarSenhaViewModel
{
    public string Email { get; set; } = string.Empty;
    public string EmailSecundario { get; set; } = string.Empty;
    public string SenhaAtual { get; set; } = string.Empty;
    public string SenhaNova { get; set; } = string.Empty;
    public string SenhaNovaConfirmacao { get; set; } = string.Empty;
    public string? Mensagem { get; set; }
    public string? Erro { get; set; }
}
