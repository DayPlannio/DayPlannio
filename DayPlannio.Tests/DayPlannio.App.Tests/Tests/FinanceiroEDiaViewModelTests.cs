using DayPlannio.App.Tests.Infra;
using DayPlannio.App.ViewModel;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Unitario")]
public class FinanceiroEDiaViewModelTests
{
    [Theory(DisplayName = "AU-10 Financeiro: entrada aparece com '+', saída com '-', em reais e pt-BR")]
    [InlineData("Entrada", 1234.5, "+ R$ 1.234,50")]
    [InlineData("entrada", 20, "+ R$ 20,00")]
    [InlineData("ENTRADA", 0.5, "+ R$ 0,50")]
    [InlineData("Saida", 99.9, "- R$ 99,90")]
    [InlineData("Saída", 10, "- R$ 10,00")]
    public void ValorFormatado(string tipo, double valor, string esperado)
    {
        using var _ = new CulturaPtBr();

        Assert.Equal(esperado, new FinanceiroItemViewModel { Tipo = tipo, Valor = (decimal)valor }.ValorFormatado);
    }

    [Fact(DisplayName = "AU-11 Financeiro: entrada é verde e saída é vermelha")]
    public void CorDoValor()
    {
        Assert.Equal(Color.FromArgb("#2E7D32"), new FinanceiroItemViewModel { Tipo = "Entrada" }.CorValor);
        Assert.Equal(Color.FromArgb("#D91C1C"), new FinanceiroItemViewModel { Tipo = "Saida" }.CorValor);
    }

    [Fact(DisplayName = "AU-12 Financeiro: tipo nulo não quebra (trata como saída)")]
    public void TipoNulo_NaoQuebra()
    {
        using var _ = new CulturaPtBr();
        var vm = new FinanceiroItemViewModel { Tipo = null, Valor = 5 };

        Assert.StartsWith("- R$", vm.ValorFormatado);
    }

    [Fact(DisplayName = "AU-13 Financeiro: data e hora no formato dd/MM/aaaa • HH:mm")]
    public void DataHoraFormatada()
    {
        using var _ = new CulturaPtBr();

        Assert.Equal("28/10/2026 • 14:05",
            new FinanceiroItemViewModel { Data = new DateTime(2026, 10, 28, 14, 5, 0) }.DataHoraFormatada);
    }

    [Fact(DisplayName = "AU-14 Dia da agenda: valores padrão (fundo branco, texto preto)")]
    public void DiaViewModel_Padrao()
    {
        var d = new DiaViewModel();

        Assert.Equal(Colors.White, d.CorFundo);
        Assert.Equal(Colors.Black, d.CorTexto);
        Assert.Equal(string.Empty, d.DiaSemana);
    }
}
