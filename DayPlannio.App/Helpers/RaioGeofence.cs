namespace DayPlannio.App.Helpers;

public static class RaioGeofence
{
    public const int Padrao = 50;

    private static readonly int[] _metros = { 20, 50, 100 };

    public static IReadOnlyList<string> Opcoes { get; } = _metros.Select(ParaTexto).ToList();

    public static string ParaTexto(int metros) =>
        $"{(_metros.Contains(metros) ? metros : Padrao)} m";

    public static int ParaMetros(string? texto)
    {
        var digitos = new string((texto ?? string.Empty).Where(char.IsDigit).ToArray());
        return int.TryParse(digitos, out var m) && _metros.Contains(m) ? m : Padrao;
    }
}
