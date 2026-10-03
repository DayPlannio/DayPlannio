using System.Text.Json;

namespace DayPlannio.Web.Models;

public class MetricasViewModel
{
    public MetricasClienteDto? Metricas { get; set; }
    public string? ErroMensagem { get; set; }
    public string Periodo { get; set; } = "mensal";

    public string CategoriaLabelsJson { get; set; } = "[]";
    public string CategoriaValoresJson { get; set; } = "[]";
    public string EvolucaoLabelsJson { get; set; } = "[]";
    public string EvolucaoValoresJson { get; set; } = "[]";
    public string TempoMedioLabelsJson { get; set; } = "[]";
    public string TempoMedioValoresJson { get; set; } = "[]";
    public string InvestimentoLabelsJson { get; set; } = "[]";
    public string InvestimentoValoresJson { get; set; } = "[]";

    public string TempoTotalTexto => Formatar(Metricas?.ResumoGeral.TempoTotalMinutos);
    public string TempoMedioTexto => Formatar(Metricas?.ResumoGeral.TempoMedioMinutos);
    public string ResumoMaior => Formatar(Metricas?.ResumoTempo.Maior);
    public string ResumoMenor => Formatar(Metricas?.ResumoTempo.Menor);
    public string ResumoMedio => Formatar(Metricas?.ResumoTempo.Medio);
    public string ResumoTotal => Formatar(Metricas?.ResumoTempo.TotalMinutos);
    public string VariacaoAtendimentos => FormatVar(Metricas?.Variacoes.Atendimentos ?? 0);
    public string VariacaoTempo => FormatVar(Metricas?.Variacoes.Tempo ?? 0);
    public string VariacaoValor => FormatVar(Metricas?.Variacoes.Valor ?? 0);

    public void BuildChartData()
    {
        if (Metricas is null)
            return;

        var m = Metricas;

        CategoriaLabelsJson = JsonSerializer.Serialize(m.ServicosPorCategoria.Select(c => c.Categoria));
        CategoriaValoresJson = JsonSerializer.Serialize(m.ServicosPorCategoria.Select(c => c.Quantidade));

        EvolucaoLabelsJson = JsonSerializer.Serialize(m.Evolucao.Select(e => e.Data.ToString("dd/MM")));
        EvolucaoValoresJson = JsonSerializer.Serialize(m.Evolucao.Select(e => e.Quantidade));

        TempoMedioLabelsJson = JsonSerializer.Serialize(m.TempoMedioPorServico.Select(s => s.Categoria));
        TempoMedioValoresJson = JsonSerializer.Serialize(
            m.TempoMedioPorServico.Select(s => s.TempoMedioMinutos ?? 0));

        InvestimentoLabelsJson = JsonSerializer.Serialize(m.InvestimentoPorServico.Select(s => s.Categoria));
        InvestimentoValoresJson = JsonSerializer.Serialize(
            m.InvestimentoPorServico.Select(s => Math.Round((double)s.Valor, 2)));
    }

    public string FormatarMinutos(double? minutos)
    {
        if (minutos is null or 0)
            return "-";
        return Formatar(minutos);
    }

    public string FormatarValor(decimal valor)
        => valor <= 0 ? "-" : valor.ToString("C");

    private static string Formatar(double? minutos)
    {
        if (minutos is null or 0)
            return "-";

        var total = (int)Math.Round(minutos.Value);
        if (total < 60)
            return $"{total}min";

        var horas = total / 60;
        var resto = total % 60;
        return resto > 0 ? $"{horas}h{resto:D2}min" : $"{horas}h";
    }

    private static string FormatVar(double valor)
    {
        if (valor == 0)
            return "= período anterior";
        var sinal = valor > 0 ? "+" : "";
        return $"{sinal}{valor:0.#}% vs período anterior";
    }
}
