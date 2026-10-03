namespace DayPlannio.App.Models;

public class Cliente
{
    public string Id { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Endereco { get; set; }
    public string? Observacoes { get; set; }
    public string? Email { get; set; }
    public DateTime? UltimaVisita { get; set; }
    public string UltimaVisitaTexto { get; set; } = "Nenhuma visita concluída";
    public DateTime CreatedAt { get; set; }

    public bool TemObservacoes =>
        !string.IsNullOrWhiteSpace(Observacoes);

    public bool TemAcessoWeb =>
        !string.IsNullOrWhiteSpace(Email);

    public bool PodeGerarCredenciais { get; set; }
}