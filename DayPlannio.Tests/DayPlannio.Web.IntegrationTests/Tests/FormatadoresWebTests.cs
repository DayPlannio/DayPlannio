using DayPlannio.Web;
using DayPlannio.Web.Models;
using Xunit;

namespace DayPlannio.Web.IntegrationTests.Tests;

[Trait("Categoria", "Unitario")]
public class FormatadoresWebTests
{
    [Theory(DisplayName = "UT-W01 Telefone é formatado com DDD e hífen")]
    [InlineData("1433334444", "(14) 3333-4444")]
    [InlineData("14999998888", "(14) 99999-8888")]
    [InlineData("(14) 99999-8888", "(14) 99999-8888")]
    [InlineData("123", "123")]
    [InlineData(null, "-")]
    [InlineData("   ", "-")]
    public void FormatarTelefone(string? entrada, string esperado) =>
        Assert.Equal(esperado, FormatHelper.FormatarTelefone(entrada));

    [Theory(DisplayName = "UT-W02 Duração em minutos vira texto legível")]
    [InlineData(null, "-")]
    [InlineData(0.2, "menos de 1 min")]
    [InlineData(45.0, "45min")]
    [InlineData(120.0, "2h")]
    [InlineData(90.0, "1h 30min")]
    public void FormatarDuracao(double? minutos, string esperado) =>
        Assert.Equal(esperado, FormatHelper.FormatarDuracao(minutos));

    [Theory(DisplayName = "UT-W03 Status do agendamento ganha nome amigável")]
    [InlineData("Agendado", "Agendado")]
    [InlineData("EmAtendimento", "Em atendimento")]
    [InlineData("EmPausa", "Em pausa")]
    [InlineData("Concluido", "Concluído")]
    [InlineData("Cancelado", "Cancelado")]
    [InlineData("Outro", "Outro")]
    [InlineData(null, "-")]
    public void StatusAmigavel(string? status, string esperado) =>
        Assert.Equal(esperado, FormatHelper.StatusAmigavel(status));

    private static TimeZoneInfo? FusoBrasilia()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return null;
    }

    [Fact(DisplayName = "UT-W04 Hora e data são mostradas no horário de Brasília")]
    public void FormatarHoraEData_Brasilia()
    {
        if (FusoBrasilia() is null) return;
        var utc = new DateTime(2026, 3, 18, 15, 30, 0, DateTimeKind.Utc);
        var madrugada = new DateTime(2026, 3, 18, 2, 0, 0, DateTimeKind.Utc);

        Assert.Equal("12:30", FormatHelper.FormatarHora(utc));
        Assert.Equal("12:30", FormatHelper.FormatarHora(DateTime.SpecifyKind(utc, DateTimeKind.Unspecified)));
        Assert.Equal("12:30", FormatHelper.FormatarHora(utc.ToLocalTime()));
        Assert.Equal("-", FormatHelper.FormatarHora(null));
        Assert.Equal("18/03/2026", FormatHelper.FormatarData(utc));
        Assert.Equal("17/03/2026", FormatHelper.FormatarData(madrugada));
    }

    [Fact(DisplayName = "UT-W05 Data local completa e curta seguem o fuso do servidor")]
    public void FormatarDataLocal()
    {
        var utc = new DateTime(2026, 3, 18, 15, 30, 0, DateTimeKind.Utc);

        Assert.Equal(utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), FormatHelper.FormatarDataLocal(utc));
        Assert.Equal(utc.ToLocalTime().ToString("dd/MM/yyyy"), FormatHelper.FormatarDataLocalCurta(utc));
    }

    [Theory(DisplayName = "UT-W06 E-mail é mascarado para a tela de recuperação")]
    [InlineData("maria@teste.com", "ma**a@teste.com")]
    [InlineData("ab@x.com", "ab@x.com")]
    [InlineData("a@x.com", "a@x.com")]
    [InlineData("semarroba", "semarroba")]
    [InlineData("", "")]
    public void EmailMascarado(string email, string esperado) =>
        Assert.Equal(esperado, new EsqueciSenhaViewModel { Email = email }.EmailMascarado);

    [Fact(DisplayName = "UT-W07 Indicadores sem dados mostram traço e gráficos vazios")]
    public void Metricas_SemDados()
    {
        var vm = new MetricasViewModel();

        vm.BuildChartData();

        Assert.Equal("-", vm.TempoTotalTexto);
        Assert.Equal("-", vm.TempoMedioTexto);
        Assert.Equal("-", vm.ResumoMaior);
        Assert.Equal("-", vm.ResumoMenor);
        Assert.Equal("-", vm.ResumoMedio);
        Assert.Equal("-", vm.ResumoTotal);
        Assert.Equal("= período anterior", vm.VariacaoAtendimentos);
        Assert.Equal("[]", vm.CategoriaLabelsJson);
        Assert.Equal("[]", vm.EvolucaoValoresJson);
    }

    [Theory(DisplayName = "UT-W08 Minutos viram texto curto de horas e minutos")]
    [InlineData(null, "-")]
    [InlineData(0.0, "-")]
    [InlineData(30.0, "30min")]
    [InlineData(120.0, "2h")]
    [InlineData(125.0, "2h05min")]
    public void FormatarMinutos(double? minutos, string esperado) =>
        Assert.Equal(esperado, new MetricasViewModel().FormatarMinutos(minutos));

    [Fact(DisplayName = "UT-W09 Valor zerado aparece como traço")]
    public void FormatarValor()
    {
        var vm = new MetricasViewModel();

        Assert.Equal("-", vm.FormatarValor(0));
        Assert.Equal("-", vm.FormatarValor(-5));
        Assert.Contains("10", vm.FormatarValor(10));
    }

    [Fact(DisplayName = "UT-W10 Dados dos gráficos e textos de variação são montados a partir das métricas")]
    public void Metricas_ComDados()
    {
        var vm = new MetricasViewModel
        {
            Metricas = new MetricasClienteDto
            {
                ResumoGeral = new ResumoGeral { TotalServicos = 3, TempoTotalMinutos = 125, TempoMedioMinutos = 45, ValorTotal = 300 },
                ResumoTempo = new ResumoTempo { Maior = 90, Menor = 20, Medio = 45, TotalMinutos = 135 },
                Variacoes = new Variacoes { Atendimentos = 12.5, Tempo = -5, Valor = 0 },
                ServicosPorCategoria = new List<ServicoCategoriaItem>
                {
                    new() { Categoria = "Limpeza", Quantidade = 2 },
                    new() { Categoria = "Pintura", Quantidade = 1 }
                },
                Evolucao = new List<EvolucaoItem> { new() { Data = new DateTime(2026, 3, 5), Quantidade = 2 } },
                TempoMedioPorServico = new List<ServicoCategoriaItem>
                {
                    new() { Categoria = "Limpeza", TempoMedioMinutos = 40 },
                    new() { Categoria = "Pintura", TempoMedioMinutos = null }
                },
                InvestimentoPorServico = new List<ServicoCategoriaItem>
                {
                    new() { Categoria = "Pintura", Valor = 120.5m }
                }
            }
        };

        vm.BuildChartData();

        Assert.Equal("[\"Limpeza\",\"Pintura\"]", vm.CategoriaLabelsJson);
        Assert.Equal("[2,1]", vm.CategoriaValoresJson);
        Assert.Equal("[2]", vm.EvolucaoValoresJson);
        Assert.Contains("05", vm.EvolucaoLabelsJson);
        Assert.Equal("[\"Limpeza\",\"Pintura\"]", vm.TempoMedioLabelsJson);
        Assert.Equal("[40,0]", vm.TempoMedioValoresJson);
        Assert.Equal("[\"Pintura\"]", vm.InvestimentoLabelsJson);
        Assert.Equal("[120.5]", vm.InvestimentoValoresJson);
        Assert.Equal("2h05min", vm.TempoTotalTexto);
        Assert.Equal("45min", vm.TempoMedioTexto);
        Assert.Equal("1h30min", vm.ResumoMaior);
        Assert.Equal("20min", vm.ResumoMenor);
        Assert.StartsWith("+12", vm.VariacaoAtendimentos);
        Assert.StartsWith("-5", vm.VariacaoTempo);
        Assert.EndsWith("vs período anterior", vm.VariacaoTempo);
        Assert.Equal("= período anterior", vm.VariacaoValor);
    }
}
