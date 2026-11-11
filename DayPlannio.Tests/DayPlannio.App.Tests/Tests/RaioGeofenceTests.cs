using DayPlannio.App.Helpers;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Unitario")]
public class RaioGeofenceTests
{
    [Fact(DisplayName = "AU-01 O raio padrão é 100 m")]
    public void Padrao_E100() => Assert.Equal(100, RaioGeofence.Padrao);

    [Fact(DisplayName = "AU-02 As opções exibidas são 20, 50 e 100 m, com o padrão marcado como recomendado")]
    public void Opcoes_SaoAsTresEMarcamRecomendado() =>
        Assert.Equal(new[] { "20 m", "50 m", "100 m (recomendado)" }, RaioGeofence.Opcoes);

    [Theory(DisplayName = "AU-03 ParaTexto formata o valor; valores fora da lista caem no padrão")]
    [InlineData(20, "20 m")]
    [InlineData(50, "50 m")]
    [InlineData(100, "100 m (recomendado)")]
    [InlineData(75, "100 m (recomendado)")]
    [InlineData(0, "100 m (recomendado)")]
    [InlineData(-10, "100 m (recomendado)")]
    public void ParaTexto(int metros, string esperado) => Assert.Equal(esperado, RaioGeofence.ParaTexto(metros));

    [Theory(DisplayName = "AU-04 ParaMetros lê o texto da tela; texto inválido, vazio ou nulo cai no padrão")]
    [InlineData("20 m", 20)]
    [InlineData("50 m", 50)]
    [InlineData("100 m (recomendado)", 100)]
    [InlineData("75 m", 100)]
    [InlineData("abc", 100)]
    [InlineData("", 100)]
    [InlineData(null, 100)]
    public void ParaMetros(string? texto, int esperado) => Assert.Equal(esperado, RaioGeofence.ParaMetros(texto));

    [Theory(DisplayName = "AU-05 ParaTexto → ParaMetros devolve o valor original (ida e volta)")]
    [InlineData(20)]
    [InlineData(50)]
    [InlineData(100)]
    public void IdaEVolta(int metros) => Assert.Equal(metros, RaioGeofence.ParaMetros(RaioGeofence.ParaTexto(metros)));
}
