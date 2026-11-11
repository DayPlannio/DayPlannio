using System.Globalization;
using DayPlannio.App.Converters;
using Xunit;

namespace DayPlannio.App.Tests.Tests;

[Trait("Categoria", "Unitario")]
public class ConvertersTests
{
    private static readonly CultureInfo Cultura = CultureInfo.InvariantCulture;

    [Theory(DisplayName = "AU-06 IgualStringConverter compara o valor com o parâmetro (usado nos botões de seleção)")]
    [InlineData("Semanal", "Semanal", true)]
    [InlineData("Semanal", "Diario", false)]
    [InlineData("semanal", "Semanal", false)]
    [InlineData(null, null, true)]
    [InlineData("x", null, false)]
    public void IgualString_Convert(string? valor, string? parametro, bool esperado) =>
        Assert.Equal(esperado, new IgualStringConverter().Convert(valor!, typeof(bool), parametro!, Cultura));

    [Fact(DisplayName = "AU-07 IgualStringConverter.ConvertBack devolve o parâmetro quando marcado e 'nada' quando desmarcado")]
    public void IgualString_ConvertBack()
    {
        var c = new IgualStringConverter();

        Assert.Equal("Mensal", c.ConvertBack(true, typeof(string), "Mensal", Cultura));
        Assert.Same(Binding.DoNothing, c.ConvertBack(false, typeof(string), "Mensal", Cultura));
    }

    [Theory(DisplayName = "AU-08 InvertedBoolConverter inverte booleanos nos dois sentidos e ignora outros tipos")]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void InvertedBool_Inverte(bool entrada, bool esperado)
    {
        var c = new InvertedBoolConverter();

        Assert.Equal(esperado, c.Convert(entrada, typeof(bool), null!, Cultura));
        Assert.Equal(esperado, c.ConvertBack(entrada, typeof(bool), null!, Cultura));
    }

    [Fact(DisplayName = "AU-09 InvertedBoolConverter devolve o valor original quando não é booleano")]
    public void InvertedBool_NaoBooleano_Passa()
    {
        Assert.Equal("texto", new InvertedBoolConverter().Convert("texto", typeof(bool), null!, Cultura));
        Assert.Null(new InvertedBoolConverter().Convert(null!, typeof(bool), null!, Cultura));
    }
}
