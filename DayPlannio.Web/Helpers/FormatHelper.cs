using System.Text.RegularExpressions;

namespace DayPlannio.Web;

public static class FormatHelper
{
    public static string FormatarTelefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return "-";

        var digitos = Regex.Replace(telefone, @"\D", "");

        if (digitos.Length == 10)
            return $"({digitos.Substring(0, 2)}) {digitos.Substring(2, 4)}-{digitos.Substring(6, 4)}";

        if (digitos.Length == 11)
            return $"({digitos.Substring(0, 2)}) {digitos.Substring(2, 5)}-{digitos.Substring(7, 4)}";

        return telefone;
    }

    public static string FormatarDataLocal(DateTime data)
    {
        return data.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
    }

    public static string FormatarDataLocalCurta(DateTime data)
    {
        return data.ToLocalTime().ToString("dd/MM/yyyy");
    }
}
