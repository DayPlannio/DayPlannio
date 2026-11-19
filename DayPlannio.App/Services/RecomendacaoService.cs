using System.Globalization;
using DayPlannio.App.Models;

namespace DayPlannio.App.Services;

public static class RecomendacaoService
{
    private static readonly CultureInfo Br = new("pt-BR");

    private const double DiferencaMinima = 1.2;

    private const double FatiaRealocada = 0.25;

    private const double LimiteCancelamento = 15;
    private const int MinimoFinalizadosParaCancelamento = 3;
    private const double LimiteTendencia = 10;
    private const int MaximoRecomendacoes = 1;

    private sealed record Desempenho(
        string Servico,
        int Quantidade,
        double Percentual,
        decimal Faturamento,
        double? Minutos)
    {
        public decimal Ticket => Quantidade > 0 ? Faturamento / Quantidade : 0;

        public double HorasTotais => Minutos.HasValue ? Quantidade * Minutos.Value / 60.0 : 0;

        public decimal? ReceitaPorHora =>
            Minutos is > 0 && Quantidade > 0 && HorasTotais > 0
                ? Faturamento / (decimal)HorasTotais
                : null;
    }

    public static List<Recomendacao> Gerar(
        MetricasPrestador? metricas,
        IReadOnlyDictionary<string, int>? tempoEstimadoPorServico = null)
    {
        var resultado = new List<Recomendacao>();

        if (metricas == null)
            return resultado;

        var servicos = metricas.PorServico
            .Where(s => s.Quantidade > 0 && !string.IsNullOrWhiteSpace(s.Servico))
            .Select(s => new Desempenho(
                s.Servico,
                s.Quantidade,
                s.Percentual,
                s.Faturamento,
                MinutosDoServico(s, tempoEstimadoPorServico)))
            .ToList();

        if (servicos.Count == 0)
            return resultado;

        var comTempo = servicos
            .Where(s => s.ReceitaPorHora.HasValue)
            .OrderByDescending(s => s.ReceitaPorHora!.Value)
            .ToList();

        var foco = comTempo.Count > 0
            ? RecomendarFocoPorHora(comTempo)
            : RecomendarFocoPorTicket(servicos);

        if (foco != null)
            resultado.Add(foco);

        if (comTempo.Count >= 2)
        {
            var preco = RecomendarPreco(comTempo, servicos);
            if (preco != null)
                resultado.Add(preco);
        }

        var cancelamento = RecomendarCancelamento(metricas);
        if (cancelamento != null)
            resultado.Add(cancelamento);

        var tendencia = RecomendarTendencia(metricas, comTempo.FirstOrDefault()?.Servico
                                                      ?? servicos.OrderByDescending(s => s.Ticket).First().Servico);
        if (tendencia != null)
            resultado.Add(tendencia);

        return resultado.Take(MaximoRecomendacoes).ToList();
    }

    private static double? MinutosDoServico(
        ServicoMetrica servico,
        IReadOnlyDictionary<string, int>? estimados)
    {
        if (servico.TempoMedioMinutos is > 0)
            return servico.TempoMedioMinutos;

        if (estimados == null)
            return null;

        foreach (var par in estimados)
        {
            if (string.Equals(par.Key, servico.Servico, StringComparison.OrdinalIgnoreCase) && par.Value > 0)
                return par.Value;
        }

        return null;
    }

    private static Recomendacao? RecomendarFocoPorHora(List<Desempenho> ordenados)
    {
        var melhor = ordenados[0];
        var melhorHora = melhor.ReceitaPorHora!.Value;

        if (ordenados.Count == 1)
        {
            return new Recomendacao
            {
                Tipo = TipoRecomendacao.Foco,
                Titulo = $"Seu serviço mais rentável: {melhor.Servico}",
                Texto = $"{melhor.Servico} rende em média {Dinheiro(melhorHora)} por hora trabalhada. " +
                        "Cadastre mais serviços para o app comparar e mostrar onde vale focar."
            };
        }

        var pior = ordenados[^1];
        var piorHora = pior.ReceitaPorHora!.Value;

        if ((double)melhorHora < (double)piorHora * DiferencaMinima)
        {
            return new Recomendacao
            {
                Tipo = TipoRecomendacao.Foco,
                Titulo = "Seus serviços rendem parecido",
                Texto = $"{melhor.Servico} lidera com {Dinheiro(melhorHora)} por hora, mas a diferença para " +
                        $"{pior.Servico} ({Dinheiro(piorHora)}/h) é pequena. Use o que você preferir e " +
                        "acompanhe as métricas nos próximos períodos."
            };
        }

        return new Recomendacao
        {
            Tipo = TipoRecomendacao.Foco,
            Titulo = $"Priorize {melhor.Servico}",
            Texto = $"{melhor.Servico} é o serviço que mais rende por hora: {Dinheiro(melhorHora)}. " +
                    $"{pior.Servico} rende {Dinheiro(piorHora)}. Se puder, dê mais espaço na agenda para {melhor.Servico}."
        };
    }

    private static Recomendacao? RecomendarFocoPorTicket(List<Desempenho> servicos)
    {
        var melhor = servicos.OrderByDescending(s => s.Ticket).First();

        if (melhor.Ticket <= 0)
            return null;

        var texto = $"{melhor.Servico} tem o maior valor médio por atendimento: {Dinheiro(melhor.Ticket)}.";

        texto += servicos.Count > 1
            ? " Ainda não há tempo registrado para comparar por hora; assim que o geofencing medir a duração " +
              "ou você informar o tempo estimado dos serviços, a recomendação fica mais precisa."
            : " Cadastre mais serviços para o app comparar.";

        return new Recomendacao
        {
            Tipo = TipoRecomendacao.Foco,
            Titulo = $"Priorize {melhor.Servico}",
            Texto = texto
        };
    }

    private static Recomendacao? RecomendarPreco(List<Desempenho> comTempo, List<Desempenho> todos)
    {
        var melhor = comTempo[0];
        var maisPedido = todos.OrderByDescending(s => s.Quantidade).First();

        if (string.Equals(maisPedido.Servico, melhor.Servico, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!maisPedido.ReceitaPorHora.HasValue)
            return null;

        var horaPedido = maisPedido.ReceitaPorHora.Value;
        var horaMelhor = melhor.ReceitaPorHora!.Value;

        if ((double)horaMelhor < (double)horaPedido * DiferencaMinima)
            return null;

        var reajuste = (double)(horaMelhor / horaPedido - 1) * 100;
        reajuste = Math.Min(reajuste, 50);

        return new Recomendacao
        {
            Tipo = TipoRecomendacao.Preco,
            Titulo = $"Revise o preço de {maisPedido.Servico}",
            Texto = $"{maisPedido.Servico} é o mais pedido ({Num(maisPedido.Percentual)}% dos atendimentos), " +
                    $"mas rende {Dinheiro(horaPedido)} por hora, menos que {melhor.Servico}. " +
                    $"Um reajuste de cerca de {Num(reajuste, "0")}% aproximaria o ganho por hora dos dois."
        };
    }

    private static Recomendacao? RecomendarCancelamento(MetricasPrestador metricas)
    {
        if (metricas.TotalFinalizados < MinimoFinalizadosParaCancelamento ||
            metricas.TaxaCancelamento < LimiteCancelamento)
            return null;

        var texto = $"{Num(metricas.TaxaCancelamento)}% dos atendimentos finalizados foram cancelados " +
                    $"({metricas.Cancelamentos} de {metricas.TotalFinalizados}).";

        if (metricas.Atendimentos > 0 && metricas.Faturamento > 0)
        {
            var perdido = metricas.Faturamento / metricas.Atendimentos * metricas.Cancelamentos;
            texto += $" Pelo seu valor médio por atendimento, isso representa cerca de {Dinheiro(perdido)} " +
                     "que deixaram de entrar.";
        }

        if (metricas.PrincipalMotivoCancelamento != null &&
            !string.IsNullOrWhiteSpace(metricas.PrincipalMotivoCancelamento.Palavra))
        {
            texto += $" O motivo mais citado foi \"{metricas.PrincipalMotivoCancelamento.Palavra}\"; " +
                     "confirmar o horário com o cliente antes do dia ajuda a reduzir isso.";
        }

        return new Recomendacao
        {
            Tipo = TipoRecomendacao.Cancelamento,
            Titulo = "Cancelamentos estão custando dinheiro",
            Texto = texto,
            Alerta = true
        };
    }

    private static Recomendacao? RecomendarTendencia(MetricasPrestador metricas, string servicoFoco)
    {
        var variacao = metricas.Variacoes.Faturamento;

        if (variacao <= -LimiteTendencia)
        {
            return new Recomendacao
            {
                Tipo = TipoRecomendacao.Tendencia,
                Alerta = true,
                Titulo = "Faturamento em queda",
                Texto = $"Seu faturamento caiu {Num(Math.Abs(variacao))}% em relação ao período anterior. " +
                        $"Priorizar {servicoFoco} na agenda é o caminho mais rápido para recuperar."
            };
        }

        if (variacao >= LimiteTendencia)
        {
            return new Recomendacao
            {
                Tipo = TipoRecomendacao.Tendencia,
                Titulo = "Faturamento em alta",
                Texto = $"Seu faturamento cresceu {Num(variacao)}% em relação ao período anterior. " +
                        $"Continue investindo em {servicoFoco} para manter o ritmo."
            };
        }

        return null;
    }

    private static string Num(double valor, string formato = "0.#") => valor.ToString(formato, Br);

    private static string Dinheiro(decimal valor) => $"R$ {valor.ToString("N2", Br)}";
}

public enum TipoRecomendacao
{
    Foco,
    Preco,
    Cancelamento,
    Tendencia
}

public class Recomendacao
{
    public TipoRecomendacao Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Texto { get; set; } = string.Empty;

    public bool Alerta { get; set; }

    public bool GeradaPorIa { get; set; }

    public decimal? GanhoEstimado { get; set; }
}