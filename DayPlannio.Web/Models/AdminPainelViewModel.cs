namespace DayPlannio.Web.Models;

public class AdminPainelViewModel
{
    public List<AdminLogItem> Logs { get; set; } = new();
    public long TotalLogs { get; set; }
    public List<AdminClienteItem> Clientes { get; set; } = new();
    public List<AdminPrestadorItem> Prestadores { get; set; } = new();
    public List<AdminFotoItem> FotosPendentes { get; set; } = new();
    public int TotalFotosPendentes { get; set; }
    public string? Mensagem { get; set; }
}

public class AdminLogItem
{
    public Guid Id { get; set; }
    public DateTime Data { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public string? UsuarioId { get; set; }
    public string? UsuarioNome { get; set; }
    public string? Email { get; set; }
}

public class AdminClienteItem
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? EmailSecundario { get; set; }
    public bool Ativo { get; set; }
    public bool PrimeiroAcesso { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class AdminPrestadorItem
{
    public Guid Id { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? UserName { get; set; }
    public string? Plano { get; set; }
    public bool PlanoAtivo { get; set; }
    public DateTime? PlanoExpiraEm { get; set; }
    public string? PlanoOrigem { get; set; }
    public string? PlanoPendente { get; set; }
    public DateTime? PlanoSolicitadoEm { get; set; }
}

public class AdminFotoItem
{
    public Guid Id { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public bool Publica { get; set; }
    public DateTime DataUpload { get; set; }
    public string Prestador { get; set; } = string.Empty;
    public string? Servico { get; set; }
}
