namespace DayPlannio.App.Models
{
    public class MetricasPrestador
    {
        public string Periodo { get; set; } = string.Empty;
        public int Atendimentos { get; set; }
        public int Clientes { get; set; }
        public double HorasTrabalhadas { get; set; }
        public decimal Faturamento { get; set; }
        public VariacoesMetricas Variacoes { get; set; } = new();
        public List<ServicoMetrica> PorServico { get; set; } = new();
        public List<EvolucaoDia> Evolucao { get; set; } = new();
        public List<MaiorAtendimento> MaioresAtendimentos { get; set; } = new();
        public ResumoDeTempo ResumoDeTempo { get; set; } = new();
        public int Cancelamentos { get; set; }
        public int TotalFinalizados { get; set; }
        public double TaxaCancelamento { get; set; }
        public int CancelamentosComMotivo { get; set; }
        public List<MotivoCancelamentoMetrica> MotivosCancelamento { get; set; } = new();
        public MotivoCancelamentoMetrica? PrincipalMotivoCancelamento { get; set; }
    }

    public class VariacoesMetricas
    {
        public double Atendimentos { get; set; }
        public double Clientes { get; set; }
        public double HorasTrabalhadas { get; set; }
        public double Faturamento { get; set; }
    }

    public class ServicoMetrica
    {
        public string Servico { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Percentual { get; set; }
        public decimal Faturamento { get; set; }
        public double? TempoMedioMinutos { get; set; }
    }

    public class EvolucaoDia
    {
        public DateTime Data { get; set; }
        public int Quantidade { get; set; }
    }

    public class MaiorAtendimento
    {
        public string Servico { get; set; } = string.Empty;
        public double DuracaoMinutos { get; set; }
    }

    public class ResumoDeTempo
    {
        public double? Maior { get; set; }
        public double? Menor { get; set; }
        public double? Medio { get; set; }
        public double TotalMinutos { get; set; }
    }

    public class MotivoCancelamentoMetrica
    {
        public string Palavra { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double Percentual { get; set; }
        public string PercentualTexto => $"{Percentual:0.#}%";
    }
}
