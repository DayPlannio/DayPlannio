namespace DayPlannio.Web.Models;

public class LoginViewModel
{
    public string Email { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
    public string? Erro { get; set; }
}
