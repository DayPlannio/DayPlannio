namespace DayPlannio.App.Models;

public class Usuario
{
    public string NomeCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Cidade { get; set; }
    public bool CidadeVisivel { get; set; }
    public bool TelefoneVisivel { get; set; }
    public string? Plano { get; set; }
    public bool PlanoAtivo { get; set; }
    public DateTime? PlanoExpiraEm { get; set; }
    public string? PlanoOrigem { get; set; }
    public string? PlanoPendente { get; set; }
    public DateTime? PlanoSolicitadoEm { get; set; }
    public bool RenovacaoAutomatica { get; set; }
    public DateTime? PlanoCanceladoEm { get; set; }
    public string? PlanoEfetivo { get; set; }
}