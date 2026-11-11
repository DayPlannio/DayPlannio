using System.Runtime.CompilerServices;

namespace DayPlannio.App;

public class App
{
    public static HttpClient HttpClient { get; set; } = null!;
}

internal static class InicializacaoDosTestes
{
    [ModuleInitializer]
    internal static void IniciarSqlite() => SQLitePCL.Batteries_V2.Init();
}
