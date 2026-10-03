using System.Text.Json.Serialization;

namespace DayPlannio.Web.Models
{
    public class LoginResposta
    {
        public Guid ClienteId { get; set; }
        public Guid UsuarioId { get; set; }
        public string Email { get; set; } = string.Empty;
        public bool PrimeiroAcesso { get; set; }
    }

    public class EncerramentoResposta
    {
        [JsonPropertyName("message")]
        public string Mensagem { get; set; } = string.Empty;
        public int AgendamentosFuturos { get; set; }
    }

    public class ResumoGeral
    {
        public int TotalServicos { get; set; }
        public double TempoTotalMinutos { get; set; }
        public double? TempoMedioMinutos { get; set; }
        public decimal ValorTotal { get; set; }
    }

    public class Variacoes
    {
        public double Atendimentos { get; set; }
        public double Tempo { get; set; }
        public double Valor { get; set; }
    }

    public class ServicoCategoriaItem
    {
        public string Categoria { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Percentual { get; set; }
        public decimal Valor { get; set; }
        public double? TempoMedioMinutos { get; set; }
    }

    public class HistoricoItem
    {
        public string Servico { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public string Profissional { get; set; } = string.Empty;
        public DateTime? Inicio { get; set; }
        public DateTime? Fim { get; set; }
        public double? DuracaoMinutos { get; set; }
        public decimal? Valor { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class EvolucaoItem
    {
        public DateTime Data { get; set; }
        public int Quantidade { get; set; }
    }

    public class ResumoTempo
    {
        public double? Maior { get; set; }
        public double? Menor { get; set; }
        public double? Medio { get; set; }
        public double TotalMinutos { get; set; }
    }

    public class MetricasClienteDto
    {
        public string Periodo { get; set; } = string.Empty;
        public ResumoGeral ResumoGeral { get; set; } = new();
        public Variacoes Variacoes { get; set; } = new();
        public List<ServicoCategoriaItem> ServicosPorCategoria { get; set; } = new();
        public List<HistoricoItem> Historico { get; set; } = new();
        public List<ServicoCategoriaItem> TempoMedioPorServico { get; set; } = new();
        public List<ServicoCategoriaItem> InvestimentoPorServico { get; set; } = new();
        public List<EvolucaoItem> Evolucao { get; set; } = new();
        public ResumoTempo ResumoTempo { get; set; } = new();
    }

    public class ClientePerfilDto
    {
        public Guid ClienteId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string EmailSecundario { get; set; } = string.Empty;
        public int AgendamentosFuturos { get; set; }
    }

    public class PortfolioItem
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public DateTime DataUpload { get; set; }
        public string? Servico { get; set; }
        public string? Cliente { get; set; }
        public string? Prestador { get; set; }
        public DateTime? DataAtendimento { get; set; }
    }

    public class PortfolioStats
    {
        public int TotalPrestadores { get; set; }
        public int TotalFotos { get; set; }
        public int TotalServicos { get; set; }
        public List<string> Cidades { get; set; } = new();
        public List<PrestadorStats> Prestadores { get; set; } = new();
        public List<ServicoStats> Servicos { get; set; } = new();
    }

    public class PrestadorStats
    {
        public string Nome { get; set; } = "";
        public string? Cidade { get; set; }
        public string? Telefone { get; set; }
        public bool CidadeVisivel { get; set; }
        public bool TelefoneVisivel { get; set; }
        public int Fotos { get; set; }
        public int Servicos { get; set; }
    }

    public class ServicoStats
    {
        public string Nome { get; set; } = "";
        public int Fotos { get; set; }
    }
}
