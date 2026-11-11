using System.Globalization;

namespace DayPlannio.App.Tests.Infra;

public sealed class CulturaPtBr : IDisposable
{
    private readonly CultureInfo _antes = CultureInfo.CurrentCulture;
    private readonly CultureInfo _antesUi = CultureInfo.CurrentUICulture;

    public CulturaPtBr()
    {
        var pt = new CultureInfo("pt-BR");
        CultureInfo.CurrentCulture = pt;
        CultureInfo.CurrentUICulture = pt;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _antes;
        CultureInfo.CurrentUICulture = _antesUi;
    }
}
